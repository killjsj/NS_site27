using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using NS_site27_api.Core;
using PlayerRoles;
using Respawning.Waves;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace NS_site27_api.Modules.EventHandle
{
    public class WaveSpawnConfig : ModuleConfigBase
    {
        public bool OverrideMiniWave { get; set; } = true;
    }

    /// <summary>
    /// Overrides mini-waves to spawn full waves instead
    /// </summary>
    public class WaveSpawnModule : ModuleBase
    {
        public override string ModuleName => "WaveSpawn";

        private WaveSpawnConfig _config;

        public override void OnEnable()
        {
            _config = GetConfig<WaveSpawnConfig>();
            if (!_config.IsEnabled) return;

            if (_config.OverrideMiniWave)
                ServerHandlers.RespawningTeam += OnRespawningTeam;
        }

        public override void OnDisable()
        {
            ServerHandlers.RespawningTeam -= OnRespawningTeam;
        }

        private void OnRespawningTeam(RespawningTeamEventArgs ev)
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
    }
}
