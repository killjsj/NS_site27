using NS_site27_api.Core.UI;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.CustomItems.API.Features;
using HarmonyLib;
using NS_site27_heavy.Core;
using NS_site27_heavy.heavy.SpecialWaveManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using Exiled.CustomRoles.API.Features;


namespace NS_site27_heavy
{
    public class Plugin : Exiled.API.Features.Plugin<PluginConfig>
    {
        public override string Name => "NS_site27_heavy";
        public override string Author => "killjsj";
        public override PluginPriority Priority => PluginPriority.Low;

        public static Plugin Instance { get; private set; }

        public override void OnEnabled()
        {
            Instance = this;
            HeavyCorePlugin.Instance = this;
            Log.Info("NS_site27 <color=red>heavy</color> plugin starting...");

            ModuleConfigManager.Initialize(this);
            HeavyCorePlugin.Harmony = new Harmony("NS_site27.plugin.heavy");
            HeavyCorePlugin.Harmony.PatchAll();

            DiscoverAndLoadModules();
            CustomRole.RegisterRoles(false);

            ServerHandlers.WaitingForPlayers += OnWaitingForPlayers;
            ServerHandlers.RestartingRound += OnRestartingRound;
            PlayerHandlers.Left += OnPlayerLeft;
            CustomItem.RegisterItems();

            Log.Info($"NS_site27 heavy plugin enabled with {HeavyCorePlugin.Modules.Count} modules.");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            ServerHandlers.WaitingForPlayers -= OnWaitingForPlayers;
            ServerHandlers.RestartingRound -= OnRestartingRound;
            PlayerHandlers.Left -= OnPlayerLeft;

            foreach (var module in HeavyCorePlugin.Modules.Reverse<IModule>())
            {
                try { module.OnDisable(); }
                catch (Exception ex) { Log.Error($"Error disabling module {module.ModuleName}: {ex}"); }
            }

            HeavyCorePlugin.Modules.Clear();
            HeavyCorePlugin.Harmony.UnpatchAll();
            HeavyCorePlugin.Harmony = null;
            HeavyCorePlugin.Instance = null;

            Log.Info("NS_site27 plugin heavy disabled.");
            base.OnDisabled();
        }

        public override void OnReloaded()
        {
            HeavyCorePlugin.Harmony.UnpatchAll();
            HeavyCorePlugin.Harmony.PatchAll();

            foreach (var module in HeavyCorePlugin.Modules)
            {
                try
                {
                    if (!module.IsEnabled) continue;
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
                        if (!obj.IsEnabled) continue;
                        obj.OnEnable();
                        HeavyCorePlugin.Modules.Add(obj);
                    
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
            HeavyCorePlugin.RestartingRound();
        }

        private void OnRestartingRound()
        {
            HeavyCorePlugin.RestartingRound();
        }

        private void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            ev.Player.CleanupPlayer();
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ReloadConfigCommand : ICommand
    {
        public string Command => "site27_reload_heavy";
        public string[] Aliases => new[] { "s27rlh" };
        public string Description => "Reload all module configs in heavy from disk";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.ServerConfigs, out response))
            {
                return false;
            }
            ModuleConfigManager.ClearCache();

            foreach (var module in HeavyCorePlugin.Modules)
            {
                try { module.OnReloadConfig(); }
                catch (Exception ex) { Log.Error($"Error reloading module {module.ModuleName}: {ex}"); }
            }

            response = $"Config cache cleared, {HeavyCorePlugin.Modules.Count} modules reloaded.";
            return true;
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ForceWaveCommand : ICommand
    {
        public string Command => "s27_forcewave";
        public string[] Aliases => new[] { "s27fw" };
        public string Description => "Force start a special wave by name";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.ServerConfigs, out response))
            {
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Usage: s27_forcewave <WaveName>,available waves:";
                foreach (var module in SpecWaveManager.RegWaves)
                {
                    response += $"\n- {module.WaveName}";
                }
                return false;
            }

            var waveName = arguments.First();
            var manager = HeavyCorePlugin.GetModule<SpecWaveManager>();
            if (manager == null)
            {
                response = "SpecWaveManager module is not loaded.";
                return false;
            }

            if (SpecWaveManager.StartWave(SpecWaveManager.GetWave(waveName)))
            {
                response = $"Started wave '{waveName}'.";
                return true;
            }

            response = $"Failed to start wave '{waveName}'. Check the wave name or whether another wave animation is running.";
            return false;
        }
    }
}
