using HarmonyLib;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using RelativePositioning;
using System.Reflection;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    [HarmonyPatch]
    internal static class FpcSyncSpoofPatch
    {
        private static readonly AccessTools.FieldRef<FirstPersonMovementModule, RelativePosition> RelPos =
    AccessTools.FieldRefAccess<FirstPersonMovementModule, RelativePosition>("_relativePosition");

        private static bool _swapped;
        private static FirstPersonMovementModule _module;
        private static RelativePosition _savedPos;
        private static float _savedYaw;
        private static float _savedPitch;
        private static PlayerMovementState _savedState;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FpcServerPositionDistributor), "GetNewSyncData");
        }

        private static void Prefix(ReferenceHub receiver, ReferenceHub target,
                                   FirstPersonMovementModule fpmm, bool isInvisible)
        {
            Restore();
            if (fpmm == null || isInvisible)
            {
                return;
            }

            if (!FpcSpoofer.TryGet(receiver, target, out FakeFpcState fake) || fake.IsEmpty)
            {
                return;
            }

            _module = fpmm;
            _savedPos = fpmm.RelativePosition;
            _savedYaw = fpmm.MouseLook.CurrentHorizontal;
            _savedPitch = fpmm.MouseLook.CurrentVertical;
            _savedState = fpmm.SyncMovementState;
            _swapped = true;

            if (fake.Position.HasValue)
            {
                RelPos(fpmm) = new RelativePosition(fake.Position.Value);
            }

            if (fake.Yaw.HasValue)
            {
                fpmm.MouseLook.CurrentHorizontal = fake.Yaw.Value;
            }

            if (fake.Pitch.HasValue)
            {
                fpmm.MouseLook.CurrentVertical = fake.Pitch.Value;
            }

            if (fake.State.HasValue)
            {
                fpmm.CurrentMovementState = fake.State.Value;
            }
        }

        private static void Finalizer()
        {
            Restore();
        }

        private static void Restore()
        {
            if (!_swapped)
            {
                return;
            }

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
