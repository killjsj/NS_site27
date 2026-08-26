using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using MapGeneration;
using UnityEngine;

namespace FacilityNavigation
{
    public static class RoomGraph
    {
        public const float DefaultElevatorCost = 4f;
        public const float DefaultLockedDoorPenalty = 8f;

        private static readonly Dictionary<RoomIdentifier, RoomNode> NodesById =
            new Dictionary<RoomIdentifier, RoomNode>(250);

        private static readonly Dictionary<RoomPair, List<RoomEdge>> EdgesByPair =
            new Dictionary<RoomPair, List<RoomEdge>>();

        private static readonly Dictionary<RoomPair, int> LinkIdCounter =
            new Dictionary<RoomPair, int>();

        private static readonly Dictionary<RoomEdge, Vector3[]> EdgePassPoints =
            new Dictionary<RoomEdge, Vector3[]>();

        private static readonly List<RoomNode> NodeList = new List<RoomNode>();

        public static IReadOnlyList<RoomNode> Nodes => NodeList;

        public static bool IsBuilt { get; private set; }

        public static int EdgeCount { get; private set; }

        public static void Build()
        {
            Clear();

            foreach (RoomIdentifier id in RoomIdentifier.AllRoomIdentifiers)
                CreateNode(id);

            AddDoorEdges();
            AddConnectorEdges();
            AddElevatorEdges();
            AddFallbackEdges();

            IsBuilt = true;
        }

        public static void Clear()
        {
            NodesById.Clear();
            EdgesByPair.Clear();
            LinkIdCounter.Clear();
            EdgePassPoints.Clear();
            NodeList.Clear();
            EdgeCount = 0;
            IsBuilt = false;
        }

        public static void Invalidate() => IsBuilt = false;

        public static void EnsureBuilt()
        {
            if (IsBuilt && !IsStale())
                return;

            if (!SeedSynchronizer.MapGenerated)
            {
                if (IsBuilt)
                    Clear();
                return;
            }

            Build();
        }

        public static RoomNode GetNode(RoomIdentifier room)
        {
            EnsureBuilt();
            return room != null && NodesById.TryGetValue(room, out RoomNode node) ? node : null;
        }

        public static bool TryGetNode(RoomIdentifier room, out RoomNode node)
        {
            node = GetNode(room);
            return node != null;
        }

        public static RoomNode GetNode(Vector3 worldPos)
        {
            EnsureBuilt();

            if (worldPos.TryGetRoom(out RoomIdentifier id) && NodesById.TryGetValue(id, out RoomNode node))
                return node;

            return NearestNode(worldPos, 25f);
        }

        private static RoomNode NearestNode(Vector3 worldPos, float maxDistance)
        {
            RoomNode best = null;
            float bestSqr = maxDistance * maxDistance;

            foreach (RoomNode node in NodeList)
            {
                if (node?.Room == null)
                    continue;

                Vector3 delta = node.Position - worldPos;
                delta.y *= 0.5f;

                float sqr = delta.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = node;
                }
            }

            return best;
        }

        public static List<RoomNode> GetIsolatedNodes()
        {
            EnsureBuilt();

            List<RoomNode> isolated = new List<RoomNode>();
            foreach (RoomNode node in NodeList)
            {
                if (node.Edges.Count == 0)
                    isolated.Add(node);
            }

            return isolated;
        }

        public static void LogDiagnostics()
        {
            EnsureBuilt();

            int doors = 0, connectors = 0, elevators = 0, transitions = 0;

            foreach (List<RoomEdge> list in EdgesByPair.Values)
            {
                foreach (RoomEdge edge in list)
                {
                    switch (edge.Type)
                    {
                        case RoomEdgeType.Door: doors++; break;
                        case RoomEdgeType.Connector: connectors++; break;
                        case RoomEdgeType.Elevator: elevators++; break;
                        default: transitions++; break;
                    }
                }
            }

            Log.Info($"[RoomGraph] rooms:{NodeList.Count} edges:{EdgeCount} " +
                     $"(door:{doors} connector:{connectors} elevator:{elevators} transition:{transitions})");

            List<RoomNode> isolated = GetIsolatedNodes();
            if (isolated.Count > 0)
                Log.Warn($"[RoomGraph] isolated rooms ({isolated.Count}): {FormatPath(isolated)}");
        }

