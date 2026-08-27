using Exiled.API.Features;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using MapGeneration;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using ElevatorDoor = Interactables.Interobjects.ElevatorDoor;

namespace Next_generationSite_27.UnionP
{
    public enum NearestRoomsMode
    {
        Never,

        IsolatedOnly,

        Always,
    }

    public class RoomGraph
    {
        private const int BatchSize = 24;

        private static int _linkCounter;

        public static NearestRoomsMode FallbackMode = NearestRoomsMode.IsolatedOnly;

        public static bool ValidateWithNavMesh;

        public static RoomGraph Instance { get; private set; }

        public Dictionary<RoomIdentifier, RoomNode> Nodes { get; private set; }
            = new Dictionary<RoomIdentifier, RoomNode>();

        public bool TryGetNode(Room room, out RoomNode node)
        {
            node = null;
            return room?.Identifier != null && Nodes.TryGetValue(room.Identifier, out node);
        }

        public bool TryGetNode(RoomIdentifier identifier, out RoomNode node)
        {
            node = null;
            return identifier != null && Nodes.TryGetValue(identifier, out node);
        }

        public static event Action OnBuilt;

        private volatile bool _building;
        private CoroutineHandle _buildHandle;

        public bool Built => Nodes.Count > 0 && !_building;
        public bool Building => _building;

        public RoomGraph()
        {
            Instance?.Cancel();

            Instance = this;
            BuildAsync();
        }

        public static RoomGraph Rebuild()
        {
            if (Instance == null)
            {
                return new RoomGraph();
            }

            Instance.Cancel();
            Instance.BuildAsync();
            return Instance;
        }

        public void Cancel()
        {
            if (_buildHandle.IsRunning)
            {
                _ = Timing.KillCoroutines(_buildHandle);
            }

            _building = false;
        }

        public void BuildAsync()
        {
            if (_building)
            {
                return;
            }

            _building = true;
            _buildHandle = Timing.RunCoroutine(_BuildRoutine());
        }


