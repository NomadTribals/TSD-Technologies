using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Net;

using System.Net.Http;

using System.Text.RegularExpressions;

using System.Threading;

using System.Threading.Tasks;

using Newtonsoft.Json;

using Newtonsoft.Json.Linq;

namespace TsdTechnologies.Overlay

{

 internal sealed class BrowserCatalogue { public BrowserEntry[] apps = new BrowserEntry[0]; }

 internal sealed class BrowserEntry

 {

  public int forgeId; public string appId="",guid="",name="",author="",summary="",description="",thumbnail="",detailUrl="",version="",spt="",requirements="";

 }

 internal sealed class BrowserSnapshot

 {

  public BrowserEntry[] apps; public bool busy; public string message=""; public string updatedUtc="";

 }

 internal sealed class TsdAppBrowser : IDisposable

 {

  private readonly string folder; private readonly Action<string> publish; private readonly HttpClient client;

  private readonly CancellationTokenSource stop=new CancellationTokenSource();

  private readonly object gate=new object(); private BrowserEntry[] entries; private DateTime updatedUtc=DateTime.MinValue,lastAttempt=DateTime.MinValue,blockedUntil=DateTime.MinValue; private bool busy,disposed;

  internal TsdAppBrowser(string folder,string bundled,Action<string> publish,HttpClient transport=null)

  {

   this.folder=folder;this.publish=publish;Directory.CreateDirectory(folder);

   client=transport??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false});

   client.Timeout=TimeSpan.FromSeconds(12);client.DefaultRequestHeaders.UserAgent.ParseAdd("TSD-Technologies-AppBrowser/1.1");

   entries=ReadCatalogue(bundled);

   string config=Path.Combine(folder,"AppCatalogue.json");

   try { if(File.Exists(config)){var custom=ReadCatalogue(File.ReadAllText(config));if(custom.Length>0){foreach(var item in custom){if(PageUrl(item.detailUrl,item.forgeId)==""){var fallback=entries.FirstOrDefault(x=>x.forgeId==item.forgeId&&x.guid==item.guid);if(fallback!=null)item.detailUrl=fallback.detailUrl;}}entries=custom;}}else File.WriteAllText(config,bundled); }catch{}

   try

