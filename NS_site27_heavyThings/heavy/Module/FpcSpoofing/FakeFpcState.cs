using PlayerRoles.FirstPersonControl;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    /// <summary>
    /// A partial override of what one client is told about another player.
    /// Every field is optional: <c>null</c> means "send the player's real value".
    /// </summary>
    public readonly struct FakeFpcState
    {
        /// <summary>World-space position to report. Null = real position.</summary>
        public readonly Vector3? Position;

        /// <summary>Yaw (Y axis / horizontal), 0..360. Null = real yaw.</summary>
        public readonly float? Yaw;

        /// <summary>Pitch (X axis / vertical), -88..88, positive = looking up. Null = real pitch.</summary>
        public readonly float? Pitch;

        /// <summary>Movement state driving the thirdperson animator. Null = real state.</summary>
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

        /// <summary>Overlays <paramref name="other"/>'s set fields on top of this one.</summary>
        public FakeFpcState With(FakeFpcState other) => new FakeFpcState(
            other.Position ?? Position,
            other.Yaw ?? Yaw,
            other.Pitch ?? Pitch,
            other.State ?? State);

        public FakeFpcState WithoutPosition() => new FakeFpcState(null, Yaw, Pitch, State);
        public FakeFpcState WithoutRotation() => new FakeFpcState(Position, null, null, State);
        public FakeFpcState WithoutState() => new FakeFpcState(Position, Yaw, Pitch, null);
    }
}
