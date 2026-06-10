using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Interactables.Interobjects.DoorUtils;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Modules.Admin;
using NS_site27_api.Modules.Duel;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace NS_site27_api.Modules.PlayerManagement
{
    public static class PlayerStateManager
    {

        public static Dictionary<string, (string player_name, string badge, List<string> color, DateTime expiration_date, bool is_permanent, string notes)> badges = new();
        public static Dictionary<Player, List<Player>> SpecList = new();
        public static Dictionary<Player, CoroutineHandle> rainbowC = new();
        public static Dictionary<Player, (Stopwatch stand, double lastTime, Vector3 lastPos)> ScpStandHP = new();

        public static List<string> colors = new List<string>() { "red", "green", "yellow", "cyan", "magenta" };
        public static Dictionary<Player, CoroutineHandle> RainbowCoroutines = new();

        public static void Init()
        {
            PlayerHandlers.InteractingDoor += OnInteractDoor;
            PlayerHandlers.EnteringPocketDimension += OnEnterPocket;
            PlayerHandlers.EscapingPocketDimension += OnEscapePocket;
            PlayerHandlers.FailingEscapePocketDimension += OnFailPocket;
        }

        public static void Deinit()
        {
            PlayerHandlers.InteractingDoor -= OnInteractDoor;
            PlayerHandlers.EnteringPocketDimension -= OnEnterPocket;
            PlayerHandlers.EscapingPocketDimension -= OnEscapePocket;
            PlayerHandlers.FailingEscapePocketDimension -= OnFailPocket;
        }

        private static Dictionary<Player, Player> _scp106Catchers = new();

        private static void OnInteractDoor(InteractingDoorEventArgs ev)
        {
            if (ev.Door.IsMoving || ev.Door.IsNonInteractable || ev.Door.IsLocked) return;
            foreach (var item in ev.Player.Items)
            {
                if (item.Base is IDoorPermissionProvider dp)
                {
                    if (ev.Door.Base.CheckPermissions(dp, out var _))
                        ev.IsAllowed = true;
                }
            }
        }

        private static void OnEnterPocket(EnteringPocketDimensionEventArgs ev)
        {
            if (ev.Player != null && ev.Scp106 != null)
                _scp106Catchers[ev.Player] = ev.Scp106;
        }

        private static void OnEscapePocket(EscapingPocketDimensionEventArgs ev)
        {
            if (ev.Player != null && _scp106Catchers.ContainsKey(ev.Player))
                _scp106Catchers.Remove(ev.Player);
        }

        private static void OnFailPocket(FailingEscapePocketDimensionEventArgs ev)
        {
            if (ev.Player != null && _scp106Catchers.TryGetValue(ev.Player, out var scp))
            {
                scp.ShowHint("<color=red>你抓到了一个目标!</color>", 5);
                _scp106Catchers.Remove(ev.Player);
            }
        }
        public static void HandleScpStandHeal(Player player)
        {
            if (!(player.Role?.Base is IFpcRole fpcRole) || !player.IsScp) return;

            double interval = 1.0;

            if (!ScpStandHP.TryGetValue(player, out var data))
                ScpStandHP[player] = (Stopwatch.StartNew(), 0.0, player.Position);

            var (stopwatch, lastHealTime, lastPos) = ScpStandHP[player];
            double elapsed = stopwatch.Elapsed.TotalSeconds;

            if (Vector3.Distance(player.Position, lastPos) < 0.5f)
            {
                if (elapsed >= CorePlugin.GetModule<PlayerManagementModule>().Config.ScpStandHealTime)
                {
                    if (elapsed - lastHealTime >= interval)
                    {
                        player.Heal(player.Role.Type.IsScp() ? CorePlugin.GetModule<PlayerManagementModule>().Config.ScpStandHealAmount : CorePlugin.GetModule<PlayerManagementModule>().Config.ScpStandHealAmount / 2);
                        ScpStandHP[player] = (stopwatch, elapsed, player.Position);
                    }
                }
            }
            else
            {
                stopwatch.Restart();
                ScpStandHP[player] = (stopwatch, 0.0, player.Position);
            }
        }
        public static void HandleBadgeSync(Player player, ReferenceHub hub)
        {
            (string player_name, string badge, List<string> color, DateTime expiration_date, bool is_permanent, string notes) badgeData = ("","",new List<string>(new[]{ "white" }),DateTime.UtcNow,true,"");
            if (!badges.TryGetValue(player.UserId, out badgeData) && !player.RemoteAdminAccess) return;
            if (DuelManager.PlayerBadges.ContainsKey(player.UserId)) return;
            var text = badgeData.badge;
            if (player.RemoteAdminAccess && AdminAssignModule.CachedGroups.ContainsKey(player))
            {
                text += $"({AdminAssignModule.CachedGroups[player].BadgeText})";
            }
            if (hub.serverRoles.Network_myText == null)
                player.RankName = text;

            if (!hub.serverRoles.Network_myText.Contains(text))
                player.RankName = text;

            if (badgeData.color != null && badgeData.color.Contains("rainbow"))
            {
                if (!rainbowC.ContainsKey(player))
                    rainbowC[player] = Timing.RunCoroutine(RainbowTimeCoroutine(player, colors));
                else if (!rainbowC[player].IsRunning)
                    rainbowC[player] = Timing.RunCoroutine(RainbowTimeCoroutine(player, colors));
            }
            else
            {
                rainbowC[player] = Timing.RunCoroutine(RainbowTimeCoroutine(player, badgeData.color));
            }
        }
        public static IEnumerator<float> RainbowTimeCoroutine(Player player, List<string> colorsList)
        {
            if (player == null) yield break;
            while (player != null)
            {
                foreach (var color in colorsList)
                {
                    if (player == null) break;
                    player.RankColor = color;
                    yield return Timing.WaitForSeconds(1.5f);
                }
                if (player == null) break;
                yield return Timing.WaitForSeconds(1.5f);
            }
        }

        public static IEnumerator<float> RainbowTimeCoroutine(Player player, string singleColor)
        {
            if (player == null) yield break;
            player.RankColor = singleColor;
            yield break;
        }
        public static void HandleSpectatorTracking(Player player, SpectatorRole spectatorRole)
        {
            if (player == null || !player.IsConnected) return;

            var target = spectatorRole?.SpectatedPlayer;

            foreach (var kv in SpecList.Keys.ToList())
            {
                if (kv == null || !kv.IsConnected)
                    SpecList.Remove(kv);
            }

            var keysToUpdate = new List<Player>();
            foreach (var entry in SpecList.ToList())
            {
                if (entry.Value.Contains(player))
                    keysToUpdate.Add(entry.Key);
            }

            foreach (var key in keysToUpdate)
            {
                SpecList[key].Remove(player);
                if (SpecList[key].Count == 0)
                    SpecList.Remove(key);
            }

            if (target == null || !target.IsConnected) return;

            if (!SpecList.ContainsKey(target))
                SpecList[target] = new List<Player>();

            if (!SpecList[target].Contains(player))
                SpecList[target].Add(player);
        }

        public static void HandleSpectatorTracking(Player player, OverwatchRole overwatch)
        {
            HandleSpectatorTracking(player, overwatch as SpectatorRole);
        }

        public static void RemoveFromSpectatorLists(Player player)
        {
            var keysToUpdate = new List<Player>();
            foreach (var entry in SpecList.ToList())
            {
                if (entry.Value.Contains(player))
                    keysToUpdate.Add(entry.Key);
            }
            foreach (var key in keysToUpdate)
            {
                SpecList[key].Remove(player);
                if (SpecList[key].Count == 0)
                    SpecList.Remove(key);
            }
        }
    }
}