        public static List<RoomNode> FindPath(RoomNode start, RoomNode goal)
        {
            return SearchFewest(start, goal, null);
        }

        public static List<RoomNode> FindPath(RoomIdentifier from, RoomIdentifier to)
        {
            EnsureBuilt();
            return SearchFewest(GetNode(from), GetNode(to), null);
        }

        public static List<RoomNode> FindPath(Vector3 from, Vector3 to)
        {
            EnsureBuilt();
            return SearchFewest(GetNode(from), GetNode(to), null);
        }

        public static List<RoomNode> FindPath(RoomIdentifier from, RoomIdentifier to, Func<RoomEdge, bool> edgeFilter)
        {
            EnsureBuilt();
            return SearchFewest(GetNode(from), GetNode(to), edgeFilter);
        }

        public static bool TryFindPath(RoomIdentifier from, RoomIdentifier to, out List<RoomNode> path)
        {
            path = FindPath(from, to);
            return path != null;
        }

        public static List<RoomNode> FindCheapestPath(RoomIdentifier from, RoomIdentifier to)
        {
            return FindCheapestPath(from, to, null);
        }

        public static List<RoomNode> FindCheapestPath(Vector3 from, Vector3 to)
        {
            EnsureBuilt();
            return SearchCheapest(GetNode(from), GetNode(to), null);
        }

        public static List<RoomNode> FindCheapestPath(RoomIdentifier from, RoomIdentifier to, Func<RoomEdge, float> costOverride)
        {
            EnsureBuilt();
            return SearchCheapest(GetNode(from), GetNode(to), costOverride);
        }

        public static float DefaultEdgeCost(RoomEdge edge)
        {
            float cost = 1f;

            if (edge.Type == RoomEdgeType.Elevator)
                cost += DefaultElevatorCost;

            if (edge.DoorBase != null && edge.DoorBase.ActiveLocks != 0)
                cost += DefaultLockedDoorPenalty;

            return cost;
        }

        public static string FormatPath(IReadOnlyList<RoomNode> path)
        {
            if (path == null || path.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    sb.Append(" -> ");
                sb.Append(path[i].ToString());
            }

            return sb.ToString();
        }

