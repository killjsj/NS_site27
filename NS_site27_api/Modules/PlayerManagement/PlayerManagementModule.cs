using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp049;
using Exiled.Events.EventArgs.Server;
using Interactables.Interobjects;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Firearms.Modules.Scp127;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI.DisplayKit;
using NS_site27_api.Modules.MySQL;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            PlayerHandlers.Shot += Shot;
            PlayerHandlers.DroppingAmmo += DroppingAmmo;
            PlayerHandlers.ChangedItem += ChangedItem;
            PlayerHandlers.Left += OnLeft;
            PlayerHandlers.Hurting += Hurting;
            ServerHandlers.RestartingRound += OnRestarting;
            ServerHandlers.WaitingForPlayers += OnWaiting;
            ServerHandlers.RoundEnded += OnRoundEnded;
            MapHandlers.GeneratorActivating += OnGeneratorActivating;
            Exiled.Events.Handlers.Scp079.GainingExperience += GainingExperience;
            Exiled.Events.Handlers.Scp049.FinishingRecall += FinishingRecall;
            Exiled.Events.Handlers.Player.UsedItem += UsedItem;
            Scp127TierManagerModule.ServerOnLevelledUp += Scp127TierManagerModule_ServerOnLevelledUp;
            PlayerHUDManager.Init();
            StaticUnityMethods.OnUpdate += StaticUnityMethods_OnUpdate;
            _ = Timing.RunCoroutine(PlayerRefreshLoop());
        }

        private void StaticUnityMethods_OnUpdate()
        {
            foreach (var item in ReferenceHub.AllHubs)
            {
                item.inventory.Network_syncMovementLimiter = 9999f;
            }
        }

        public override void OnDisable()
        {
            PlayerHandlers.ChangingRole -= OnChangingRole;
            PlayerHandlers.DroppingAmmo -= DroppingAmmo;
            PlayerHandlers.Verified -= OnVerified;
            PlayerHandlers.Died -= OnDied;
            PlayerHandlers.Shot -= Shot;
            PlayerHandlers.Hurting -= Hurting;
            PlayerHandlers.Escaped -= OnEscaped;
            Exiled.Events.Handlers.Player.UsedItem -= UsedItem;
            PlayerHandlers.Left -= OnLeft;
            PlayerHandlers.ChangedItem -= ChangedItem;
            ServerHandlers.RestartingRound -= OnRestarting;
            ServerHandlers.WaitingForPlayers -= OnWaiting;
            ServerHandlers.RoundEnded -= OnRoundEnded;
            Exiled.Events.Handlers.Scp079.GainingExperience -= GainingExperience;
            Exiled.Events.Handlers.Scp049.FinishingRecall -= FinishingRecall;
            Scp127TierManagerModule.ServerOnLevelledUp -= Scp127TierManagerModule_ServerOnLevelledUp;
            StaticUnityMethods.OnUpdate -= StaticUnityMethods_OnUpdate;

            MapHandlers.GeneratorActivating -= OnGeneratorActivating;

            PlayerHUDManager.Deinit();
        }
        public void DroppingAmmo(DroppingAmmoEventArgs ev)
        {
            ev.IsAllowed = false;
        }
        public void ChangedItem(ChangedItemEventArgs ev)
        {

                try
                {
                    if (ev.Player == null)
                    {
                        return;
                    }
                    if (allItems == null)
                    {
                        allItems = (ItemType[])Enum.GetValues(typeof(ItemType));
                    }
                    foreach (ItemType itemType in allItems)
                    {
                        if (GetItemBase(itemType)?.Category != ItemCategory.Ammo) { continue; }
                        ev.Player.Inventory.ServerSetAmmo(itemType, 200);
                    }
                }
                catch (Exception e) { Log.Warn($"[PM] ChangedItem: {e}"); }
            
        }
        public void Hurting(HurtingEventArgs ev)
        {
            if (ev.Player != null && ev.DamageHandler.Type == DamageType.Scp207)
            {
                ev.IsAllowed = false;
            }
        }
        public void Shot(ShotEventArgs ev)
        {
            if (ev.Player != null && ev.Item.Type != ItemType.ParticleDisruptor && ev.Item is Firearm firearm)
            {
                ev.Player.SetAmmo(firearm.AmmoType, 120);
            }
        }
        private void Scp127TierManagerModule_ServerOnLevelledUp(InventorySystem.Items.Firearms.Firearm obj)
        {
            Player player = Player.Get(obj.Owner);
            if (player != null)
            {
                _ = PlayerDataManager.AddPoint(player, 1, AddPointReason.Scp127Upgrade);
            }
        }
        private void UsedItem(UsedItemEventArgs ev)
        {
            if (ev.Player == null)
            {
                return;
            }

            switch (ev.Item.Type)
            {
                case ItemType.SCP207:
                case ItemType.SCP268:
                case ItemType.SCP2176:
                case ItemType.SCP1853:
                case ItemType.SCP1576:
                case ItemType.AntiSCP207:
                case ItemType.SCP1344:
                case ItemType.SCP1509:
                case ItemType.Scp021J:
                    _ = PlayerDataManager.AddPoint(ev.Player, 1, AddPointReason.UseScpItem);
                    break;
            }
        }
        private void FinishingRecall(FinishingRecallEventArgs ev)
        {
            if (ev.IsAllowed)
            {
                _ = PlayerDataManager.AddPoint(ev.Player, 1, AddPointReason.Scp049Revive);
            }
        }
        private void GainingExperience(Exiled.Events.EventArgs.Scp079.GainingExperienceEventArgs ev)
        {
            if (ev.Player == null)
            {
                return;
            }

            switch (ev.GainType)
            {
                case PlayerRoles.PlayableScps.Scp079.Scp079HudTranslation.ExpGainHidStopped:
                    _ = PlayerDataManager.AddPoint(ev.Player, 1, AddPointReason.Scp079StopHid);
                    break;
                case PlayerRoles.PlayableScps.Scp079.Scp079HudTranslation.ExpGainBlockingHuman:
                    _ = PlayerDataManager.AddPoint(ev.Player, 1, AddPointReason.Scp079BlockHuman);
                    break;
                case PlayerRoles.PlayableScps.Scp079.Scp079HudTranslation.ExpGainTeammateProtection:
                    _ = PlayerDataManager.AddPoint(ev.Player, 1, AddPointReason.Scp079ProtectTeammate);
                    break;
                case PlayerRoles.PlayableScps.Scp079.Scp079HudTranslation.ExpGainTerminationAssist:
                    _ = PlayerDataManager.AddPoint(ev.Player, 1, AddPointReason.Scp079KillAssist);
                    break;
            }
        }
        private MySQLConnect SQL => Plugin.Instance?.connect;

        private async void OnVerified(VerifiedEventArgs ev)
        {
            try
            {
                if (ev.Player == null || ev.Player.IsNPC)
                {
                    return;
                }

                var sql = SQL;
                if (sql != null)
                {
                    _ = PlayerDataManager.GetServerTime(ev.Player);
                    _ = sql.UpdateAsync(ev.Player.UserId, ev.Player.Nickname, last_time: DateTime.Now, ip: ev.Player.IPAddress);
                }

                PlayerHUDManager.RegisterPlayer(ev.Player);
            }
            catch (Exception ex) { Log.Error($"[PM] OnVerified: {ex}"); }
        }
        public static ItemBase GetItemBase(ItemType type)
        {
            if (!InventoryItemLoader.AvailableItems.TryGetValue(type, out var value))
            {
                return null;
            }

            return value;
        }
        public static ItemType[] allItems = null;
        private void OnChangingRole(ChangingRoleEventArgs ev)
        {
            _ = Timing.CallDelayed(0.4f, () =>
            {
                try
                {
                    if (ev.Player == null)
                    {
                        return;
                    }
                    if (allItems == null)
                    {
                        allItems = (ItemType[])Enum.GetValues(typeof(ItemType));
                    }
                    foreach (ItemType itemType in allItems)
                    {
                        if (GetItemBase(itemType)?.Category != ItemCategory.Ammo) { continue; }
                        ev.Player.Inventory.ServerSetAmmo(itemType, 200);
                    }
                }
                catch (Exception e) { Log.Warn($"[PM] ChangingRole: {e}"); }
            });
        }

        private void OnDied(DiedEventArgs ev)
        {
            _ = GetOrCreateStats(ev.Player);
            //if (diedStats != null) { PlayerDataManager.AddPoint(ev.Player, -2); }

            if (ev.Attacker == null)
            {
                return;
            }

            if (ev.Player != ev.Attacker) { _ = PlayerDataManager.AddPoint(ev.Attacker, 2, AddPointReason.Kill); }

            _ = PlayerDataManager.AddDeath(ev.Player);
            _ = PlayerDataManager.AddKills(ev.Attacker);

            bool isScpKill = ev.TargetOldRole.IsScp() && ev.TargetOldRole != RoleTypeId.Scp0492;
            bool isAttackerScp0492 = ev.Attacker.Role.Type == RoleTypeId.Scp0492;
            if (isScpKill)
            {
                _ = PlayerDataManager.AddPoint(ev.Attacker, 2, AddPointReason.KillScp);
            }

            if (isAttackerScp0492 && ev.DamageHandler.Type != DamageType.Firearm)
            {
                _ = PlayerDataManager.AddPoint(ev.Attacker, 1, AddPointReason.KillScp0492);
            }

            if (ev.Attacker.Role.Type == RoleTypeId.Scp106)
            {
                _ = PlayerDataManager.AddPoint(ev.Attacker, 1, AddPointReason.KillScp106);
            }

            if (ev.DamageHandler.Type == DamageType.PocketDimension)
            {
                _ = PlayerDataManager.AddPoint(ev.Attacker, 1, AddPointReason.PocketDimensionKill);
            }

            if (ev.Attacker.Role.Type == RoleTypeId.Scp939)
            {
                _ = PlayerDataManager.AddPoint(ev.Attacker, 1, AddPointReason.KillScp939);
            }
        }

        private void OnEscaped(EscapedEventArgs ev)
        {
            _ = PlayerDataManager.AddPoint(ev.Player, 2, AddPointReason.Escape);
            _ = PlayerDataManager.AddEscape(ev.Player);
        }

        private async void OnLeft(LeftEventArgs ev)
        {
            try
            {
                var sql = SQL;
                if (sql == null ||  ev.Player.IsNPC)
                {
                    return;
                }

                var session = PlayerDataManager.GetServerTime(ev.Player);
                var (uid, name, experience, expMultiplier, point, ip, last_time, total_duration, today_duration) = await sql.QueryUserAsync(ev.Player.UserId);
                var total = (total_duration ?? TimeSpan.Zero) + session;
                await sql.UpdateAsync(ev.Player.UserId, name: ev.Player.Nickname, today_duration: PlayerDataManager.GetTodayTime(ev.Player), total_duration: total);
                await sql.UpdateAsync(ev.Player.UserId, point: (await GetOrCreateStats(ev.Player)).Points);

                PlayerDataManager.StopServerTime(ev.Player);
                _ = PlayerDataManager.TodayTimers.Remove(ev.Player);
                _ = PlayerStateManager.HasRenamedPlayers.Remove(ev.Player);
                _ = PlayerDataManager.TodayTimeCache.Remove(ev.Player);
                ev.Player.RemoveLayer("PlayerManager");

            }
            catch (Exception ex) { Log.Error($"[PM] OnLeft: {ex}"); }
        }

        private async void OnRestarting()
        {
            try
            {
                var sql = SQL;
                if (sql == null)
                {
                    return;
                }

                foreach (var kv in PlayerDataManager.TodayTimers.ToArray())
                {
                    kv.Value.Stop();
                    var session = PlayerDataManager.GetServerTime(kv.Key);
                    PlayerDataManager.StopServerTime(kv.Key);
                    var (uid, name, experience, expMultiplier, point, ip, last_time, total_duration, today_duration) = await sql.QueryUserAsync(kv.Key.UserId);
                    await sql.UpdateAsync(kv.Key.UserId, name: kv.Key.Nickname, today_duration: PlayerDataManager.GetTodayTime(kv.Key), total_duration: (total_duration ?? TimeSpan.Zero) + session);
                }
                foreach (var item in RoundStats)
                {
                    await sql.UpdateAsync(item.Key.UserId, point: item.Value.Points);

                }
                PlayerDataManager.TodayTimeCache.Clear();
                PlayerStateManager.HasRenamedPlayers.Clear();
                PlayerDataManager.TodayTimers.Clear();
            }
            catch (Exception ex) { Log.Error($"[PM] OnRestarting: {ex}"); }
        }

        private void OnWaiting()
        {
            Scp330Interobject.MaxAmountPerLife = 4;
            RoundStats.Clear();
            PlayerStateManager.HasRenamedPlayers.Clear();
        }

        private void OnGeneratorActivating(GeneratorActivatingEventArgs ev)
        {
            if (ev.Generator.LastActivator != null)
            {
                foreach (var p in Player.Enumerable.Where(x => x.Role.Team == ev.Generator.LastActivator.Role.Team))
                {
                    _ = PlayerDataManager.AddPoint(p, 1, AddPointReason.GeneratorActivation);
                }
            }
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
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
                        if (player == null)
                        {
                            continue;
                        }

                        switch (player.Role.Type)
                        {
                            case RoleTypeId.NtfCaptain:
                            case RoleTypeId.NtfSpecialist:
                            case RoleTypeId.NtfPrivate:
                            case RoleTypeId.NtfSergeant:
                                PlayerHUDManager.ntf++; break;
                            case RoleTypeId.Scientist: PlayerHUDManager.doc++; break;
                            case RoleTypeId.FacilityGuard: PlayerHUDManager.gruad++; break;
                            case RoleTypeId.ChaosRifleman:
                            case RoleTypeId.ChaosConscript:
                            case RoleTypeId.ChaosMarauder:
                            case RoleTypeId.ChaosRepressor:
                                PlayerHUDManager.chaos++; break;
                            case RoleTypeId.ClassD: PlayerHUDManager.dd++; break;
                        }
                        if (player == null)
                        {
                            continue;
                        }

                        if(!player.IsNPC)
                            PlayerStateManager.HandleBadgeSync(player, player.ReferenceHub);

                        if (player.Role is SpectatorRole spectatorRole)
                        {
                            PlayerStateManager.HandleSpectatorTracking(player, spectatorRole);
                        }
                        else if (player.Role is OverwatchRole overwatch)
                        {
                            PlayerStateManager.HandleSpectatorTracking(player, overwatch);
                        }

                        try { PlayerStateManager.HandleScpStandHeal(player); }
                        catch (Exception e) { Log.Error($"[scpheal] {player?.Nickname ?? "Unknown"}: {e.GetType().Name} - {e.Message}"); }
                        if(!player.IsNPC)
                            PlayerStateManager.HandlePlayerRenamer(player);
                    }
                }
                catch (Exception e) { Log.Error($"[PM] Refresh: {e}"); }

                yield return Timing.WaitForSeconds(0.3f);
            }
        }

        public static PlayerManagementModule Get()
        {
            return CorePlugin.Modules.OfType<PlayerManagementModule>().FirstOrDefault();
        }

        public class RoundStatistics
        {
            public int Kills;
            public int Escapes;
            public int Deaths;
            public int Points;
        }

        public static Dictionary<Player, RoundStatistics> RoundStats = new();

        public static async Task<RoundStatistics> GetOrCreateStats(Player player)
        {
            if (player == null)
            {
                return null;
            }

            if (!RoundStats.ContainsKey(player))
            {
                RoundStats[player] = new RoundStatistics
                {
                    Points = await PlayerDataManager.GetPoint(player)
                };
            }
            return RoundStats[player];
        }
    }
}
