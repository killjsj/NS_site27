using Exiled.API.Features;
using Exiled.API.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using MEC;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using NS_site27_api.Modules.EventHandle;

namespace NS_site27_api.Core
{
    public interface IModule
    {
        string ModuleName { get; }
        bool IsEnabled { get; }
        void OnEnable();
        void OnDisable();
        void OnReloadConfig();
    }

    public static class CorePlugin
    {
        public static Plugin Instance { get; internal set; }
        public static HarmonyLib.Harmony Harmony { get; internal set; }
        public static List<IModule> Modules { get; } = new List<IModule>();
        public static List<CoroutineHandle> ClearOnEnd { get; } = new List<CoroutineHandle>();

        public static string ConfigFolder => Instance.ConfigPath;

        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, bool stopOnEnd = true)
        {
            var ch = Timing.RunCoroutine(coroutine);
            if (stopOnEnd)
                ClearOnEnd.Add(ch);
            return ch;
        }
        public static T GetModule<T>() where T : IModule
        {
            return Modules.OfType<T>().FirstOrDefault();
        }
        public static void RestartingRound()
        {
            foreach (var item in ClearOnEnd)
            {
                if (item.IsRunning)
                    Timing.KillCoroutines(item);
            }
            ClearOnEnd.Clear();
        }
    }

    public abstract class ModuleBase<T> : IModule where T : ModuleConfigBase, new()
    {
        public abstract string ModuleName { get; }
        public virtual bool IsEnabled => Config.IsEnabled;
        public abstract void OnEnable();
        public abstract void OnDisable();

        public virtual void OnReloadConfig()
        {
            
        }
        public T Config => GetConfig();
        public T GetConfig()
        {
            return ModuleConfigManager.Get<T>(ModuleName);
        }
    }

    public class PluginConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; }

        [Description("数据库连接地址")]
        public string IpAddress { get; set; } = "127.0.0.1";
        [Description("数据库端口")]
        public uint Port { get; set; } = 3306;
        [Description("数据库用户名")]
        public string Username { get; set; } = "root";
        [Description("数据库密码")]
        public string Password { get; set; } = "";
        [Description("数据库库名")]
        public string Database { get; set; } = "scp_site27";
        [Description("是否启用数据库")]
        public bool IsEnableDatabase { get; set; } = true;
    }
}
