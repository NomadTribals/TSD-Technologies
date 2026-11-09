using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace TsdTechnologies.Overlay
{
    internal sealed class TsdUnifiedConfig
    {
        private readonly ConfigFile primary;
        private readonly ConfigFile legacy;
        private readonly HashSet<ConfigDefinition> saved = new HashSet<ConfigDefinition>();

        internal TsdUnifiedConfig(ConfigFile primary, string legacyPath)
        {
            this.primary = primary;
            if (File.Exists(primary.ConfigFilePath))
            {
                string section = "";
                foreach (string raw in File.ReadLines(primary.ConfigFilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                    int equals = line.IndexOf('=');
                    if (equals > 0 && section.Length > 0) saved.Add(new ConfigDefinition(section, line.Substring(0, equals).Trim()));
                }
            }
            if (File.Exists(legacyPath))
            {
                legacy = new ConfigFile(legacyPath, false);
                legacy.SaveOnConfigSet = false;
            }
        }

        internal ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            var definition = new ConfigDefinition(section, key);
            var entry = primary.Bind(section, key, defaultValue, description);
            if (!saved.Contains(definition) && legacy != null)
                entry.Value = legacy.Bind(section, key, defaultValue, description).Value;
            saved.Add(definition);
            return entry;
        }
    }
}
