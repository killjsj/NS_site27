using AutoEvent.API;
using AutoEvent.API.Enums;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.CustomItems.API.Features;
using HarmonyLib;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules._Keycard;
using NS_site27_api.Modules.EventHandle;
using NS_site27_api.Modules.MySQL;
using NS_site27_api.Modules.PlayerManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;


namespace NS_site27_api
{
    public class Plugin : Exiled.API.Features.Plugin<PluginConfig>
    {
        public override string Name => "NS_site27";
        public override string Author => "killjsj";
        public override PluginPriority Priority => PluginPriority.Low;

        public static Plugin Instance { get; private set; }
        public MySQLConnect connect = new MySQLConnect();

        public static List<SettingBase> MenuCache = new List<SettingBase>();
        public static Dictionary<Player, List<SettingBase>> PlayerMenuCache = new Dictionary<Player, List<SettingBase>>();

        public static IEnumerable<SettingBase> Register(Player player, SettingBase setting, bool bypassCheck = false)
            => Register(player, new SettingBase[] { setting }, bypassCheck);

        public static IEnumerable<SettingBase> Register(Player player, IEnumerable<SettingBase> settings, bool bypassCheck = false)
        {
            if (!PlayerMenuCache.TryGetValue(player, out var playerMenu))
            {
                playerMenu = new List<SettingBase>();
                PlayerMenuCache[player] = playerMenu;
            }

            var result = SettingBase.Register(player, settings.Where(x => bypassCheck || !playerMenu.Any(y => y.Id == x.Id))).ToList();
            playerMenu.AddRange(result);
            return result;
        }

        public static IEnumerable<SettingBase> Unregister(Player player, SettingBase setting = null, bool bypassCheck = false)
            => Unregister(player, new SettingBase[] { setting }, bypassCheck);

        public static IEnumerable<SettingBase> Unregister(Player player, IEnumerable<SettingBase> settings = null, bool bypassCheck = false)
        {
            if (!PlayerMenuCache.TryGetValue(player, out var playerMenu) || playerMenu.Count == 0)
                return Enumerable.Empty<SettingBase>();

            var result = SettingBase.Unregister(player, settings.Where(x => bypassCheck || playerMenu.Any(y => y.Id == x.Id))).ToList();
            playerMenu.RemoveAll(x => result.Contains(x));
            return result;
        }

        private IUIService _uiService;

        public override void OnEnabled()
        {
            Instance = this;
            CorePlugin.Instance = this;
            Log.Info("NS_site27 plugin starting...");

            ModuleConfigManager.Initialize(this);

            _uiService = new RueIHintService();
            UIManager.Initialize(_uiService);

            if (Config.IsEnableDatabase)
            {
                var connStr = $"Server={Config.IpAddress};Port={Config.Port};Database={Config.Database};Uid={Config.Username};Pwd={Config.Password};allowPublicKeyRetrieval=true;Connection Timeout=30;";
                connect.Connect(Config.IpAddress, Config.Port, Config.Username, Config.Password, Config.Database);
            }

            CorePlugin.Harmony = new Harmony("NS_site27.plugin");
            CorePlugin.Harmony.PatchAll();

            DiscoverAndLoadModules();

            WireModuleDependencies();

            ServerHandlers.WaitingForPlayers += OnWaitingForPlayers;
            ServerHandlers.RestartingRound += OnRestartingRound;
            PlayerHandlers.Left += OnPlayerLeft;
            CustomItem.RegisterItems();

            Log.Info($"NS_site27 plugin enabled with {CorePlugin.Modules.Count} modules.");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            ServerHandlers.WaitingForPlayers -= OnWaitingForPlayers;
            ServerHandlers.RestartingRound -= OnRestartingRound;
            PlayerHandlers.Left -= OnPlayerLeft;

            foreach (var module in CorePlugin.Modules.Reverse<IModule>())
            {
                try { module.OnDisable(); }
                catch (Exception ex) { Log.Error($"Error disabling module {module.ModuleName}: {ex}"); }
            }

            CorePlugin.Modules.Clear();
            MenuCache.Clear();
            PlayerMenuCache.Clear();
            CorePlugin.Harmony.UnpatchAll();
            CorePlugin.Harmony = null;
            CorePlugin.Instance = null;

            connect.Close();

            Log.Info("NS_site27 plugin disabled.");
            base.OnDisabled();
        }

        public override void OnReloaded()
        {
            CorePlugin.Harmony.UnpatchAll();
            CorePlugin.Harmony.PatchAll();

            foreach (var module in CorePlugin.Modules)
            {
                try
                {
                    module.OnDisable();
                    module.OnEnable();
                }
                catch (Exception ex) { Log.Error($"Error reloading module {module.ModuleName}: {ex}"); }
            }
            base.OnReloaded();
        }

        private void DiscoverAndLoadModules()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var moduleTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IModule).IsAssignableFrom(t))
                .ToList();

            foreach (var type in moduleTypes)
            {
                try
                {

                        var obj = (IModule)Activator.CreateInstance(type);
                        if (!obj.IsEnabled) continue;
                        obj.OnEnable();
                        CorePlugin.Modules.Add(obj);
                    
                    Log.Info($"Module '{obj.ModuleName}' loaded.");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to load module {type.FullName}: {ex}");
                }
            }

        }

        private void WireModuleDependencies()
        {
            var keycardModule = CorePlugin.Modules.OfType<KeycardModule>().FirstOrDefault();
            var eventHandleModule = CorePlugin.Modules.OfType<EventHandleModule>().FirstOrDefault();
            var playerMgmtModule = CorePlugin.Modules.OfType<PlayerManagementModule>().FirstOrDefault();

            if (keycardModule != null)
                keycardModule.SetSQL(connect);

            if (eventHandleModule != null)
                eventHandleModule.SetSQL(connect);

            if (playerMgmtModule != null)
                playerMgmtModule.SetSQL(connect);
        }

        private void OnWaitingForPlayers()
        {
            CorePlugin.RestartingRound();
        }

        private void OnRestartingRound()
        {
            CorePlugin.RestartingRound();
        }

        private void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            _uiService?.CleanupPlayer(ev.Player);
            PlayerMenuCache.Remove(ev.Player);
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ReloadConfigCommand : ICommand
    {
        public string Command => "site27_reload";
        public string[] Aliases => new[] { "s27rl" };
        public string Description => "Reload all module configs from disk";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.ServerConfigs, out response))
            {
                return false;
            }
            ModuleConfigManager.ClearCache();

            foreach (var module in CorePlugin.Modules)
            {
                try { module.OnReloadConfig(); }
                catch (Exception ex) { Log.Error($"Error reloading module {module.ModuleName}: {ex}"); }
            }

            response = $"Config cache cleared, {CorePlugin.Modules.Count} modules reloaded.";
            return true;
        }
    }
}
