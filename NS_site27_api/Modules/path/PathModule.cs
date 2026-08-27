using Exiled.API.Features;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using MapGeneration;
using NS_site27_api.Core;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Player = Exiled.API.Features.Player;
using Room = Exiled.API.Features.Room;

namespace FacilityNavigation
{
    public class PathModule : ModuleBase<NullModuleConfig>
    {
        public override string ModuleName => "path";

        public static Dictionary<string, Mesh> readableMeshes = new();

        public override void OnEnable()
        {
            LoadReadableMeshes(Path.Combine(ModuleConfigManager.ConfigDir, "readable_meshes.bundle"));

            Exiled.Events.Handlers.Server.WaitingForPlayers += w;
            ServerEvents.MapGenerated += OnMapGenerated;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= w;
            ServerEvents.MapGenerated -= OnMapGenerated;
        }

        private void OnMapGenerated(MapGeneratedEventArgs ev)
        {
            RoomGraph.Invalidate();
            RoomGraph.EnsureBuilt();

            Log.Info($"[PATH] RoomGraph ready: {RoomGraph.Nodes.Count} rooms / {RoomGraph.EdgeCount} edges");
        }
        public static LayerMask layer = 0;

        public void w()
        {
            List<int> layers = new();

            var s = new GameObject();
            var surf = s.AddComponent<NavMeshSurface>();

            foreach (RoomIdentifier room in RoomIdentifier.AllRoomIdentifiers)
            {
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
            }

            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = layer;
            surf.overrideVoxelSize = true;
            surf.voxelSize = 0.0625f;
            surf.overrideTileSize = true;
            surf.tileSize = 384;
            surf.collectObjects = CollectObjects.All;
            var Meshes = new Dictionary<MeshCollider, Mesh>();
            foreach (var r in Room.List)
            {
                ReplaceMesh(r, Meshes);
            }

            surf.BuildNavMesh();
            foreach (var kvp in Meshes)
            {
                kvp.Key.sharedMesh = kvp.Value;
            }
        }

        private static void ReplaceMesh(Room room, Dictionary<MeshCollider, Mesh> originalMeshes)
        {
            var filters = room.GameObject.GetComponentsInChildren<MeshCollider>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                if (readableMeshes.TryGetValue(filter.sharedMesh.name.Replace("(Instance)", "").Trim(), out var readableMesh))
                {
                    originalMeshes[filter] = filter.sharedMesh;
                    filter.sharedMesh = readableMesh;
                }
            }
        }

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
                if (mesh.name != null)
                {
                    string key = mesh.name.Replace("(Instance)", "").Trim();
                    readableMeshes[key] = mesh;
                    s++;
                }
            }

            bundle.Unload(false);
            Log.Info($"[PATH] Loaded {readableMeshes.Count} readable meshes from AssetBundle. a:{a},s{s}");
        }

        public static bool TryFindPathAtoB(Vector3 a, Vector3 b,
            out List<Vector3> corners, out List<RoomNode> routeRooms)
        {
            RoomGraph.EnsureBuilt();
            return NavPathBuilder.TryBuildFullPath(a, b, out corners, out routeRooms);
        }

        public static bool TryFindPathAtoB(Player player, Vector3 target,
            out List<Vector3> corners, out List<RoomNode> routeRooms)
        {
            return TryFindPathAtoB(player.Position, target, out corners, out routeRooms);
        }

        public static string DescribeRoute(List<RoomNode> rooms, List<Vector3> corners)
        {
            return rooms == null || rooms.Count == 0
                ? "unreachable"
                : $"{rooms.Count} rooms | {corners.Count} waypoints\n" +
                   RoomGraph.FormatPathDetailed(rooms);
        }
    }
}
