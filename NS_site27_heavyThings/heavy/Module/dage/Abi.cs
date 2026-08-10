using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Enums;
using DrawableLine;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HarmonyLib;
using InventorySystem.Items.Firearms.Modules;
using MEC;
using NAudio.Wave;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.Abilities;
using NS_site27_heavy.Core;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Utils.Networking;

namespace NS_site27_heavy.heavy.Module.dage
{
    internal class DageAbi1 : PassAbility, IModule
    {
        public override string Name => "自瞄透视";

        public override string Des => "200m不屏息";

        public string ModuleName => Name;

        public bool IsEnabled => true;
        public static List<ReferenceHub> vaild = new();
        internal static bool IsOwner(Player hub)
        {
            return hub != null && vaild.Contains(hub.ReferenceHub);
        }

        internal static bool IsOwner(ReferenceHub hub)
        {
            return hub != null && vaild.Contains(hub);
        }

        private Vector3 lastPos;
        private float lastVer, lastHo;
        public override float checktime => 0.015f;
        public override void OnCheck(Player player)
        {
            if (player.Role.Base is not IFpcRole fpc)
            {
                return;
            }

            var module = fpc.FpcModule;
            Vector3 pos = module.Position;
            float ver = module.MouseLook.CurrentVertical;
            float ho = module.MouseLook.CurrentHorizontal;

            bool still = (pos - lastPos).sqrMagnitude < 0.0001f
                         && !module.Motor.RotationDetected
                         && Mathf.Abs(Mathf.DeltaAngle(lastVer, ver)) < 0.1f
                         && Mathf.Abs(Mathf.DeltaAngle(lastHo, ho)) < 0.1f;

            lastPos = pos;
            lastVer = ver;
            lastHo = ho;

            if (still)
            {
                if (!LineXray.IsEnabled(player.ReferenceHub))
                {
                    LineXray.Enable(player.ReferenceHub, 200);
                }
            }
            else if (LineXray.IsEnabled(player.ReferenceHub))
            {
                LineXray.Disable(player.ReferenceHub);
            }
        }
        public void restart()
        {
            AimOverride.ClearAll();
        }
        public override void Unregister(Player player)
        {
            base.Unregister(player);
            if (LineXray.IsEnabled(player.ReferenceHub))
            {
                LineXray.Disable(player.ReferenceHub);
            }
            _ = vaild.Remove(player.ReferenceHub);
        }
        public override AbilityBase Register(Player player)
        {
            vaild.Add(player.ReferenceHub);
            return base.Register(player);
        }
        private const float ConeHalfAngle = 20f;
        private const float MaxRange = 150f;
        public void shoot(ShootingEventArgs ev)
        {
            if (!ev.IsAllowed)
            {
                return;
            }

            ReferenceHub hub = ev.Player?.ReferenceHub;
            if (!IsOwner(ev.Player))
            {
                return;
            }

            Transform cam = ev.Player.CameraTransform;
            Vector3 origin = cam.position + (cam.forward * 0.1f);

            bool preferHead = AimTargeting.PrefersHeadshot(ev.Firearm?.Base);

            if (!AimTargeting.TryFindTarget(hub, origin, cam.forward, preferHead, out HitboxIdentity target))
            {
                return;
            }

            Vector3 aimPoint = target.CenterOfMass;

            AimOverride.Set(hub, target);

            ev.Direction = (aimPoint - origin).normalized;
            _ = TryLookAt(ev.Player.ReferenceHub, aimPoint);
            DrawTracer(hub, origin, aimPoint);

        }
        private static void DrawTracer(ReferenceHub receiver, Vector3 start, Vector3 end)
        {
            float alpha = Mathf.Clamp01(1f - (Vector3.Distance(start, end) / AimTargeting.DefaultMaxRange) + 0.01f);

            new DrawableLineMessage(
                0.7f,
                Color.red * new Color(1f, 1f, 1f, alpha),
                new Vector3[2] { start, end }
            ).SendToHubsConditionally(x => x == receiver);
        }

        [HarmonyPatch]
        internal static class ExactRayPatch
        {
            // RandomizeRay is protected -> resolve manually
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(HitscanHitregModuleBase), "RandomizeRay");
            }