        private IEnumerator<float> _BuildRoutine()
        {
            Log.Info("[RoomGraph] building...");

            Room[] rooms = Room.List.Where(r => r != null && r.Identifier != null).ToArray();

            var nodes = new Dictionary<RoomIdentifier, RoomNode>(rooms.Length);

            int batch = 0;
            foreach (Room room in rooms)
            {
                if (nodes.ContainsKey(room.Identifier))
                {
                    Log.Warn($"[RoomGraph] duplicate RoomIdentifier for {room.Name} - skipping.");
                    continue;
                }

                nodes[room.Identifier] = new RoomNode(room);

                if (++batch % BatchSize == 0)
                {
                    yield return Timing.WaitForOneFrame;
                }
            }

            if (nodes.Count != rooms.Length)
            {
                Log.Error($"[RoomGraph] node count mismatch: {rooms.Length} source rooms produced " +
                          $"{nodes.Count} nodes. {rooms.Length - nodes.Count} room(s) were lost.");
            }
            else
            {
                Log.Debug($"[RoomGraph] {nodes.Count} nodes from {rooms.Length} source rooms " +
                          $"({RoomIdentifier.AllRoomIdentifiers.Count} identifiers registered).");
            }

            Nodes = nodes;

            Dictionary<RoomIdentifier, RoomNode> byIdentifier = nodes;

            var connected = new HashSet<RoomPair>();
            int doorEdges = 0, connectorEdges = 0, elevatorEdges = 0, transitionEdges = 0;

            batch = 0;
            foreach (DoorVariant door in DoorVariant.AllDoors
                                                    .Where(d => d != null)
                                                    .OrderBy(d => d.GetInstanceID()))
            {
                if (!door.RoomsAlreadyRegistered || door.Rooms == null)
                {
                    continue;
                }

                doorEdges += LinkAllPairs(
                    door.Rooms, byIdentifier, connected,
                    RoomEdgeType.Door, door, door.transform.position);

                if (++batch % BatchSize == 0)
                {
                    yield return Timing.WaitForOneFrame;
                }
            }

            batch = 0;
            foreach (RoomConnector connector in RoomConnector.AllConnectors
                                                             .Where(c => c != null)
                                                             .OrderBy(c => c.GetInstanceID()))
            {
                if (!connector.RoomsAlreadyRegistered || connector.Rooms == null)
                {
                    continue;
                }

                connectorEdges += LinkAllPairs(
                    connector.Rooms, byIdentifier, connected,
                    RoomEdgeType.Connector, null, connector.transform.position);

                if (++batch % BatchSize == 0)
                {
                    yield return Timing.WaitForOneFrame;
                }
            }

            var shafts = new Dictionary<ElevatorGroup, List<ElevatorDoor>>();
            foreach (DoorVariant door in DoorVariant.AllDoors
                                                    .Where(d => d != null)
                                                    .OrderBy(d => d.GetInstanceID()))
            {
                if (door is not ElevatorDoor elevatorDoor || !elevatorDoor.RoomsAlreadyRegistered)
                {
                    continue;
                }

                if (!shafts.TryGetValue(elevatorDoor.Group, out List<ElevatorDoor> list))
                {
                    shafts[elevatorDoor.Group] = list = new List<ElevatorDoor>();
                }

                list.Add(elevatorDoor);
            }

            foreach (KeyValuePair<ElevatorGroup, List<ElevatorDoor>> shaft in shafts.OrderBy(s => s.Key))
            {
                List<ElevatorDoor> doors = shaft.Value;

                for (int i = 0; i < doors.Count; i++)
                {
                    for (int j = i + 1; j < doors.Count; j++)
                    {
                        ElevatorDoor a = doors[i];
                        ElevatorDoor b = doors[j];
                        if (a.Rooms == null || b.Rooms == null)
                        {
                            continue;
                        }

                        foreach (RoomIdentifier ra in a.Rooms)
                        {
                            foreach (RoomIdentifier rb in b.Rooms)
                            {
                                if (TryAddEdge(ra, rb, byIdentifier, connected, RoomEdgeType.Elevator,
                                               a, a.transform.position, b.transform.position))
                                {
                                    elevatorEdges++;
                                }
                            }
                        }
                    }
                }

                yield return Timing.WaitForOneFrame;
            }

            int rescued = 0;
            int rejected = 0;

            if (FallbackMode != NearestRoomsMode.Never)
            {
                batch = 0;
                foreach (Room room in rooms)
                {
                    if (!nodes.TryGetValue(room.Identifier, out RoomNode nodeA))
                    {
                        continue;
                    }

                    if (FallbackMode == NearestRoomsMode.IsolatedOnly && nodeA.Edges.Count > 0)
                    {
                        continue;
                    }

                    IEnumerable<Room> neighbours = room.NearestRooms;
                    if (neighbours == null)
                    {
                        continue;
                    }

                    foreach (Room neighbour in neighbours)
                    {
                        if (neighbour == null || neighbour.Identifier == null ||
                            neighbour.Identifier == room.Identifier)
                        {
                            continue;
                        }

                        if (!nodes.TryGetValue(neighbour.Identifier, out RoomNode nodeB))
                        {
                            continue;
                        }

                        var pair = new RoomPair(room.Identifier, neighbour.Identifier);
                        if (!connected.Add(pair))
                        {
                            continue;
                        }

                        Vector3 mid = (room.Position + neighbour.Position) * 0.5f;

                        if (!TryResolveConnectionPoint(mid, room.Identifier, neighbour.Identifier,
                                                       out Vector3 point))
                        {
                            _ = connected.Remove(pair);
                            rejected++;
                            Log.Debug($"[RoomGraph] failed link {room.Name} <-> " +
                                      $"{neighbour.Name}: midpoint {mid} not walkable");
                            continue;
                        }

                        int linkId = ++_linkCounter;
                        nodeA.Edges.Add(new RoomEdge(nodeA, nodeB, null, RoomEdgeType.Transition, point, linkId));
                        nodeB.Edges.Add(new RoomEdge(nodeB, nodeA, null, RoomEdgeType.Transition, point, linkId));
                        transitionEdges++;

                        Log.Debug($"[RoomGraph] link: {room.Name} <-> {neighbour.Name} at {point}");
                    }

                    if (nodeA.Edges.Count > 0)
                    {
                        rescued++;
                    }

                    if (++batch % BatchSize == 0)
                    {
                        yield return Timing.WaitForOneFrame;
                    }
                }
            }

            _building = false;

            List<RoomNode> isolatedNodes = nodes.Values.Where(n => n.Edges.Count == 0).ToList();

            foreach (RoomNode n in isolatedNodes)
            {
                Log.Warn($"[RoomGraph] i room: {n.Room.Name} ({n.Room.Type}) at {n.Position}");
            }

            OnBuilt?.Invoke();
        }

        private static bool TryResolveConnectionPoint(
            Vector3 candidate, RoomIdentifier a, RoomIdentifier b, out Vector3 point)
        {
            point = candidate;

            if (!ValidateWithNavMesh)
            {
                return true;
            }

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                return false;
            }

