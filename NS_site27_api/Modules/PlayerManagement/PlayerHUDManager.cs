using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.Commands.PluginManager;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp914;
using Exiled.Events.EventArgs.Warhead;
using Exiled.Events.Handlers;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using NS_site27_api.Extensions;
using NS_site27_api.Modules.Chat;
using Org.BouncyCastle.Crypto.Prng.Drbg;
using PlayerRoles;
using Respawning;
using Respawning.Waves;
using Scp914;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        public static Stopwatch WaveCalc = new Stopwatch();

        public static List<ScoreChange> ScoreQueue = new List<ScoreChange>();
        public static Queue<(Player p, Scp914KnobSetting knob, bool act)> Scp914q = new Queue<(Player, Scp914KnobSetting, bool)>();

        public struct ScoreChange { public Player Player; public int Amount; public string Reason; public float Time; }
        public struct ElevatorInteractInfo { public Vector3 InteractAt; public Player Interactor; public float InteractTime; }
        public struct NukeInteractInfo {public Player Interactor; public float InteractTime; public bool acted; }
        public static List<ElevatorInteractInfo> ElevatorInteractions = new List<ElevatorInteractInfo>();
        public static List<NukeInteractInfo> NukeInteractions = new List<NukeInteractInfo>();



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
            if (ev.IsAllowed)
            {
                if (!ev.NewRole.IsAlive())
                {
                    RegisterSpectatorHints(ev.Player);
                }
                else
                {
                    UnregisterSpectatorHints(ev.Player);
                }
            }
            if (Scp.Contains(ev.Player))
            {
                RemoveScp(ev.Player);
            }
            Timing.CallDelayed(0.2f, () =>
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
            });
        }
        public static void RegisterPlayer(Player player)
        {
            if (player == null) return;

            player.AddMessage("Always_InfoShow", PlayerHudLVShow, -1,
                UIPosition.FromXY(-100, 40));

            player.AddMessage("RoleHUD", RoleShowGetter, -1,
                UIPosition.FromXY(0, 350));

            player.AddMessage("ElevatorHint", ElevatorHintGetter, -1,
                UIPosition.FromXY(0, 750));

            player.AddMessage("Nuke", NukeHintGetter, -1,
    UIPosition.FromXY(0, 800));

            player.AddMessage("ScoreHint", ScoreGetter, -1,
                UIPosition.FromEnum(ScreenPosition.Center));

            player.AddMessage("914Hint", Scp914Getter, -1,
                UIPosition.FromXY(0, 850));
            ChatManager.SetupPlayer(player);
        }

        public static void UnregisterPlayer(Player player)
        {
            if (player == null) return;
            player.RemoveMessage("Always_InfoShow");
            player.RemoveMessage("RoleHUD");
            player.RemoveMessage("ElevatorHint");
            player.RemoveMessage("ScoreHint");
            player.RemoveMessage("914Hint");
            player.RemoveMessage("SpawnHUD");
            player.RemoveMessage("NtfSpawnHUD");
            player.RemoveMessage("ChaosSpawnHUD");
        }

        public static void RegisterSpectatorHints(Player player)
        {
            if (player == null) return;
            player.AddMessage("SpawnHUD", SpawnHintGetter, -1, UIPosition.FromXY(0, 790));
            player.AddMessage("NtfSpawnHUD", NtfSpawnGetter, -1, UIPosition.FromXY(300, 900));
            player.AddMessage("ChaosSpawnHUD", ChaosSpawnGetter, -1, UIPosition.FromXY(300, 900));
        }

        public static void UnregisterSpectatorHints(Player player)
        {
            if (player == null) return;
            player.RemoveMessage("SpawnHUD");
            player.RemoveMessage("NtfSpawnHUD");
            player.RemoveMessage("ChaosSpawnHUD");
        }


        public static string ScpText = "<align=right><color=red>SCP{scp}:<color=green>♥ {hp} <color=purple>🔰 {sh} <color=yellow>位于 {pos}</color>";
        public static string Scp079Text = "<align=right><color=red>SCP079:<color=green>LV {lv} <color=yellow>Exp {exp}</color>";
        public static string ZombieText = "<align=right><color=red>SCP049-2:<color=green>{count}个</color>";

        public static List<Player> Scp = new List<Player>();
        public static Hint shower;
        static CoroutineHandle refresher;
        static void WaitingForPlayers()
        {
            Scp.Clear();
            if (refresher.IsRunning)
            {
                Timing.KillCoroutines(refresher);
            }
            refresher = Timing.RunCoroutine(Refresher());
        }
        static void Died(DiedEventArgs ev)
        {
            if (Scp.Contains(ev.Player))
            {
                RemoveScp(ev.Player);
            }
        }

        static void Left(LeftEventArgs ev)
        {
            if (Scp.Contains(ev.Player))
            {
                RemoveScp(ev.Player);
            }
        }

        private static void AddScp(Player player, RoleTypeId role)
        {
            if (Scp.Contains(player)) return;
            Scp.Add(player);
        }

        private static void RemoveScp(Player player)
        {
            if (!Scp.Contains(player)) return;
            Scp.Remove(player);
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
                        if (Scp914q.Count > max) { while (Scp914q.Count > max) Scp914q.Dequeue(); }

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
                            t += $"<size=22><color=green>{k.p.Nickname}</color> {(k.act ? "激活了914 模式:" : "修改914模式到 ")}<color=yellow>{trans}</color></size>\n";
                        }
                        Scp914Str = (t, Time.time);
                    }
                    if(Time.time - Scp914Str.startTime > 5f)
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

        static bool IsScpRole(RoleTypeId role)
        {
            return role == RoleTypeId.Scp173 || role == RoleTypeId.Scp106 || role == RoleTypeId.Scp049 ||
                   role == RoleTypeId.Scp079 || role == RoleTypeId.Scp096 || role == RoleTypeId.Scp0492 ||
                   role == RoleTypeId.Scp939 || role == RoleTypeId.Scp3114;
        }

        static string GetScpNumber(RoleTypeId role)
        {
            return role switch
            {
                RoleTypeId.Scp049 => "049",
                RoleTypeId.Scp079 => "079",
                RoleTypeId.Scp096 => "096",
                RoleTypeId.Scp106 => "106",
                RoleTypeId.Scp173 => "173",
                RoleTypeId.Scp3114 => "3114",
                RoleTypeId.Scp939 => "939",
                _ => "???"
            };
        }
        private static string[] RoleShowGetter(Player player)
        {
            string v = "<align=right><b>";
            if (player != null && !player.IsScp)
            {
                v += "<size=19>";
                if (player.Role.Team == Team.FoundationForces || player.Role.Team == Team.Scientists)
                    v += $"<color=#00FFFF>{doc}:博士数量</color>\n<color=#808080>{gruad}:保安数量</color>\n<color=#0000FF>{ntf}:九尾数量</color>";
                else if (player.Role.Team == Team.ChaosInsurgency || player.Role.Team == Team.ClassD)
                    v += $"<color=yellow>{dd}:dd数量</color>\n<color=#009900>{chaos}:混沌数量</color>";
            }
            else if (player != null && player.IsScp)
            {
                v += "<size=17>";
                var ZombieCount = 0;
                foreach (var item in Scp)
                {
                    var hp = item.Health;
                    var sh = item.HumeShield;
                    if (item.Role == RoleTypeId.Scp0492)
                    {
                        ZombieCount += 1;
                    }
                    else if (item.Role is Scp079Role scp079)
                    {
                        v += $"<color=red>SCP079:<color=green>LV {scp079.Level.ToString()} <color=yellow>🔋 {scp079.Energy:F0}/{scp079.MaxEnergy}</color>\n";
                    }
                    else if (item.Role is Scp096Role scp096)
                    {
                        string RageStatuts = "";
                        switch (scp096.RageState)
                        {
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Docile:
                                RageStatuts = "<color=#00FFFF>哭泣</color>";
                                break;
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Distressed:
                                RageStatuts = "<color=green>启动愤怒中</color>";
                                break;
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Enraged:
                                RageStatuts = "<color=yellow>正在愤怒</color>";
                                break;
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Calming:
                                RageStatuts = "<color=red>!!! 无法愤怒 !!!</color>";
                                break;
                        }
                        v += $"<color=red>SCP096:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} {RageStatuts}\n";
                    }
                    else if (item.Role is Scp3114Role scp3114)
                    {
                        var r = scp3114.StolenRole;
                        if (r == RoleTypeId.None || scp3114.DisguiseStatus != PlayerRoles.PlayableScps.Scp3114.Scp3114Identity.DisguiseStatus.Active) r = RoleTypeId.Scp3114;
                        v += $"<color=red>SCP3114:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>目前角色:{r.RoleToString()}\n";
                    }
                    else if (item.Role is Scp106Role scp106)
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>目前体力:{scp106.Vigor * 100:F0}%\n";

                    }
                    else if (item.Role is Scp173Role scp173)
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>{(scp173.BreakneckActive ? "超速移动中" : "")}{(scp173.IsObserved ? " <color=red>!! 被观察 !!" : "")}\n";

                    }
                    else if (item.Role is Scp049Role scp049)
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>{(scp049.IsCallActive ? "散发护盾中" : "")}{(scp049.IsRecalling ? " <color=red>!! 复活他人中 !!" : "")}\n";

                    }
                    else
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0}";
                        if (item.Role == RoleTypeId.Scp939)
                        {
                            v += $" <color=yellow>目前体力:{player.Stamina*100:F0}%";
                        }
                        v += "\n";
                    }
                }
                if (ZombieCount > 0)
                {
                    v += $"<color=red>SCP049-2:<color=green>{ZombieCount}个\n";
                }
            }
            v += "</color></b></size></align>";
            return new[] { v };
        }
        private static string[] PlayerHudLVShow(Player player)
        {
            if (player == null) return new[] { "", "" };

            Player target = player;
            int specCount = 0;

            if (player.Role is SpectatorRole specRole && specRole.SpectatedPlayer != null)
                target = specRole.SpectatedPlayer;

            if (PlayerStateManager.SpecList.ContainsKey(target))
                specCount = PlayerStateManager.SpecList[target].Count;

            var stats = PlayerManagementModule.GetOrCreateStats(target);
            bool isSpec = player.Role is SpectatorRole;
            string re = "<align=center><size=22>";
            string upLine = BuildFirstLine(target, isSpec);
            string downLine = BuildSecondLine(target, stats, specCount, isSpec);
            re += upLine + "\n" + downLine;
            //bool UIParted = false;
            //foreach (var item in UIParts)
            //{
            //    try
            //    {
            //        re += item.GetMessagePart(player,isSpec);
            //        if (!re.EndsWith("\n"))
            //        {
            //            re += " | ";
            //            UIParted = true;
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Log.Error(e);
            //    }
            //}
            //if(UIParted)
            //    re.Remove(re.Length - 3, 3);
            re += "</size></align>";
            

            return new[] { re };
        }
        private static string BuildFirstLine(Player player, bool isSpec)
        {
            if (player == null) return "";

            string teamName = player.Role.Team switch
            {
                Team.FoundationForces => "基金会",
                Team.ChaosInsurgency => "混沌",
                Team.Scientists => "基金会",
                Team.ClassD => "混沌",
                Team.OtherAlive => "教程",
                Team.SCPs => "SCP",
                _ => "死人"
            };
            string teamColor = player.Role.Team switch
            {
                Team.FoundationForces => "#0000FF",
                Team.ChaosInsurgency => "#00AA00",
                Team.Scientists => "#0000FF",
                Team.ClassD => "#00AA00",
                Team.SCPs => "#FF0000",
                _ => "#FFFFFF"
            };

            var conduct = ConductManager.GetConduct(player);
            var phase = PhaseManager.GetPhase(player);

            return $"" +
                   $"<color=#FFFF00>{(isSpec ? "玩家:" : "欢迎回来:")} {player.Nickname}</color> | " +
                   $"<color={ConductManager.ConductToColor(conduct)}>品行:{ConductManager.ConductToName(conduct)}</color> | " +
                   $"<color={PhaseManager.PhaseToColor(phase)}>阶段:{PhaseManager.GetPhaseProgressString(player, phase)}</color> | " +
                   $"<color={teamColor}>阵营:{teamName}</color>" +
                   $"";
        }

        private static string BuildSecondLine(Player player, PlayerManagementModule.RoundStatistics stats, int specCount, bool isSpec)
        {
            if (player == null || stats == null) return "";

            var dur = PlayerDataManager.GetAllTime(player);
            int waves = GetWaveCount(player);

            return $"" +
                   $"<color=#FFD700>总得分:{stats.Points}</color> | " +
                   $"<color=#00FF00>击杀:{stats.Kills}</color> | " +
                   $"<color=#FF0000>死亡:{stats.Deaths}</color> | " +
                   (isSpec ? "" : $"<color=#FF00FF>总时长:{dur.TotalDays:F0}天{dur.Hours:D2}时{dur.Minutes:D2}分</color> | ") +
                   $"<color=#87CEEB>观众:{specCount}</color>" +
                   $"";
        }

        private static int GetWaveCount(Player player)
        {
            try { return player.IsNTF ? ntfWave : ChaosCount; }
            catch { return 0; }
        }

        private static string[] ElevatorHintGetter(Player player)
        {
            if (player == null) return new[] { "" };
            string r = "";
            bool hasContent = false;

            foreach (var item in ElevatorInteractions.ToArray().Where(x => Vector3.Distance(x.InteractAt, player.ReferenceHub.transform.position) <= 9f))
            {
                if (Time.time - item.InteractTime <= 2f)
                {
                    if (!hasContent) { r = "<size=22><color=#FFFF00>"; hasContent = true; }
                    r += $"{item.Interactor.Nickname}激活电梯\n";
                }
                else { ElevatorInteractions.Remove(item); }
            }
            if (hasContent) r += "</color></size>";
            return new[] { r };
        }


        private static string[] NukeHintGetter(Player player)
        {
            if (player == null) return new[] { "" };
            string r = "";
            bool hasContent = false;
            List<NukeInteractInfo> toRemove = new List<NukeInteractInfo>();
            foreach (var item in NukeInteractions)
            {
                if (Time.time - item.InteractTime <= 2f)
                {
                    if (!hasContent) { r = $"<size=22><color={(item.acted ? "red" : "green")}>"; hasContent = true; }
                    r += $"{item.Interactor.Nickname}{(item.acted ? " 已启动核弹" : "已关闭核弹")}\n";
                }
                else {
                    toRemove.Add(item); 
                }
            }
            foreach (var item in toRemove)
            {
                NukeInteractions.Remove(item);
            }
            if (hasContent) r += "</color></size>";
            return new[] { r };
        }

        private static string[] ScoreGetter(Player player)
        {
            if (player == null) return new[] { "" };
            ScoreQueue.RemoveAll(x => Time.time - x.Time > 1f || x.Player == null);
            var mine = ScoreQueue.Where(x => x.Player == player).ToList();
            if (mine.Count == 0) return new[] { "" };
            var latest = mine.Last();
            string color = latest.Amount > 0 ? "#00FF00" : "#FF4444";
            string sign = latest.Amount > 0 ? "+" : "";
            ScoreQueue.RemoveAll(x => x.Player == player);
            return new[] { $"<size=24><color={color}>{sign}{latest.Amount} 积分 ({latest.Reason})</color></size>" };
        }
        public static (string str,float startTime) Scp914Str = ("", 0f);
        private static string[] Scp914Getter(Player player)
        {
            if (player == null || player.CurrentRoom?.Type != RoomType.Lcz914) return new[] { "" };
            return new[] { Scp914Str.str };
        }

        private static string[] NtfSpawnGetter(Player player)
        {
            if (player == null || player.IsAlive || !(player.Role is SpectatorRole)) return new[] { "" };

            var big = WaveManager.Waves.FirstOrDefault(x => x is NtfSpawnWave) as NtfSpawnWave;
            var small = WaveManager.Waves.FirstOrDefault(x => x is NtfMiniWave) as NtfMiniWave;

            string result = "";
            if (big != null)
            {
                double left = Math.Max(0, big.Timer.TimeLeft);
                result = $"<align=left><size=25><color=#0000FFFF>🚁九尾狐: {TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>";
            }
            if (small != null)
            {
                double left = Math.Max(0, small.Timer.TimeLeft);
                result += "\n" + $"<align=left><size=25><color=#0000FFFF>🚁九尾增援:{TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>";
            }
            return result == "" ? new[] { "" } : new[] { result };
        }

        private static string[] ChaosSpawnGetter(Player player)
        {
            if (player == null || player.IsAlive || !(player.Role is SpectatorRole)) return new[] { "" };

            var big = WaveManager.Waves.FirstOrDefault(x => x is ChaosSpawnWave) as ChaosSpawnWave;
            var small = WaveManager.Waves.FirstOrDefault(x => x is ChaosMiniWave) as ChaosMiniWave;

            string result = "";
            if (big != null)
            {
                double left = Math.Max(0, big.Timer.TimeLeft);
                result = $"<align=right><size=25><color=#008000FF>🚗混沌: {TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>";
            }
            if (small != null)
            {
                double left = Math.Max(0, small.Timer.TimeLeft);
                result += "\n" + $"<align=right><size=25><color=#008000FF>🚗混沌增援:{TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>";
            }
            return result == "" ? new[] { "" } : new[] { result };
        }

        private static string[] SpawnHintGetter(Player player)
        {
            if (player == null || player.IsAlive || !(player.Role is SpectatorRole)) return new[] { "" };

            var ChaosBig = WaveManager.Waves.FirstOrDefault(x => x is ChaosSpawnWave) as ChaosSpawnWave;
            var NtfBig = WaveManager.Waves.FirstOrDefault(x => x is NtfSpawnWave) as NtfSpawnWave;
            var NtfSmall = WaveManager.Waves.FirstOrDefault(x => x is NtfMiniWave) as NtfMiniWave;
            var ChaosSmall = WaveManager.Waves.FirstOrDefault(x => x is ChaosMiniWave) as ChaosMiniWave;

            if ((ChaosSmall?.IsAnimationPlaying == true) || (NtfBig?.IsAnimationPlaying == true) ||
                (NtfSmall?.IsAnimationPlaying == true) || (ChaosBig?.IsAnimationPlaying == true))
            { if (!WaveCalc.IsRunning) WaveCalc.Restart(); }
            else { WaveCalc.Stop(); }

            if (ChaosBig?.IsAnimationPlaying == true)
                return new[] { $"<size=22><color=#FFC0CB>你将在{(ChaosBig.AnimationDuration - WaveCalc.Elapsed.TotalSeconds):F0}秒后复活为🚗混沌</color></size>" };
            if (NtfBig?.IsAnimationPlaying == true)
                return new[] { $"<size=22><color=#FFC0CB>你将在{(NtfBig.AnimationDuration - WaveCalc.Elapsed.TotalSeconds):F0}秒后复活为🚁九尾狐</color></size>" };
            if (ChaosSmall?.IsAnimationPlaying == true)
                return new[] { $"<size=22><color=#FFC0CB>你将在{(ChaosSmall.AnimationDuration - WaveCalc.Elapsed.TotalSeconds):F0}秒后复活为🚗混沌增援</color></size>" };
            if (NtfSmall?.IsAnimationPlaying == true)
                return new[] { $"<size=22><color=#FFC0CB>你将在{(NtfSmall.AnimationDuration - WaveCalc.Elapsed.TotalSeconds):F0}秒后复活为🚁九尾狐增援</color></size>" };

            return new[] { "" };
        }

        public static void AddScoreChange(Player player, int amount, string reason)
        {
            ScoreQueue.Add(new ScoreChange { Player = player, Amount = amount, Reason = reason, Time = Time.time });
        }

        public static void InteractingElevator(InteractingElevatorEventArgs ev)
        {
            if (ev.IsAllowed && ev.Lift != null && ev.Player != null && ev.Lift.Status == Interactables.Interobjects.ElevatorChamber.ElevatorSequence.Ready)
            {
                ElevatorInteractions.RemoveAll(x => x.Interactor == ev.Player && (Time.time - x.InteractTime) < 0.3f);
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

        public static void AnnouncingNtfEntrance(AnnouncingNtfEntranceEventArgs ev) => ntfWave++;
        public static void AnnouncingChaosEntrance(AnnouncingChaosEntranceEventArgs ev) => ChaosCount++;
        static List<IUIPart> UIParts = new List<IUIPart>();

        public static IUIPart GetUIPart(int index)
        {
            return UIParts[index];
        }

        public static IUIPart[] GetUIParts()
        {
            return UIParts.ToArray();
        }

        public static void AddUIPart(IUIPart part)
        {
            UIParts.Add(part);
        }

        public static void AddUIPart(IUIPart part, int index)
        {
            UIParts.Insert(index, part);
        }

        public static void RemoveUIPart(int index)
        {
            UIParts.RemoveAt(index);
        }
    }
}
