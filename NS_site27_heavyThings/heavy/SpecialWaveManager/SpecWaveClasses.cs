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
        public virtual string GetWaitingSpawningUIText() => "";
        public virtual WaveUIPosition WaveUIPosition { get; set; } = WaveUIPosition.NeverDisplay;
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
        public bool TryStartAnimation(Player[] WaitingToSpawn,Action<SpecialWave, Player[]> OnPlayDone);
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
