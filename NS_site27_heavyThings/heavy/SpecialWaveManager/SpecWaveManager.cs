using Exiled.API.Features;
using GameCore;
using MEC;
using Mirror;
using NS_site27_api.Modules.PlayerManagement;
using NS_site27_heavy.Core;
using ProjectMER.Commands.Map;
using Respawning;
using Respawning.Waves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NS_site27_heavy.heavy.SpecialWaveManager
{
    public class SpecWaveManager : ModuleBase<SpecWaveManager.SPWConfig>
    {
        public override string ModuleName => "SWM";
        public static SpecWaveManager Ins;
        public static List<SpecialWave> RegWaves = new();
        public static bool IsInAnim = false;
        public static bool CanStartSpawn => !IsInAnim && WaveSpawner.AnyPlayersAvailable;
        private CoroutineHandle loop;
        public override void OnDisable()
        {
            PlayerHUDManager.BuildSpawnUIEvent -= PlayerHUDManager_BuildSpawnUIEvent;
            RestartingRound();
            Exiled.Events.Handlers.Server.WaitingForPlayers -= WaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted -= RoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound -= RestartingRound;
            foreach (var item in RegWaves)
            {
                if (item is INeedInitWave i)
                {
                    i.Deinit();
                }
            }
        }

        public override void OnEnable()
        {
            PlayerHUDManager.BuildSpawnUIEvent += PlayerHUDManager_BuildSpawnUIEvent;
            Ins = this;
            Exiled.Events.Handlers.Server.WaitingForPlayers += WaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted += RoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound += RestartingRound;
            var assembly = Assembly.GetCallingAssembly();
            var moduleTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(SpecialWave).IsAssignableFrom(t))
                .ToList();

            foreach (var type in moduleTypes)
            {
                try
                {
                    var obj = (SpecialWave)Activator.CreateInstance(type);
                    RegWaves.Add(obj);
                    if(obj is INeedInitWave i)
                    {
                        i.Init();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to load wave {type.FullName}: {ex}");
                }
            }
        }
        public void RoundStarted()
        {
            foreach (var item in RegWaves)
            {
                if (item is ITiming timing)
                {
                    timing.LastSpawnTime = Time.time;
                }
            }
        }
        private void PlayerHUDManager_BuildSpawnUIEvent(PlayerHUDManager.UILocate locate, ref StringBuilder AppendString)
        {
            if(!Round.IsStarted) return;
            bool isSpawning = false;
            WaveUIPosition waveUIPosition = WaveUIPosition.None;
            switch (locate)
            {
                case PlayerHUDManager.UILocate.Left:
                    waveUIPosition = WaveUIPosition.Left;
                    AppendString.Append("<align=left><size=25>");
                    break;
                case PlayerHUDManager.UILocate.Right:
                    AppendString.Append("<align=right><size=25>");
                    waveUIPosition = WaveUIPosition.Right;
                    break;
                case PlayerHUDManager.UILocate.Middle:
                    isSpawning = true;
                    break;
                default:
                    break;
            }
            if(isSpawning)
            {
                if(CurrentWave !=null && CurrentWave is IAnimWave anim)
                {
                    var r = anim.GetSpawingUIText();
                    if (!string.IsNullOrEmpty(r))
                    {
                        AppendString.AppendLine(r);
                    }
                }
            }
            else
            {
                foreach (var item in RegWaves)
                {
                    if (item.WaveUIPosition == waveUIPosition)
                    {
                        var r = item.GetWaitingSpawningUIText();
                        if(!string.IsNullOrEmpty(r))
                        {
                            AppendString.AppendLine(r);
                        }
                    }
                }
            }
            AppendString.Append("</size></align>");

        }

        public void WaitingForPlayers()
        {
            loop = Timing.RunCoroutine(MainLoop());
        }
        public void RestartingRound()
        {
            if (loop.IsRunning)
            {
                Timing.KillCoroutines(loop);
            }
            foreach (var item in RegWaves)
            {
                item.OnRestartRound();
            }
        }
        public static void OnAnimDone(SpecialWave item, Player[] WaitingToSpawn)
        {
            try
            {
                var (spawnSuccess, spawnedPlayers) = item.SpawnPlayers(WaitingToSpawn);
                if (spawnSuccess)
                {
                    if (item is ITiming timingWave2)
                    {
                        timingWave2.LastSpawnTime = Time.time;
                    }
                    if (item is ICountedWave countedWave2)
                    {
                        countedWave2.RemainCount--;
                    }
                    Log.Info($"[SWM] spawned wave {item.GetType().Name}");
                }
                else
                {
                    Log.Error($"[SWM] Failed to spawn players for wave {item.GetType().Name}.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[SWM] Failed to spawn players for wave {item.GetType().Name} with Exception:{e}");
            } finally
            {
                IsInAnim = false;
                CurrentWave = null;
            }
        }
        public IEnumerator<float> MainLoop()
        {
            while (true)
            {
                if (Round.IsStarted)
                {
                    foreach (var item in RegWaves)
                    {
                        try
                        {
                            bool isInNeedToCallCheck = true;
                            if (item is ITiming timingWave)
                            {
                                if (Time.time - timingWave.LastSpawnTime < timingWave.SpawnTotalTime)
                                {
                                    isInNeedToCallCheck = false;
                                }
                            }
                            if (item is ICountedWave countedWave)
                            {
                                if (countedWave.RemainCount <= 0)
                                {
                                    isInNeedToCallCheck = false;
                                }
                            }
                            if (isInNeedToCallCheck && CanStartSpawn)
                            {
                                var (success, output) = item.CheckWaveConditions();
                                if (success)
                                {
                                    StartWave(item);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            IsInAnim = false;
                            Log.Error($"[SWM] Failed to check for wave {item.GetType().Name} with Exception:{e}");
                        }
                    }
                }
                yield return Timing.WaitForSeconds(0.2f);

            }
        }
        public static SpecialWave GetWave(string name)
        {
            return SpecWaveManager.RegWaves.FirstOrDefault(x =>
                string.Equals(x.GetType().Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.GetType().FullName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.WaveName, name, StringComparison.OrdinalIgnoreCase)
                );
        }
        public static SpecialWave CurrentWave = null;
        public static bool StartWave(SpecialWave wave)
        {
            if (wave == null || !CanStartSpawn)
                return false;
            Log.Info($"[SWM] spawning wave {wave.GetType().Name}");
            CurrentWave = wave;
            IsInAnim = true;
            try
            {
                var p = WaveSpawner.GetAvailablePlayers(PlayerRoles.Team.OtherAlive, wave.MaxSpawnedOnce).Select(x=>Player.Get(x)).ToArray();
                if (wave is IAnimWave animWave)
                {
                    Log.Info($"[SWM] spawning wave {wave.GetType().Name} - Anim");
                    var started = animWave.TryStartAnimation(p,OnAnimDone);
                    if (!started)
                    {
                        IsInAnim = false;
                    }
                    return started;
                }

                OnAnimDone(wave,p);
                return true;
            }
            catch (Exception ex)
            {
                IsInAnim = false;
                Log.Error($"[SWM] Failed to start wave {wave.GetType().Name} with Exception:{ex}");
                return false;
            }finally
            {
                foreach (var item in RegWaves)
                {
                    if(item is ITiming timing && item != wave)
                    {
                        timing.LastSpawnTime = Time.time;
                    }
                }
            }
        }
        public class SPWConfig : ModuleConfigBase
        {
        }
    }
}