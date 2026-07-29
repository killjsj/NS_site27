using AutoEvent.API;
using AutoEvent.API.Enums;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.CustomItems.API.Features;
using Exiled.CustomRoles.API.Features;
using HarmonyLib;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using NS_site27_api.Core.UI.DisplayKit;
using NS_site27_api.Modules._Keycard;
using NS_site27_api.Modules.EventHandle;
using NS_site27_api.Modules.MySQL;
using NS_site27_api.Modules.PlayerManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;


namespace NS_site27_api
{
    public class Plugin : Exiled.API.Features.Plugin<PluginConfig>
    {
        public override string Name => "NS_site27";
        public override string Author => "killjsj";
        public override PluginPriority Priority => PluginPriority.Medium;

        public static Plugin Instance { get; private set; }
        public MySQLConnect connect = new();

        public override void OnEnabled()
        {
            Instance = this;
            CorePlugin.Instance = this;
            Log.Info("NS_site27 plugin starting...");

            ModuleConfigManager.Initialize(this);

            var d = new DisplayKitRunner();
            d.Enable();
            if (Config.IsEnableDatabase)
            {
                _ = connect.ConnectAsync(Config.IpAddress, Config.Port, Config.Username, Config.Password, Config.Database);
            }

            CorePlugin.Harmony = new Harmony("NS_site27.plugin");
            CorePlugin.Harmony.PatchAll();

            DiscoverAndLoadModules();
            CustomRole.RegisterRoles(false);


            ServerHandlers.WaitingForPlayers += OnWaitingForPlayers;
            ServerHandlers.RestartingRound += OnRestartingRound;
            CustomItem.RegisterItems();

            Log.Info($"NS_site27 plugin enabled with {CorePlugin.Modules.Count} modules.");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            ServerHandlers.WaitingForPlayers -= OnWaitingForPlayers;
            ServerHandlers.RestartingRound -= OnRestartingRound;

            DisplayKitRunner.Instance?.Disable();

            foreach (var module in CorePlugin.Modules.Reverse<IModule>())
            {
                try { module.OnDisable(); }
                catch (Exception ex) { Log.Error($"Error disabling module {module.ModuleName}: {ex}"); }
            }

            CorePlugin.Modules.Clear();
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
                    if (!module.IsEnabled)
                    {
                        continue;
                    }

                    module.OnDisable();
                    module.OnEnable();
                }
                catch (Exception ex) { Log.Error($"Error reloading module {module.ModuleName}: {ex}"); }
            }
            base.OnReloaded();
        }

        public static void DiscoverAndLoadModules()
        {
            var assembly = Assembly.GetCallingAssembly();
            var moduleTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IModule).IsAssignableFrom(t))
                .ToList();

            foreach (var type in moduleTypes)
            {
                try
                {

                    var obj = (IModule)Activator.CreateInstance(type);
                    if (!obj.IsEnabled)
                    {
                        continue;
                    }

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
        private void OnWaitingForPlayers()
        {
            CorePlugin.RestartingRound();
        }

        private void OnRestartingRound()
        {
            CorePlugin.RestartingRound();
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