            if (!hit.position.TryGetRoom(out RoomIdentifier owner))
            {
                return false;
            }

            if (owner != a && owner != b)
            {
                return false;
            }

            point = hit.position;
            return true;
        }

        private static int LinkAllPairs(
            RoomIdentifier[] connectorRooms,
            Dictionary<RoomIdentifier, RoomNode> byIdentifier,
            HashSet<RoomPair> connected,
            RoomEdgeType type,
            DoorVariant door,
            Vector3 point)
        {
            int added = 0;

            for (int i = 0; i < connectorRooms.Length; i++)
            {
                for (int j = i + 1; j < connectorRooms.Length; j++)
                {
                    if (TryAddEdge(connectorRooms[i], connectorRooms[j], byIdentifier, connected,
                                   type, door, point, point))
                    {
                        added++;
                    }
                }
            }

            return added;
        }

        private static bool TryAddEdge(
            RoomIdentifier a,
            RoomIdentifier b,
            Dictionary<RoomIdentifier, RoomNode> byIdentifier,
            HashSet<RoomPair> connected,
            RoomEdgeType type,
            DoorVariant door,
            Vector3 pointFromA,
            Vector3 pointFromB)
        {
            if (a == null || b == null || a == b)
            {
                return false;
            }

            if (!byIdentifier.TryGetValue(a, out RoomNode nodeA) ||
                !byIdentifier.TryGetValue(b, out RoomNode nodeB))
            {
                return false;
            }

            _ = connected.Add(new RoomPair(a, b));

            int linkId = ++_linkCounter;
            nodeA.Edges.Add(new RoomEdge(nodeA, nodeB, door, type, pointFromA, linkId));
            nodeB.Edges.Add(new RoomEdge(nodeB, nodeA, door, type, pointFromB, linkId));
            return true;
        }


        public List<Room> GetRoomPath(Room start, Room end, Func<RoomEdge, bool> passable = null)
        {
            List<RoomEdge> edges = GetRoomEdgePath(start, end, passable);
            if (edges == null)
            {
                return null;
            }

            var rooms = new List<Room> { start };
            foreach (RoomEdge edge in edges)
            {
                rooms.Add(edge.To.Room);
            }

            return rooms;
        }

        public List<RoomEdge> GetRoomEdgePath(Room start, Room end, Func<RoomEdge, bool> passable = null)
        {
            if (start == null || end == null)
            {
                Log.Debug("[RoomGraph] GetRoomEdgePath: start or end is null.");
                return null;
            }

            if (!Built)
            {
                Log.Warn($"[RoomGraph] GetRoomEdgePath called before the graph finished building " +
                         $"(nodes={Nodes.Count}, building={_building}).");
                return null;
            }

            if (start == end)
            {
                return new List<RoomEdge>();
            }

            if (!TryGetNode(start, out RoomNode s))
            {
                Log.Warn($"[RoomGraph] start room '{start.Name}' is not in the graph.");
                return null;
            }

            if (!TryGetNode(end, out RoomNode e))
            {
                Log.Warn($"[RoomGraph] target room '{end.Name}' is not in the graph.");
                return null;
            }

            if (s == e)
            {
                return new List<RoomEdge>();
            }

            passable ??= IsEdgePassable;

            var open = new FastPriorityQueue<RoomNode>();
            var came = new Dictionary<RoomNode, RoomEdge>(Nodes.Count);
            var g = new Dictionary<RoomNode, float>(Nodes.Count) { [s] = 0f };
            var visited = new HashSet<RoomNode>();

            open.Enqueue(s, Vector3.Distance(s.Position, e.Position));

            while (open.Count > 0)
            {
                RoomNode cur = open.Dequeue();
                if (cur == null || !visited.Add(cur))
                {
                    continue;
                }

                if (cur == e)
                {
                    return ReconstructEdgePath(came, cur, s);
                }

                float gCur = g.TryGetValue(cur, out float gv) ? gv : float.MaxValue;

                foreach (RoomEdge edge in cur.Edges)
                {
                    if (!passable(edge))
                    {
                        continue;
                    }

                    RoomNode nb = edge.To;
                    if (visited.Contains(nb))
                    {
                        continue;
                    }

                    float cost = gCur
                               + Vector3.Distance(cur.Position, edge.ConnectionPoint)
                               + Vector3.Distance(edge.ConnectionPoint, nb.Position);

                    float gNb = g.TryGetValue(nb, out float gnv) ? gnv : float.MaxValue;
                    if (cost + 0.0001f >= gNb)
                    {
                        continue;
                    }

                    came[nb] = edge;
                    g[nb] = cost;
                    open.Enqueue(nb, cost + Vector3.Distance(nb.Position, e.Position));
                }
            }

            Log.Debug($"[RoomGraph] no route from '{start.Name}' to '{end.Name}'.");
            return null;
        }

