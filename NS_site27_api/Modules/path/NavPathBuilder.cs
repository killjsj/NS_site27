using Exiled.API.Features;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityNavigation
{
    public static class NavPathBuilder
    {
        public const float MaxSampleDistance = 6f;

        private static readonly NavMeshPath SharedPath = new();

        public static bool TryBuildFullPath(Vector3 from, Vector3 to,
            out List<Vector3> corners, out List<RoomNode> rooms)
        {
            corners = new List<Vector3>();

            if (!RoomGraph.TryBuildWaypoints(from, to, out rooms, out List<Vector3> waypoints))
            {
                return false;
            }

            NudgeWaypointsOntoRoute(waypoints);

            if (waypoints.Count == 1)
            {
                corners.Add(Snap(from));
                return true;
            }

            Vector3 current = Snap(from);
            corners.Add(current);

            for (int i = 1; i < waypoints.Count; i++)
            {
                Vector3 next = Snap(waypoints[i]);
                AppendSegment(current, next, corners);
                current = next;
            }

            return true;
        }

        private static void NudgeWaypointsOntoRoute(List<Vector3> waypoints)
        {
            for (int i = 1; i < waypoints.Count - 1; i++)
            {
                Vector3 forward = waypoints[i + 1] - waypoints[i];
                forward.y = 0f;

                float magnitude = forward.magnitude;
                if (magnitude < 0.01f)
                {
                    continue;
                }

                waypoints[i] = ResolveInsidePoint(waypoints[i], forward / magnitude);
            }
        }

        private static Vector3 ResolveInsidePoint(Vector3 point, Vector3 forward)
        {
            Vector3[] candidates =
            {
                point,
                point + (forward * 1.5f),
                point + (forward * 3f),
                point - (forward * 1.5f),
            };

            foreach (Vector3 candidate in candidates)
            {
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return point;
        }

        public static bool TryBuildFullPath(Player player, Vector3 target,
            out List<Vector3> corners, out List<RoomNode> rooms)
        {
            return TryBuildFullPath(player.Position, target, out corners, out rooms);
        }

        public static bool TryBuildRoomRoute(Vector3 from, Vector3 to, out List<RoomNode> rooms)
        {
            return RoomGraph.TryBuildWaypoints(from, to, out rooms, out _);
        }

        private static void AppendSegment(Vector3 from, Vector3 to, List<Vector3> corners)
        {
            if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, SharedPath))
            {
                Vector3[] segmentCorners = SharedPath.corners;

                if (SharedPath.status == NavMeshPathStatus.PathComplete)
                {
                    for (int i = 1; i < segmentCorners.Length; i++)
                    {
                        AddCorner(corners, segmentCorners[i]);
                    }

                    return;
                }

                if (SharedPath.status == NavMeshPathStatus.PathPartial &&
                    segmentCorners.Length > 1)
                {
                    Vector3 last = segmentCorners[segmentCorners.Length - 1];

                    bool progressed = (last - from).sqrMagnitude > 0.25f &&
                                      (to - last).sqrMagnitude < (to - from).sqrMagnitude &&
                                      (to - last).sqrMagnitude < 16f;

                    if (progressed)
                    {
                        for (int i = 1; i < segmentCorners.Length; i++)
                        {
                            AddCorner(corners, segmentCorners[i]);
                        }

                        ReplaceLastIfStuck(corners, to);
                        return;
                    }
                }
            }

            AddCorner(corners, to);
        }

        private static void ReplaceLastIfStuck(List<Vector3> corners, Vector3 target)
        {
            if (corners.Count > 0 && (corners[corners.Count - 1] - target).sqrMagnitude > 0.01f)
            {
                AddCorner(corners, target);
            }
        }

        private static void AddCorner(List<Vector3> corners, Vector3 point)
        {
            if (corners.Count > 0 && (corners[corners.Count - 1] - point).sqrMagnitude < 0.0004f)
            {
                return;
            }

            corners.Add(point);
        }

        public static Vector3 Snap(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, MaxSampleDistance, NavMesh.AllAreas)
                ? hit.position
                : position;
        }
    }
}
