using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using GameCore;
using LabApi.Events.Arguments.ServerEvents;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using PlayerRoles;
using ProjectMER.Commands.Utility;
using Respawning.Waves;
using System.Collections.Generic;
using System.Linq;

namespace NS_site27_api.Modules.EventHandle.Handlers
{
    public static class RoundEventHandler
    {
        //public static List<ReferenceHub> DoNotCountDummyHubs = new();
        public static void OnWaitingForPlayers()
        {
            BroadcastHandler.Start();
        }
        public static void OnRoundTryEnding(RoundEndingConditionsCheckEventArgs ev)
        {
            //if (!ev.CanEnd)
            //    return;

            //// 统计各阵营存活人数（排除 DummyHub）
            //int mtf = 0, chaos = 0, scientists = 0, classD = 0;
            //int scps = 0, zombies = 0, flamingos = 0;

            //foreach (var hub in ReferenceHub.AllHubs)
            //{
            //    if (DoNotCountDummyHubs.Contains(hub))
            //        continue;  // 关键：跳过所有 DummyNPC

            //    switch (hub.GetTeam())
            //    {
            //        case Team.SCPs:
            //            if (hub.GetRoleId() == RoleTypeId.Scp0492)
            //                zombies++;
            //            else
            //                scps++;
            //            break;
            //        case Team.FoundationForces:
            //            mtf++;
            //            break;
            //        case Team.ChaosInsurgency:
            //            chaos++;
            //            break;
            //        case Team.Scientists:
            //            scientists++;
            //            break;
            //        case Team.ClassD:
            //            classD++;
            //            break;
            //        case Team.Flamingos:
            //            flamingos++;
            //            break;
            //    }
            //}

            //int foundation = mtf + scientists;
            //int insurgents = chaos + classD;
            //int anomalies = scps + zombies;

            //// 计算存活阵营数（和游戏原逻辑完全一致）
            //int teamCount = 0;
            //if (foundation > 0) teamCount++;
            //if (insurgents > 0) teamCount++;
            //if (anomalies > 0) teamCount++;
            //if (flamingos > 0) teamCount++;
            //if (teamCount > 1)
            //{
            //    ev.CanEnd = false;
            //}
        }
        public static void OnRoundStarted()
        {
            NS_site27_api.Modules.Abilities.PassAbility.Init();
        }

        public static void OnRespawningTeam(RespawningTeamEventArgs ev)
        {
            //BUG: Mini-wave force spawn may cause unexpected wave composition
            if (!ev.Wave.IsMiniWave) return;

            ev.IsAllowed = false;
            switch (ev.Wave.Faction)
            {
                case Faction.FoundationStaff:
                    WaveSpawner.SpawnWave(new NtfSpawnWave());
                    break;
                case Faction.FoundationEnemy:
                    WaveSpawner.SpawnWave(new ChaosSpawnWave());
                    break;
            }
            ev.Wave.Timer.SetTime(0);
        }

        public static void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            var module = CorePlugin.Modules.OfType<ItemCleanerModule>().FirstOrDefault();
            if (module == null) return;

            var cfg = module.GetConfig();
            if (!cfg.RoundEndFF) return;

            ServerConsole.FriendlyFire = true;
            ServerConfigSynchronizer.RefreshAllConfigs();
            foreach (var player in Player.Enumerable)
            {
                player.AddMessage("RoundEndFF", cfg.RoundEndFFText, 2, ScreenPosition.CenterTop);
            }
        }

        public static void OnRestartingRound()
        {
            BroadcastHandler.Stop();

            var module = CorePlugin.Modules.OfType<ItemCleanerModule>().FirstOrDefault();
            if (module != null)
            {
                module.NotTodayScp.Clear();
                module.CurrentFFManager = null;
            }
        }
    }
}