        private static List<RoomEdge> ReconstructEdgePath(
            Dictionary<RoomNode, RoomEdge> came, RoomNode end, RoomNode start)
        {
            var path = new Stack<RoomEdge>();
            RoomNode current = end;

            var guard = new HashSet<RoomNode> { current };

            while (came.TryGetValue(current, out RoomEdge edge))
            {
                path.Push(edge);
                current = edge.From;

                if (!guard.Add(current))
                {
                    break;
                }

                if (current == start)
                {
                    break;
                }
            }

            return path.ToList();
        }

        public static bool IsEdgePassable(RoomEdge edge)
        {
            return edge != null && edge.To != null;
        }
    }

    public class RoomNode
    {
        public Room Room { get; }
        public Vector3 Position => Room.Position;
        public List<RoomEdge> Edges { get; }

        public RoomNode(Room room)
        {
            Room = room;
            Edges = new List<RoomEdge>(4);
        }

        public override string ToString()
        {
            return Room != null ? $"{Room.Name} ({Room.Type})" : "<null>";
        }
    }

    public enum RoomEdgeType
    {
        Door,
        Connector,
        Elevator,
        Transition,
    }

    public class RoomEdge
    {
        public RoomNode From { get; }
        public RoomNode To { get; }

        public DoorVariant DoorBase { get; }

        public Exiled.API.Features.Doors.Door Door =>
            DoorBase == null ? null : Exiled.API.Features.Doors.Door.Get(DoorBase);

        public RoomEdgeType Type { get; }

        public Vector3 ConnectionPoint { get; }

        public int LinkId { get; }

        public RoomEdge(RoomNode from, RoomNode to, DoorVariant door,
                        RoomEdgeType type = RoomEdgeType.Door, Vector3? point = null, int linkId = 0)
        {
            From = from;
            To = to;
            DoorBase = door;
            Type = type;
            LinkId = linkId;
            ConnectionPoint = point
                              ?? (door != null ? door.transform.position
                                               : (from.Position + to.Position) * 0.5f);
        }

        public override string ToString()
        {
            return $"{From} -[{Type}#{LinkId}]-> {To}";
        }
    }

    public readonly struct RoomPair : IEquatable<RoomPair>
    {
        public readonly RoomIdentifier A;
        public readonly RoomIdentifier B;

        public RoomPair(RoomIdentifier a, RoomIdentifier b)
        {
            if (a == null || b == null)
            {
                A = a;
                B = b;
                return;
            }

            if (a.GetInstanceID() <= b.GetInstanceID())
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(RoomPair other)
        {
            return A == other.A && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is RoomPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashA = A == null ? 0 : A.GetInstanceID();
                int hashB = B == null ? 0 : B.GetInstanceID();
                return (hashA * 397) ^ hashB;
            }
        }
    }

    public class FastPriorityQueue<T> where T : class
    {
        private readonly List<(T item, float prio)> _heap = new();

        public int Count => _heap.Count;

        public void Clear()
        {
            _heap.Clear();
        }

        public void Enqueue(T item, float prio)
        {
            _heap.Add((item, prio));

            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_heap[p].prio <= prio)
                {
                    break;
                }

                (_heap[i], _heap[p]) = (_heap[p], _heap[i]);
                i = p;
            }
        }

        public T Dequeue()
        {
            if (_heap.Count == 0)
            {
                return null;
            }

            T top = _heap[0].item;
            var last = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);

            if (_heap.Count == 0)
            {
                return top;
            }

            _heap[0] = last;

            int i = 0;
            while (true)
            {
                int l = (2 * i) + 1;
                int r = l + 1;
                int s = i;

                if (l < _heap.Count && _heap[l].prio < _heap[s].prio)
                {
                    s = l;
                }

                if (r < _heap.Count && _heap[r].prio < _heap[s].prio)
                {
                    s = r;
                }

                if (s == i)
                {
                    break;
                }

                (_heap[i], _heap[s]) = (_heap[s], _heap[i]);
                i = s;
            }

            return top;
        }
    }
}
