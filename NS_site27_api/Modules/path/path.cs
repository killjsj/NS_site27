using CommandSystem;
using DrawableLine;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.Handlers;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using NetworkManagerUtils.Dummies;
using Next_generationSite_27.UnionP;
using NS_site27_api.Core;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Utils.Networking;
using Player = Exiled.API.Features.Player;
using Room = Exiled.API.Features.Room;

namespace NS_site27_api.Modules.path
{
    public class path : ModuleBase<pathconfig>
    {
        public override string ModuleName => "path";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= w;

        }

        public override void OnEnable()
        {
            LoadReadableMeshes(Path.Combine(ModuleConfigManager.ConfigDir, "readable_meshes.bundle"));

            Exiled.Events.Handlers.Server.WaitingForPlayers += w;
        }
        private NavMeshSurface surface;
        public static Dictionary<string, Mesh> readableMeshes = new();

        private void LoadReadableMeshes(string bundlePath)
        {
            Log.Info($"Load form {bundlePath}");
            if (!File.Exists(bundlePath))
            {
                Log.Error("[PATH] readable_meshes.bundle not found! Using empty list.");
                return;
            }

            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Log.Error("[PATH] Failed to load AssetBundle.");
                return;
            }

            var allMeshes = bundle.LoadAllAssets<Mesh>();
            int s = 0;
            int a = 0;
            foreach (var mesh in allMeshes)
            {
                a++;
                if(mesh.name != null)
                {
                    string key = mesh.name.Replace("(Instance)", "").Trim();
                    readableMeshes[key] = mesh;
                    s++;
                }

            }
            bundle.Unload(false);
            Log.Info($"[PATH] Loaded {readableMeshes.Count} readable meshes from AssetBundle. a:{a},s{s}");
        }

        public void w()
        {
            new RoomGraph();

            LayerMask layer = 0;
            List<int> layers = new();

            foreach (RoomIdentifier room in RoomIdentifier.AllRoomIdentifiers)
                if (room.gameObject.activeSelf)
                {
                    Collider[] colliders = room.gameObject.GetComponentsInChildren<Collider>();
                    foreach (Collider col in colliders)
                    {
                        if (!layers.Contains(col.gameObject.layer))
                        {
                            layers.Add(col.gameObject.layer);
                            layer |= col.gameObject.layer;
                        }
                    }
                }
            foreach (var r in Room.List)
            {
                BuildNavMeshForRoom(r, layer);
            }
        }
        private static void BuildNavMeshForRoom(Room room,LayerMask layer)
        {
            var surface = room.GameObject.GetComponent<NavMeshSurface>();
            if (surface == null) surface = room.GameObject.AddComponent<NavMeshSurface>();

            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes; // 必须用 RenderMeshes 来读取顶点
            surface.layerMask = layer;
            surface.collectObjects = CollectObjects.All;

            // 替换所有 MeshFilter 为可读版本
            var filters = room.GameObject.GetComponentsInChildren<MeshFilter>(true);
            var originalMeshes = new Dictionary<MeshFilter, Mesh>();
            int a = 0;
            int i = 0;
            //foreach (var filter in filters)
            //{
            //    a++;
            //    if (filter.sharedMesh == null) continue;
            //    if (readableMeshes.TryGetValue(filter.sharedMesh.name, out var readableMesh))
            //    {
            //        originalMeshes[filter] = filter.sharedMesh;
            //        filter.sharedMesh = readableMesh;
            //        i++;
            //    }
            //}
            ////Log.Info($"Readable deploy:a:{a} i:{i}");
            //try
            //{
            //    //surface.BuildNavMesh();
            //}
            //finally
            //{
            //    // 恢复原始 Mesh
            //    foreach (var kvp in originalMeshes)
            //    {
            //        kvp.Key.sharedMesh = kvp.Value;
            //    }
            //}
        }
        public static List<Vector3> GetPath(Room room, Vector3 start, Vector3 end)
        {
            NavMeshSurface surface = room.GameObject.GetComponent<NavMeshSurface>();
            if (surface == null || surface.navMeshData == null)
            {
                Log.Info("rebuild");
                BuildNavMeshForRoom(room, -1);
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.SamplePosition(start, out NavMeshHit hitStart, 2f, NavMesh.AllAreas))
            {
                Log.Warn($"[PATH] Start pos not on NavMesh! Start: {start}");
                return new List<Vector3>();
            }
            if (!NavMesh.SamplePosition(end, out NavMeshHit hitEnd, 2f, NavMesh.AllAreas))
            {
                Log.Warn($"[PATH] End pos not on NavMesh! End: {end}");
                return new List<Vector3>();
            }
            if (NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path))
            {
                Log.Info(path.status);
                return new List<Vector3>(path.corners);
            }
                Log.Info(path.status);
            return new List<Vector3>();
        }
        public static List<Vector3> GetDetailedPath(Room startRoom, Room targetRoom, Vector3 startPos)
        {
            var roomPath = RoomGraph.Instance.GetRoomPath(startRoom, targetRoom);
            if (roomPath == null || roomPath.Count == 0)
            {
                Log.Info("roomPath == null || roomPath.Count == 0");
                return null;
            }

            List<Vector3> fullPath = new List<Vector3>();
            Vector3 currentEntry = startPos;

            for (int i = 0; i < roomPath.Count; i++)
            {
                Room curRoom = roomPath[i];
                Room nextRoom = (i < roomPath.Count - 1) ? roomPath[i + 1] : null;
                Vector3 exitPoint;

                if (nextRoom != null)
                {
                    // 找到当前房间通往下一房间的边，获取门/连接点
                    RoomNode curNode = RoomGraph.Instance.Nodes[curRoom];
                    RoomEdge edge = curNode.Edges.FirstOrDefault(e => e.To.Room == nextRoom);
                    exitPoint = edge != null ? edge.ConnectionPoint : (curRoom.Position + nextRoom.Position) * 0.5f;
                }
                else
                {
                    // 最后一个房间，终点取目标房间中心（也可改为离该房间最近的入口点）
                    exitPoint = targetRoom.Position;
                }
                Log.Info("CallingGetPath");
                var roomPathPoints = GetPath(curRoom, currentEntry, exitPoint);
                Log.Info($"roomPathPoints != null && roomPathPoints.Count > 0:{roomPathPoints != null && roomPathPoints.Count > 0}");

                if (roomPathPoints != null && roomPathPoints.Count > 0)
                {
                    // 去重：若上一路径终点与当前路径起点极近，则跳过起点
                    if (fullPath.Count > 0 && roomPathPoints.Count > 0 &&
                        Vector3.Distance(fullPath[fullPath.Count - 1], roomPathPoints[0]) < 0.1f)
                    {
                        fullPath.AddRange(roomPathPoints.Skip(1));
                    }
                    else
                    {
                        fullPath.AddRange(roomPathPoints);
                    }
                }
                else
                {
                    // 房间内寻路失败时，退而直接添加出口点
                    fullPath.Add(exitPoint);
                }

                currentEntry = exitPoint; // 下一个房间的入口即当前出口
            }

            return fullPath;
        }
        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        class FinderRoomCommand : ICommand
        {
            string ICommand.Command { get; } = "pathroom";
            string[] ICommand.Aliases { get; } = new[] { "" };
            string ICommand.Description { get; } = "!!! 使用后产生很多教程角色寻路";
            bool ICommand.Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                var runner = Player.Get(sender);
                response = "";
                if (arguments.Count < 1)
                {
                    foreach (var arg in Enum.GetNames(typeof(RoomType)))
                    {
                        response += arg + " \n";
                    }
                    return false;
                }

                if (runner.KickPower < 12)
                {
                    response = "你没权 （player.KickPower < 12）";
                    return false;
                }

                if (!Enum.TryParse<RoomType>(arguments.First(), true, out var r))
                {
                    response = "Failed to parse!";
                    return false;
                }

                if (runner.CurrentRoom == null)
                {
                    response = "Player has no current room.";
                    return false;
                }

                var targetRoomId = RoomIdentifier.AllRoomIdentifiers.FirstOrDefault(x => Room.Get(x).Type == r);
                if (targetRoomId == null)
                {
                    response = "Target room not found.";
                    return false;
                }
                var nav = RoomGraph.Instance;
                var re = GetDetailedPath(runner.CurrentRoom, Room.Get(targetRoomId),runner.Position);

                // 修复：检查路径是否存在
                if (re == null || re.Count == 0)
                {
                    response = "No path found.";
                    return false;
                }

                int i = 0;
                foreach (var item in re)
                {
                    Npc npc = new Npc(DummyUtils.SpawnDummy($"{i}"));

                    Timing.CallDelayed(0.5f, delegate
                    {
                        npc.Role.Set(RoleTypeId.Tutorial);
                        npc.Position = item + Vector3.up * 0.3f;
                        npc.Health = npc.MaxHealth;
                    });
                    Player.Dictionary.Add(npc.GameObject, npc);
                    response += $"  {i} spawned at {item}\n";
                    i++;
                }
                var offsetPoints = re.Select(p => p + Vector3.up * 0.5f).ToArray();
                new DrawableLineMessage(100f, Color.red * new Color(1, 1, 1), offsetPoints).SendToHubsConditionally(x => x == runner.ReferenceHub);
                return true;
            }
        }

        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        class FinderPosCommand : ICommand
        {
            string ICommand.Command { get; } = "pathPos";
            string[] ICommand.Aliases { get; } = new[] { "" };
            string ICommand.Description { get; } = "!!! 使用后产生很多教程角色寻路";

            bool ICommand.Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                var runner = Player.Get(sender);
                response = "";
                if (arguments.Count < 3)
                {
                    response = "No pos";
                    return false;
                }

                if (runner.KickPower < 12)
                {
                    response = "你没权 （player.KickPower < 12）";
                    return false;
                }

                var x = float.Parse(arguments.ElementAt(0));
                var y = float.Parse(arguments.ElementAt(1));
                var z = float.Parse(arguments.ElementAt(2));
                //var pathfinding = Pathfinding.nav;

                //// 获取路径点
                //var re = pathfinding.GetPathPoints(
                //    target.Position, new Vector3(x, y, z)
                //);
                var nav = RoomGraph.Instance;
                var re = nav.GetRoomPath(runner.CurrentRoom, Room.Get(new Vector3(x, y, z)));
                // 修复：检查路径是否存在
                if (re == null || re.Count == 0)
                {
                    response = $"No path found.Runner:{runner.CurrentRoom}";

                    return false;
                }
                int i = 0;
                foreach (var item in re)
                {
                    Npc npc = new Npc(DummyUtils.SpawnDummy($"{i}"));
                    Timing.CallDelayed(0.5f, delegate
                    {
                        npc.Role.Set(RoleTypeId.Tutorial);
                        npc.Position = item.Position + Vector3.up * 2f;
                        npc.Health = npc.MaxHealth;
                    });
                    Player.Dictionary.Add(npc.GameObject, npc);
                    response += $"  {i} spawned at {item}\n";
                    i++;
                }
                return true;
            }
        }
    }
    public class pathconfig : ModuleConfigBase
    {

    }
}
