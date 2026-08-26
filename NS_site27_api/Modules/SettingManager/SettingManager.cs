using Exiled.API.Features.Core.UserSettings;
using NS_site27_api.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace NS_site27_api.Modules.SettingManagement
{
    public class SettingManager : ModuleBase<SettingManagerConfig>
    {
        public static SettingManager Instance { get; private set; }

        public override string ModuleName => "SettingManager";
        public List<SettingBase> MenuCache { get; } = new List<SettingBase>();
        public Dictionary<Player, List<SettingBase>> PlayerMenuCache { get; } = new Dictionary<Player, List<SettingBase>>();

        public override void OnEnable()
        {
            PlayerHandlers.Left += OnPlayerLeft;
            Instance = this;
        }

        public override void OnDisable()
        {
            MenuCache.Clear(); PlayerHandlers.Left -= OnPlayerLeft;

            PlayerMenuCache.Clear();
            Instance = null;
        }
        private void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            _ = PlayerMenuCache.Remove(ev.Player);
        }

        public IEnumerable<SettingBase> Register(Player player, SettingBase setting, bool bypassCheck = false)
        {
            return Register(player, new SettingBase[] { setting }, bypassCheck);
        }

        public IEnumerable<SettingBase> Register(Player player, IEnumerable<SettingBase> settings, bool bypassCheck = false)
        {
            if (player == null)
            {
                return Enumerable.Empty<SettingBase>();
            }

            if (!PlayerMenuCache.TryGetValue(player, out var playerMenu))
            {
                playerMenu = new List<SettingBase>();
                PlayerMenuCache[player] = playerMenu;
            }

            var result = SettingBase.Register(
                player,
                settings.Where(x => bypassCheck || !playerMenu.Any(y => y.Id == x.Id))
            ).ToList();

            playerMenu.AddRange(result);
            return result;
        }

        public IEnumerable<SettingBase> Unregister(Player player, SettingBase setting = null, bool bypassCheck = false)
        {
            return Unregister(player, new SettingBase[] { setting }, bypassCheck);
        }

        public IEnumerable<SettingBase> Unregister(Player player, IEnumerable<SettingBase> settings = null, bool bypassCheck = false)
        {
            if (player == null)
            {
                return Enumerable.Empty<SettingBase>();
            }

            if (!PlayerMenuCache.TryGetValue(player, out var playerMenu) || playerMenu.Count == 0)
            {
                return Enumerable.Empty<SettingBase>();
            }

            var result = SettingBase.Unregister(
                player,
                settings.Where(x => bypassCheck || playerMenu.Any(y => y.Id == x.Id))
            ).ToList();

            _ = playerMenu.RemoveAll(result.Contains);
            return result;
        }
        public SettingBase GetOrCreateKeybindSetting(
            int keyId,
            string name,
            KeyCode key,
            string desc,
            Action<Player,bool> onPressed)
        {
            var existing = MenuCache.FirstOrDefault(x => x.Id == keyId);
            if (existing != null)
            {
                return existing;
            }

            try
            {
                var setting = new KeybindSetting(
                    keyId,
                    name,
                    key,
                    true,
                    hintDescription: desc,
                    onChanged: (p, sb) =>
                    {
                        if (sb is KeybindSetting kbs && kbs.Id == keyId)
                        {
                            onPressed(p, kbs.IsPressed);
                        }
                    });
                MenuCache.Add(setting);
                return setting;
            }
            catch
            {
                return null;
            }
        }
        public void RegisterForPlayer(Player player, SettingBase setting)
        {
            if (player == null || setting == null)
            {
                return;
            }

            try { _ = Register(player, setting); } catch { }
        }
        public void UnregisterForPlayer(Player player, SettingBase setting)
        {
            if (player == null || setting == null)
            {
                return;
            }

            try { _ = Unregister(player, setting); } catch { }
        }
    }
}
