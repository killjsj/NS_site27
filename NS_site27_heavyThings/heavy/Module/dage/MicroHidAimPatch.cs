using HarmonyLib;
using InventorySystem.Items.MicroHID;
using InventorySystem.Items.MicroHID.Modules;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    [HarmonyPatch(typeof(BacktrackerModule), nameof(BacktrackerModule.BacktrackAll))]
    internal static class MicroHidAimPatch
    {
        public const float ConeHalfAngle = 30f;

        private const float FallbackRange = 10f;

        private static readonly Action WrapperDelegate = InvokeWithRedirect;
        private static ReferenceHub _owner;
        private static float _range;
        private static Action _inner;
        private static bool _pending;

        private static void Prefix(BacktrackerModule __instance, ref Action callback)
        {
            if (callback == null || _pending)
            {
                return;
            }

            MicroHIDItem micro = __instance.MicroHid;
            ReferenceHub owner = micro?.Owner;

            if (owner == null || !DageAbi1.vaild.Contains(owner))
            {
                return;
            }

            _owner = owner;
            _range = micro.CycleController.TryGetLastFiringController(out FiringModeControllerModule ctrl)
                ? ctrl.FiringRange
                : FallbackRange;
            _inner = callback;
            _pending = true;

            callback = WrapperDelegate;
        }

        private static void Finalizer()
        {
            _pending = false;
            _owner = null;
            _inner = null;
        }
        public static bool TryLookDirection(ReferenceHub hub, Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            dir.Normalize();

            float vertical = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            vertical = Mathf.Clamp(vertical, -88f, 88f);
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

        private static void InvokeWithRedirect()
        {
            ReferenceHub owner = _owner;
            float range = _range;
            Action inner = _inner;

            _pending = false;
            _owner = null;
            _inner = null;

            if (inner == null)
            {
                return;
            }

            if (owner == null || owner.roleManager.CurrentRole is not IFpcRole)
            {
                inner();
                return;
            }

            Transform cam = owner.PlayerCameraReference;
            Quaternion saved = cam.rotation;

            try
            {
                bool found = AimTargeting.TryFindTarget(
owner,
cam.position,
cam.forward,
preferHeadshot: false,
out HitboxIdentity target,
coneHalfAngle: ConeHalfAngle,
maxRange: range,
losMask: PlayerRolesUtils.AttackMask);

                if (found)
                {
                    Vector3 dir = target.CenterOfMass - cam.position;
                    if (dir.sqrMagnitude > 1e-6f)
                    {
                        cam.rotation = Quaternion.LookRotation(dir.normalized);
                    }

                    _ = TryLookAt(owner, target.CenterOfMass);

                }

                inner();
            }
            finally
            {
                cam.rotation = saved;
            }
        }
    }
}
