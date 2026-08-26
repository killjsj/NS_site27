using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Modules;
using PlayerRoles;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    /// <summary>
    /// Picks what the redirected bullet should be aimed at.
    /// <para>
    /// Two stages: first the enemy closest to the crosshair (by angle, not by distance), then the
    /// highest-damage hitbox on that enemy that actually has line of sight. If the head is blocked
    /// but the torso isn't, you get the torso rather than a wasted shot.
    /// </para>
    /// </summary>
    public static class AimTargeting
    {
        /// <summary>Half-angle of the acquisition cone, in degrees.</summary>
        public const float DefaultConeHalfAngle = 20f;

        /// <summary>Maximum acquisition range. Vanilla damage falloff makes anything beyond this pointless.</summary>
        public const float DefaultMaxRange = 150f;

        private struct Candidate
        {
            public HitboxIdentity Hitbox;
            public float Dot;
            public float Dist;
        }

        private static readonly Dictionary<ReferenceHub, float> PlayerScores = new();
        private static readonly Dictionary<ReferenceHub, List<Candidate>> PlayerHitboxes = new();
        private static readonly List<ReferenceHub> SortedPlayers = new();

        /// <summary>
        /// True if this firearm's hitreg actually applies hitbox multipliers. Buckshot does not
        /// (<c>BuckshotHitreg.UseHitboxMultipliers</c> is false, and <c>FirearmDamageHandler</c>
        /// then forces <c>HitboxType.Body</c>), DropSo for shotguns a headshot is worth nothing and the
        /// bigger torso target is strictly better.
        /// </summary>
        public static bool PrefersHeadshot(Firearm firearm)
        {
            return firearm == null || !firearm.TryGetModule(out HitscanHitregModuleBase hitreg, true)
                || hitreg.UseHitboxMultipliers;
        }

        /// <summary>
        /// Finds the best hitbox to aim at, or returns false if nothing qualifies.
        /// </summary>
        /// <param name="shooter">The firing player. Never targeted, and only their enemies are considered.</param>
        /// <param name="origin">Ray origin — use the muzzle offset you will actually fire from.</param>
        /// <param name="forward">Look direction used to score candidates.</param>
        /// <param name="preferHeadshot">
        /// See <see cref="PrefersHeadshot"/>. When false, the torso is ranked above the head.
        /// </param>
        /// <param name="losMask">
        /// Layer mask for the line-of-sight check. Defaults to the firearm hitreg mask. MicroHID
        /// uses <c>PlayerRolesUtils.AttackMask</c> instead — pass whatever the weapon will actually
        /// trace against, or you will lock onto targets the weapon cannot reach.
        /// </param>
        public static bool TryFindTarget(ReferenceHub shooter,
                                         Vector3 origin,
                                         Vector3 forward,
                                         bool preferHeadshot,
                                         out HitboxIdentity best,
                                         float coneHalfAngle = DefaultConeHalfAngle,
                                         float maxRange = DefaultMaxRange,
                                         int? losMask = null)
        {
            best = null;

            if (shooter == null)
            {
                return false;
            }

            PlayerScores.Clear();
            PlayerHitboxes.Clear();
            SortedPlayers.Clear();

            float minDot = Mathf.Cos(coneHalfAngle * Mathf.Deg2Rad);

            // ---- stage 1: bucket every in-cone enemy hitbox by owner -------------------------
            foreach (HitboxIdentity hb in HitboxIdentity.Instances)
            {
                if (hb == null)
                {
                    continue;
                }

                ReferenceHub owner = hb.TargetHub;
                if (owner == null || owner == shooter || !owner.roleManager.CurrentRole.RoleTypeId.IsAlive())
                {
                    continue;
                }

                if (!HitboxIdentity.IsEnemy(shooter, owner))
                {
                    continue;
                }

                Vector3 delta = hb.CenterOfMass - origin;
                float dist = delta.magnitude;
                if (dist < 0.01f || dist > maxRange)
                {
                    continue;
                }

                float dot = Vector3.Dot(forward, delta / dist);
                if (dot < minDot)
                {
                    continue;
                }

                if (!PlayerHitboxes.TryGetValue(owner, out List<Candidate> list))
                {
                    PlayerHitboxes[owner] = list = new List<Candidate>();
                    SortedPlayers.Add(owner);
                    PlayerScores[owner] = dot;
                }
                else if (dot > PlayerScores[owner])
                {
                    PlayerScores[owner] = dot;
                }

                list.Add(new Candidate { Hitbox = hb, Dot = dot, Dist = dist });
            }

            if (SortedPlayers.Count == 0)
            {
                return false;
            }

            int mask = losMask ?? HitscanHitregModuleBase.HitregMask;

            // Closest to the crosshair first — NOT closest in space. A body at your feet should not
            // beat the enemy you are actually looking at.
            SortedPlayers.Sort((a, b) => PlayerScores[b].CompareTo(PlayerScores[a]));

            // ---- stage 2: best visible hitbox on the best visible player ---------------------
            foreach (ReferenceHub owner in SortedPlayers)
            {
                List<Candidate> list = PlayerHitboxes[owner];

                list.Sort((a, b) =>
                {
                    int rank = Rank(b.Hitbox.HitboxType, preferHeadshot)
                        .CompareTo(Rank(a.Hitbox.HitboxType, preferHeadshot));
                    return rank != 0 ? rank : b.Dot.CompareTo(a.Dot);
                });

                foreach (Candidate c in list)
                {
                    if (!HasLineOfSight(shooter, origin, c, owner, mask))
                    {
                        continue;
                    }

                    best = c.Hitbox;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Higher is better. Mirrors FirearmDamageHandler.HitboxDamageMultipliers.</summary>
        private static int Rank(HitboxType type, bool preferHeadshot)
        {
            return type switch
            {
                HitboxType.Headshot => preferHeadshot ? 3 : 1,// x2.0 damage
                HitboxType.Body => 2,// x1.0
                HitboxType.Limb => 0,// x0.7
                _ => 0,
            };
        }

        /// <summary>
        /// Must use the same mask the weapon itself traces against. Checking against a
        /// Player/Hitbox-only mask would happily lock through concrete and then eat the wall.
        /// </summary>
        private static readonly RaycastHit[] LosHits = new RaycastHit[64];

        private static bool HasLineOfSight(ReferenceHub shooter, Vector3 origin,
                                           Candidate candidate, ReferenceHub owner, int mask)
        {
            Vector3 dir = (candidate.Hitbox.CenterOfMass - origin).normalized;

            int count = Physics.RaycastNonAlloc(origin, dir, LosHits, candidate.Dist + 0.5f, mask);
            if (count == 0)
            {
                return true;
            }

            // RaycastNonAlloc 不保证有序，手动找最近的非自身命中
            float nearest = float.MaxValue;
            Collider blockerCol = null;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = LosHits[i];

                // 自己的身体不算障碍物 —— 原版在 Fire() 里会把它关掉，但那时机在 Shooting 之后
                if (h.collider.TryGetComponent(out HitboxIdentity self) && self.TargetHub == shooter)
                {
                    continue;
                }

                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    blockerCol = h.collider;
                }
            }

            return blockerCol == null || (blockerCol.TryGetComponent(out HitboxIdentity blocker)
                && blocker.TargetHub == owner);
        }
    }
}
