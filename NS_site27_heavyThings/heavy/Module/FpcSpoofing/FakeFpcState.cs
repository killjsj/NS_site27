using PlayerRoles.FirstPersonControl;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    public readonly struct FakeFpcState
    {
        public readonly Vector3? Position;

        public readonly float? Yaw;

        public readonly float? Pitch;

        public readonly PlayerMovementState? State;

        public FakeFpcState(
            Vector3? position = null,
            float? yaw = null,
            float? pitch = null,
            PlayerMovementState? state = null)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
            State = state;
        }

        public bool IsEmpty =>
            !Position.HasValue && !Yaw.HasValue && !Pitch.HasValue && !State.HasValue;

        public FakeFpcState With(FakeFpcState other)
        {
            return new FakeFpcState(
            other.Position ?? Position,
            other.Yaw ?? Yaw,
            other.Pitch ?? Pitch,
            other.State ?? State);
        }

        public FakeFpcState WithoutPosition()
        {
            return new FakeFpcState(null, Yaw, Pitch, State);
        }

        public FakeFpcState WithoutRotation()
        {
            return new FakeFpcState(Position, null, null, State);
        }

        public FakeFpcState WithoutState()
        {
            return new FakeFpcState(Position, Yaw, Pitch, null);
        }
    }
}
