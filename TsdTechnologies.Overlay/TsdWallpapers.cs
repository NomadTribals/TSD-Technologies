using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;

namespace TsdTechnologies.Overlay
{
    internal sealed class TsdWallpapers
    {
        internal string Folder { get; private set; }
        private readonly ConfigEntry<string> selected;
        private readonly ConfigEntry<string> mode;
        private readonly Action<string> send;
        private readonly object gate = new object();
        private List<Entry> entries = Builtins();
        private int scanning;
        internal TsdWallpapers(ConfigEntry<string> selected, ConfigEntry<string> mode, Action<string> send)
        {
            this.selected = selected; this.mode = mode; this.send = send;
            Folder = Path.Combine(BepInEx.Paths.ConfigPath, "TsdTechnologies", "Wallpapers");
            Directory.CreateDirectory(Folder);
        }
        private static List<Entry> Builtins() { return new List<Entry> {
            new Entry { id = "builtin:signal", name = "TSD Signal", style = "signal" },
            new Entry { id = "builtin:horizon", name = "TSD Horizon", style = "horizon" }
        }; }
        internal void Refresh()
        {
            if (Interlocked.Exchange(ref scanning, 1) != 0) return;
            Task.Run(() => {
                string message = "Wallpapers refreshed.";
                var found = Builtins();
                int skipped = 0;
                try
                {
                    Directory.CreateDirectory(Folder);
                    string thumbnails = Path.Combine(Folder, ".thumbnails");
                    Directory.CreateDirectory(thumbnails);
                    foreach (string path in Directory.EnumerateFiles(Folder).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    {
                        string extension = Path.GetExtension(path).ToLowerInvariant();
                        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") continue;
                        if (found.Count >= 66) { skipped++; continue; }
                        try
                        {
                            var file = new FileInfo(path);
                            if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length == 0 || file.Length > 20 * 1024 * 1024) { skipped++; continue; }
                            string stamp = file.Length + ":" + file.LastWriteTimeUtc.Ticks;
                            string hash;
                            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(file.Name + ":" + stamp))).Replace("-", "").ToLowerInvariant();
                            string thumbnailName = hash + ".jpg";
                            string thumbnailPath = Path.Combine(thumbnails, thumbnailName);
                            using (var stream = File.OpenRead(path))
                            using (var image = System.Drawing.Image.FromStream(stream, false, false))
                            {
                                if (image.Width < 1 || image.Height < 1 || image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 33554432
                                    || (image.RawFormat.Guid != ImageFormat.Png.Guid && image.RawFormat.Guid != ImageFormat.Jpeg.Guid)) { skipped++; continue; }
                                if (!File.Exists(thumbnailPath))
                                    using (var thumbnail = new Bitmap(240, 135))
                                    using (var graphics = System.Drawing.Graphics.FromImage(thumbnail))
                                    {
                                        graphics.Clear(System.Drawing.Color.FromArgb(9, 11, 14));
                                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                        float scale = Math.Min(240f / image.Width, 135f / image.Height);
                                        int width = Math.Max(1, (int)(image.Width * scale)), height = Math.Max(1, (int)(image.Height * scale));
                                        graphics.DrawImage(image, (240 - width) / 2, (135 - height) / 2, width, height);
                                        thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                                    }
                            }
                            found.Add(new Entry { id = "file:" + file.Name, name = Path.GetFileNameWithoutExtension(file.Name), style = "custom",
                                url = "https://tsd-wallpapers.assets/" + Uri.EscapeDataString(file.Name) + "?v=" + file.LastWriteTimeUtc.Ticks,
                                thumbnail = "https://tsd-wallpapers.assets/.thumbnails/" + thumbnailName });
                        }
                        catch { skipped++; }
                    }
                    if (skipped > 0) message += " " + skipped + " image(s) skipped; use PNG/JPG up to 20 MB and 8192 pixels per side.";
                }
                catch { message = "The wallpaper folder could not be read. Built-in wallpapers are available."; }
                finally
                {
                    lock (gate) entries = found;
                    Interlocked.Exchange(ref scanning, 0);
                    Publish(message);
                }
            });
        }
        internal void Apply(string payload)
        {
            try
            {
                if (string.IsNullOrEmpty(payload) || payload.Length > 2048) return;
                var request = JsonUtility.FromJson<Selection>(payload);
                if (request == null || (request.mode != "fill" && request.mode != "fit")) { Publish("Choose Fill or Fit."); return; }
                lock (gate)
                    if (!entries.Any(e => e.id == request.id)) { Publish("That wallpaper is unavailable. Refresh the list and try again."); return; }
                selected.Value = request.id;
                mode.Value = request.mode;
                Publish("Wallpaper saved.");
            }
            catch { Publish("The wallpaper could not be saved. Try refreshing the list."); }
        }
        private void Publish(string message)
        {
            List<Entry> snapshot;
            lock (gate) snapshot = new List<Entry>(entries);
            string id = snapshot.Any(e => e.id == selected.Value) ? selected.Value : "builtin:signal";
            if (id != selected.Value) message = "Previous wallpaper unavailable; using TSD Signal. " + message;
            try { send(JsonUtility.ToJson(new Catalog { items = snapshot, selected = id, mode = mode.Value == "fit" ? "fit" : "fill", folder = Folder, message = message })); }
            catch { /* The tablet may be shutting down while a refresh completes. */ }
        }
        [Serializable] internal sealed class Entry { public string id, name, style, url, thumbnail; }
        [Serializable] internal sealed class Catalog { public List<Entry> items; public string selected, mode, folder, message; }
        [Serializable] private sealed class Selection { public string id, mode; }
    }
}
