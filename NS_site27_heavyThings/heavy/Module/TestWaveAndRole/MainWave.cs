using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.Handlers;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MEC;
using NS_site27_heavy.heavy.SpecialWaveManager;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using RemoteAdmin.Communication;
using Respawning.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    public class MainWave : SpecialWave, ICountedWave, IAnimWave, INeedInitWave, ITiming
    {
        public override string WaveName => "test";

        public int TotalCount => 3;

        public int RemainCount { get; set; } = 3;

        public float SpawnTotalTime { get; set; } = 30;

        public float LastSpawnTime { get; set; } = 0;
        public override WaveUIPosition WaveUIPosition { get; set; } = WaveUIPosition.Left;
        public override (bool success, string output) CheckWaveConditions(bool isDebug = false)
        {
            return (true, "Only force");
        }

        public override void OnRestartRound()
        {
            if (handle.IsRunning) Timing.KillCoroutines(handle);
            if (root != null)
            {
                root?.GetComponent<SchematicObject>()?.Destroy();
            }
            isInAnim = false;
            HasLanded = false;
            LandEndWhenScaleChanged = null;
            PlayerCamera = null;
            HeliAnim = null;
            root = null;
            OnPlayDone = null;
            CurrentSpawning = null;
        }

        public override (bool success, List<Player> spawnedPlayers) SpawnPlayers(List<Player> WaitingToSpawn)
        {
            foreach (var item in WaitingToSpawn)
            {
                MainRole.r.AddRole(item);
            }
            foreach (var item in CurrentSpawning)
            {
                if (item.Role.Base is IFpcRole i)
                {
                    i.FpcModule.Motor.GravityController.Gravity = FpcGravityController.DefaultGravity;
                }
                                item.DisableEffect(Exiled.API.Enums.EffectType.Invigorated);
            }
            CurrentSpawning.RemoveWhere(x=>WaitingToSpawn.Contains(x));
            return (true, WaitingToSpawn);
        }

        void INeedInitWave.Deinit()
        {
            PlayerEvents.ValidatedVisibility -= PlayerEvents_ValidatedVisibility;
        }
        public HashSet<Player> CurrentSpawning;
        public bool isInAnim = false;
        private void PlayerEvents_ValidatedVisibility(LabApi.Events.Arguments.PlayerEvents.PlayerValidatedVisibilityEventArgs ev)
        {
            if (isInAnim &&!HasLanded&& ev.IsVisible && CurrentSpawning != null)
            {
                if (ev.Player != null && ev.Target != null)
                {
                    if (CurrentSpawning.Contains(ev.Player) && CurrentSpawning.Contains(ev.Target))
                    {
                        ev.IsVisible = false;
                    }
                }
            }
        }

        void INeedInitWave.Init()
        {
            PlayerEvents.ValidatedVisibility += PlayerEvents_ValidatedVisibility;

        }
        CoroutineHandle handle;

        public bool HasLanded = false;
        public GameObject LandEndWhenScaleChanged;
        public GameObject PlayerCamera;
        public Animator HeliAnim;
        public GameObject root;
        public Action<SpecialWave, List<Player>> OnPlayDone;
        public IEnumerator<float> AnimUpdater()
        {
            isInAnim = true;
                yield return Timing.WaitForSeconds(0.02f);
            var CalledSpawn = false;
            while (true)
            {
                if (PlayerCamera == null)
                    break;
                if (CurrentSpawning == null)
                    break;
                if (HeliAnim == null)
                    break;
                if (LandEndWhenScaleChanged == null)
                    break;
                if (Exiled.API.Features.Round.IsEnded)
                    break;
                var stateInfo = HeliAnim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("start"))
                {
                    var scale = LandEndWhenScaleChanged.transform.localScale;
                    HasLanded = scale.x >= 1.2f && scale.y >= 1.2f && scale.z >= 1.2f;

                    if (!HasLanded)
                    {
                        foreach (var player in CurrentSpawning)
                        {
                            try
                            {
                                if (player == null)
                                    continue;
                                if (!player.IsAlive)
                                    continue;
                                if (player.Role.Base is IFpcRole i)
                                {
                                    i.FpcModule.Motor.GravityController.Gravity = Vector3.zero;
                                }
                                player.EnableEffect(Exiled.API.Enums.EffectType.Invigorated);
                                player.CurrentItem = null;
                                Vector3 pos = PlayerCamera.transform.position;

                                if ((player.Position - pos).sqrMagnitude > 0.0004f) // 大约2cm
                                {
                                    player.ReferenceHub.TryOverridePosition(pos);
                                }
                                Vector3 playerEuler = player.Rotation.eulerAngles;
                                Vector3 cameraEuler = PlayerCamera.transform.eulerAngles;

                                float playerPitch = playerEuler.x;
                                if (playerPitch > 180f) playerPitch -= 360f;
                                playerPitch = -Mathf.Clamp(playerPitch, -90f, 90f);

                                float playerYaw = playerEuler.y;
                                if (playerYaw < 0f) playerYaw += 360f;
                                else if (playerYaw > 360f) playerYaw -= 360f;

                                float centerPitch = cameraEuler.x;
                                if (centerPitch > 180f) centerPitch -= 360f;
                                centerPitch = -Mathf.Clamp(centerPitch, -90f, 90f);

                                float centerYaw = cameraEuler.y;
                                if (centerYaw < 0f) centerYaw += 360f;
                                else if (centerYaw > 360f) centerYaw -= 360f;

                                float deltaPitch = Mathf.DeltaAngle(centerPitch, playerPitch);
                                float deltaYaw = Mathf.DeltaAngle(centerYaw, playerYaw);

                                const float limit = 20f;

                                if (Mathf.Abs(deltaPitch) > limit || Mathf.Abs(deltaYaw) > limit)
                                {
                                    float clampedDeltaPitch = Mathf.Clamp(deltaPitch, -limit, limit);
                                    if (Mathf.Abs(deltaPitch) <= limit) clampedDeltaPitch = 0;
                                    float clampedDeltaYaw = Mathf.Clamp(deltaYaw, -limit, limit);
                                    if (Mathf.Abs(deltaYaw) <= limit) clampedDeltaYaw = 0;

                                    float finalPitch = centerPitch + clampedDeltaPitch;
                                    float finalYaw = centerYaw + clampedDeltaYaw;

                                    finalPitch = Mathf.Clamp(finalPitch, -90f, 90f);
                                    finalYaw = finalYaw % 360f;
                                    if (finalYaw < 0f) finalYaw += 360f;

                                    player.ReferenceHub.TryOverrideRotation(new Vector2(finalPitch, finalYaw));
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Warn(e);
                            }
                        }
                    }
                    else if(!CalledSpawn)
                    {
                        CalledSpawn = true;
                        OnPlayDone?.Invoke(this, CurrentSpawning.ToList());

                    }
                }
                else
                {
                    break;
                }

                yield return Timing.WaitForOneFrame;
            }
            foreach (var item in CurrentSpawning)
            {
                if (item.Role.Base is IFpcRole i && i.FpcModule.Motor.GravityController.Gravity == Vector3.zero)
                {
                    i.FpcModule.Motor.GravityController.Gravity = FpcGravityController.DefaultGravity;
                }
            }
            CurrentSpawning?.Clear();
            CurrentSpawning = null;
            root?.GetComponent<SchematicObject>()?.Destroy();
            isInAnim = false;
            HasLanded = true;
            
        }
        public bool TryStartAnimation(List<Player> WaitingToSpawn, Action<SpecialWave, List<Player>> OnPlayDone)
        {
            OnRestartRound();
            CurrentSpawning = new(); 
            this.OnPlayDone = OnPlayDone;
            foreach (var item in WaitingToSpawn)
            {
                item.RoleManager.ServerSetRole(PlayerRoles.RoleTypeId.Tutorial, PlayerRoles.RoleChangeReason.None);
                CurrentSpawning.Add(item);
            }
            var hp = new SerializableSchematic()
            {
                SchematicName = "testHeli"
            };
            root = hp.SpawnOrUpdateObject();
            if (root != null)
            {
                foreach (var item in root.transform.GetComponentsInChildren<Transform>())
                {
                    switch (item.name)
                    {
                        case "Landsite":
                            item.transform.position = new UnityEngine.Vector3(123, 289, 21);
                            break;
                        case "Heli":
                            HeliAnim = item.GetComponent<Animator>();
                            break;
                        case "LandEndWhenScaleChanged":
                            LandEndWhenScaleChanged = item.gameObject;
                            break;
                        case "PlayerCamera":
                            PlayerCamera = item.gameObject;
                            break;
                        default:
                            break;
                    }
                }
                Timing.CallDelayed(0.01f, () =>
                {
                    HeliAnim.Play("start");
                    handle = Timing.RunCoroutine(AnimUpdater(),Segment.LateUpdate);

                });
                return true;
            }
            else
            {
                return false;
            }
        }

        public float GetPlayedTime()
        {
            if(HeliAnim == null)
                return 0;
            var stateInfo = HeliAnim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("start"))
            {
                return stateInfo.normalizedTime * stateInfo.length;
            }
            else
            {
                return 0;
            }
        }

        public string GetSpawingUIText()
        {
            if (HeliAnim == null)
                return "";
            var stateInfo = HeliAnim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("start"))
            {
                return $"<color=#0000FF>Spawning test wave in {Math.Max(0, 9.2 - GetPlayedTime()):F0}s</color>";
            }
            return "";
        }

        public override string GetWaitingSpawningUIText()
        {
            if (this.RemainCount <= 0) return "";
            return $"<color=#F000FF>TestWave RemainTime:{Math.Max(this.SpawnTotalTime - (Time.time - this.LastSpawnTime), 0):F0}s</color>";
        }
    }
}
