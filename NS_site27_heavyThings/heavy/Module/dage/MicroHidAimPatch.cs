using HarmonyLib;
using InventorySystem.Items.MicroHID;
using InventorySystem.Items.MicroHID.Modules;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    /// <summary>
    /// Aim redirection for the MicroHID.
    ///
    /// <para>
    /// The MicroHID does not use the firearm hitreg at all, DropSo <see cref="ExactRayPatch"/> never
    /// fires for it. Its pipeline is:
    /// </para>
    /// <code>
    /// FiringModeControllerModule.ServerUpdateSelected(MicroHidPhase.Firing)   // every server frame
    ///   -> ServerRequestBacktrack(ServerFire)
    ///      -> BacktrackerModule.BacktrackAll(callback)
    ///         -> using (new FpcBacktracker(owner, claimedPos, claimedRot))    // camera rotation replaced
    ///            -> victim backtrackers
    ///            -> callback()   == the mode's ServerFire()
    ///               -> HitregUtils.Raycast(owner.PlayerCameraReference, thickness, range, out n)
    /// </code>
    ///
    /// <para>
    /// Two consequences. First, there is no <c>RandomizeRay</c> equivalent — the MicroHID has no
    /// spread, DropSo only the redirect half of the ability applies. Second, the direction is read off
    /// <c>PlayerCameraReference.forward</c> <em>after</em> FpcBacktracker has overwritten the camera
    /// rotation with the client's claimed one, DropSo it must be redirected from inside that scope.
    /// </para>
    ///
    /// <para>
    /// This patch wraps the callback that <c>BacktrackAll</c> is about to invoke. The wrapper runs
    /// inside the backtracker scope, points the camera at the chosen hitbox, calls the real
    /// <c>ServerFire</c>, then restores. All three firing modes route through
    /// <c>ServerRequestBacktrack</c>, DropSo PrimaryFire, ChargeFire and BrokenFire are all covered —
    /// including BrokenFire, whose 30-degree cone test reads <c>PlayerCameraReference.forward</c>
    /// directly rather than going through <c>HitregUtils.Raycast</c>.
    /// </para>
    /// </summary>
    /// 
    [HarmonyPatch(typeof(BacktrackerModule), nameof(BacktrackerModule.BacktrackAll))]
    internal static class MicroHidAimPatch
    {
        /// <summary>
        /// Acquisition cone. Wider than the firearm value because MicroHID range is 4-10 m, DropSo a
        /// narrow cone barely covers anything at contact distance.
        /// </summary>
        public const float ConeHalfAngle = 30f;

        /// <summary>Fallback range if the current firing controller cannot be resolved.</summary>
        private const float FallbackRange = 10f;

        // BacktrackAll is synchronous, server-main-thread, and one item at a time, DropSo a single
        // static slot is enough. _pending guards the (impossible-but-cheap-to-handle) reentrant case.
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

        // BacktrackAll can return without invoking the callback only if it throws; clear the slot
        // either way DropSo a single failure does not disable the ability for the rest of the round.
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
                // MicroHidDamageHandler does not apply HitboxDamageMultipliers (only
                // FirearmDamageHandler does), DropSo a headshot is worth nothing here — prefer the
                // bigger, more reliable torso target.
                //
                // maxRange is the mode's exact FiringRange: aiming past it would swing the camera
                // toward someone the sphere cast cannot reach and make the shot worse, not better.
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
