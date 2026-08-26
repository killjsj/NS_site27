using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Items;
using Exiled.API.Features.Spawn;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Models.Arguments;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.UI.Utilities;
using Interactables;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using MEC;
using NAudio.Wave;
using NS_site27_api.Core;
using NS_site27_api.Modules.CustomRolePlus;
using NS_site27_api.Modules.SettingManagement;
using PlayerRoles;
using PlayerStatsSystem;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.Weapons
{
    [CustomItem(ItemType.Lantern)]
    internal class BetterHid : CustomItemPlus
    {
        public override uint Id { get; set; } = 2131;
        public override string Name { get; set; } = "a";
        public override string Description { get; set; } = "b";
        public override float Weight { get; set; } = 2f;
        public override SpawnProperties SpawnProperties { get; set; } = null;
        public SettingBase settingL { get; private set; }
        public SettingBase settingH { get; private set; }
        public DynamicHint hint = null;
        public Dictionary<Item, BHID_playerInfos> BHIDs = new();
        private readonly Dictionary<string, float> AudioLengths = new();
        private void RegisterBHIDAudio(string id, string file)
        {
            string path = Path.Combine(
                NS_site27_heavy.Core.ModuleConfigManager.ConfigDir,
                file
            );


            DefaultAudioManager.Instance.RegisterAudio(
                id,
                () => File.OpenRead(path)
            );


            AudioLengths[id] = GetAudioDuration(path);
        }
        public string HintUpdater(AutoContentUpdateArg ev)
        {
            var re = "";
            if (Player.TryGet(ev.PlayerDisplay.ReferenceHub, out var p))
            {
                if (!Check(p.CurrentItem)) return re;
                var info = TryGetBHID_Info(p.CurrentItem);
                if(info != null)
                {
                    re += $"{info.Status},Battery:{info.CurrentBattery / TotalDamage * 100:F1}({info.CurrentBattery:F1}/{TotalDamage})";
                }
            }
            return re;
        }

        public override void Init()
        {
            base.Init();
            hint = new()
            {
                AutoText = HintUpdater,
                FontSize = 23,
                RightBoundary = 0,
                TargetX = 800,
                TargetY = 800,
            };
            RegisterBHIDAudio(
                "BHID.WindupStart",
                "WindupStart.wav"
            );
            RegisterBHIDAudio(
                "BHID.WindupLoop",
                "WindupLoop.wav"
            );
            RegisterBHIDAudio(
                "BHID.ShootingL",
                "ShootingL.wav"
            );
            RegisterBHIDAudio(
                "BHID.ShootingH",
                "ShootingH.wav"
            );
            RegisterBHIDAudio(
                "BHID.WindDown",
                "WindDown.wav"
            );
            RegisterBHIDAudio(
                "BHID.End",
                "End.wav"
            );
            InitSetting();

            StaticUnityMethods.OnUpdate += OnUpdateFrame;
            Exiled.Events.Handlers.Player.ChangedItem += ChangedItem;
        }
        public override void Destroy()
        {
            StaticUnityMethods.OnUpdate -= OnUpdateFrame;
            Exiled.Events.Handlers.Player.ChangedItem -= ChangedItem;
            BHIDs.Clear();
            base.Destroy();
        }

        protected override void OnWaitingForPlayers()
        {
            base.OnWaitingForPlayers();
            BHIDs.Clear();
        }
        public void ChangedItem(ChangedItemEventArgs ev)
        {
            var player = ev.Player;
            if (!Check(ev.OldItem))
            {
                SettingManager.Instance.UnregisterForPlayer(ev.Player, settingL);
                SettingManager.Instance.UnregisterForPlayer(ev.Player, settingH);
                var ui = PlayerUI.Get(player);
                ui.PlayerDisplay.RemoveHint(hint);
            }
            if (Check(ev.Item))
            {
                SettingManager.Instance.RegisterForPlayer(ev.Player, settingL);
                SettingManager.Instance.RegisterForPlayer(ev.Player, settingH);
                var ui = PlayerUI.Get(player);
                ui.PlayerDisplay.AddHint(hint);
            }
        }
        private void InitSetting()
        {
            if (CorePlugin.Instance == null)
            {
                return;
            }

            int LkeyId = (int)Id + ((int)KeyCode.Mouse0 * 7919);
            settingL = SettingManager.Instance?.GetOrCreateKeybindSetting(
                LkeyId, "Light", KeyCode.Mouse0, "Light",
                (p, o) => OnPressing(p, o, false));
            int HkeyId = (int)Id + ((int)KeyCode.Mouse2 * 7919);
            settingH = SettingManager.Instance?.GetOrCreateKeybindSetting(
                HkeyId, "Heavy", KeyCode.Mouse1, "Heavy",
                (p, o) => OnPressing(p, o, true));
        }
        public BHID_playerInfos TryGetBHID_Info(Item item)
        {
            if (item == null)
            {
                return null;
            }

            if (!BHIDs.TryGetValue(item, out var bHID))
            {
                bHID = new BHID_playerInfos
                {
                    Item = item,
                    Status = BHID_status.Idle
                };
                BHIDs.Add(item, bHID);
            }
            return bHID;
        }
        public void OnPressing(Player player, bool isPressed, bool Heavy)
        {
            var info = TryGetBHID_Info(player.CurrentItem);
            if (info == null)
            {
                return;
            }

            if (info.Status == BHID_status.Idle)
            {
                info.choose = Heavy ? LastFireModeChoose.H : LastFireModeChoose.L;
            }
            switch (info.Status)
            {
                case BHID_status.Idle:
                    if (isPressed)
                    {
                        if (info.CurrentBattery <= 0)
                        {
                        }
                        else
                        {
                            ChangeStatus(info, BHID_status.Windup);
                            info.StartPressingTime = Time.time;
                        }
                    }
                    break;
                case BHID_status.Windup:
                    if (!isPressed)
                    {
                        ChangeStatus(info, BHID_status.WindDown);
                        info.LastReleaseTime = Time.time;
                    }
                    break;
                case BHID_status.WoundUpSustain:
                    if (isPressed && !Heavy)
                    {
                        ChangeStatus(info, BHID_status.Shooting);
                    }
                    else if (!isPressed)
                    {
                        ChangeStatus(info, BHID_status.WindDown);
                        info.LastReleaseTime = Time.time;
                    }
                    break;
                case BHID_status.Shooting:
                    if (!isPressed)
                    {
                        if (info.choose == LastFireModeChoose.H && !Heavy)
                        {
                            ChangeStatus(info, BHID_status.WoundUpSustain);
                            break;
                        }
                        ChangeStatus(info, BHID_status.ShootEnd);
                        info.LastReleaseTime = Time.time;
                    }
                    break;
                default:
                    break;
            }
        }
        //单电炮最理想化最多伤害(Windup等也从这里扣)
        public const float TotalDamage = 8000f;
        //后摇
        public const float RecoverTime = 0.3f;
        //前摇
        public const float WindupTime_L = 1f;
        public const float WindupTime_H = 3f;

        public const float WindupEnergyForSecond = 60f;
        public float WindupEnergyForTick => WindupEnergyForSecond * Time.deltaTime;
        public const float WindupSustainEnergyForSecond = 30f;
        public float WindupSustainEnergyForTick => WindupSustainEnergyForSecond * Time.deltaTime;

        public const float DamageForSecond_L = 400f;
        public float DamageForTick_L => DamageForSecond_L * Time.deltaTime;
        public const float DamageForSecond_H = 1100f;
        public float DamageForTick_H => DamageForSecond_H * Time.deltaTime;

        public void OnUpdateFrame()
        {
            foreach (var item in BHIDs)
            {
                var info = item.Value;
                switch (info.Status)
                {
                    case BHID_status.Idle:
                        break;
                    case BHID_status.Windup:
                        if (Time.time - info.StartPressingTime >= (info.choose == LastFireModeChoose.H ? WindupTime_H : WindupTime_L))
                        {
                            if (info.choose == LastFireModeChoose.L)
                            {
                                ChangeStatus(info, BHID_status.Shooting);

                            }
                            else
                            {
                                ChangeStatus(info, BHID_status.WoundUpSustain);

                            }
                        }
                        info.CurrentBattery = Mathf.Max(0, info.CurrentBattery - WindupEnergyForTick);
                        if (info.CurrentBattery <= 0)
                        {
                            ChangeStatus(info, BHID_status.Idle);

                        }
                        break;
                    case BHID_status.WoundUpSustain:
                        info.CurrentBattery = Mathf.Max(0, info.CurrentBattery - WindupSustainEnergyForTick);
                        if (info.CurrentBattery <= 0)
                        {
                            ChangeStatus(info, BHID_status.Idle);

                        }
                        break;
                    case BHID_status.Shooting:
                        var d = info.choose == LastFireModeChoose.H ? DamageForTick_H : DamageForTick_L;
                        info.CurrentBattery = Mathf.Max(0, info.CurrentBattery - d);
                        var o = info.Item.Owner;
                        Raycast(o.CameraTransform, 0.65f, 10, out var num);
                        foreach (IDestructible destructible in DetectedDestructibles)
                        {
                            if (destructible is HitboxIdentity hi && hi.TargetHub == o.ReferenceHub) continue;
                            _ = ServerDealDamage(destructible, o, d);
                        }
                        for (int i = 0; i < num; i++)
                        {
                            if (DetectionsNonAlloc[i].TryGetComponent<InteractableCollider>(out InteractableCollider interactableCollider) && CheckIntercolLineOfSight(o.CameraTransform.position, interactableCollider))
                            {
                                HandlePotentialDoor(interactableCollider, o);
                            }
                        }
                        if (info.CurrentBattery <= 0)
                        {
                            ChangeStatus(info, BHID_status.ShootEnd);

                        }
                        break;
                    case BHID_status.ShootEnd:
                    case BHID_status.WindDown:
                        if (Time.time - info.LastReleaseTime >= RecoverTime)
                        {
                            ChangeStatus(info, BHID_status.Idle);

                        }
                        break;
                    default:
                        break;
                }
            }

        }

        public enum BHID_status
        {
            Idle,
            Windup,
            WoundUpSustain,
            Shooting,
            ShootEnd,
            WindDown
        }

        public enum LastFireModeChoose
        {
            L, H
        }
        public class BHID_playerInfos
        {
            public Item Item;
            public BHID_status Status;
            public LastFireModeChoose choose;
            public float CurrentBattery = TotalDamage;
            public float StartPressingTime = 0;
            public float LastReleaseTime = 0;

            public int AudioSid = -1;
            public CoroutineHandle AudioLoopCoroutine;
        }
        private float GetAudioDuration(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Log.Warn($"音频文件不存在: {filePath}");
                    return 0f;
                }

                using var reader = new AudioFileReader(filePath);
                return (float)reader.TotalTime.TotalSeconds;
            }
            catch (Exception ex)
            {
                Log.Warn($"获取音频时长失败: {ex.Message}");
                return 0f;
            }
        }
        private float GetAudioLength(string key)
        {
            if (AudioLengths.TryGetValue(key, out var length))
            {
                return length;
            }

            return 1f; // 防止没有长度导致循环异常
        }
        private void ChangeStatus(BHID_playerInfos info, BHID_status status)
        {
            if (info.Status == status)
            {
                return;
            }

            StopAudio(info);
            info.Status = status;
            var player = info.Item.Owner;
            if (player == null)
            {
                return;
            }

            switch (status)
            {
                case BHID_status.Windup:
                    info.AudioSid = PlayTrackingAudio(
                        info,
                        "BHID.WindupStart",
                        1f
                    );
                    break;
                case BHID_status.WoundUpSustain:
                    StartLoopAudio(
                        info,
                        "BHID.WindupLoop",
                        GetAudioLength("BHID.WindupLoop")
                    );
                    break;
                case BHID_status.Shooting:
                    StartLoopAudio(
                        info,
                        info.choose == LastFireModeChoose.H ? "BHID.ShootingH" : "BHID.ShootingL",
                        GetAudioLength(info.choose == LastFireModeChoose.H ? "BHID.ShootingH" : "BHID.ShootingL")
                    );
                    break;
                case BHID_status.WindDown:
                    info.AudioSid = PlayTrackingAudio(
                        info,
                        "BHID.WindDown",
                        1f
                    );
                    break;
                case BHID_status.ShootEnd:
                    info.AudioSid = PlayTrackingAudio(
                        info,
                        "BHID.End",
                        1f
                    );
                    break;
            }
        }
        private void StartLoopAudio(BHID_playerInfos info, string key, float length)
        {
            if (info.AudioLoopCoroutine.IsRunning)
            {
                _ = Timing.KillCoroutines(info.AudioLoopCoroutine);
            }
            info.AudioLoopCoroutine =
                Timing.RunCoroutine(
                    AudioLoop(info, key, length)
                );
        }
        private IEnumerator<float> AudioLoop(
            BHID_playerInfos info,
            string key,
            float length)
        {
            while (true)
            {
                info.AudioSid =
                    PlayTrackingAudio(
                        info,
                        key,
                        length
                    );
                yield return Timing.WaitForSeconds(length);
                if (info.AudioSid != -1 &&
                   DefaultAudioManager.Instance.IsValidSession(info.AudioSid))
                {
                    DefaultAudioManager.Instance.StopAudio(info.AudioSid);
                }
                if (info.Status is not BHID_status.WoundUpSustain and
                   not BHID_status.Shooting)
                {
                    yield break;
                }
            }
        }
        private int PlayTrackingAudio(BHID_playerInfos info, string key, float length)
        {
            return DefaultAudioManager.Instance.PlayTrackingAudio<int>(
                key,
                () =>
                {
                    return info.Item.Owner == null ? Vector3.zero : info.Item.Owner.Position;
                },
                () => true,
                0,
                (p, _) => p.IsReady,
                priority: AudioPriority.High,
                lifespan: length,
                maxDistance: 100,
                minDistance: 20
            );
        }
        private void StopAudio(BHID_playerInfos info)
        {
            if (info.AudioSid != -1)
            {
                if (DefaultAudioManager.Instance.IsValidSession(info.AudioSid))
                {
                    DefaultAudioManager.Instance.StopAudio(info.AudioSid);
                }
                info.AudioSid = -1;
            }

            if (info.AudioLoopCoroutine.IsRunning)
            {
                _ = Timing.KillCoroutines(info.AudioLoopCoroutine);
            }
        }

        private static readonly DoorLockReason BypassableLocks = DoorLockReason.Regular079 | DoorLockReason.Lockdown079 | DoorLockReason.NoPower | DoorLockReason.Lockdown2176;
        private void HandlePotentialDoor(InteractableCollider interactable, Player Onwer)
        {
            BreakableDoor breakableDoor = interactable.Target as BreakableDoor;
            if (breakableDoor == null)
            {
                return;
            }
            if (breakableDoor.TargetState)
            {
                return;
            }
            if (!breakableDoor.AllowInteracting(Onwer.ReferenceHub, interactable.ColliderId))
            {
                return;
            }
            if ((breakableDoor.ActiveLocks & (ushort)~(ushort)BypassableLocks) == 0)
            {
                breakableDoor.NetworkTargetState = true;
            }
        }
        private bool CheckIntercolLineOfSight(Vector3 originPoint, InteractableCollider collider)
        {
            Transform transform = collider.transform;
            Vector3 vector = transform.position + transform.TransformDirection(collider.VerificationOffset);
            return !Physics.Linecast(originPoint, vector, out RaycastHit raycastHit, PlayerRolesUtils.AttackMask) || raycastHit.collider.transform == transform;
        }
        public static readonly CachedLayerMask DetectionMask = new("Hitbox", "Glass", "Door");

        public static readonly Collider[] DetectionsNonAlloc = new Collider[64];

        public static readonly RaycastHit[] HitsNonAlloc = new RaycastHit[64];

        public static readonly HashSet<uint> DetectedNetIds = new();

        public static readonly List<IDestructible> DetectedDestructibles = new();

        public static bool ServerDealDamage(IDestructible target, Player Onwer, float damage)
        {
            var microHidDamageHandler = new CustomReasonDamageHandler("1",damage);
            if (!target.Damage(damage, microHidDamageHandler, target.CenterOfMass))
            {
                return false;
            }
                Hitmarker.SendHitmarkerDirectly(Onwer.ReferenceHub, 1f, true, HitmarkerType.Regular);
            
            return true;
        }

        public static void Raycast(Transform plyCam, float thickness, float range, out int detections)
        {
            Vector3 position = plyCam.position;
            detections = Physics.SphereCastNonAlloc(position, thickness, plyCam.forward, HitsNonAlloc, range, DetectionMask);
            DetectedDestructibles.Clear();
            DetectedNetIds.Clear();
            for (int i = 0; i < detections; i++)
            {
                Collider collider = HitsNonAlloc[i].collider;
                DetectionsNonAlloc[i] = collider;
                if (collider.TryGetComponent<IDestructible>(out IDestructible destructible) && (!Physics.Linecast(position, destructible.CenterOfMass, out RaycastHit raycastHit, PlayerRolesUtils.AttackMask) || !(raycastHit.collider != collider)) && DetectedNetIds.Add(destructible.NetworkId))
                {
                    DetectedDestructibles.Add(destructible);
                }
            }
        }
    }
}