            private static bool Prefix(HitscanHitregModuleBase __instance, Ray ray, float angle, ref Ray __result)
            {
                ReferenceHub owner = __instance.Firearm?.Owner;
                if (owner == null || !AimOverride.TryConsume(owner, ray.origin, out Vector3 dir))
                {
                    return true;                        // vanilla: spread as normal
                }

                __result = new Ray(ray.origin, dir);    // exact, no cone
                return false;
            }
        }

        public static bool TryLookDirection(ReferenceHub hub, Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            dir.Normalize();

            // vertical: + up, - down
            float vertical = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            vertical = Mathf.Clamp(vertical, -88f, 88f);          // ClampVertical does this anyway

            // horizontal: world yaw, 0..360
            float horizontal = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (horizontal < 0f)
            {
                horizontal += 360f;
            }

            return hub.TryOverrideRotation(new Vector2(vertical, horizontal));
        }

        public static bool TryLookAt(ReferenceHub hub, Vector3 worldPoint)
        {
            return TryLookDirection(hub, worldPoint - hub.PlayerCameraReference.position);
        }

        private static readonly CachedLayerMask l = new("Player", "Hitbox");
        public static RaycastHit[] ConeCastAll(
             Vector3 origin,
             Vector3 direction,
             float maxDistance = MaxRange,
             float halfAngle = ConeHalfAngle,
             float sphereRadius = 0.1f)
        {

            direction.Normalize();
            Collider[] colliders = Physics.OverlapSphere(origin, maxDistance, l.Mask);
            List<RaycastHit> hits = new();

            foreach (Collider col in colliders)
            {
                Vector3 closestPoint = col.ClosestPoint(origin);
                Vector3 toClosest = closestPoint - origin;
                float dist = toClosest.magnitude;
                if (dist < 0.001f)
                {
                    continue;
                }

                Vector3 dirToClosest = toClosest / dist;
                if (Vector3.Angle(direction, dirToClosest) <= halfAngle)
                {
                    if (Physics.Raycast(origin, dirToClosest, out RaycastHit hit, maxDistance, l.Mask))
                    {
                        hits.Add(hit);
                    }
                }
            }

            hits.Sort((a, b) => a.distance.CompareTo(b.distance));
            return hits.ToArray();
        }
        public void OnEnable()
        {
            Exiled.Events.Handlers.Player.Shooting += shoot;
            Exiled.Events.Handlers.Server.RestartingRound += restart;
        }

        public void OnDisable()
        {
            Exiled.Events.Handlers.Player.Shooting -= shoot;
            Exiled.Events.Handlers.Server.RestartingRound -= restart;
        }

        public void OnReloadConfig()
        {
        }

        public DageAbi1(Player pl)
        {
            pass_player = pl;
            vaild.Add(pl.ReferenceHub);
        }
        public DageAbi1()
        {

        }
    }
    public class rot : PassAbility
    {
        //public override KeyCode KeyCode => KeyCode.Mouse2;

        public override string Name => "它转起来了!";

        public override string Des => "";
        public override AbilityBase Register(Player player)
        {
            FpcSpoofing.FakeYawSpinController.Start(player.ReferenceHub);
            return base.Register(player);
        }
        public override void OnCheck(Player player)
        {
        }
        public override void Unregister(Player player)
        {
            base.Unregister(player);
            FpcSpoofing.FakeYawSpinController.Stop(player.ReferenceHub);

        }
        internal rot(Player player) : base(player)
        {
            //TotalCount = 1;
        }
        public rot() : base()
        {
            //TotalCount = 1;
        }
    }
    public class jum : KeyAbility
    {
        //public override KeyCode KeyCode => KeyCode.Mouse2;

        public override string Name => "跳两下起飞了";

        public override string Des => "";

        public override KeyCode KeyCode => KeyCode.Space;

        public override bool OnTrigger()
        {
            if (player.Role.Base is IFpcRole fpc)
            {
                if (fpc.FpcModule?.Motor?.JumpController != null)
                {
                    fpc.FpcModule?.Motor?.JumpController.ForceJump(fpc.FpcModule.JumpSpeed);
                    return true;
                }
            }
            return false;
        }
        public override int TotalCount { get; set; } = 999;
        public override double time => 0;
        public override float WaitForDoneTime => 0.005f;
        public override float CoolDownRemaining { get => base.CoolDownRemaining; set => base.CoolDownRemaining = value; }
        public override AbilityBase Register(Player player)
        {
            if (player.Role.Base is IFpcRole fpc)
            {
                fpc.FpcModule.FallDamageSettings.Enabled = false;
            }
            return base.Register(player);
        }
    }
    public class yx : PassAbility
    {
        public override string Name => "大哥忘关就是开?";
        public override string Des => "";
        public int sid = -1;
        private static float audioLength = 0f; // 缓存音频时长
        public CoroutineHandle ch;

        public override AbilityBase Register(Player player)
        {
            sid = DefaultAudioManager.Instance.PlayTrackingAudio<int>(
                "dage",
                () => player.Position,
                () => true,
                0,
                (p, _) => p.IsReady,
                priority: AudioPriority.Medium,
                lifespan: audioLength,
                maxDistance: 100,
                minDistance: 20
            );
            ch = Timing.RunCoroutine(replayer());
            return base.Register(player);
        }
        public IEnumerator<float> replayer()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(audioLength);
                if (sid != -1)
                {
                    DefaultAudioManager.Instance.DestroySession(sid);

                    sid = DefaultAudioManager.Instance.PlayTrackingAudio<int>(
                        "dage",
                        () => pass_player.Position,
                        () => true,
                        0,
                        (p, _) => p.IsReady,
                        priority: AudioPriority.Medium,
                        lifespan: audioLength,
                        maxDistance: 100,
                        minDistance: 20
                    );
                }
            }
        }
        public override void OnCheck(Player player)
        {

        }

        public override void Unregister(Player player)
        {
            base.Unregister(player);
            if (sid != -1)
            {
                DefaultAudioManager.Instance.DestroySession(sid);
            }
            if (ch.IsRunning) Timing.KillCoroutines(ch);
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

        public yx() : base()
        {
            DefaultAudioManager.Instance.RegisterAudio("dage",
                () => File.OpenRead(Path.Combine(ModuleConfigManager.ConfigDir, "dage.wav")));
            audioLength = GetAudioDuration(Path.Combine(ModuleConfigManager.ConfigDir, "dage.wav"));
            Log.Info($"len:{audioLength}");
        }
    }
}
