using Exiled.API.Features;
using PlayerRoles;
using Respawning.Config;
using Respawning.Waves;
using System;
using System.Collections.Generic;

namespace NS_site27_heavy.heavy.SpecialWaveManager
{
    public enum l
    {
        l, m, h
    }
    public abstract class SpecialWave : SpawnableWaveBase, IWaveConfig //SpawnableWaveBase -> event
    {
        public abstract (bool success, string output) CheckWaveConditions(bool isDebug = false);
        public abstract (bool success, List<Player> spawnedPlayers) SpawnPlayers(List<Player> WaitingToSpawn);
        public abstract void OnRestartRound();
        public override int MaxWaveSize => MaxSpawnedOnce;
        public override Faction TargetFaction => Faction.Unclassified;
        public override IWaveConfig Configuration => this;

        public abstract string WaveName { get; }
        public virtual l WaveLev => l.m;
        public virtual int WaveWei => 1;
        public virtual int MaxSpawnedOnce => 999;
        public virtual string GetWaitingSpawningUIText()
        {
            return "";
        }

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
        float SpawnTotalTime { get; set; }
        float LastSpawnTime { get; set; }
    }
    public interface IAnimWave
    {
        float GetPlayedTime();
        string GetSpawingUIText();
        bool TryStartAnimation(List<Player> WaitingToSpawn, Action<SpecialWave, List<Player>> OnPlayDone);
    }

    public interface INeedInitWave
    {
        void Init();
        void Deinit();
    }
    public interface ICountedWave
    {
        int TotalCount { get; }
        int RemainCount { set; get; }
    }
}
