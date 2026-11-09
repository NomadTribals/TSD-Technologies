using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using BepInEx;using BepInEx.Bootstrap;using BepInEx.Configuration;using TsdTechnologies.Api;using UnityEngine;using WebOverlay;
namespace TsdTechnologies.Overlay
{
 [BepInPlugin("com.nomadtribals.tsdtechnologies.overlay","TSD Technologies - Field OS","1.0.0")]
 [BepInDependency(TabletRuntime.PluginGuid)]
 [BepInDependency("com.anvil.weboverlay","1.12.0")]
 public sealed class TabletOsPlugin:BaseUnityPlugin
 {
  private IWebOverlay overlay;private ConfigFile settings;private TsdUnifiedConfig migration;private TsdWallpapers wallpapers;private string appDocumentsFolder;
  private ConfigEntry<KeyCode> hotkey;private ConfigEntry<string> color;private ConfigEntry<bool> sounds;private ConfigEntry<float> volume,brightness;
  private bool visible,capturing;private float suppressUntil,timer;private long lastContextTicks;private long appRevision=-1;
  private readonly Dictionary<string,AppHost> hosts=new Dictionary<string,AppHost>();
  private void Awake(){
   settings=Chainloader.PluginInfos[TabletRuntime.PluginGuid].Instance.Config;
   migration=new TsdUnifiedConfig(settings,Path.Combine(Paths.ConfigPath,"com.nomadtribals.tacticalsupplydrops.cfg"));
   hotkey=migration.Bind("Controls","TabletToggleKey",KeyCode.F9,"Open or close your tablet.");
   color=migration.Bind("Appearance","InterfaceColor","#e0a43b","Interface accent color (#RRGGBB).");
   sounds=migration.Bind("Tablet Audio","SoundsEnabled",true,"Enable device sounds.");volume=migration.Bind("Tablet Audio","Volume",.5f,"Device sound volume, 0 to 1.");brightness=migration.Bind("Appearance","Brightness",.55f,"Screen brightness, 0.55 to 1.25.");
   MigrateFiles("Wallpapers");
   appDocumentsFolder=Path.Combine(Paths.ConfigPath,"TsdTechnologies","AppDocuments");Directory.CreateDirectory(appDocumentsFolder);
   wallpapers=new TsdWallpapers(migration.Bind("Appearance","Wallpaper","builtin:signal","Home wallpaper."),migration.Bind("Appearance","WallpaperFit","fill","Custom image sizing: fill or fit."),json=>overlay?.Post("tsd-wallpapers",json,PostOptions.Retain));
   color.SettingChanged+=(sender,args)=>{if(ValidColor(color.Value)){overlay?.ExecuteScript("window.tsd.onInterfaceColorLoaded("+Quote(color.Value)+");");foreach(var host in hosts.Values)host.SendTheme(color.Value);}};
   Logger.LogInfo("TSD Technologies Field OS ready. Installed apps register independently.");
  }
  private void Update(){
   if(!capturing&&Time.unscaledTime>=suppressUntil&&Input.GetKeyDown(hotkey.Value))ToggleTablet();
   timer+=Time.unscaledDeltaTime;if(timer<.25f)return;timer=0;
   if(overlay==null)return;
   var state=TabletRuntime.Context;if(state.UpdatedUtcTicks!=lastContextTicks){lastContextTicks=state.UpdatedUtcTicks;SendContext();}
   RefreshApps();
  }
  private void ToggleTablet(){
   if(overlay!=null){if(visible)overlay.ExecuteScript("window.tsd.beginTabletShutdown();");else{var state=TabletRuntime.Context;if(!state.IsFresh||!state.HasTablet)return;overlay.Show();visible=true;SendPreferences();SendContext();overlay.ExecuteScript("window.tsd.setTabletVisible(true);");}return;}
   var context=TabletRuntime.Context;if(!context.IsFresh||!context.HasTablet){Logger.LogWarning("Carry your TSD Field Terminal to open the tablet.");return;}
   overlay=WebOverlays.Create("TSD Technologies - Field OS",new OverlayOptions{Width=1200,Height=800,MinWidth=1000,MinHeight=650,Frame=false,RememberBounds=true,CloseKeys=new int[0],FreeCursorWhileShown=true,ClickThroughWhenUnfocused=true,DispatchOnMainThread=true,
    VirtualHosts=new[]{new VirtualHost{Host="tsd-wallpapers.assets",Folder=wallpapers.Folder,Access=HostAccess.DenyCors},new VirtualHost{Host="tsd-apps.assets",Folder=appDocumentsFolder,Access=HostAccess.DenyCors}}});
   if(overlay==null)return;visible=true;var created=overlay;
   created.VisibilityChanged+=value=>{visible=value;if(!value){foreach(var host in hosts.Values)host.NotifyVisibility(false);created.ExecuteScript("window.tsd.setTabletVisible(false);");}};
   created.KeyPressed+=key=>{if(!capturing&&visible&&Time.unscaledTime>=suppressUntil&&(key==0x1B||key==WebOverlayPlugin.VirtualKey(hotkey.Value)))created.ExecuteScript("window.tsd.beginTabletShutdown();");};
   created.ChannelMessage+=HandleChannel;
   created.ChannelsFailed+=()=>Logger.LogError("Field OS communication failed.");
   created.Failed+=()=>{Logger.LogError("Field OS window failed: "+created.FailureMessage);foreach(var h in hosts.Values)h.NotifyVisibility(false);hosts.Clear();created.Dispose();if(ReferenceEquals(overlay,created))overlay=null;visible=false;appRevision=-1;};
   created.LoadHtml(Resource("Tablet.html"));
  }
  private void HandleChannel(string channel,string payload){
   Logger.LogDebug("Field OS channel: "+channel);
   if(channel=="tsd-ready"){appRevision=-1;RefreshApps();wallpapers.Refresh();SendPreferences();SendContext();overlay.ExecuteScript("window.tsd.onInterfaceColorLoaded("+Quote(ValidColor(color.Value)?color.Value:"#e0a43b")+");window.tsd.onTabletHotkeyLoaded("+Quote(hotkey.Value.ToString())+");window.tsd.setTabletVisible(true);");return;}
   if(channel=="tsd-power"){overlay.Hide();visible=false;return;}
   if(channel=="tsd-wallpaper-refresh"){wallpapers.Refresh();return;}if(channel=="tsd-wallpaper-apply"){wallpapers.Apply(payload);return;}
   if(channel=="tsd-hotkey-capture"){capturing=true;return;}if(channel=="tsd-hotkey-cancel"){capturing=false;suppressUntil=Time.unscaledTime+.25f;return;}
   if(channel=="tsd-hotkey-set"){capturing=false;KeyCode key;if(Enum.TryParse(payload,true,out key)&&key!=KeyCode.None&&Enum.IsDefined(typeof(KeyCode),key)){hotkey.Value=key;foreach(var host in hosts.Values)host.SendDeviceKey();suppressUntil=Time.unscaledTime+.35f;overlay.ExecuteScript("window.tsd.onTabletHotkeySaved("+Quote(key.ToString())+");");}else overlay.ExecuteScript("window.tsd.onTabletHotkeyError();");return;}
   if(channel=="tsd-interface-color-set"){if(ValidColor(payload)){color.Value=payload.ToLowerInvariant();overlay.ExecuteScript("window.tsd.onInterfaceColorSaved("+Quote(color.Value)+");");}else overlay.ExecuteScript("window.tsd.onInterfaceColorError();");return;}
   if(channel=="tsd-preference-set"){SavePreference(payload);return;}
   if(channel=="tech-app-message"){
    try{if(payload==null||payload.Length>1048576)return;var m=JsonUtility.FromJson<AppMessage>(payload);AppHost host;if(m!=null&&m.id!=null&&hosts.TryGetValue(m.id,out host)&&m.channel!=null&&m.channel.Length<=100){host.Receive(m.channel,m.payload??"");}}catch(Exception ex){Logger.LogWarning("App message failed: "+ex.Message);}return;
   }
   if(channel=="tech-app-ready"){AppHost host;if(hosts.TryGetValue(payload??"",out host))host.MarkReady();return;}
   if(channel=="tech-app-visible"){try{var m=JsonUtility.FromJson<AppVisibility>(payload);foreach(var pair in hosts)pair.Value.NotifyVisibility(m!=null&&m.visible&&m.id==pair.Key);}catch{}return;}
  }
  private void RefreshApps(){
   long revision=TabletRuntime.Revision;if(revision==appRevision)return;appRevision=revision;var apps=TabletRuntime.GetApplications();
   foreach(var id in hosts.Keys.Where(id=>!apps.Any(a=>a.Id==id)).ToArray()){hosts[id].NotifyVisibility(false);hosts.Remove(id);}
   var manifest=new Manifest{apps=new List<AppInfo>()};
   foreach(var app in apps){if(!hosts.ContainsKey(app.Id)){var host=new AppHost(this,app);hosts.Add(app.Id,host);try{app.Attach(host);}catch(Exception ex){Logger.LogError("Could not attach app "+app.Id+": "+ex.Message);hosts.Remove(app.Id);continue;}}manifest.apps.Add(new AppInfo{id=app.Id,name=app.Name,iconSvg=app.IconSvg,url=WriteAppDocument(app.Id,app.Document),channels=app.Channels,logPage=app.LogPage});}
   overlay.Post("tech-apps",JsonUtility.ToJson(manifest),PostOptions.Retain);
  }
  private void SendContext(){var state=TabletRuntime.Context;overlay?.ExecuteScript("window.tsd.onTabletAccessReceived("+JsonUtility.ToJson(new ContextMessage{hasAccess=state.IsFresh&&state.HasTablet,inRaid=state.InRaid,nickname=state.Nickname,raidTime=state.RaidTime})+");");}
  private void SendPreferences(){overlay?.Post("tsd-preferences",JsonUtility.ToJson(new DevicePreferences{sounds=sounds.Value,volume=Finite(volume.Value,.5f,0,1),brightness=Finite(brightness.Value,.55f,.55f,1.25f)}),PostOptions.Retain);}
  private void SavePreference(string payload){try{if(payload==null||payload.Length>4096)return;var m=JsonUtility.FromJson<Preference>(payload);if(m==null)return;float value;switch(m.key){case "tsd-tablet-sounds":if(m.value=="true"||m.value=="false")sounds.Value=m.value=="true";break;case "tsd-tablet-volume":if(float.TryParse(m.value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))volume.Value=Finite(value,.5f,0,1);break;case "tsd-tablet-brightness":if(float.TryParse(m.value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))brightness.Value=Finite(value,.55f,.55f,1.25f);break;}}catch(Exception ex){Logger.LogWarning("Device preference could not be saved: "+ex.Message);}}
  private static float Finite(float value,float fallback,float min,float max){return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);}
  private static bool ValidColor(string value){return value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$");}
  private static string Quote(string value){return JsonUtility.ToJson(new ScriptValue{value=value}).Substring(9).TrimEnd('}');}
  private string WriteAppDocument(string id,string source){
   string document=AppDocument(source);string hash;
   using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(document))).Replace("-","").ToLowerInvariant();
   string file=id+"-"+hash.Substring(0,16)+".html";string path=Path.Combine(appDocumentsFolder,file);
   if(!File.Exists(path))File.WriteAllText(path,document,new System.Text.UTF8Encoding(false));
   return "https://tsd-apps.assets/"+Uri.EscapeDataString(file);
  }
  private static string AppDocument(string document){string bridge="<meta charset='utf-8'><script>"+Resource("AppBridge.js")+"</script>";int head=document.IndexOf("<head",StringComparison.OrdinalIgnoreCase);int end=head>=0?document.IndexOf('>',head):-1;return end>=0?document.Insert(end+1,bridge):bridge+document;}
  private static string Resource(string name){using(var stream=typeof(TabletOsPlugin).Assembly.GetManifestResourceStream("TsdTechnologies.Overlay."+name))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
  private void MigrateFiles(string name){try{string old=Path.Combine(Paths.ConfigPath,"TacticalSupplyDrops",name),target=Path.Combine(Paths.ConfigPath,"TsdTechnologies",name);Directory.CreateDirectory(target);string marker=Path.Combine(target,".legacy-migration-complete");if(File.Exists(marker))return;if(Directory.Exists(old))foreach(var file in Directory.EnumerateFiles(old)){string path=Path.Combine(target,Path.GetFileName(file));if(!File.Exists(path))File.Copy(file,path);}File.WriteAllText(marker,"Device files migrated.");}catch(Exception ex){Logger.LogWarning("Could not migrate device files: "+ex.Message);}}
  private void OnDestroy(){foreach(var host in hosts.Values)host.NotifyVisibility(false);overlay?.Dispose();overlay=null;hosts.Clear();}
  private sealed class AppHost:ITabletAppHost{
   private readonly TabletOsPlugin owner;private readonly TabletApplication app;private bool ready,shown;private readonly Queue<Action> pending=new Queue<Action>();
   public event Action<string,string> ChannelMessage;public event Action<bool> VisibilityChanged;
   internal AppHost(TabletOsPlugin owner,TabletApplication app){this.owner=owner;this.app=app;}
   internal void Receive(string channel,string payload){try{ChannelMessage?.Invoke(channel,payload);}catch(Exception ex){owner.Logger.LogWarning("App "+app.Id+" failed: "+ex.Message);}}
   internal void MarkReady(){ready=true;while(pending.Count>0)pending.Dequeue()();SendTheme(owner.color.Value);}
   internal void NotifyVisibility(bool value){if(shown==value)return;shown=value;try{VisibilityChanged?.Invoke(value);}catch(Exception ex){owner.Logger.LogWarning("App visibility: "+ex.Message);}}
   private void Dispatch(Action action){if(ready)action();else{if(pending.Count>=128)pending.Dequeue();pending.Enqueue(action);}}
   public void ExecuteScript(string script){Dispatch(()=>owner.overlay?.Post("tech-app-script",JsonUtility.ToJson(new ScriptMessage{id=app.Id,script=script})));}
   public void Post(string channel,string payload,TabletPostOptions options=TabletPostOptions.None){if(!app.Channels.Contains(channel))throw new ArgumentException("Declare app channel before posting: "+channel);Dispatch(()=>owner.overlay?.Post("tech-app:"+app.Id+":"+channel,payload,options==TabletPostOptions.LatestOnly?PostOptions.LatestOnly:PostOptions.None));}
   internal void SendDeviceKey(){ExecuteScript("window.tabletApp.setDeviceKey && window.tabletApp.setDeviceKey("+Quote(owner.hotkey.Value.ToString())+");");}
   internal void SendTheme(string value){SendDeviceKey();ExecuteScript("window.tabletApp.setTheme && window.tabletApp.setTheme("+Quote(ValidColor(value)?value:"#e0a43b")+");");}
  }
  [Serializable]private class ScriptValue{public string value;}
  [Serializable]private class AppMessage{public string id,channel,payload;}
  [Serializable]private class AppVisibility{public string id;public bool visible;}
  [Serializable]private class ScriptMessage{public string id,script;}
  [Serializable]private class Manifest{public List<AppInfo> apps;}
  [Serializable]private class AppInfo{public string id,name,iconSvg,url,logPage;public string[] channels;}
  [Serializable]private class ContextMessage{public bool hasAccess,inRaid;public string nickname,raidTime;}
  [Serializable]private class DevicePreferences{public bool sounds;public float volume,brightness;}
  [Serializable]private class Preference{public string key,value;}
 }
}
