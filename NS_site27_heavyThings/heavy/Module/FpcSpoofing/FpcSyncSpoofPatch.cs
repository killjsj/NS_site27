using System.Reflection;
using HarmonyLib;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using RelativePositioning;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    /// <summary>
    /// Hooks <c>FpcServerPositionDistributor.GetNewSyncData(receiver, target, fpmm, isInvisible)</c>.
    /// <para>
    /// That method is the only place in the sync pipeline that sees both the receiver and the target,
    /// so it is where "show player X differently to client Y" belongs. It reads four values off the
    /// live module; the prefix swaps in the fake ones, the original builds the packet, the finalizer
    /// puts the real ones back.
    /// </para>
    /// <para>
    /// Per-receiver delta compression is handled for free: the original writes whatever it built into
    /// <c>PreviouslySent[receiver][target]</c>, so the _bitPosition / _bitMouseLook dirty flags stay
    /// consistent with what that specific client was actually sent.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class FpcSyncSpoofPatch
    {
        // RelativePosition's setter is private and fires waypoint-change side effects, so write the field.
        private static readonly AccessTools.FieldRef<FirstPersonMovementModule, RelativePosition> RelPos =
            AccessTools.FieldRefAccess<FirstPersonMovementModule, RelativePosition>("_relativePosition");

        private static bool _swapped;
        private static FirstPersonMovementModule _module;
        private static RelativePosition _savedPos;
        private static float _savedYaw;
        private static float _savedPitch;
        private static PlayerMovementState _savedState;

        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(FpcServerPositionDistributor), "GetNewSyncData");

        private static void Prefix(ReferenceHub receiver, ReferenceHub target,
                                   FirstPersonMovementModule fpmm, bool isInvisible)
        {
            Restore();               // defensive: a previous call that bailed before the finalizer

            if (fpmm == null || isInvisible)
                return;              // invisible targets are sent default(FpcSyncData) regardless

            if (!FpcSpoofer.TryGet(receiver, target, out FakeFpcState fake) || fake.IsEmpty)
                return;

            _module = fpmm;
            _savedPos = fpmm.RelativePosition;
            _savedYaw = fpmm.MouseLook.CurrentHorizontal;
            _savedPitch = fpmm.MouseLook.CurrentVertical;
            _savedState = fpmm.SyncMovementState;
            _swapped = true;

            // Position first: FpcSyncData encodes yaw relative to the position's waypoint
            // (mLook.GetSyncValues(pos.WaypointId, ...)), so the two must be swapped in this order.
            if (fake.Position.HasValue)
                RelPos(fpmm) = new RelativePosition(fake.Position.Value);

            if (fake.Yaw.HasValue)
                fpmm.MouseLook.CurrentHorizontal = fake.Yaw.Value;      // setter wraps into 0..360

            if (fake.Pitch.HasValue)
                fpmm.MouseLook.CurrentVertical = fake.Pitch.Value;      // setter clamps to +/-88

            if (fake.State.HasValue)
                fpmm.CurrentMovementState = fake.State.Value;           // public setter -> SyncMovementState
        }

        // Runs like a finally block: also fires if the original throws.
        private static void Finalizer() => Restore();

        private static void Restore()
        {
            if (!_swapped)
                return;

            _swapped = false;

            if (_module != null)
            {
                RelPos(_module) = _savedPos;
                _module.MouseLook.CurrentHorizontal = _savedYaw;
                _module.MouseLook.CurrentVertical = _savedPitch;
                _module.CurrentMovementState = _savedState;
            }

            _module = null;
        }
    }
}
