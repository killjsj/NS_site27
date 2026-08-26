using PlayerRoles.FirstPersonControl;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    /// <summary>
    /// Per-(receiver, target) overrides applied to the FPC position/rotation stream.
    /// <para>
    /// Nothing here sends packets. <see cref="FpcSyncSpoofPatch"/> reads this store from inside
    /// <c>FpcServerPositionDistributor.GetNewSyncData</c>, which the game already calls once per
    /// receiver per target per tick.
    /// </para>
    /// </summary>
    public static class FpcSpoofer
    {
        // receiver -> target -> what that receiver should be told about that target
        private static readonly Dictionary<ReferenceHub, Dictionary<ReferenceHub, FakeFpcState>> Fakes
            = new();

        /// <summary>
        /// Merges <paramref name="state"/> into whatever is already registered.
        /// Fields left null keep their previous override (or the player's real value if never set).
        /// </summary>
        public static void Set(ReferenceHub receiver, ReferenceHub target, FakeFpcState state)
        {
            if (receiver == null || target == null || receiver == target)
            {
                return;
            }

            if (!Fakes.TryGetValue(receiver, out Dictionary<ReferenceHub, FakeFpcState> inner))
            {
                Fakes[receiver] = inner = new Dictionary<ReferenceHub, FakeFpcState>();
            }

            _ = inner.TryGetValue(target, out FakeFpcState existing);
            FakeFpcState merged = existing.With(state);

            if (merged.IsEmpty)
            {
                _ = inner.Remove(target);
            }
            else
            {
                inner[target] = merged;
            }
        }

        /// <summary>Replaces the entry outright instead of merging.</summary>
        public static void Replace(ReferenceHub receiver, ReferenceHub target, FakeFpcState state)
        {
            if (receiver == null || target == null || receiver == target)
            {
                return;
            }

            if (state.IsEmpty)
            {
                Clear(receiver, target);
                return;
            }

            if (!Fakes.TryGetValue(receiver, out Dictionary<ReferenceHub, FakeFpcState> inner))
            {
                Fakes[receiver] = inner = new Dictionary<ReferenceHub, FakeFpcState>();
            }

            inner[target] = state;
        }

        public static void SetPosition(ReferenceHub receiver, ReferenceHub target, Vector3 position)
        {
            Set(receiver, target, new FakeFpcState(position: position));
        }

        public static void SetYaw(ReferenceHub receiver, ReferenceHub target, float yaw)
        {
            Set(receiver, target, new FakeFpcState(yaw: yaw));
        }

        public static void SetPitch(ReferenceHub receiver, ReferenceHub target, float pitch)
        {
            Set(receiver, target, new FakeFpcState(pitch: pitch));
        }

        public static void SetRotation(ReferenceHub receiver, ReferenceHub target, float yaw, float pitch)
        {
            Set(receiver, target, new FakeFpcState(yaw: yaw, pitch: pitch));
        }

        public static void SetState(ReferenceHub receiver, ReferenceHub target, PlayerMovementState state)
        {
            Set(receiver, target, new FakeFpcState(state: state));
        }

        /// <summary>Drops every override this receiver has for this target.</summary>
        public static void Clear(ReferenceHub receiver, ReferenceHub target)
        {
            if (receiver == null || !Fakes.TryGetValue(receiver, out Dictionary<ReferenceHub, FakeFpcState> inner))
            {
                return;
            }

            _ = inner.Remove(target);
            if (inner.Count == 0)
            {
                _ = Fakes.Remove(receiver);
            }
        }

        /// <summary>Drops only the position override; rotation/state overrides survive.</summary>
        public static void ClearPosition(ReferenceHub receiver, ReferenceHub target)
        {
            Strip(receiver, target, s => s.WithoutPosition());
        }

        /// <summary>Drops only the yaw + pitch overrides.</summary>
        public static void ClearRotation(ReferenceHub receiver, ReferenceHub target)
        {
            Strip(receiver, target, s => s.WithoutRotation());
        }

        /// <summary>Drops only the movement-state override.</summary>
        public static void ClearState(ReferenceHub receiver, ReferenceHub target)
        {
            Strip(receiver, target, s => s.WithoutState());
        }

        /// <summary>Drops every override of this target, for every receiver.</summary>
        public static void ClearTarget(ReferenceHub target)
        {
            if (target == null)
            {
                return;
            }

            var emptied = new List<ReferenceHub>();
            foreach (var kv in Fakes)
            {
                _ = kv.Value.Remove(target);
                if (kv.Value.Count == 0)
                {
                    emptied.Add(kv.Key);
                }
            }

            foreach (ReferenceHub r in emptied)
            {
                _ = Fakes.Remove(r);
            }
        }

        /// <summary>Drops everything this receiver was being lied to about.</summary>
        public static void ClearReceiver(ReferenceHub receiver)
        {
            if (receiver != null)
            {
                _ = Fakes.Remove(receiver);
            }
        }

        /// <summary>Call on plugin disable and on round restart — keys hold ReferenceHub references.</summary>
        public static void ClearAll()
        {
            Fakes.Clear();
        }

        internal static bool TryGet(ReferenceHub receiver, ReferenceHub target, out FakeFpcState state)
        {
            state = default;
            return receiver != null
                && Fakes.TryGetValue(receiver, out Dictionary<ReferenceHub, FakeFpcState> inner)
                && inner.TryGetValue(target, out state);
        }

        private static void Strip(ReferenceHub receiver, ReferenceHub target,
                                  System.Func<FakeFpcState, FakeFpcState> transform)
        {
            if (receiver == null || !Fakes.TryGetValue(receiver, out Dictionary<ReferenceHub, FakeFpcState> inner))
            {
                return;
            }

            if (!inner.TryGetValue(target, out FakeFpcState current))
            {
                return;
            }

            FakeFpcState next = transform(current);
            if (next.IsEmpty)
            {
                _ = inner.Remove(target);
                if (inner.Count == 0)
                {
                    _ = Fakes.Remove(receiver);
                }
            }
            else
            {
                inner[target] = next;
            }
        }
    }
}
