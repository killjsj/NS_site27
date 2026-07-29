using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp914;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using NS_site27_api.Core.UI.DisplayKit;
using PlayerRoles;
using Scp914;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Log = Exiled.API.Features.Log;
using MapHandlers = Exiled.Events.Handlers.Map;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using Scp914Handlers = Exiled.Events.Handlers.Scp914;

namespace NS_site27_api.Modules.PlayerManagement
{
    public static class PlayerHUDManager
    {
        public static int doc, ntf, gruad, chaos, dd;
        public static int ntfWave, ChaosCount;
        public static Stopwatch WaveCalc = new();

        public static List<ScoreChange> ScoreQueue = new();
        public static Queue<(Player p, Scp914KnobSetting knob, bool act)> Scp914q = new();

        public struct ScoreChange { public Player Player; public int Amount; public string Reason; public float Time; }
        public struct ElevatorInteractInfo { public Vector3 InteractAt; public Player Interactor; public float InteractTime; }
        public struct NukeInteractInfo { public Player Interactor; public float InteractTime; public bool acted; }
        public static List<ElevatorInteractInfo> ElevatorInteractions = new();
        public static List<NukeInteractInfo> NukeInteractions = new();



        public static void Init()
        {
            PlayerHandlers.InteractingElevator += InteractingElevator;
            Scp914Handlers.ChangingKnobSetting += ChangingKnobSetting;
            Scp914Handlers.Activating += Activating;
            Exiled.Events.Handlers.Warhead.Starting += Starting;
            Exiled.Events.Handlers.Warhead.Stopping += Stopping;
            MapHandlers.AnnouncingNtfEntrance += AnnouncingNtfEntrance;
            MapHandlers.AnnouncingChaosEntrance += AnnouncingChaosEntrance;
            PlayerHandlers.ChangingRole += ChangingRole;
            Exiled.Events.Handlers.Server.WaitingForPlayers += WaitingForPlayers;
            Exiled.Events.Handlers.Player.Died += Died;
            Exiled.Events.Handlers.Player.Left += Left;
        }

        public static void Deinit()
        {
            PlayerHandlers.InteractingElevator -= InteractingElevator;
            Exiled.Events.Handlers.Warhead.Starting -= Starting;
            Exiled.Events.Handlers.Warhead.Stopping -= Stopping;
            Scp914Handlers.ChangingKnobSetting -= ChangingKnobSetting;
            Scp914Handlers.Activating -= Activating;
            MapHandlers.AnnouncingNtfEntrance -= AnnouncingNtfEntrance;
            MapHandlers.AnnouncingChaosEntrance -= AnnouncingChaosEntrance;
            PlayerHandlers.ChangingRole -= ChangingRole;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= WaitingForPlayers;
            Exiled.Events.Handlers.Player.Died -= Died;
            Exiled.Events.Handlers.Player.Left -= Left;
        }
        public static void Starting(StartingEventArgs ev)
        {
            NukeInteractions.Add(new NukeInteractInfo { Interactor = ev.Player, InteractTime = Time.time, acted = true });
        }
        public static void Stopping(StoppingEventArgs ev)
        {
            NukeInteractions.Add(new NukeInteractInfo { Interactor = ev.Player, InteractTime = Time.time, acted = false });
        }
        public static void ChangingRole(ChangingRoleEventArgs ev)
        {
            if (Scp.Contains(ev.Player))
            {
                RemoveScp(ev.Player);
            }
            _ = Timing.CallDelayed(0.2f, () =>
            {
                if (IsScpRole(ev.Player.Role))
                {
                    AddScp(ev.Player, ev.NewRole);
                }
                else
                {
                    if (Scp.Contains(ev.Player))
                    {
                        RemoveScp(ev.Player);
                    }
                }
                PlayerManagerDisplayKitHUD.ClearCacheForPlayer(ev.Player);

            });
        }
        private static bool IsScpRole(RoleTypeId role)
        {
            return role is RoleTypeId.Scp173 or RoleTypeId.Scp106 or RoleTypeId.Scp049 or
                   RoleTypeId.Scp079 or RoleTypeId.Scp096 or RoleTypeId.Scp0492 or
                   RoleTypeId.Scp939 or RoleTypeId.Scp3114;
        }

