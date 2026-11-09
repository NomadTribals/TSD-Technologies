using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace TsdTechnologies.Api
{
    public enum TabletPostOptions { None, LatestOnly }
    public interface ITabletAppHost
    {
        event Action<string, string> ChannelMessage;
        event Action<bool> VisibilityChanged;
        void ExecuteScript(string script);
        void Post(string channel, string payload, TabletPostOptions options = TabletPostOptions.None);
    }
    public sealed class TabletApplication
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IconSvg { get; set; }
        public string Document { get; set; }
        public string[] Channels { get; set; } = new string[0];
        public string LogPage { get; set; }
        public Action<ITabletAppHost> Attach { get; set; }
    }
    public sealed class TabletContext
    {
        public bool HasTablet { get; set; }
        public bool InRaid { get; set; }
        public string Nickname { get; set; }
        public string RaidTime { get; set; }
        public long UpdatedUtcTicks { get; set; }
        public bool IsFresh { get { return UpdatedUtcTicks > 0 && Math.Abs(DateTime.UtcNow.Ticks - UpdatedUtcTicks) < TimeSpan.FromSeconds(6).Ticks; } }
    }
    public static class TabletRuntime
    {
        public const string PluginGuid = "com.nomadtribals.tsdtechnologies";
        public const string TerminalTemplateId = "6a0000000000000000000002";
        private static readonly object Gate = new object();
        private static long revision;
        public static long Revision { get { lock(Gate) return revision; } }
        private static TabletContext context = new TabletContext();
        private static readonly Dictionary<string, TabletApplication> Applications = new Dictionary<string, TabletApplication>(StringComparer.Ordinal);
        public static TabletContext Context { get { lock(Gate) return context; } }
        public static void PublishContext(TabletContext value) { if(value == null) throw new ArgumentNullException(nameof(value)); lock(Gate) context = value; }
        public static void Register(TabletApplication app)
        {
            if(app == null || !Regex.IsMatch(app.Id ?? "", "^[a-z][a-z0-9_.-]{1,63}$") || string.IsNullOrWhiteSpace(app.Name) || app.Name.Length > 80
                || string.IsNullOrWhiteSpace(app.Document) || app.Document.Length > 2097152 || (app.IconSvg?.Length ?? 0) > 65536 || app.Attach == null)
                throw new ArgumentException("An app requires a valid id, name, document and attach handler.");
            if(app.Channels == null || app.Channels.Length > 32 || app.Channels.Any(c=>string.IsNullOrEmpty(c)||!Regex.IsMatch(c,"^[a-zA-Z0-9_.-]{1,80}$")))
                throw new ArgumentException("App channels must be named and limited to 32.");
            lock(Gate) { if(Applications.ContainsKey(app.Id)) throw new InvalidOperationException("Tablet app id is already registered: " + app.Id); Applications.Add(app.Id, app); revision++; }
        }
        public static void Unregister(string id) { lock(Gate) if(Applications.Remove(id)) revision++; }
        public static TabletApplication[] GetApplications() { lock(Gate) return Applications.Values.OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray(); }
    }
}
