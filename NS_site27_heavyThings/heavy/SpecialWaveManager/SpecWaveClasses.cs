using Exiled.API.Features;
using NS_site27_heavy.Core;
using PlayerRoles;
using Respawning.Config;
using Respawning.Waves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_heavy.heavy.SpecialWaveManager
{
    public abstract class SpecialWave : SpawnableWaveBase, IWaveConfig //SpawnableWaveBase -> event
    {
        public abstract (bool success, string output) CheckWaveConditions(bool isDebug = false);
        public abstract (bool success, List<Player> spawnedPlayers) SpawnPlayers(List<Player> WaitingToSpawn);
        public abstract void OnRestartRound();
        public override int MaxWaveSize => MaxSpawnedOnce;
        public override Faction TargetFaction => Faction.Unclassified;
        public override IWaveConfig Configuration => this;

        public abstract string WaveName { get; }
        public virtual int MaxSpawnedOnce => 999;
        public virtual string GetWaitingSpawningUIText() => "";
        public virtual WaveUIPosition WaveUIPosition { get; set; } = WaveUIPosition.NeverDisplay;
        public bool IsEnabled { get; set; } = true;
        public override void PopulateQueue(Queue<RoleTypeId> queueToFill, int playersToSpawn)
        {
        }
    }
    public enum WaveUIPosition
    {
        None,
        Left,
        Right,
        NeverDisplay,
    }
    public interface ITiming
    {
        public float SpawnTotalTime { get; set; }
        public float LastSpawnTime { get; set; }
    }
    public interface IAnimWave
    {
        public float GetPlayedTime();
        public string GetSpawingUIText();
        public bool TryStartAnimation(List<Player> WaitingToSpawn, Action<SpecialWave, List<Player>> OnPlayDone);
    }

    public interface INeedInitWave
    {
        public void Init();
        public void Deinit();
    }
    public interface ICountedWave
    {
        public int TotalCount { get; }
        public int RemainCount { set; get; }
    }
}