        public static void RegisterPlayer(Player player)
        {
            if (player == null)
            {
                return;
            }

            player.AddLayer("PlayerManager");
        }



        public static HashSet<Player> Scp = new();
        public static Hint shower;
        private static CoroutineHandle refresher;
        private static void WaitingForPlayers()
        {
            UpdateTip();
            Scp.Clear();
            if (refresher.IsRunning)
            {
                _ = Timing.KillCoroutines(refresher);
            }
            refresher = Timing.RunCoroutine(Refresher());
        }
        private static void Died(DiedEventArgs ev)
        {
            if (Scp.Contains(ev.Player))
            {
                RemoveScp(ev.Player);
            }
            PlayerManagerDisplayKitHUD.ClearCacheForPlayer(ev.Player);
        }

        private static void Left(LeftEventArgs ev)
        {
            if (Scp.Contains(ev.Player))
            {
                RemoveScp(ev.Player);
            }
            PlayerManagerDisplayKitHUD.ClearCacheForPlayer(ev.Player);
        }

        private static void AddScp(Player player, RoleTypeId role)
        {
            if (Scp.Contains(player))
            {
                return;
            }

            _ = Scp.Add(player);
        }

        private static void RemoveScp(Player player)
        {
            if (!Scp.Contains(player))
            {
                return;
            }

            _ = Scp.Remove(player);
        }
        public static IEnumerator<float> Refresher()
        {
            while (true)
            {
                try
                {
                    if (Scp914q.Count != 0)
                    {
                        int max = 6;
                        if (Scp914q.Count > max)
                        {
                            while (Scp914q.Count > max)
                            {
                                _ = Scp914q.Dequeue();
                            }
                        }

                        string t = "";
                        while (Scp914q.TryDequeue(out var k))
                        {
                            string trans = k.knob switch
                            {
                                Scp914KnobSetting.Rough => "超粗",
                                Scp914KnobSetting.Coarse => "粗加",
                                Scp914KnobSetting.OneToOne => "1:1",
                                Scp914KnobSetting.Fine => "精加",
                                Scp914KnobSetting.VeryFine => "超精",
                                _ => ""
                            };
                            t += $"<size=22>{(k.act ? $"<color=green>{k.p.Nickname} 激活了914 模式:<color=yellow>{trans}" : "")}</color></size>\n";
                        }
                        Scp914Str = (t, Time.time);
                    }
                    if (Time.time - Scp914Str.startTime > 15f)
                    {
                        Scp914Str = ("", 0f);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                yield return Timing.WaitForSeconds(0.2f);
            }
        }
        public static string CurrentTip = "";
        public static int ntfReinforceCount = 0;
        public static int ChaosReinforceCount = 0;

        public static int GetWaveCount(Player player)
        {
            try { return player.IsNTF ? ntfWave : ChaosCount; }
            catch { return 0; }
        }
        public static (string str, float startTime) Scp914Str = ("", 0f);

        public static void InteractingElevator(InteractingElevatorEventArgs ev)
        {
            if (ev.IsAllowed && ev.Lift != null && ev.Player != null && ev.Lift.Status == Interactables.Interobjects.ElevatorChamber.ElevatorSequence.Ready)
            {
                _ = ElevatorInteractions.RemoveAll(x => x.Interactor == ev.Player && (Time.time - x.InteractTime) < 0.3f);
                ElevatorInteractions.Add(new ElevatorInteractInfo { InteractAt = ev.Player.Position, Interactor = ev.Player, InteractTime = Time.time });
            }
        }

        public static void ChangingKnobSetting(ChangingKnobSettingEventArgs ev)
        {
            Scp914q.Enqueue((ev.Player, ev.KnobSetting, false));
        }

        public static void Activating(ActivatingEventArgs ev)
        {
            Scp914q.Enqueue((ev.Player, ev.KnobSetting, true));
        }
        public static void UpdateTip()
        {
            CurrentTip = PlayerManagementModule.Get().Config.tips.RandomItem();
        }
        public static void AnnouncingNtfEntrance(AnnouncingNtfEntranceEventArgs ev)
        {
            UpdateTip();
            ntfWave++;
        }
        public static void AnnouncingChaosEntrance(AnnouncingChaosEntranceEventArgs ev)
        {
            UpdateTip();
            ChaosCount++;
        }
    }
}
