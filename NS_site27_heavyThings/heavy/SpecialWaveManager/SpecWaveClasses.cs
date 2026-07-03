using Exiled.API.Features;
using NS_site27_heavy.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_heavy.heavy.SpecialWaveManager
{
    public abstract class SpecialWave
    {
        public abstract (bool success, string output) CheckWaveConditions(bool isDebug = false);
        public abstract (bool success,Player[] spawnedPlayers) SpawnPlayers(Player[] WaitingToSpawn);
        public abstract void OnRestartRound();
        public abstract string WaveName { get; }
        public virtual int MaxSpawnedOnce => 999;
    }
    public interface ITimingWave
    {
        float SpawnTotalTime { get; }
        float LastSpawnTime { get; set; }
    }
    public interface IAnimWave 
    {
        bool TryStartAnimation(Player[] WaitingToSpawn,Action<SpecialWave, Player[]> OnPlayDone);
    }

    public interface INeedInit
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
