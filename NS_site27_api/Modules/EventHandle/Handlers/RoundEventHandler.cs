using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using GameCore;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
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

        public static void OnRoundEnded(RoundEndedEventArgs ev)
        {
            var module = CorePlugin.Modules.OfType<EventHandleModule>().FirstOrDefault();
            if (module == null) return;

            var cfg = module.GetConfig();
            if (!cfg.RoundEndFF) return;

            ServerConsole.FriendlyFire = true;
            ServerConfigSynchronizer.RefreshAllConfigs();
            foreach (var player in Player.List)
            {
                player.AddMessage("RoundEndFF", cfg.RoundEndFFText, 2, ScreenPosition.CenterTop);
            }
        }

        public static void OnRestartingRound()
        {
            BroadcastHandler.Stop();

            var module = CorePlugin.Modules.OfType<EventHandleModule>().FirstOrDefault();
            if (module != null)
            {
                module.NotTodayScp.Clear();
                module.CurrentFFManager = null;
            }
        }
    }
}
