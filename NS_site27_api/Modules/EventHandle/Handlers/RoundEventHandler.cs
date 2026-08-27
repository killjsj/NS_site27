using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using LabApi.Events.Arguments.ServerEvents;
using NS_site27_api.Core;
using NS_site27_api.Modules.MessageModule;
using PlayerRoles;
using Respawning.Waves;
using System.Linq;

namespace NS_site27_api.Modules.EventHandle.Handlers
{
    public static class RoundEventHandler
    {
        public static void OnWaitingForPlayers()
        {
            BroadcastHandler.Start();
        }
        public static void OnRoundTryEnding(RoundEndingConditionsCheckEventArgs ev)
        {





        }
        public static void OnRoundStarted()
        {
        }

        public static void OnRespawningTeam(RespawningTeamEventArgs ev)
        {
            if (!ev.Wave.IsMiniWave)
            {
                return;
            }

            ev.IsAllowed = false;
            switch (ev.Wave.Faction)
            {
                case Faction.FoundationStaff:
                    _ = WaveSpawner.SpawnWave(new NtfSpawnWave());
                    break;
                case Faction.FoundationEnemy:
                    _ = WaveSpawner.SpawnWave(new ChaosSpawnWave());
                    break;
            }
            ev.Wave.Timer.SetTime(0);
        }

        public static void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            var module = CorePlugin.Modules.OfType<ItemCleanerModule>().FirstOrDefault();
            if (module == null)
            {
                return;
            }

            var cfg = module.GetConfig();
            if (!cfg.RoundEndFF)
            {
                return;
            }

            ServerConsole.FriendlyFire = true;
            ServerConfigSynchronizer.RefreshAllConfigs();
            foreach (var player in Player.Enumerable)
            {
                player.AddHint("RoundEndff", 2, x => new MsgUpdateResult() { Content = cfg.RoundEndFFText });
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
