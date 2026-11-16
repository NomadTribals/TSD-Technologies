using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using WebOverlay;

namespace TsdTechnologies.Overlay
{
    // Satellite bridge: streams Satellite's installed page, never copies its map pack.
    internal sealed class SatelliteBridge : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly SemaphoreSlim slots = new SemaphoreSlim(24);
        private readonly Uri backend;
        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<int> width, height, right, top;
        private readonly ConfigEntry<MapExpansionMode> expansionMode;
        private readonly MapExpansionControl expansionControl=new MapExpansionControl();
        private readonly ConfigEntry<int> expandedSize;
        private readonly ConfigEntry<float> expandedOpacity;
        private readonly ConfigEntry<bool> expandedWheelZoom;
        private readonly ConfigEntry<bool> fastMapRendering;
        private readonly ConfigEntry<bool> showRaidTimer,showGameClock;
        private string clockState;
        private bool expanded;
        private float panCooldown;
        private readonly bool migratePreview;
        private IWebOverlay hud;
        private string layout;
        private readonly string preferencesFile;
        private volatile string preferences = "{}";
        private readonly string preferencesToken=Guid.NewGuid().ToString("N");
        private readonly object preferencesGate=new object();


        internal volatile string Connection = "ready";
        internal volatile int MapVirtualKey = 77;
        internal string Origin { get; }
        internal string MapUrl => Origin + "/satellite/";
        internal bool Enabled => enabled.Value;
        internal void UpdateExpansion(bool available,bool held,bool pressed) {
            SetExpanded(expansionControl.Update(expansionMode.Value,available&&enabled.Value&&Connection=="ready",held,pressed));
        }
        internal SatelliteBridge(ConfigFile config, string preferencesFolder = null)
        {
            backend = new Uri(RequestHandler.Host.TrimEnd('/') + "/");
            try { string existing=File.ReadAllText(config.ConfigFilePath);migratePreview=existing.Contains("[Satellite Preview]")&&!existing.Contains("[Satellite]"); } catch { }
            preferencesFile = Path.Combine(preferencesFolder ?? Path.Combine(BepInEx.Paths.ConfigPath,"TsdTechnologies","Satellite"),"Preferences.json");
            try { if(File.Exists(preferencesFile)) { string saved=File.ReadAllText(preferencesFile,Encoding.UTF8); if(saved.Length<=131072)preferences=JObject.Parse(saved).ToString(Newtonsoft.Json.Formatting.None); } } catch { }
            enabled = BindMap(config,"MinimapEnabled",false,new ConfigDescription("Show Satellite in a small click-through tablet during raids."));
            config.Bind("Satellite","HoldMapKeyToExpand",true,"Legacy setting.");
            config.Remove(new ConfigDefinition("Satellite","HoldMapKeyToExpand"));
            expansionMode = config.Bind("Satellite", "MapExpansionMode", MapExpansionMode.Hold, "Hold: enlarge while the map key is held. Toggle: press once to enlarge, again to restore. Interactive Maps remains in the tablet.");
            expandedSize = config.Bind("Satellite", "ExpandedMapSizePercent", 85, new ConfigDescription("Expanded map width and height as a percentage of the game view.",new AcceptableValueRange<int>(50,100)));
            expandedOpacity = config.Bind("Satellite", "ExpandedMapOpacity", .8f, new ConfigDescription("Opacity of the enlarged map only: 0.8 is 80%, 1 is fully opaque. The small minimap stays opaque.",new AcceptableValueRange<float>(.35f,1f)));
            expandedWheelZoom = config.Bind("Satellite","ExpandedMapMouseWheelZoom",true,"Use the mouse wheel to zoom the enlarged map. Has no effect while the map is small or hidden.");
            fastMapRendering=config.Bind("Satellite","MinimapPerformanceMode",true,"Lower minimap drawing resolution to reduce rendering load. The enlarged map still shows a larger area with normal icon sizes. Disable for native-resolution drawing.");
            fastMapRendering.SettingChanged+=(sender,args)=>ApplyExpandedState();
            showRaidTimer=config.Bind("Satellite","MinimapRaidTimer",true,"Show remaining raid time in amber at the minimap's top right.");
            showGameClock=config.Bind("Satellite","MinimapGameClock",true,"Show current in-game time at the minimap's top left.");
            expandedSize.SettingChanged+=(sender,args)=>ApplyExpandedState();
            expandedOpacity.SettingChanged+=(sender,args)=>ApplyExpandedState();
            width = BindMap(config,"MinimapWidth",360,new ConfigDescription("Map width in screen pixels.", new AcceptableValueRange<int>(240, 800)));
            height = BindMap(config,"MinimapHeight",300,new ConfigDescription("Map height in screen pixels.", new AcceptableValueRange<int>(200, 700)));
            right = BindMap(config,"MinimapRight",24,new ConfigDescription("Distance from right edge, in pixels.", new AcceptableValueRange<int>(0, 3000)));
            top = BindMap(config,"MinimapTop",130,new ConfigDescription("Distance from top edge, in pixels.", new AcceptableValueRange<int>(0, 2000)));
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            Origin = "http://127.0.0.1:" + port;
            listener.Prefixes.Add(Origin + "/"); listener.Start();
            Task.Run((Func<Task>)Listen);

        }
        private ConfigEntry<T> BindMap<T>(ConfigFile config,string key,T value,ConfigDescription description) {
            if(migratePreview){var previous=config.Bind("Satellite Preview",key,value,description);value=previous.Value;config.Remove(new ConfigDefinition("Satellite Preview",key));}
            return config.Bind("Satellite",key,value,description);
        }
        // The real page request is the availability check. A separate preflight must
        // never prevent a working Satellite installation from loading.
        internal void CheckConnection(bool force = false) { if (force) Connection = "ready"; }
        internal bool SavePreferences(string json)
        {
            lock(preferencesGate) {
            try {
                if(json==null||json.Length>131072)return false;
                string saved=JObject.Parse(json).ToString(Newtonsoft.Json.Formatting.None);
                Directory.CreateDirectory(Path.GetDirectoryName(preferencesFile));
                string temp=preferencesFile+".tmp";File.WriteAllText(temp,saved,new UTF8Encoding(false));
                if(File.Exists(preferencesFile))File.Replace(temp,preferencesFile,null);else File.Move(temp,preferencesFile);
                preferences=saved;return true;
            }catch{return false;}
            }
        }
        internal void Toggle() { enabled.Value = !enabled.Value; }
        internal void SetExpanded(bool value) {
            value=value&&enabled.Value;
            if(expanded==value)return;expanded=value;ApplyExpandedState();
        }
        private void ApplyExpandedState() {
            hud?.ExecuteScript("window.tsdSetMapPerformance && window.tsdSetMapPerformance("+(fastMapRendering.Value?"true":"false")+");");
            hud?.ExecuteScript("window.tsdSetMapExpanded && window.tsdSetMapExpanded("+(expanded?"true":"false")+","+expandedSize.Value+","+expandedOpacity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+");");
        }
        internal void PanExpandedMap(int horizontal,int vertical,float deltaTime) {
            if(!expanded||hud?.IsVisible!=true||(horizontal==0&&vertical==0)){panCooldown=0;return;}
            panCooldown-=deltaTime;if(panCooldown>0)return;panCooldown=.1f;
            string keys=(horizontal<0?"'ArrowLeft',":horizontal>0?"'ArrowRight',":"")+(vertical<0?"'ArrowDown',":vertical>0?"'ArrowUp',":"");
            hud.ExecuteScript("window.tsdPanMap && window.tsdPanMap(["+keys.TrimEnd(',')+"]);");
        }
        internal void ZoomExpandedMap(float scroll) {
            if(!expanded||hud?.IsVisible!=true||!expandedWheelZoom.Value||float.IsNaN(scroll)||float.IsInfinity(scroll)||Math.Abs(scroll)<.001f)return;
            float steps=Math.Max(-4f,Math.Min(4f,scroll));
            hud.ExecuteScript("window.tsdZoomMap && window.tsdZoomMap("+steps.ToString(System.Globalization.CultureInfo.InvariantCulture)+");");
        }
        internal void UpdateClocks(string gameTime,string remaining) {
            if(hud?.IsVisible!=true)return;
            string state=(showGameClock.Value?gameTime:null)+"|"+(showRaidTimer.Value?remaining:null);
            if(state==clockState)return;
            clockState=state;
            hud.ExecuteScript("window.tsdSetClocks && window.tsdSetClocks("+Newtonsoft.Json.JsonConvert.SerializeObject(showGameClock.Value?gameTime:null)+","+Newtonsoft.Json.JsonConvert.SerializeObject(showRaidTimer.Value?remaining:null)+");");
        }
        internal void Tick(bool inRaid, bool tabletOpen,bool gameplayVisible=true)
        {
            bool show = enabled.Value && inRaid && !tabletOpen && Connection == "ready";
            if (!show) { if (hud != null) { hud.Dispose(); hud = null; layout = null; } return; }
            if(!gameplayVisible){if(hud?.IsVisible==true)hud.Hide();return;}
            string next = width.Value + ":" + height.Value + ":" + right.Value + ":" + top.Value;
            if (hud != null && layout == next) {if(!hud.IsVisible)hud.Show();return;}
            hud?.Dispose(); layout = next;
            hud = WebOverlays.Create("TSD Satellite Minimap", new OverlayOptions {
                Transparent = true, Interactive = false, Frame = false, RememberBounds = false,
                DispatchOnMainThread = true, CloseKeys = new int[0], AllowedOrigins = new[] { Origin }
            });
            if (hud == null) return;
            var created = hud;
            created.PageLoaded+=()=>{clockState=null;ApplyExpandedState();};
            clockState=null;
            created.Failed += () => { created.Dispose(); if (ReferenceEquals(hud, created)) { hud = null; enabled.Value = false; } };
            using (var stream = typeof(SatelliteBridge).Assembly.GetManifestResourceStream("TsdTechnologies.Overlay.Minimap.html"))
            using (var reader = new StreamReader(stream))
                created.LoadHtml(reader.ReadToEnd().Replace("__RIGHT__",right.Value.ToString()).Replace("__TOP__",top.Value.ToString()).Replace("__WIDTH__",width.Value.ToString()).Replace("__HEIGHT__",height.Value.ToString()).Replace("__URL__",MapUrl));
        }
        private async Task Listen()
        {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync().ConfigureAwait(false); }
                catch (HttpListenerException) { break; } catch (ObjectDisposedException) { break; }
                _ = Serve(context);
            }
        }
        private async Task Serve(HttpListenerContext context)
        {
            bool acquired = false;
            try
            {
                // Read-only local Satellite route. No arbitrary URLs, credentials or game API proxy.
                string path = context.Request.Url.AbsolutePath;
                if(path=="/tsd/preferences"&&context.Request.HttpMethod=="POST") {
                    if(context.Request.RemoteEndPoint==null||!IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address)
                        ||context.Request.Headers["X-TSD-Preferences"]!=preferencesToken||context.Request.ContentLength64<0||context.Request.ContentLength64>131072)
                    {context.Response.StatusCode=403;return;}
                    string json;using(var reader=new StreamReader(context.Request.InputStream,Encoding.UTF8))json=await reader.ReadToEndAsync().ConfigureAwait(false);
                    context.Response.StatusCode=SavePreferences(json)?204:400;return;
                }
                if (context.Request.HttpMethod != "GET" || !path.StartsWith("/satellite/", StringComparison.Ordinal)
                    || context.Request.RemoteEndPoint == null || !IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address))
                { context.Response.StatusCode = 403; return; }
                var target = new Uri(backend, context.Request.RawUrl);
                if (target.Authority != backend.Authority || !target.AbsolutePath.StartsWith("/satellite/", StringComparison.Ordinal))
                { context.Response.StatusCode = 403; return; }
                acquired = await slots.WaitAsync(0).ConfigureAwait(false);
                if (!acquired) { context.Response.StatusCode = 503; return; }
                var request = (HttpWebRequest)WebRequest.Create(target);
                request.ServicePoint.ConnectionLimit = 32;
                request.AllowAutoRedirect = false; request.Timeout = 10000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                // Only SPT's exact loopback endpoint may use its self-signed certificate.
                request.ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
                    errors == System.Net.Security.SslPolicyErrors.None || target.IsLoopback;
                using (stop.Token.Register(request.Abort))
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var input = response.GetResponseStream())
                {
                    if (path == "/satellite/") Connection = "ready";
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.ContentType;
                    bool tile=path.StartsWith("/satellite/map/",StringComparison.Ordinal)&&path.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase);
                    context.Response.Headers["Cache-Control"] = tile ? response.Headers["Cache-Control"] ?? "private, max-age=300" : "no-store";
                    if(tile){foreach(string header in new[]{"ETag","Last-Modified"})if(!string.IsNullOrEmpty(response.Headers[header]))context.Response.Headers[header]=response.Headers[header];}
                    context.Response.SendChunked = true;
                    if (path == "/satellite/")
                    {
                        string html;
                        using (var reader = new StreamReader(input, Encoding.UTF8)) html = await reader.ReadToEndAsync().ConfigureAwait(false);
                        // Only this view is restyled; upstream files and full-page preferences stay intact.
                        string adapter = "<style>html,body{overflow:hidden!important}body>*:not(#stage){display:none!important}#stage{inset:0!important;width:100%!important;height:100%!important}</style><script>window.addEventListener('load',()=>{const f=document.getElementById('follow');if(f&&f.classList.contains('paused'))f.click();window.dispatchEvent(new Event('resize'));});</script>";
                        if (context.Request.QueryString["tsdMini"] != "1") adapter = "";
                        else using(var resource=typeof(SatelliteBridge).Assembly.GetManifestResourceStream("TsdTechnologies.Overlay.MinimapNavigation.js"))
                        using(var reader=new StreamReader(resource))adapter+="<script>"+reader.ReadToEnd()+"</script>";
                        string savedPreferences;
                        using(var resource=typeof(SatelliteBridge).Assembly.GetManifestResourceStream("TsdTechnologies.Overlay.SatellitePreferences.js"))
                        using(var reader=new StreamReader(resource))
                            savedPreferences="<script>"+reader.ReadToEnd().Replace("__MINI__",context.Request.QueryString["tsdMini"]=="1"?"true":"false").Replace("__PREFERENCES_TOKEN__",preferencesToken).Replace("__PREFERENCES__",Convert.ToBase64String(Encoding.UTF8.GetBytes(preferences)))+"</script>";
                        string keys = "<script>(()=>{let key=" + MapVirtualKey + ";window.addEventListener('message',e=>{if(e.source===parent&&e.data?.tsdMapKey!==undefined)key=Number(e.data.tsdMapKey)||0;});document.addEventListener('keydown',e=>{if(e.repeat||e.ctrlKey||e.altKey||e.metaKey||e.target?.closest?.('input,textarea,select,[contenteditable]'))return;if(key&&e.keyCode===key){e.preventDefault();e.stopImmediatePropagation();parent.postMessage({tsdMapShortcut:true},'*');}},true);})();</script>";
                        html = html.Replace("</head>", adapter + savedPreferences + keys + "</head>");
                        byte[] bytes = Encoding.UTF8.GetBytes(html);
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, stop.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        byte[] buffer = new byte[65536]; int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, stop.Token).ConfigureAwait(false)) != 0)
                        {
                            await context.Response.OutputStream.WriteAsync(buffer, 0, read, stop.Token).ConfigureAwait(false);
                            await context.Response.OutputStream.FlushAsync(stop.Token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex) {
                if (context.Request.Url.AbsolutePath == "/satellite/") {
                    var failed = (ex as WebException)?.Response as HttpWebResponse;
                    Connection = failed?.StatusCode == HttpStatusCode.NotFound ? "missing-server" : "unavailable";
                    failed?.Dispose();
                }
                try { context.Response.StatusCode = 502; byte[] b = Encoding.UTF8.GetBytes("Satellite is unavailable. Start the SPT server with Satellite installed, then reload Maps."); await context.Response.OutputStream.WriteAsync(b, 0, b.Length); } catch { } }
            finally { try { context.Response.Close(); } catch { } if (acquired) slots.Release(); }
        }
        public void Dispose() { hud?.Dispose(); hud = null; stop.Cancel(); listener.Close(); }
    }
}
