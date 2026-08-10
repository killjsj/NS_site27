using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MapGeneration;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Next_generationSite_27.UnionP
{
    // 完整优化版 RoomGraph，可直接替换使用
    public class RoomGraph
    {
        public static RoomGraph Instance;
        public Dictionary<Room, RoomNode> Nodes { get; private set; } = new Dictionary<Room, RoomNode>();
        private volatile bool building = false;
        public bool Built => Nodes.Count > 0 && !building;

        //public static InternalNavigator InternalNav = new InternalNavigator();

        public RoomGraph()
        {
            Instance = this;
            BuildAsync();
        }

        // ---------------- 异步构建（批次 + 预分配 + 减少 GC） ----------------
        public void BuildAsync()
        {
            if (building) return;
            building = true;
            Timing.RunCoroutine(_BuildRoutine());
        }

        private IEnumerator<float> _BuildRoutine()
        {
            Log.Info("[RoomGraph] 开始构建房间图...");
            Nodes.Clear();

            var rooms = Room.List.Where(r => r != null).ToArray();
            Nodes = new Dictionary<Room, RoomNode>(rooms.Length);

            int batch = 0;
            // 建立节点
            foreach (var room in rooms)
            {
                Nodes[room] = new RoomNode(room);
                if (++batch % 16 == 0) yield return Timing.WaitForOneFrame;
            }

            // 门连边
            var connectedPairs = new HashSet<RoomPair>(rooms.Length * 2);
            batch = 0;
            foreach (var door in Door.List)
            {
                if (door == null || door.Rooms == null) continue;

                var arr = door.Rooms.ToArray();
                if (arr.Length < 2) continue; // 单向门
                var rA = arr[0];
                var rB = arr[1];
                if (rA == null || rB == null) continue;
                if (!Nodes.TryGetValue(rA, out var aNode) || !Nodes.TryGetValue(rB, out var bNode)) continue;

                var edgeAB = new RoomEdge(aNode, bNode, door);
                var edgeBA = new RoomEdge(bNode, aNode, door);
                aNode.Edges.Add(edgeAB);
                bNode.Edges.Add(edgeBA);
                connectedPairs.Add(new RoomPair(rA.Identifier, rB.Identifier));

                if (++batch % 32 == 0 || batch % 16 == 0) yield return Timing.WaitForOneFrame;
            }

            // 邻近房间连边（跳过已连接对）
            batch = 0;
            foreach (var room in rooms)
            {
                if (room == null || !Nodes.TryGetValue(room, out var nodeA)) continue;
                foreach (var neighbor in room.NearestRooms)
                {
                    if (neighbor == null || neighbor == room) continue;
                    if (!Nodes.TryGetValue(neighbor, out var nodeB)) continue;
                    var pair = new RoomPair(room.Identifier, neighbor.Identifier);
                    if (connectedPairs.Contains(pair)) continue;

                    var mid = (room.Position + neighbor.Position) * 0.5f;
                    var edgeA = new RoomEdge(nodeA, nodeB, null, RoomEdgeType.Transition, mid);
                    var edgeB = new RoomEdge(nodeB, nodeA, null, RoomEdgeType.Transition, mid);
                    nodeA.Edges.Add(edgeA);
                    nodeB.Edges.Add(edgeB);
                    connectedPairs.Add(pair);
                }
                if (++batch % 32 == 0 || batch % 16 == 0) yield return Timing.WaitForOneFrame;
            }

            building = false;
            Log.Info($"[RoomGraph] 构建完成: {Nodes.Count} 个房间, {Nodes.Sum(n => n.Value.Edges.Count)} 条边。");
            yield break;
        }

        // ---------------- 跨房间 A*（使用快速优先队列 + lazy update） ----------------
        public List<Room> GetRoomPath(Room start, Room end)
        {
            if (start == null || end == null || !Built) { 
                Log.Info($"start == null || end == null || !Built{start == null || end == null || !Built} Built:{!Built}");

                return null; 
            }
            if (start == end) return new List<Room> { start };

            var s = Nodes[start];
            var e = Nodes[end];

            var open = new FastPriorityQueue<RoomNode>();
            var came = new Dictionary<RoomNode, RoomEdge>(Nodes.Count);
            var g = new Dictionary<RoomNode, float>(Nodes.Count);
            var visited = new HashSet<RoomNode>();

            foreach (var n in Nodes.Values) g[n] = float.MaxValue;

            g[s] = 0f;
            open.Enqueue(s, Vector3.Distance(s.Position, e.Position));

            while (open.Count > 0)
            {
                var cur = open.Dequeue();
                if (visited.Contains(cur)) continue; // lazy-dequeued duplicate
                visited.Add(cur);

                if (cur == e) return ReconstructRoomPath(came, cur, s);

                foreach (var edge in cur.Edges)
                {
                    if (!IsEdgePassable(edge)) continue;
                    var nb = edge.To;
                    var cost = g[cur] + Vector3.Distance(cur.Position, edge.ConnectionPoint);
                    if (cost + 0.0001f < g[nb])
                    {
                        came[nb] = edge;
                        g[nb] = cost;
                        var f = cost + Vector3.Distance(nb.Position, e.Position);
                        open.Enqueue(nb, f); // 允许重复进入堆，使用 visited 过滤已处理
                    }
                }
            }
            Log.Info($"end");

            return null;
        }

        private List<Room> ReconstructRoomPath(Dictionary<RoomNode, RoomEdge> came, RoomNode end, RoomNode start)
        {
            var path = new Stack<Room>();
            var current = end;
            path.Push(current.Room);
            while (came.TryGetValue(current, out var edge))
            {
                current = edge.From;
                path.Push(current.Room);
                if (current == start) break;
            }
            return path.ToList();
        }

        private static bool IsEdgePassable(RoomEdge edge)
        {
            if (edge.Type == RoomEdgeType.Transition) return true;
            if (edge.Door == null) return false;
            return true;
        }
    }

    // ----------------- 基础类型 -----------------
    public class RoomNode
    {
        public Room Room { get; private set; }
        public Vector3 Position => Room.Position;
        public HashSet<RoomEdge> Edges { get; private set; }
        public RoomNode(Room r)
        {
            Room = r;
            Edges = new HashSet<RoomEdge>();
        }
    }

    public enum RoomEdgeType { Door, Transition }

    public class RoomEdge
    {
        public RoomNode From { get; private set; }
        public RoomNode To { get; private set; }
        public Door Door { get; private set; }
        public RoomEdgeType Type { get; private set; }
        public Vector3 ConnectionPoint { get; private set; }

        public RoomEdge(RoomNode from, RoomNode to, Door door, RoomEdgeType type = RoomEdgeType.Door, Vector3? point = null)
        {
            From = from;
            To = to;
            Door = door;
            Type = type;
            ConnectionPoint = point ?? (door != null ? door.Position : (from.Position + to.Position) * 0.5f);
        }
    }

    public struct RoomPair : IEquatable<RoomPair>
    {
        public RoomIdentifier A;
        public RoomIdentifier B;
        public RoomPair(RoomIdentifier a, RoomIdentifier b)
        {
            if (a.MainCoords.x < b.MainCoords.x || (a.MainCoords.x == b.MainCoords.x && a.MainCoords.z < b.MainCoords.z))
            {
                A = a; B = b;
            }
            else
            {
                A = b; B = a;
            }
        }
        public bool Equals(RoomPair o) { return A == o.A && B == o.B; }
        public override int GetHashCode() { return A.GetHashCode() * 31 ^ B.GetHashCode(); }
    }

    // ----------------- 更快的优先队列（允许重复项、lazy 去重） -----------------
    public class FastPriorityQueue<T> where T : class
    {
        private readonly List<(T item, float prio)> heap = new List<(T, float)>();
        public int Count => heap.Count;

        public void Enqueue(T item, float prio)
        {
            heap.Add((item, prio));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heap[p].prio <= prio) break;
                var tmp = heap[i]; heap[i] = heap[p]; heap[p] = tmp;
                i = p;
            }
        }

        public T Dequeue()
        {
            var top = heap[0].item;
            var last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return top;
            heap[0] = last;
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, s = i;
                if (l < heap.Count && heap[l].prio < heap[s].prio) s = l;
                if (r < heap.Count && heap[r].prio < heap[s].prio) s = r;
                if (s == i) break;
                var tmp = heap[i]; heap[i] = heap[s]; heap[s] = tmp;
                i = s;
            }
            return top;
        }
    }
}
