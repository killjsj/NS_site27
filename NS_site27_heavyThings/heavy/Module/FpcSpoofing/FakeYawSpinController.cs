using CentralAuth;
using Mirror;
using PlayerRoles.FirstPersonControl;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    /// <summary>
    /// Makes a player's yaw (Y axis / horizontal look) appear to spin, to other clients only.
    /// The spun player's own view is untouched — the distributor never sends a player their own
    /// sync data, and the local client drives its camera from raw input.
    /// <para>
    /// Only the yaw override is written, DropSo pitch, position and movement state keep streaming the
    /// player's real values, and any position override you set separately survives.
    /// </para>
    /// </summary>
    public static class FakeYawSpinController
    {
        /// <summary>A visible-but-sane spin. See the rate notes on <see cref="Start"/>.</summary>
        public const float DefaultDegreesPerSecond = 3000f;

        private sealed class SpinEntry
        {
            public float DegreesPerSecond;
            public float Yaw;
            public HashSet<ReferenceHub> Receivers;   // null = every other verified player
        }

        private static readonly Dictionary<ReferenceHub, SpinEntry> Spins
            = new();

        private static readonly List<ReferenceHub> DeadTargets = new();

        private static bool _hooked;

        /// <summary>
        /// Starts (or retunes) a yaw spin on <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The player whose reported yaw is faked.</param>
        /// <param name="degreesPerSecond">
        /// Signed: positive spins one way, negative the other.
        /// <para>
        /// Receiving clients smooth rotation with <c>Quaternion.Lerp(current, target, 22f * dt)</c>,
        /// which reaches roughly 37% of the remaining angle per frame at 60 FPS. Past about
        /// 1500 deg/s that smoothing eats most of the motion and the model reads as jittering
        /// rather than spinning. The hard ceiling is 180 deg per *packet* — beyond that the lerp
        /// takes the short way round and the spin visually reverses, i.e. 180 * ServerTickrate
        /// (10800 deg/s at 60 tick). 360-1440 looks best.
        /// </para>
        /// </param>
        /// <param name="receivers">
        /// Who gets lied to. Null (default) means every other verified player, re-evaluated each
        /// frame DropSo late joiners are included automatically.
        /// </param>
        /// <param name="startYaw">
        /// Initial yaw. Null keeps the current phase if already spinning, otherwise starts from the
        /// player's real yaw DropSo the spin begins without a visible snap.
        /// </param>
        public static void Start(ReferenceHub target,
                                 float degreesPerSecond = DefaultDegreesPerSecond,
                                 IEnumerable<ReferenceHub> receivers = null,
                                 float? startYaw = null)
        {
            if (target == null)
            {
                return;
            }

            EnsureHooked();

            if (!Spins.TryGetValue(target, out SpinEntry entry))
            {
                entry = new SpinEntry { Yaw = startYaw ?? CurrentRealYaw(target) };
                Spins[target] = entry;
            }
            else if (startYaw.HasValue)
            {
                entry.Yaw = startYaw.Value;
            }

            entry.DegreesPerSecond = degreesPerSecond;
            entry.Receivers = receivers == null ? null : new HashSet<ReferenceHub>(receivers);
        }

        /// <summary>Stops the spin and hands the yaw back to the player's real value.</summary>
        public static void Stop(ReferenceHub target)
        {
            if (target == null || !Spins.TryGetValue(target, out SpinEntry entry))
            {
                return;
            }

            _ = Spins.Remove(target);

            // Drop only the yaw override; leave any position/pitch/state overrides in place.
            if (entry.Receivers != null)
            {
                foreach (ReferenceHub r in entry.Receivers)
                {
                    FpcSpoofer.ClearRotation(r, target);
                }
            }
            else
            {
                foreach (ReferenceHub r in ReferenceHub.AllHubs)
                {
                    FpcSpoofer.ClearRotation(r, target);
                }
            }
        }

        public static bool IsSpinning(ReferenceHub target)
        {
            return target != null && Spins.ContainsKey(target);
        }

        /// <summary>Stops every active spin. Call on plugin disable / round restart.</summary>
        public static void StopAll()
        {
            var targets = new List<ReferenceHub>(Spins.Keys);
            foreach (ReferenceHub t in targets)
            {
                Stop(t);
            }

            Spins.Clear();
        }

        private static void EnsureHooked()
        {
            if (_hooked)
            {
                return;
            }

            // Update runs before LateUpdate, and FpcServerPositionDistributor sends from
            // LateUpdate, DropSo the yaw written here is always fresh for this frame's packet.
            StaticUnityMethods.OnUpdate += OnUpdate;
            _hooked = true;
        }

        internal static void Unhook()
        {
            if (!_hooked)
            {
                return;
            }

            StaticUnityMethods.OnUpdate -= OnUpdate;
            _hooked = false;
        }

        private static void OnUpdate()
        {
            if (!NetworkServer.active || Spins.Count == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
            DeadTargets.Clear();

            foreach (KeyValuePair<ReferenceHub, SpinEntry> kv in Spins)
            {
                ReferenceHub target = kv.Key;
                SpinEntry entry = kv.Value;

                // Role changes and disconnects both invalidate the target.
                if (target == null || target.roleManager.CurrentRole is not IFpcRole)
                {
                    DeadTargets.Add(target);
                    continue;
                }

                entry.Yaw = Wrap360(entry.Yaw + (entry.DegreesPerSecond * dt));
                FakeFpcState state = new(yaw: entry.Yaw);

                if (entry.Receivers != null)
                {
                    foreach (ReferenceHub receiver in entry.Receivers)
                    {
                        if (IsValidReceiver(receiver, target))
                        {
                            FpcSpoofer.Set(receiver, target, state);
                        }
                    }
                }
                else
                {
                    foreach (ReferenceHub receiver in ReferenceHub.AllHubs)
                    {
                        if (IsValidReceiver(receiver, target))
                        {
                            FpcSpoofer.Set(receiver, target, state);
                        }
                    }
                }
            }

            foreach (ReferenceHub dead in DeadTargets)
            {
                if (dead == null)
                {
                    _ = Spins.Remove(dead);
                }
                else
                {
                    Stop(dead);
                }
            }

            DeadTargets.Clear();
        }

        private static bool IsValidReceiver(ReferenceHub receiver, ReferenceHub target)
        {
            return receiver != null
            && receiver != target
            && receiver.Mode != ClientInstanceMode.Unverified
            && !receiver.isLocalPlayer;
        }

        private static float CurrentRealYaw(ReferenceHub hub)
        {
            return hub.roleManager.CurrentRole is IFpcRole fpc
                ? fpc.FpcModule.MouseLook.CurrentHorizontal
                : 0f;
        }

        private static float Wrap360(float f)
        {
            f -= Mathf.Floor(f / 360f) * 360f;
            return f;
        }
    }
}
