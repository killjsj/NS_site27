using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.Handlers;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MEC;
using NS_site27_heavy.heavy.SpecialWaveManager;
using PlayerRoles.FirstPersonControl;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using RemoteAdmin.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    public class MainWave : SpecialWave, ICountedWave, IAnimWave, INeedInit
    {
        public override string WaveName => "test";

        int ICountedWave.TotalCount => 0;

        int ICountedWave.RemainCount { get; set; } = 0;

        public override (bool success, string output) CheckWaveConditions(bool isDebug = false)
        {
            return (false, "Only force");
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

        public override (bool success, Player[] spawnedPlayers) SpawnPlayers(Player[] WaitingToSpawn)
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
            }
            return (true, WaitingToSpawn);
        }

        void INeedInit.Deinit()
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

        void INeedInit.Init()
        {
            PlayerEvents.ValidatedVisibility += PlayerEvents_ValidatedVisibility;

        }
        CoroutineHandle handle;

        public bool HasLanded = false;
        public GameObject LandEndWhenScaleChanged;
        public GameObject PlayerCamera;
        public Animator HeliAnim;
        public GameObject root;
        public Action<SpecialWave, Player[]> OnPlayDone;
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
                                player.CurrentItem = null;
                                player.ReferenceHub.TryOverridePosition(PlayerCamera.transform.position);
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
                        OnPlayDone?.Invoke(this, CurrentSpawning.ToArray());

                    }
                }
                else
                {
                    break;
                }

                yield return Timing.WaitForOneFrame;
            }
            CurrentSpawning?.Clear();
            CurrentSpawning = null;
            root?.GetComponent<SchematicObject>()?.Destroy();
            isInAnim = false;
            HasLanded = true;
            
        }
        public bool TryStartAnimation(Player[] WaitingToSpawn, Action<SpecialWave, Player[]> OnPlayDone)
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
                            item.transform.position = new UnityEngine.Vector3(0, 301, -40);
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
                    handle = Timing.RunCoroutine(AnimUpdater());

                });
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
