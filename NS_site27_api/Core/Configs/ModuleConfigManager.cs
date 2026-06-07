using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NS_site27_api.Core
{
    public abstract class ModuleConfigBase
    {
        [YamlIgnore]
        public string ModuleName { get; set; }
        [YamlMember(Description="是否启用 reload不重载这个 需要重启服务器")]
        public bool IsEnabled { get; set; } = true;

    }

    public static class ModuleConfigManager
    {
        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();

        private static readonly Dictionary<string, object> ConfigCache = new Dictionary<string, object>();
        private static string ConfigDir;

        public static void Initialize(Plugin plugin)
        {
            ConfigDir = Path.Combine(Path.GetDirectoryName(plugin.ConfigPath),"ModulesConfig" , Server.Port.ToString());
            if (!Directory.Exists(ConfigDir)) { 
                Directory.CreateDirectory(ConfigDir);
            }
        }

        public static void ClearCache()
        {
            ConfigCache.Clear();
        }
        public static T Get<T>(string moduleName) where T : ModuleConfigBase, new()
        {
            if (ConfigCache.TryGetValue(moduleName, out var cached))
                return (T)cached;

            var path = Path.Combine(ConfigDir, $"{moduleName}.yml");

            if (File.Exists(path))
            {
                try
                {
                    var yaml = File.ReadAllText(path);
                    var config = Deserializer.Deserialize<T>(yaml);
                    config.ModuleName = moduleName;
                    ConfigCache[moduleName] = config;
                    return config;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load config for {moduleName}, using defaults: {ex.Message}");
                }
            }

            var defaultConfig = new T { ModuleName = moduleName };
            ConfigCache[moduleName] = defaultConfig;

            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(path, Serializer.Serialize(defaultConfig));
                Log.Info($"Created default config for module {moduleName} at {path}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to write default config for {moduleName}: {ex}");
            }

            return defaultConfig;
        }
    }
}