   {

    string path=Path.Combine(folder,"AppBrowser-cache.json");if(!File.Exists(path)||new FileInfo(path).Length>524288)return;

    var cached=JsonConvert.DeserializeObject<BrowserSnapshot>(File.ReadAllText(path));

    DateTime stamp;

    if(cached!=null&&DateTime.TryParse(cached.updatedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out stamp)&&stamp<=DateTime.UtcNow)

    {

     foreach(var seed in entries){var item=cached.apps?.FirstOrDefault(x=>x.forgeId==seed.forgeId&&x.guid==seed.guid);if(item!=null)Copy(item,seed);}

     updatedUtc=stamp;

    }

   }catch{}

  }

  internal static BrowserEntry[] ReadCatalogue(string json)

  {

   if(json==null||json.Length>131072)throw new InvalidDataException("Invalid app catalogue size.");

   var list=JsonConvert.DeserializeObject<BrowserCatalogue>(json)?.apps??new BrowserEntry[0];

   return list.Where(x=>x!=null&&x.forgeId>0&&Regex.IsMatch(x.appId??"","^[a-z][a-z0-9_.-]{1,63}$")&&!string.IsNullOrWhiteSpace(x.guid)&&!string.IsNullOrWhiteSpace(x.name)).GroupBy(x=>x.forgeId).Select(g=>g.First()).Take(12).ToArray();

  }

  internal string Page(string payload)

  {

   if(payload=="dependents")return "https://sp-mod.com/mod/3336/tsd-technologies#dependents";

   int id;if(!int.TryParse(payload,out id)||!entries.Any(x=>x.forgeId==id))return null;

   var entry=entries.First(x=>x.forgeId==id);var page=PageUrl(entry.detailUrl,id);return page.Length>0?page:null;

  }

  internal void Request(bool force)

  {

   lock(gate)

   {

    if(disposed)return;

    if(busy){Emit("Refreshing app information…");return;}

    var now=DateTime.UtcNow;
    if(now<blockedUntil){Emit("Forge requested a pause. Saved app information is available; try again later.");return;}

    if((!force&&now-updatedUtc<TimeSpan.FromHours(6))||now-lastAttempt<TimeSpan.FromSeconds(30)){Emit("App catalogue ready. Downloads are installed outside the game.");return;}

    busy=true;lastAttempt=now;Emit("Refreshing app information…");

   }

   _=Task.Run(Refresh);

  }

  private async Task Refresh()

  {

   bool failed=false,changed=false;

   try

   {

    foreach(var seed in entries)

    {

     try

     {

      var mod=await Fetch("mod/"+seed.forgeId);

      var versions=await Fetch("mod/"+seed.forgeId+"/versions?include=dependencies&per_page=6&sort=-version");

      var result=Parse(seed,mod,versions);

      lock(gate){Copy(result,seed);}changed=true;

     }

     catch(ForgeThrottled ex){lock(gate){blockedUntil=DateTime.UtcNow+ex.Delay;}failed=true;break;}
     catch(OperationCanceledException){if(stop.IsCancellationRequested)return;failed=true;}

     catch{failed=true;}

     if(entries.Length>1)await Task.Delay(400,stop.Token);

    }

    lock(gate)

    {

     if(changed)updatedUtc=DateTime.UtcNow;

     busy=false;

     var snapshot=Snapshot(failed?"Forge is unavailable for some apps. Saved information is shown.":"App information updated. Downloads are installed outside the game.");

     if(changed)try{File.WriteAllText(Path.Combine(folder,"AppBrowser-cache.json"),JsonConvert.SerializeObject(snapshot));}catch{}

     Emit(snapshot.message);

    }

   }

   catch(OperationCanceledException){}

   finally{lock(gate){busy=false;}}

  }

  private sealed class ForgeThrottled:Exception{internal readonly TimeSpan Delay;internal ForgeThrottled(TimeSpan delay){Delay=delay;}}
  private async Task<JObject> Fetch(string relative)

  {

   using(var request=new HttpRequestMessage(HttpMethod.Get,"https://sp-mod.com/api/v0/"+relative))

   using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,stop.Token))

   {

    if((int)response.StatusCode==429){var retry=response.Headers.RetryAfter;var delay=retry?.Delta??(retry?.Date.HasValue==true?retry.Date.Value.UtcDateTime-DateTime.UtcNow:TimeSpan.FromMinutes(1));throw new ForgeThrottled(delay>TimeSpan.Zero?delay:TimeSpan.FromMinutes(1));}
    response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>524288)throw new InvalidDataException("Response too large.");

    using(var stream=await response.Content.ReadAsStreamAsync())using(var memory=new MemoryStream())

    {

     var buffer=new byte[8192];int n;

     while((n=await stream.ReadAsync(buffer,0,buffer.Length,stop.Token))>0){if(memory.Length+n>524288)throw new InvalidDataException("Response too large.");memory.Write(buffer,0,n);}

     var json=JObject.Parse(System.Text.Encoding.UTF8.GetString(memory.ToArray()));

     if((bool?)json["success"]!=true)throw new InvalidDataException("Invalid Forge response.");return json;

    }

   }

  }

  internal static BrowserEntry Parse(BrowserEntry seed,JObject mod,JObject versions)

  {

   var data=mod["data"] as JObject;

   if(data==null||(int?)data["id"]!=seed.forgeId||(string)data["guid"]!=seed.guid)throw new InvalidDataException("App identity mismatch.");

   var version=(versions["data"] as JArray)?.OfType<JObject>().FirstOrDefault(v=>(v["dependencies"] as JArray)?.Any(d=>(string)d["guid"]=="com.nomadtribals.tsdtechnologies"||(string)d["mod_guid"]=="com.nomadtribals.tsdtechnologies")==true);

   if(version==null)throw new InvalidDataException("No tablet-dependent version found.");

   var dependencies=(version["dependencies"] as JArray)?.Select(d=>(string)d["name"]??(string)d["mod_name"]).Where(x=>!string.IsNullOrEmpty(x)).ToArray()??new string[0];

   return new BrowserEntry{forgeId=seed.forgeId,appId=seed.appId,guid=seed.guid,name=Text((string)data["name"],100),author=Text((string)data["owner"]?["name"],100),summary=Text((string)data["teaser"],350),description=Text((string)data["description"],8000),thumbnail=ImageUrl((string)data["thumbnail"]),detailUrl=PageUrl((string)data["detail_url"],seed.forgeId),version=Text((string)version["version"],60),spt=Text((string)version["spt_version_constraint"],100),requirements=string.Join(", ",dependencies)};

  }

  internal static string PageUrl(string url,int id)
  {
   Uri value;
   if(!Uri.TryCreate(url,UriKind.Absolute,out value)||value.Scheme!="https"||value.Host!="sp-mod.com"||!value.IsDefaultPort||!string.IsNullOrEmpty(value.UserInfo)||value.Query!=""||value.Fragment!="")return "";
   return Regex.IsMatch(value.AbsolutePath,"^/mod/"+id+"/[a-z0-9]+(?:-[a-z0-9]+)*$")?value.AbsoluteUri:"";
  }
  internal static string ImageUrl(string url)

  {

   Uri value;return Uri.TryCreate(url,UriKind.Absolute,out value)&&value.Scheme=="https"&&value.Host=="files.sp-mod.com"&&string.IsNullOrEmpty(value.UserInfo)&&value.IsDefaultPort?value.AbsoluteUri:"";

  }

  private static string Text(string value,int max)

  {

   if(value==null)return "";value=Regex.Replace(value,"<(br|/p|/li|/h[1-6])[^>]*>","\n",RegexOptions.IgnoreCase);

   value=WebUtility.HtmlDecode(Regex.Replace(value,"<[^>]*>",""));return value.Length>max?value.Substring(0,max)+"…":value;

  }

  private static void Copy(BrowserEntry from,BrowserEntry to)

  {

   var page=PageUrl(from.detailUrl,to.forgeId);if(page.Length>0)to.detailUrl=page;
   to.name=from.name;to.author=from.author;to.summary=from.summary;to.description=from.description;to.thumbnail=ImageUrl(from.thumbnail);to.version=from.version;to.spt=from.spt;to.requirements=from.requirements;

  }

  private BrowserSnapshot Snapshot(string message)=>new BrowserSnapshot{apps=entries,busy=busy,message=message,updatedUtc=updatedUtc==DateTime.MinValue?"":updatedUtc.ToString("o")};

  private void Emit(string message){if(!disposed)publish(JsonConvert.SerializeObject(Snapshot(message)));}

  public void Dispose(){lock(gate){disposed=true;stop.Cancel();client.Dispose();}}

 }

}
