using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Modules;
using PlayerRoles;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
                                    public static class AimTargeting
    {
                public const float DefaultConeHalfAngle = 20f;

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

                                                        public static bool PrefersHeadshot(Firearm firearm)
        {
            return firearm == null || !firearm.TryGetModule(out HitscanHitregModuleBase hitreg, true)
                || hitreg.UseHitboxMultipliers;
        }

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

                                    SortedPlayers.Sort((a, b) => PlayerScores[b].CompareTo(PlayerScores[a]));

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

                private static int Rank(HitboxType type, bool preferHeadshot)
        {
            return type switch
            {
                HitboxType.Headshot => preferHeadshot ? 3 : 1,                HitboxType.Body => 2,                HitboxType.Limb => 0,                _ => 0,
            };
        }

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

                        float nearest = float.MaxValue;
            Collider blockerCol = null;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = LosHits[i];

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
