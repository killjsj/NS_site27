using CentralAuth;
using Exiled.API.Extensions;
using Exiled.API.Features.Toys;
using Exiled.API.Structs;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_api.Core.UI
{
    public static class LineXray
    {
        public const float DefaultRange = 60f;
        public const int MaxMarkers = 60;

        private static readonly Dictionary<ReferenceHub, Dictionary<ReferenceHub, Primitive>> Cubes = new();
        private static readonly List<ReferenceHub> DeadViewers = new();

        private static bool _hooked;

        public static void Enable(ReferenceHub viewer, float range = DefaultRange, bool enemiesOnly = true)
        {
            if (viewer == null)
            {
                return;
            }

            EnsureHooked();
            if (Cubes.TryGetValue(viewer, out var oldDict))
            {
                foreach (var cube in oldDict.Values)
                {
                    cube?.Destroy();
                }
                _ = Cubes.Remove(viewer);
            }
            var settings = new ViewerSettings { Range = range, EnemiesOnly = enemiesOnly };
            _viewerSettings[viewer] = settings;
            Cubes[viewer] = new Dictionary<ReferenceHub, Primitive>();
        }

        public static void Disable(ReferenceHub viewer)
        {
            if (viewer == null)
            {
                return;
            }

            if (Cubes.TryGetValue(viewer, out var dict))
            {
                foreach (var cube in dict.Values)
                {
                    cube?.Destroy();
                }
                _ = Cubes.Remove(viewer);
            }
            _ = _viewerSettings.Remove(viewer);
        }

        public static bool IsEnabled(ReferenceHub viewer)
        {
            return viewer != null && Cubes.ContainsKey(viewer);
        }

        public static void DisableAll()
        {
            foreach (var viewerDict in Cubes.Values)
            {
                foreach (var cube in viewerDict.Values)
                {
                    cube?.Destroy();

                }
            }
            Cubes.Clear();
            _viewerSettings.Clear();
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

        public static void Unhook()
        {
            if (!_hooked)
            {
                return;
            }

            StaticUnityMethods.OnUpdate -= OnUpdate;
            _hooked = false;
        }
        public const float MaxAnchorDistance = 4f;
        public const float MinAnchorDistance = 0.6f;
        public const float ObstacleClearance = 0.85f;
        public const float Padding = 0.08f;
        private static void OnUpdate()
        {
            if (!NetworkServer.active || Cubes.Count == 0)
            {
                return;
            }

            DeadViewers.Clear();

            foreach (var viewerEntry in Cubes)
            {
                ReferenceHub viewer = viewerEntry.Key;
                if (!IsValidViewer(viewer))
                {
                    DeadViewers.Add(viewer);
                    continue;
                }

                if (!_viewerSettings.TryGetValue(viewer, out var settings))
                {
                    continue;
                }

                UpdateViewerCubes(viewer, settings, viewerEntry.Value);
            }
            foreach (var deadViewer in DeadViewers)
            {
                if (Cubes.TryGetValue(deadViewer, out var dict))
                {
                    foreach (var cube in dict.Values)
                    {
                        cube?.Destroy();
                    }
                    _ = Cubes.Remove(deadViewer);
                }
                _ = _viewerSettings.Remove(deadViewer);
            }
            DeadViewers.Clear();
        }
        private static void UpdateViewerCubes(ReferenceHub viewer, ViewerSettings settings,
    Dictionary<ReferenceHub, Primitive> viewerCubes)
        {
            HashSet<ReferenceHub> stillVisible = new();

            if (viewer.roleManager.CurrentRole is not IFpcRole viewerFpc)
            {
                return;
            }

            Vector3 origin = viewerFpc.FpcModule.Position;
            float sqrRange = settings.Range * settings.Range;
            int drawn = 0;

            Transform cam = viewer.PlayerCameraReference;
            Vector3 camPos = cam.position;
            Quaternion camRot = cam.rotation;
            Quaternion invRot = Quaternion.Inverse(camRot);
            float tanHalfV = Mathf.Tan(ScreenProjection.GetVerticalFov(viewer) * 0.5f * Mathf.Deg2Rad);

            Transform viewerTransform = viewer.transform;
            foreach (ReferenceHub target in ReferenceHub.AllHubs)
            {
                if (drawn >= MaxMarkers)
                {
                    break;
                }

                if (target == null || target == viewer || target.isLocalPlayer)
                {
                    continue;
                }

                if (target.Mode == ClientInstanceMode.Unverified || !target.IsAlive())
                {
                    continue;
                }

                if (target.roleManager.CurrentRole is not IFpcRole targetFpc)
                {
                    continue;
                }

                if ((targetFpc.FpcModule.Position - origin).sqrMagnitude > sqrRange)
                {
                    continue;
                }

                bool enemy = HitboxIdentity.IsEnemy(viewer, target);
                if (settings.EnemiesOnly && !enemy)
                {
                    continue;
                }

                if (!ScreenProjection.TryGetWorldBounds(target, out Bounds bounds))
                {
                    continue;
                }

                if (!TryGetAnchoredCubeTransform(camPos, camRot, invRot, tanHalfV, bounds,
        out Vector3 worldPos, out Quaternion worldRot, out Vector3 worldScale))
                {
                    continue;
                }

                _ = stillVisible.Add(target);
                Vector3 localPos = viewerTransform.InverseTransformPoint(worldPos);
                Quaternion localRot = Quaternion.Inverse(viewerTransform.rotation) * worldRot;
                Vector3 localScale = worldScale;

                if (viewerCubes.TryGetValue(target, out Primitive cube))
                {
                    if (cube == null)
                    {
                        _ = viewerCubes.Remove(target);
                        continue;
                    }
                    cube.Color = ColorFor(viewer, target);
                    cube.Transform.localPosition = localPos;
                    cube.Transform.localRotation = localRot;
                    cube.Transform.localScale = localScale;
                }
                else
                {
                    Primitive newCube = Primitive.Create(new PrimitiveSettings(
                        PrimitiveType.Cube,
                        ColorFor(viewer, target),
                        worldPos,
                        worldRot.eulerAngles,
                        worldScale,
                        false));

                    if (newCube == null)
                    {
                        continue;
                    }

                    newCube.Base.syncInterval = 0;
                    newCube.MovementSmoothing = 0;
                    newCube.Transform.parent = viewerTransform;
                    newCube.Transform.localPosition = localPos;
                    newCube.Transform.localRotation = localRot;
                    newCube.Transform.localScale = localScale;

                    newCube.Flags = AdminToys.PrimitiveFlags.Visible;
                    newCube.Spawn();

                    viewerCubes[target] = newCube;
                    drawn++;
                }
            }

            List<ReferenceHub> toRemove = new();
            foreach (var pair in viewerCubes)
            {
                if (!stillVisible.Contains(pair.Key))
                {
                    pair.Value?.Destroy();
                    toRemove.Add(pair.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _ = viewerCubes.Remove(key);
            }
        }
        private static Color ColorFor(ReferenceHub target, ReferenceHub enemyHub)
        {
            if (enemyHub == null)
            {
                return Color.clear;
            }

            var dis = Mathf.Clamp(
                Vector3.Distance(target.GetPosition(), enemyHub.GetPosition())
                , 0.01f, 30f);
            var a = Mathf.Lerp(0, 1, dis / 30f);
            var enemy = HitboxIdentity.IsEnemy(target, enemyHub);
            return !enemy
    ? new Color(0.31f, 0.78f, 1f, a)
    : target.GetTeam() switch
    {
        Team.SCPs => new Color(0.78f, 0.16f, 0.78f, a),
        Team.FoundationForces => new Color(0.24f, 0.55f, 1f, a),
        Team.ChaosInsurgency => new Color(0.20f, 0.75f, 0.24f, a),
        Team.Scientists => new Color(0.94f, 0.90f, 0.55f, a),
        Team.ClassD => new Color(1f, 0.55f, 0.16f, a),
        _ => new Color(0.90f, 0.24f, 0.24f, a),
    };
        }
        private static bool TryGetAnchoredCubeTransform(
    Vector3 camPos,
    Quaternion camRot,
    Quaternion invRot,
    float tanHalfV,
    Bounds bounds,
    out Vector3 pos,
    out Quaternion rot,
    out Vector3 scale)
        {
            pos = Vector3.zero;
            rot = camRot;
            scale = Vector3.one;

            Vector3 toTarget = bounds.center - camPos;
            float targetDistance = toTarget.magnitude;

            if (targetDistance < 0.05f)
            {
                return false;
            }

            Vector3 dir = toTarget / targetDistance;

            if (!TryGetAngularExtent(camPos, invRot, tanHalfV, bounds,
                    out float hMin, out float hMax, out float vMin, out float vMax))
            {
                return false;
            }

            float maxAnchor = Mathf.Min(MaxAnchorDistance, targetDistance);
            float anchor = maxAnchor;

            if (Physics.Raycast(camPos, dir, out RaycastHit hit, maxAnchor,
                    VisionInformation.VisionLayerMask))
            {
                anchor = hit.distance * ObstacleClearance;
            }

            anchor = Mathf.Clamp(anchor, MinAnchorDistance, maxAnchor);

            float halfHeight = anchor * tanHalfV;

            if (Padding > 0f)
            {
                float px = (hMax - hMin) * Padding;
                float py = (vMax - vMin) * Padding;
                hMin -= px;
                hMax += px;
                vMin -= py;
                vMax += py;
            }

            float centerH = (hMin + hMax) * 0.5f;
            float centerV = (vMin + vMax) * 0.5f;
            float width = (hMax - hMin) * halfHeight;
            float height = (vMax - vMin) * halfHeight;
            float depth = Mathf.Max(width, height) * 0.1f;

            pos = camPos + (camRot * new Vector3(centerH * halfHeight, centerV * halfHeight, anchor));
            rot = camRot;
            scale = new Vector3(width, height, depth);

            return true;
        }

        private static bool TryGetAngularExtent(
            Vector3 camPos,
            Quaternion invRot,
            float tanHalfV,
            Bounds bounds,
            out float hMin,
            out float hMax,
            out float vMin,
            out float vMax)
        {
            hMin = vMin = float.MaxValue;
            hMax = vMax = float.MinValue;

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            bool any = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));

                Vector3 local = invRot * (corner - camPos);

                if (local.z <= 0.01f)
                {
                    continue;
                }

                float h = local.x / local.z / tanHalfV;
                float v = local.y / local.z / tanHalfV;

                any = true;
                if (h < hMin)
                {
                    hMin = h;
                }

                if (h > hMax)
                {
                    hMax = h;
                }

                if (v < vMin)
                {
                    vMin = v;
                }

                if (v > vMax)
                {
                    vMax = v;
                }
            }

            return any;
        }
        private static bool IsValidViewer(ReferenceHub viewer)
        {
            return viewer != null
                && viewer.Mode != ClientInstanceMode.Unverified
                && viewer.connectionToClient != null
                && viewer.roleManager.CurrentRole is IFpcRole;
        }

        private sealed class ViewerSettings
        {
            public float Range = DefaultRange;
            public bool EnemiesOnly = true;
        }

        private static readonly Dictionary<ReferenceHub, ViewerSettings> _viewerSettings = new();
    }
}

