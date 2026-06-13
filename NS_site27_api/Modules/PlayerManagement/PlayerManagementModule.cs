using Discord;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Interactables.Interobjects;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using MySqlX.XDevAPI;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MapHandlers = Exiled.Events.Handlers.Map;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace NS_site27_api.Modules.PlayerManagement
{
    public class PlayerManagementModule : ModuleBase<PlayerManagementConfig>
    {
        public override string ModuleName => "PlayerManagement";

        public override void OnEnable()
        {

            PlayerHandlers.ChangingRole += OnChangingRole;
            PlayerHandlers.Verified += OnVerified;
            PlayerHandlers.Died += OnDied;
            PlayerHandlers.Escaped += OnEscaped;
            PlayerHandlers.Left += OnLeft;
            ServerHandlers.RestartingRound += OnRestarting;
            ServerHandlers.WaitingForPlayers += OnWaiting;
            ServerHandlers.RoundEnded += OnRoundEnded;
            MapHandlers.GeneratorActivating += OnGeneratorActivating;

            PlayerHUDManager.Init();
            CorePlugin.RunCoroutine(PlayerRefreshLoop(), false);
        }

        public override void OnDisable()
        {
            PlayerHandlers.ChangingRole -= OnChangingRole;
            PlayerHandlers.Verified -= OnVerified;
            PlayerHandlers.Died -= OnDied;
            PlayerHandlers.Escaped -= OnEscaped;
            PlayerHandlers.Left -= OnLeft;
            ServerHandlers.RestartingRound -= OnRestarting;
            ServerHandlers.WaitingForPlayers -= OnWaiting;
            ServerHandlers.RoundEnded -= OnRoundEnded;
            MapHandlers.GeneratorActivating -= OnGeneratorActivating;

            PlayerHUDManager.Deinit();
        }


        private MySQLConnect SQL => Plugin.Instance?.connect;

        private void OnVerified(VerifiedEventArgs ev)
        {
            var sql = SQL;
            if (sql != null)
            {
                ExperienceManager.GetServerTime(ev.Player);
                sql.Update(ev.Player.UserId, ev.Player.Nickname, last_time: DateTime.Now, ip: ev.Player.IPAddress);
            }

            PlayerHUDManager.RegisterPlayer(ev.Player);
        }

        private void OnChangingRole(ChangingRoleEventArgs ev)
        {
            Timing.CallDelayed(0.4f, () =>
            {
                try
                {
                    if (ev.Player == null) return;
                    foreach (AmmoType ammoType in Enum.GetValues(typeof(AmmoType)))
                    {
                        int newAmmo = (int)Math.Floor(ev.Player.GetAmmo(ammoType) * 1.5f);
                        if (newAmmo > ushort.MaxValue) newAmmo = ushort.MaxValue;
                        ev.Player.SetAmmo(ammoType, (ushort)newAmmo);
                    }
                }
                catch (Exception e) { Log.Warn($"[PM] ChangingRole: {e}"); }
            });
        }

        private void OnDied(DiedEventArgs ev)
        {
            var diedStats = GetOrCreateStats(ev.Player);
            if (diedStats != null) { diedStats.Deaths++; ExperienceManager.AddPoint(ev.Player, -1); }

            if (ev.Attacker == null) return;

            var atkStats = GetOrCreateStats(ev.Attacker);
            if (atkStats != null && ev.Player != ev.Attacker) { atkStats.Kills++; ExperienceManager.AddPoint(ev.Attacker, 1); }

            bool isScpKill = ev.Player.Role.Type.IsScp();
            bool isAttackerScp = ev.Attacker.IsScp;

            //BUG: Check may not account for all damage types
            if (!isAttackerScp)
            {
                int exp = isScpKill ? 50 : 10;
                if (ev.Player.Role.Type == RoleTypeId.Scp0492) exp = 20;

                int bonus = 0;
                bonus += 10 * ev.Player.GetEffect(EffectType.Scp207).Intensity;
                bonus += 20 * ev.Player.GetEffect(EffectType.Scp1344).Intensity;
                bonus += 10 * ev.Player.GetEffect(EffectType.Scp1853).Intensity;

                ExperienceManager.AddExp(ev.Attacker, exp + bonus, !isScpKill);
            }
            else
            {
                int exp = 10;
                exp += 10 * ev.Player.GetEffect(EffectType.Scp207).Intensity;
                exp += 20 * ev.Player.GetEffect(EffectType.Scp1344).Intensity;
                exp += 10 * ev.Player.GetEffect(EffectType.Scp1853).Intensity;
                ExperienceManager.AddExp(ev.Attacker, exp, true);
            }
        }

        private void OnEscaped(EscapedEventArgs ev)
        {
            ExperienceManager.AddExp(ev.Player, 25);
            if (ev.Player.IsCuffed)
                ExperienceManager.AddExp(ev.Player.Cuffer, 15, false);
        }

        private void OnLeft(LeftEventArgs ev)
        {
            var sql = SQL;
            if (sql == null) return;
            var session = ExperienceManager.GetServerTime(ev.Player);
            var user = sql.QueryUser(ev.Player.UserId);
            var total = (user.total_duration ?? TimeSpan.Zero) + session;
            sql.Update(ev.Player.UserId, name: ev.Player.Nickname, today_duration: ExperienceManager.GetTodayTime(ev.Player), total_duration: total);
            sql.Update(ev.Player.UserId, point: GetOrCreateStats(ev.Player).Points);

            ExperienceManager.StopServerTime(ev.Player);
            // 移除玩家的今日计时器，防止在OnRestarting中重复添加时间
            ExperienceManager.TodayTimers.Remove(ev.Player);
            ExperienceManager.TodayTimeCache.Remove(ev.Player);

            PlayerHUDManager.UnregisterPlayer(ev.Player);
        }

        private void OnRestarting()
        {
            var sql = SQL;
            if (sql == null) return;
            foreach (var kv in ExperienceManager.TodayTimers.ToArray())
            {
                kv.Value.Stop();
                var session = ExperienceManager.GetServerTime(kv.Key);
                ExperienceManager.StopServerTime(kv.Key);
                var user = sql.QueryUser(kv.Key.UserId);
                sql.Update(kv.Key.UserId, name: kv.Key.Nickname, today_duration: ExperienceManager.GetTodayTime(kv.Key), total_duration: (user.total_duration ?? TimeSpan.Zero) + session);
            }
            foreach (var item in RoundStats)
            {
                sql.Update(item.Key.UserId, point:item.Value.Points);

            }
            ExperienceManager.TodayTimeCache.Clear();
            ExperienceManager.TodayTimers.Clear();
            ExperienceManager.ExpCache.Clear();
        }

        private void OnWaiting()
        {
            Scp330Interobject.MaxAmountPerLife = 4;
            RoundStats.Clear();
        }

        private void OnGeneratorActivating(GeneratorActivatingEventArgs ev)
        {
            if (ev.Generator.LastActivator != null)
            {
                foreach (var p in Player.Enumerable.Where(x => x.Role.Team == ev.Generator.LastActivator.Role.Team))
                    ExperienceManager.AddExp(p, 15, true);
            }
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            foreach (var player in Player.Enumerable)
            {
                ExperienceManager.AddExp(player, 5);
                if (ev.LeadingTeam == Exiled.API.Enums.LeadingTeam.Anomalies && player.Role.Type.IsScp())
                    ExperienceManager.AddExp(player, 10);
                if ((ev.LeadingTeam == Exiled.API.Enums.LeadingTeam.FacilityForces || ev.LeadingTeam == Exiled.API.Enums.LeadingTeam.ChaosInsurgency) && player.Role.Type.IsHuman())
                    ExperienceManager.AddExp(player, 10);
            }
        }

        private IEnumerator<float> PlayerRefreshLoop()
        {
            while (true)
            {
                try
                {
                    PlayerHUDManager.ntf = 0;
                    PlayerHUDManager.doc = 0;
                    PlayerHUDManager.dd = 0;
                    PlayerHUDManager.gruad = 0;
                    PlayerHUDManager.chaos = 0;

                    foreach (var player in Player.Enumerable)
                    {
                        if (player == null) continue;
                        switch (player.Role.Type)
                        {
                            case RoleTypeId.NtfCaptain: case RoleTypeId.NtfSpecialist:
                            case RoleTypeId.NtfPrivate: case RoleTypeId.NtfSergeant:
                                PlayerHUDManager.ntf++; break;
                            case RoleTypeId.Scientist: PlayerHUDManager.doc++; break;
                            case RoleTypeId.FacilityGuard: PlayerHUDManager.gruad++; break;
                            case RoleTypeId.ChaosRifleman: case RoleTypeId.ChaosConscript:
                            case RoleTypeId.ChaosMarauder: case RoleTypeId.ChaosRepressor:
                                PlayerHUDManager.chaos++; break;
                            case RoleTypeId.ClassD: PlayerHUDManager.dd++; break;
                        }
                    if (player == null) continue;
                        PlayerStateManager.HandleBadgeSync(player, player.ReferenceHub);

                        if (player.Role is SpectatorRole spectatorRole)
                            PlayerStateManager.HandleSpectatorTracking(player, spectatorRole);
                        else if (player.Role is OverwatchRole overwatch)
                            PlayerStateManager.HandleSpectatorTracking(player, overwatch);

                        try { PlayerStateManager.HandleScpStandHeal(player); }
                        catch (Exception e) { Log.Error($"[scpheal] {player?.Nickname ?? "Unknown"}: {e.GetType().Name} - {e.Message}"); }
                    }
                }
                catch (Exception e) { Log.Error($"[PM] Refresh: {e}"); }
                yield return Timing.WaitForSeconds(0.3f);
            }
        }

        public static PlayerManagementModule Get() =>
            CorePlugin.Modules.OfType<PlayerManagementModule>().FirstOrDefault();

        public class RoundStatistics
        {
            public int Kills;
            public int Escapes;
            public int Deaths;
            public int Points;
        }

        public static Dictionary<Player, RoundStatistics> RoundStats = new Dictionary<Player, RoundStatistics>();

        public static RoundStatistics GetOrCreateStats(Player player)
        {
            if (player == null) return null;
            if (!RoundStats.ContainsKey(player))
            {
                RoundStats[player] = new RoundStatistics();
                RoundStats[player].Points = ExperienceManager.GetPoint(player);
            }
            return RoundStats[player];
        }
    }
}
