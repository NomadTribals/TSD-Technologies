using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TsdTechnologies.Overlay
{
    internal sealed class TsdStaticMaps
    {
        internal string Folder { get; private set; }
        private readonly Action<string> send;
        internal TsdStaticMaps(Action<string> send)
        {
            this.send=send;
            Folder=Path.Combine(BepInEx.Paths.ConfigPath,"TsdTechnologies","TabletMaps");
            Directory.CreateDirectory(Folder);
        }
        internal void SendCatalog()
        {
            var result=new Catalog{maps=new List<Sheet>(),message="No map sheets installed."};
            try
            {
                string path=Path.Combine(Folder,"catalog.json");
                if(File.Exists(path) && new FileInfo(path).Length<=262144)
                {
                    var source=JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
                    if(source!=null && source.maps!=null)
                        foreach(var sheet in source.maps)
                        {
                            if(result.maps.Count>=8)break;
                            if(!ValidSheet(sheet))continue;
                            foreach(var tile in sheet.tiles)tile.url="https://tsd-maps.assets/"+Uri.EscapeDataString(tile.file);
                            result.maps.Add(sheet);
                        }
                }
                if(result.maps.Count>0)result.message="Reference map Â· drag to pan Â· scroll to zoom Â· no live tracking.";
            }
            catch { result.message="Map sheets could not be loaded."; }
            send("window.tsd.onStaticMapsReceived && window.tsd.onStaticMapsReceived("+JsonUtility.ToJson(result)+");");
        }
        private bool ValidSheet(Sheet sheet)
        {
            if(sheet==null || string.IsNullOrWhiteSpace(sheet.id) || string.IsNullOrWhiteSpace(sheet.label)
                || sheet.id.Length>80 || sheet.label.Length>120 || sheet.width<1 || sheet.height<1
                || sheet.width>8192 || sheet.height>8192 || (long)sheet.width*sheet.height>16777216
                || sheet.tiles==null || sheet.tiles.Count==0 || sheet.tiles.Count>256)return false;
            foreach(var tile in sheet.tiles)
            {
                if(tile==null || string.IsNullOrWhiteSpace(tile.file) || Path.GetFileName(tile.file)!=tile.file
                    || !tile.file.EndsWith(".png",StringComparison.OrdinalIgnoreCase) || tile.x<0 || tile.y<0
                    || tile.width<1 || tile.height<1 || (long)tile.x+tile.width>sheet.width || (long)tile.y+tile.height>sheet.height)return false;
                string path=Path.Combine(Folder,tile.file);
                if(!File.Exists(path) || new FileInfo(path).Length>2097152)return false;
                using(var stream=File.OpenRead(path))
                {
                    var header=new byte[24];if(stream.Read(header,0,24)!=24)return false;
                    byte[] signature={137,80,78,71,13,10,26,10};
                    for(int i=0;i<signature.Length;i++)if(header[i]!=signature[i])return false;
                    int width=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19];
                    int height=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23];
                    if(width!=tile.width || height!=tile.height)return false;
                }
            }
            return true;
        }
        [Serializable]internal sealed class Catalog{public List<Sheet> maps;public string message;}
        [Serializable]internal sealed class Sheet{public string id,label;public int width,height;public List<Tile> tiles;}
        [Serializable]internal sealed class Tile{public string file,url;public int x,y,width,height;}
    }
}
