using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TsdTechnologies.Overlay
{
 internal sealed class TsdAppLayout
 {
  readonly string path;
  public string[] Order{get;private set;}=new string[0];
  public TsdAppLayout(string folder){path=Path.Combine(folder,"AppLayout.json");if(File.Exists(path)){try{Order=Parse(File.ReadAllText(path));}catch{Order=new string[0];}}}
  internal static string[] Parse(string json){if(json==null||json.Length>32768)throw new ArgumentException("Invalid app layout.");var order=JObject.Parse(json)["order"] as JArray;if(order==null||order.Count>128||order.Any(k=>k.Type!=JTokenType.String||!Regex.IsMatch((string)k,"^(builtin|app):[a-z][a-z0-9_.-]{1,64}$")))throw new ArgumentException("Invalid app layout.");return order.Values<string>().Distinct(StringComparer.Ordinal).ToArray();}
  public void Save(string json){var order=Parse(json);Directory.CreateDirectory(Path.GetDirectoryName(path));var temporary=path+".pending";File.WriteAllText(temporary,new JObject{["version"]=1,["order"]=new JArray(order)}.ToString(Formatting.None),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);Order=order;}
 }
}