        public static string FormatPathDetailed(IReadOnlyList<RoomNode> path)
        {
            if (path == null || path.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            sb.Append("[0] ").AppendLine(path[0].ToString());

            for (int i = 1; i < path.Count; i++)
            {
                RoomEdge edge = FindPrimaryEdgeBetween(path[i - 1], path[i]);
                sb.Append("    ");
                sb.Append(edge == null ? "-->" : $"-[{edge.Type}#{edge.LinkId}]->");
                sb.Append(' ');
                sb.Append('[').Append(i).Append("] ");
                sb.AppendLine(path[i].ToString());
            }

            return sb.ToString().TrimEnd();
        }

        public static RoomEdge FindPrimaryEdgeBetween(RoomNode a, RoomNode b)
        {
            if (a == null || b == null)
                return null;

            return GetBestEdgeBetween(a, b);
        }

        public static bool TryBuildWaypoints(Vector3 from, Vector3 to,
            out List<RoomNode> rooms, out List<Vector3> waypoints)
        {
            EnsureBuilt();

            rooms = null;
            waypoints = null;

            List<RoomNode> path = SearchFewest(GetNode(from), GetNode(to), null);
            if (path == null)
                return false;

            rooms = path;
            waypoints = new List<Vector3> { from };

            for (int i = 0; i < path.Count - 1; i++)
            {
                RoomEdge edge = GetBestEdgeBetween(path[i], path[i + 1]);

                if (edge == null)
                {
                    waypoints.Add(FindBoundaryPoint(
                        path[i].Room != null ? path[i].Room.Identifier : null,
                        path[i + 1].Room != null ? path[i + 1].Room.Identifier : null));
                    continue;
                }

                foreach (Vector3 point in GetPassPoints(edge, path[i]))
                    waypoints.Add(point);
            }

            waypoints.Add(to);
            return true;
        }

        public static Vector3[] GetPassPoints(RoomEdge edge, RoomNode travellingFrom)
        {
            if (edge == null)
                return Array.Empty<Vector3>();

            Vector3[] points = EdgePassPoints.TryGetValue(edge, out Vector3[] stored)
                ? stored
                : new[] { edge.ConnectionPoint };

            bool forward = ReferenceEquals(edge.From, travellingFrom) || !ReferenceEquals(edge.To, travellingFrom);

            if (forward || points.Length < 2)
                return points;

            Vector3[] reversed = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                reversed[i] = points[points.Length - 1 - i];

            return reversed;
        }

        private static RoomEdge GetBestEdgeBetween(RoomNode a, RoomNode b)
        {
            RoomPair pair = new RoomPair(a.Room?.Identifier, b.Room?.Identifier);

            if (!EdgesByPair.TryGetValue(pair, out List<RoomEdge> list) || list.Count == 0)
                return null;

            RoomEdge best = list[0];

            for (int i = 1; i < list.Count; i++)
            {
                if (RankOf(list[i].Type) < RankOf(best.Type))
                    best = list[i];
            }

            return best;
        }

        private static int RankOf(RoomEdgeType type)
        {
            switch (type)
            {
                case RoomEdgeType.Door: return 0;
                case RoomEdgeType.Connector: return 1;
                case RoomEdgeType.Elevator: return 2;
                default: return 3;
            }
        }

        private static void CreateNode(RoomIdentifier id)
        {
            if (id == null || NodesById.ContainsKey(id))
                return;

            Room exRoom = Room.Get(id);
            if (exRoom == null)
                return;

            RoomNode node = new RoomNode(exRoom);
            NodesById.Add(id, node);
            NodeList.Add(node);
        }

        private static void AddDoorEdges()
        {
            foreach (DoorVariant door in DoorVariant.AllDoors)
            {
                if (door == null || door is ElevatorDoor || !door.RoomsAlreadyRegistered)
                    continue;

                RoomEdgeType type = door is CheckpointDoor ? RoomEdgeType.Transition : RoomEdgeType.Door;

                if (!RegisterRoomsPair(door.Rooms, door, type, door.transform.position))
                    RegisterProbedPair(door, type);
            }
        }

        private static void AddConnectorEdges()
        {
            foreach (RoomConnector connector in RoomConnector.AllConnectors)
            {
                if (connector == null)
                    continue;

                RegisterRoomsPair(connector.Rooms, null, RoomEdgeType.Connector, connector.transform.position);
            }
        }

        private static void AddElevatorEdges()
        {
            foreach (ElevatorGroup group in Enum.GetValues(typeof(ElevatorGroup)))
            {
                List<ElevatorDoor> doors = ElevatorDoor.GetDoorsForGroup(group);
                if (doors == null || doors.Count < 2)
                    continue;

                RoomIdentifier[] floorRooms = new RoomIdentifier[doors.Count];
                for (int i = 0; i < doors.Count; i++)
                    floorRooms[i] = ResolveElevatorFloorRoom(doors[i]);

                for (int i = 0; i < doors.Count - 1; i++)
                {
                    ElevatorDoor lower = doors[i];
                    ElevatorDoor upper = doors[i + 1];

                    if (lower == null || upper == null)
                        continue;

                    RegisterEdge(floorRooms[i], floorRooms[i + 1], lower,
                        RoomEdgeType.Elevator,
                        (lower.TargetPosition + upper.TargetPosition) * 0.5f,
                        i,
                        new[] { lower.TargetPosition, upper.TargetPosition });
                }
            }
        }

        private static RoomIdentifier ResolveElevatorFloorRoom(ElevatorDoor door)
        {
            if (door == null)
                return null;

            if (door.TargetPosition.TryGetRoom(out RoomIdentifier id))
                return id;

            if (door.transform.position.TryGetRoom(out id))
                return id;

            if (door.Chamber != null && door.Chamber.CurrentRoom != null)
                return door.Chamber.CurrentRoom;

            Vector3 origin = door.transform.position;
            RoomIdentifier nearest = null;
            float bestSqr = 14f * 14f;

            foreach (RoomIdentifier candidate in RoomIdentifier.AllRoomIdentifiers)
            {
                if (candidate == null)
                    continue;

                Vector3 delta = candidate.transform.position - origin;
                delta.y = 0f;

                float sqr = delta.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static void AddFallbackEdges()
        {
            foreach (RoomIdentifier room in RoomIdentifier.AllRoomIdentifiers)
            {
                if (room == null)
                    continue;

                foreach (RoomIdentifier other in room.ConnectedRooms)
                {
                    if (other == null)
                        continue;

                    RoomPair pair = new RoomPair(room, other);
                    if (EdgesByPair.ContainsKey(pair))
                        continue;

                    RegisterEdge(room, other, null, RoomEdgeType.Transition, FindBoundaryPoint(room, other), 0);
                }
            }
        }

        public static Vector3 FindBoundaryPoint(RoomIdentifier a, RoomIdentifier b)
        {
            if (a == null || b == null)
                return Vector3.zero;

            Vector3 from = a.transform.position;
            Vector3 to = b.transform.position;

            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 0.5f)
                return from;

            Vector3 dir = delta / dist;

            Vector3 lastInside = from;
            for (float d = 1f; d <= dist; d += 1f)
            {
                Vector3 sample = from + dir * d;

                if (!sample.TryGetRoom(out RoomIdentifier id))
                    continue;

                if (id != a)
                {
                    Vector3 boundary = (lastInside + sample) * 0.5f;
                    return new Vector3(boundary.x, Mathf.Max(lastInside.y, sample.y), boundary.z);
                }

                lastInside = sample;
            }

            return lastInside + dir * 2f;
        }

        private static bool RegisterRoomsPair(RoomIdentifier[] rooms, DoorVariant door, RoomEdgeType type, Vector3 point)
        {
            if (rooms == null || rooms.Length < 2)
                return false;

            if (!TryGetTwoDistinct(rooms, out RoomIdentifier first, out RoomIdentifier second))
                return false;

            return FinalizePair(first, second, door, type, point);
        }

        private static void RegisterProbedPair(DoorVariant door, RoomEdgeType type)
        {
            Vector3 origin = door.transform.position;
            Vector3 forward = door.transform.forward;

            List<RoomIdentifier> found = new List<RoomIdentifier>(4);

            CollectProbe(found, origin, forward);
            CollectProbe(found, origin, -forward);
            CollectProbe(found, origin, Vector3.left);
            CollectProbe(found, origin, Vector3.right);

            for (int i = 0; i < found.Count; i++)
            {
                for (int j = i + 1; j < found.Count; j++)
                {
                    if (found[i] != found[j])
                    {
                        FinalizePair(found[i], found[j], door, type, origin);
                        return;
                    }
                }
            }
        }

        private static void CollectProbe(List<RoomIdentifier> results, Vector3 origin, Vector3 direction)
        {
            float[] distances = { 2f, 4f, 6f, 9f };

            foreach (float distance in distances)
            {
                Vector3 sample = origin + direction * distance;

                if (sample.TryGetRoom(out RoomIdentifier id) && !results.Contains(id))
                {
                    results.Add(id);
                    return;
                }

                Vector3 lowered = sample - Vector3.up * 2f;
                if (lowered.TryGetRoom(out id) && !results.Contains(id))
                {
                    results.Add(id);
                    return;
                }
            }
        }

        private static bool TryGetTwoDistinct(RoomIdentifier[] rooms, out RoomIdentifier first, out RoomIdentifier second)
        {
            first = null;
            second = null;

            foreach (RoomIdentifier candidate in rooms)
            {
                if (candidate == null)
                    continue;

                if (first == null)
                {
                    first = candidate;
                    continue;
                }

                if (candidate != first)
                {
                    second = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool FinalizePair(RoomIdentifier a, RoomIdentifier b, DoorVariant door,
            RoomEdgeType type, Vector3 point)
        {
            if (a == null || b == null || a == b)
                return false;

            if (type == RoomEdgeType.Door && a.Zone != b.Zone)
                type = RoomEdgeType.Transition;

            RegisterEdge(a, b, door, type, point, 0);
            return true;
        }

        private static void RegisterEdge(RoomIdentifier a, RoomIdentifier b, DoorVariant door,
            RoomEdgeType type, Vector3? point, int linkIdOverride, Vector3[] passPoints = null)
        {
            RoomNode na = GetNodeRaw(a);
            RoomNode nb = GetNodeRaw(b);

            if (na == null || nb == null || ReferenceEquals(na, nb))
                return;

            RoomPair pair = new RoomPair(a, b);

            int linkId;
            if (!LinkIdCounter.TryGetValue(pair, out int next))
            {
                next = 0;
                linkId = linkIdOverride;
            }
            else
            {
                linkId = linkIdOverride > 0 ? linkIdOverride : next;
            }

            LinkIdCounter[pair] = Mathf.Max(next, linkId) + 1;

            RoomEdge edge = new RoomEdge(na, nb, door, type, point, linkId);

            na.Edges.Add(edge);
            nb.Edges.Add(edge);

            if (!EdgesByPair.TryGetValue(pair, out List<RoomEdge> list))
            {
                list = new List<RoomEdge>(2);
                EdgesByPair.Add(pair, list);
            }

            list.Add(edge);

            EdgePassPoints[edge] = passPoints ?? new[] { edge.ConnectionPoint };

            EdgeCount++;
        }

        private static RoomNode GetNodeRaw(RoomIdentifier id)
        {
            return id != null && NodesById.TryGetValue(id, out RoomNode node) ? node : null;
        }

        private static RoomNode Other(RoomEdge edge, RoomNode current)
        {
            if (ReferenceEquals(edge.From, current))
                return edge.To;

            if (ReferenceEquals(edge.To, current))
                return edge.From;

            return null;
        }

        private static List<RoomNode> SearchFewest(RoomNode start, RoomNode goal, Func<RoomEdge, bool> edgeFilter)
        {
            if (start == null || goal == null)
                return null;

            if (ReferenceEquals(start, goal))
                return new List<RoomNode> { start };

            Dictionary<RoomNode, RoomNode> prev = new Dictionary<RoomNode, RoomNode>
            {
                { start, null }
            };

            Queue<RoomNode> queue = new Queue<RoomNode>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                RoomNode current = queue.Dequeue();

                if (ReferenceEquals(current, goal))
                    break;

                foreach (RoomEdge edge in current.Edges)
                {
                    if (edgeFilter != null && !edgeFilter(edge))
                        continue;

                    RoomNode next = Other(edge, current);
                    if (next == null || prev.ContainsKey(next))
                        continue;

                    prev[next] = current;
                    queue.Enqueue(next);
                }
            }

            return Reconstruct(prev, goal);
        }

        private static List<RoomNode> SearchCheapest(RoomNode start, RoomNode goal, Func<RoomEdge, float> costOverride)
        {
            if (start == null || goal == null)
                return null;

            Func<RoomEdge, float> cost = costOverride ?? DefaultEdgeCost;

            if (ReferenceEquals(start, goal))
                return new List<RoomNode> { start };

            Dictionary<RoomNode, float> dist = new Dictionary<RoomNode, float>
            {
                { start, 0f }
            };

            Dictionary<RoomNode, RoomNode> prev = new Dictionary<RoomNode, RoomNode>();
            HashSet<RoomNode> closed = new HashSet<RoomNode>();
            List<RoomNode> open = new List<RoomNode> { start };

            while (open.Count > 0)
            {
                int bestIndex = 0;
                float bestDist = dist[open[0]];

                for (int i = 1; i < open.Count; i++)
                {
                    float d = dist[open[i]];
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestIndex = i;
                    }
                }

                RoomNode current = open[bestIndex];
                open.RemoveAt(bestIndex);

                if (!closed.Add(current))
                    continue;

                if (ReferenceEquals(current, goal))
                    break;

                foreach (RoomEdge edge in current.Edges)
                {
                    RoomNode next = Other(edge, current);
                    if (next == null || closed.Contains(next))
                        continue;

                    float newDist = dist[current] + cost(edge);

                    if (!dist.TryGetValue(next, out float oldDist) || newDist < oldDist)
                    {
                        dist[next] = newDist;
                        prev[next] = current;
                        open.Add(next);
                    }
                }
            }

            return closed.Contains(goal) ? Reconstruct(prev, goal) : null;
        }

        private static List<RoomNode> Reconstruct(Dictionary<RoomNode, RoomNode> prev, RoomNode goal)
        {
            if (!prev.ContainsKey(goal))
                return null;

            List<RoomNode> path = new List<RoomNode>();
            RoomNode current = goal;

            while (current != null)
            {
                path.Add(current);
                prev.TryGetValue(current, out RoomNode parent);
                current = parent;
            }

            path.Reverse();
            return path;
        }

        private static bool IsStale()
        {
            if (NodeList.Count == 0 || NodeList.Count != RoomIdentifier.AllRoomIdentifiers.Count)
                return true;

            foreach (RoomIdentifier key in NodesById.Keys)
            {
                if (key == null)
                    return true;
            }

            return false;
        }
    }
}
