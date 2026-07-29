using Exiled.API.Features;
using MEC;
using NS_site27_api.Modules.PlayerManagement;
using NS_site27_heavy.Core;
using Respawning.Waves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            PlayerManagerDisplayKitHUD.BuildSpawnUIEvent -= PlayerHUDManager_BuildSpawnUIEvent;
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
            PlayerManagerDisplayKitHUD.BuildSpawnUIEvent += PlayerHUDManager_BuildSpawnUIEvent;
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
                    if (obj is INeedInitWave i)
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
        private void PlayerHUDManager_BuildSpawnUIEvent(PlayerManagerDisplayKitHUD.UILocate locate, ref StringBuilder AppendString)
        {
            if (!Round.IsStarted)
            {
                return;
            }

            bool isSpawning = false;
            WaveUIPosition waveUIPosition = WaveUIPosition.None;
            switch (locate)
            {
                case PlayerManagerDisplayKitHUD.UILocate.Left:
                    waveUIPosition = WaveUIPosition.Left;
                    _ = AppendString.Append("<align=left><size=25>");
                    break;
                case PlayerManagerDisplayKitHUD.UILocate.Right:
                    _ = AppendString.Append("<align=right><size=25>");
                    waveUIPosition = WaveUIPosition.Right;
                    break;
                case PlayerManagerDisplayKitHUD.UILocate.Middle:
                    isSpawning = true;
                    break;
                default:
                    break;
            }
            if (isSpawning)
            {
                if (CurrentWave != null && CurrentWave is IAnimWave anim && CurrentWave.IsEnabled)
                {
                    var r = anim.GetSpawingUIText();
                    if (!string.IsNullOrEmpty(r))
                    {
                        _ = AppendString.AppendLine(r);
                    }
                }
            }
            else
            {
                foreach (var item in RegWaves)
                {
                    if (item.WaveUIPosition == waveUIPosition && item.IsEnabled)
                    {
                        var r = item.GetWaitingSpawningUIText();
                        if (!string.IsNullOrEmpty(r))
                        {
                            _ = AppendString.AppendLine(r);
                        }
                    }
                }
            }
            _ = AppendString.Append("</size></align>");

        }

        public void WaitingForPlayers()
        {
            loop = Timing.RunCoroutine(MainLoop());
        }
        public void RestartingRound()
        {
            if (loop.IsRunning)
            {
                _ = Timing.KillCoroutines(loop);
            }
            foreach (var item in RegWaves)
            {
                item.OnRestartRound();
            }
        }
        public static void OnAnimDone(SpecialWave item, List<Player> WaitingToSpawn)
        {
            try
            {
                var roleAssignments = new Dictionary<ReferenceHub, PlayerRoles.RoleTypeId>();
                foreach (var player in WaitingToSpawn)
                {
                    roleAssignments.Add(player.ReferenceHub, PlayerRoles.RoleTypeId.CustomRole);
                }
                var LabEvent = new LabApi.Events.Arguments.ServerEvents.WaveRespawningEventArgs(item, roleAssignments);
                LabApi.Events.Handlers.ServerEvents.OnWaveRespawning(LabEvent);
                if (LabEvent.IsAllowed)
                {
                    WaitingToSpawn = LabEvent.SpawningPlayers.Select(Player.Get).ToList();
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
                            var LabEvented = new LabApi.Events.Arguments.ServerEvents.WaveRespawnedEventArgs(item, spawnedPlayers.Select(x => LabApi.Features.Wrappers.Player.Get(x.ReferenceHub)).ToList());
                            LabApi.Events.Handlers.ServerEvents.OnWaveRespawned(LabEvented);

                            Exiled.Events.Handlers.Server.OnRespawnedTeam(item, spawnedPlayers.Select(x => x.ReferenceHub).ToList());
                            Log.Info($"[SWM] spawned wave {item.GetType().Name}");
                        }
                        else
                        {
                            Log.Error($"[SWM] Failed to spawn players for wave {item.GetType().Name}.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[SWM] Failed to spawn players for wave {item.GetType().Name} with Exception:{e}");
            }
            finally
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
                            if (isInNeedToCallCheck && CanStartSpawn && item.IsEnabled)
                            {
                                var (success, output) = item.CheckWaveConditions();
                                if (success)
                                {
                                    _ = StartWave(item);
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
            if (wave == null || !CanStartSpawn || !wave.IsEnabled)
            {
                return false;
            }

            Log.Info($"[SWM] spawning wave {wave.GetType().Name}");
            CurrentWave = wave;
            IsInAnim = true;
            try
            {
                var p = WaveSpawner.GetAvailablePlayers(PlayerRoles.Team.OtherAlive, wave.MaxSpawnedOnce).Select(Player.Get).ToList();
                if (wave is IAnimWave animWave)
                {
                    Log.Info($"[SWM] spawning wave {wave.GetType().Name} - Anim");
                    var started = animWave.TryStartAnimation(p, OnAnimDone);
                    if (!started)
                    {
                        IsInAnim = false;
                    }
                    return started;
                }

                OnAnimDone(wave, p);
                return true;
            }
            catch (Exception ex)
            {
                IsInAnim = false;
                Log.Error($"[SWM] Failed to start wave {wave.GetType().Name} with Exception:{ex}");
                return false;
            }
            finally
            {
                foreach (var item in RegWaves)
                {
                    if (item is ITiming timing && item != wave)
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