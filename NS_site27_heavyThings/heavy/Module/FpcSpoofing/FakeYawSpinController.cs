using CentralAuth;
using Mirror;
using PlayerRoles.FirstPersonControl;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.FpcSpoofing
{
    public static class FakeYawSpinController
    {
        public const float DefaultDegreesPerSecond = 3000f;

        private sealed class SpinEntry
        {
            public float DegreesPerSecond;
            public float Yaw;
            public HashSet<ReferenceHub> Receivers;
        }

        private static readonly Dictionary<ReferenceHub, SpinEntry> Spins
            = new();

        private static readonly List<ReferenceHub> DeadTargets = new();

        private static bool _hooked;

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

        public static void Stop(ReferenceHub target)
        {
            if (target == null || !Spins.TryGetValue(target, out SpinEntry entry))
            {
                return;
            }

            _ = Spins.Remove(target);

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
