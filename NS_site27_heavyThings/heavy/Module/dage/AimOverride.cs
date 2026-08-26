using PlayerRoles;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    /// <summary>
    /// One-shot store of "the next bullet this player fires should go here".
    /// <para>
    /// Written from the Shooting event, consumed inside the RandomizeRay patch. Shooting ->
    /// ProcessShot -> Fire -> RandomizeRay is one synchronous call stack in the same frame, DropSo an
    /// entry from an earlier frame means the shot never reached hitreg (cancelled by another
    /// plugin, rejected by a module) and must be discarded rather than applied to a later shot.
    /// </para>
    /// </summary>
    internal static class AimOverride
    {
        private struct Entry
        {
            public HitboxIdentity Hitbox;
            public int Frame;
        }

        private static readonly Dictionary<ReferenceHub, Entry> Pending = new();

        public static void Set(ReferenceHub shooter, HitboxIdentity target)
        {
            if (shooter == null || target == null)
            {
                return;
            }

            Pending[shooter] = new Entry { Hitbox = target, Frame = Time.frameCount };
        }

        /// <summary>
        /// Pops the pending override and resolves it into a direction.
        /// </summary>
        /// <param name="origin">
        /// The <em>backtracked</em> muzzle position from inside the hitreg call. Recomputing the
        /// direction here rather than reusing one captured at event time keeps the aim correct
        /// after FpcBacktracker has rewound the shooter (and possibly the victim).
        /// </param>
        public static bool TryConsume(ReferenceHub shooter, Vector3 origin, out Vector3 dir)
        {
            dir = default;

            if (shooter == null || !Pending.TryGetValue(shooter, out Entry e))
            {
                return false;
            }

            _ = Pending.Remove(shooter);

            if (e.Frame != Time.frameCount)
            {
                return false;
            }

            if (e.Hitbox == null || e.Hitbox.TargetHub == null || !e.Hitbox.TargetHub.roleManager.CurrentRole.RoleTypeId.IsAlive())
            {
                return false;
            }

            Vector3 delta = e.Hitbox.CenterOfMass - origin;
            if (delta.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            dir = delta.normalized;
            return true;
        }

        public static void Clear(ReferenceHub shooter)
        {
            if (shooter != null)
            {
                _ = Pending.Remove(shooter);
            }
        }

        public static void ClearAll()
        {
            Pending.Clear();
        }
    }
}
