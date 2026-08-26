using AdminToys;
using Exiled.API.Features;
using HarmonyLib;
using LabApi.Events.Handlers;
using MEC;
using NS_site27_heavy.heavy.SpecialWaveManager;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace NS_site27_heavy.heavy.Module.AirDrop
{
    public class AirMainWave : SpecialWave, ICountedWave, IAnimWave, INeedInitWave, ITiming
    {
        public override string WaveName => "air-test";

        public int TotalCount => 3;

        public int RemainCount { get; set; } = 3;

        public float SpawnTotalTime { get; set; } = 30;

        public float LastSpawnTime { get; set; } = 0;
        public override WaveUIPosition WaveUIPosition { get; set; } = WaveUIPosition.Left;
        public override (bool success, string output) CheckWaveConditions(bool isDebug = false)
        {
            return (true, "Only force");
        }
        private CoroutineHandle handle;
        public Action<SpecialWave, List<Player>> OnPlayDone;
        public Animator DropAnim;
        public Animator ParaAnim = null;

        public AdminToyBase status = null;
        public AdminToyBase DummyGameObject = null;
        public AdminToyBase PlayerCamera1;
        public AdminToyBase PlayerCamera2;

        public static SchematicObject DropSo = null;
        public static SchematicObject ParaSo = null;

        private const float CreateDummyPhase = 2f;
        private const float JumpPhase = 3f;
        private const float ChangeCameraPhase = 4f;
        private const float OpenParaPhase = 5f;
        private const float DestroyParaAndKillDummyPhase = 6f;
        private const float EndPhase = 7f;
        public float phase => status == null ? 0f : status.transform.localScale.x;
        public HashSet<Player> CurrentSpawning;
        public bool isInAnim = false;
        public bool isPlaying => status != null && phase < EndPhase;
        public bool isUsingCam2 => status != null && phase >= ChangeCameraPhase;

        public Npc dummy;
        public bool isCreateDummy => status != null && phase >= CreateDummyPhase;
        public bool Jumped = false;
        public bool isJump => status != null && phase >= JumpPhase;
        public bool killed = false;
        public bool isDestroyParaAndKillDummyPhase => status != null && phase >= DestroyParaAndKillDummyPhase;

        public void CreatePara()
        {
            if (ParaSo == null)
            {
                var hp = new SerializableSchematic()
                {
                    SchematicName = "parachute"
                };
                var root = hp.SpawnOrUpdateObject();
                if (root != null)
                {
                    if (root.TryGetComponent<SchematicObject>(out ParaSo))
                    {
                        foreach (var item in ParaSo.AdminToyBases)
                        {
                            switch (item.name)
                            {
                                case "anim":
                                    ParaAnim = item.GetComponent<Animator>();
                                    break;
                                default:
                                    break;
                            }
                        }
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
                return;
            }
        }
        public void AttachPara()
        {
            if (ParaSo != null && dummy != null)
            {
                ParaAnim.transform.parent = dummy.GameObject.transform;
                if (dummy.Role.Base is IFpcRole fpc)
                {
                    if (fpc.FpcModule.CharacterModelInstance is not AnimatedCharacterModel animatedCharacterModel)
                    {
                        return;
                    }
                    var anim_get = typeof(AnimatedCharacterModel).PropertyGetter("Animator");
                    if (anim_get != null)
                    {
                        Animator animator = (Animator)anim_get.Invoke(animatedCharacterModel, null);
                        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                        {
                            Log.Info("animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman");
                            return;
                        }
                        foreach (var item in ParaSo.AdminToyBases)
                        {
                            if (Enum.TryParse<HumanBodyBones>(item.name, true, out var bonetype))
                            {
                                Transform boneTransform = animator.GetBoneTransform(bonetype);
                                if (boneTransform != null)
                                {
                                    var f = item.gameObject.AddComponent<follower>();
                                    f.TargetFollower = boneTransform;
                                    f.ThisFollower = item.CachedTransform;
                                    item.NetworkMovementSmoothing = 0;
                                    item.MovementSmoothing = 0;
                                    item.syncInterval = 0;
                                }
                            }
                            else
                            {
                            }
                        }
                    }
                }
                ParaAnim.SetBool("Start", true);
            }
        }
        public void StopPara()
        {
            if (ParaSo != null && dummy != null)
            {
                ParaAnim.SetBool("Exit", true);
                _ = Timing.CallDelayed(5f, () =>
                {
                    if (ParaSo != null)
                    {
                        ParaSo?.Destroy();
                        ParaSo = null;
                    }
                    ParaAnim = null;
                });
            }
        }
        public override void OnRestartRound()
        {
            if (handle.IsRunning)
            {
                _ = Timing.KillCoroutines(handle);
            }
            if (DropSo != null)
            {
                DropSo?.Destroy();
                DropSo = null;
            }
            if (ParaSo != null)
            {
                ParaSo?.Destroy();
                ParaSo = null;
            }
            isInAnim = false;
            PlayerCamera1 = null;
            ParaAnim = null;
            DropAnim = null;
            OnPlayDone = null;
            CurrentSpawning = null;
            Jumped = false;
            killed = false;
            dummy = null;
            status = null;
            DummyGameObject = null;
        }

        public override (bool success, List<Player> spawnedPlayers) SpawnPlayers(List<Player> WaitingToSpawn)
        {
            foreach (var item in WaitingToSpawn)
            {
                S049Role.r.AddRole(item);
            }
            foreach (var item in CurrentSpawning)
            {
                if (item.Role.Base is IFpcRole i)
                {
                    i.FpcModule.Motor.GravityController.Gravity = FpcGravityController.DefaultGravity;
                }
                item.DisableEffect(Exiled.API.Enums.EffectType.Invigorated);
            }
            _ = CurrentSpawning.RemoveWhere(WaitingToSpawn.Contains);
            return (true, WaitingToSpawn);
        }

        void INeedInitWave.Deinit()
        {
            PlayerEvents.ValidatedVisibility -= PlayerEvents_ValidatedVisibility;
        }
        private void PlayerEvents_ValidatedVisibility(LabApi.Events.Arguments.PlayerEvents.PlayerValidatedVisibilityEventArgs ev)
        {
            if (isInAnim && isPlaying && ev.IsVisible && CurrentSpawning != null)
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
        public IEnumerator<float> AnimUpdater()
        {
            isInAnim = true;
            yield return Timing.WaitForOneFrame;
            var CalledSpawn = false;
            while (true)
            {
                if (PlayerCamera1 == null)
                {
                    break;
                }

                if (CurrentSpawning == null)
                {
                    break;
                }

                if (DropAnim == null)
                {
                    break;
                }

                if (status == null)
                {
                    break;
                }

                var stateInfo = DropAnim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("startLandDrop"))
                {
                    if (isPlaying)
                    {
                        var usingCamera = isUsingCam2 ? PlayerCamera2 : PlayerCamera1;
                        if (isCreateDummy && !isDestroyParaAndKillDummyPhase)
                        {
                            if (dummy == null)
                            {
                                dummy = Npc.Spawn("anim", PlayerRoles.RoleTypeId.Tutorial, true);
                            }
                            if (dummy?.Role?.Base is IFpcRole fpcRole)
                            {
                                fpcRole.FpcModule.Motor.GravityController.Gravity = Vector3.zero;
                                fpcRole.FpcModule.ServerOverridePosition(DummyGameObject.CachedTransform.position);
                            }
                            AttachPara();
                        }
                        else if (isDestroyParaAndKillDummyPhase && !killed)
                        {
                            dummy?.Destroy();
                            dummy = null;
                            StopPara();
                            killed = true;
                        }
                        if (isJump && !Jumped)
                        {
                            Jumped = true;
                            if (dummy?.Role?.Base is IFpcRole fpcRole)
                            {
                                fpcRole.FpcModule.Motor.JumpController.ForceJump(5f);
                            }
                        }
                        foreach (var player in CurrentSpawning)
                        {
                            try
                            {
                                if (player == null)
                                {
                                    continue;
                                }

                                if (!player.IsAlive)
                                {
                                    continue;
                                }

                                if (player.Role.Base is IFpcRole i)
                                {
                                    i.FpcModule.Motor.GravityController.Gravity = Vector3.zero;
                                    i.FpcModule.ServerOverridePosition(usingCamera.CachedTransform.position);
                                }
                                player.EnableEffect(Exiled.API.Enums.EffectType.Invigorated);
                                _ = player.EnableEffect(Exiled.API.Enums.EffectType.Fade, 255, 0);
                                player.CurrentItem = null;
                                player.Rotation = usingCamera.Rotation;
                            }
                            catch (Exception e)
                            {
                                Log.Warn(e);
                            }
                        }
                    }
                    else if (!CalledSpawn)
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
            DropSo?.Destroy();
            isInAnim = false;
        }


        public bool TryStartAnimation(List<Player> WaitingToSpawn, Action<SpecialWave, List<Player>> OnPlayDone)
        {
            OnRestartRound();
            CurrentSpawning = new();
            this.OnPlayDone = OnPlayDone;
            foreach (var item in WaitingToSpawn)
            {
                item.RoleManager.ServerSetRole(PlayerRoles.RoleTypeId.Tutorial, PlayerRoles.RoleChangeReason.None);
                _ = CurrentSpawning.Add(item);
            }
            CreatePara();
            var hp = new SerializableSchematic()
            {
                SchematicName = "testAirDropSpawn"
            };
            var root = hp.SpawnOrUpdateObject();
            if (root != null)
            {
                if (root.TryGetComponent<SchematicObject>(out DropSo))
                {
                    foreach (var item in DropSo.AdminToyBases)
                    {
                        switch (item.name)
                        {
                            case "Landsite":
                                item.transform.position = new UnityEngine.Vector3(123, 289, 21);
                                break;
                            case "anim":
                                DropAnim = item.GetComponent<Animator>();
                                break;
                            case "status":
                                status = item;
                                break;
                            case "PlayerCamera1":
                                PlayerCamera1 = item;
                                break;
                            case "PlayerCamera2":
                                PlayerCamera2 = item;
                                break;
                            case "Dummy":
                                DummyGameObject = item;
                                break;
                            default:
                                break;
                        }
                    }
                    _ = Timing.CallDelayed(0.01f, () =>
                    {
                        handle = Timing.RunCoroutine(AnimUpdater(), Segment.LateUpdate);

                    });
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        public float GetPlayedTime()
        {
            if (DropAnim == null)
            {
                return 0;
            }

            var stateInfo = DropAnim.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("startLandDrop") ? stateInfo.normalizedTime * stateInfo.length : 0;
        }

        public string GetSpawingUIText()
        {
            if (DropAnim == null)
            {
                return "";
            }

            var stateInfo = DropAnim.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("startLandDrop") ? $"<color=#0000FF>Spawning test wave in {Math.Max(0, 9.2 - GetPlayedTime()):F0}s</color>" : "";
        }

        public override string GetWaitingSpawningUIText()
        {
            return RemainCount <= 0
                ? ""
                : $"<color=#F000FF>TestWave RemainTime:{Math.Max(SpawnTotalTime - (Time.time - LastSpawnTime), 0):F0}s</color>";
        }
    }
    public class follower : MonoBehaviour
    {
        public Transform TargetFollower;
        public Transform ThisFollower;
        public Vector3 offset = new(0, 0, 0);
        public void LateUpdate()
        {
            if (ThisFollower == null)
            {
                ThisFollower = transform;
            }
            if (TargetFollower != null)
            {
                ThisFollower.position = TargetFollower.position + offset;
                ThisFollower.rotation = TargetFollower.rotation;
                //Log.Info($"pos:{ThisFollower.position}");
            }
        }
    }
}
