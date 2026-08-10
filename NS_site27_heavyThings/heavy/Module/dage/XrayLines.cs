using CentralAuth;
using DrawableLine;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_api.Core.UI
{
    /// <summary>
    /// Through-wall player markers built from <see cref="DrawableLineMessage"/>, which every stock
    /// client already handles — no client mod, no DisplayKit.
    ///
    /// <para>
    /// <c>DrawableLinesManager</c> renders with a depth-tested material, so a box drawn at the
    /// target's real position is hidden by geometry. The fix is to keep the marker <b>in front of
    /// the wall</b>: draw a flat rectangle a short distance along the direction to the target,
    /// scaled by <c>anchorDistance / targetDistance</c> so it covers exactly the same angle of the
    /// viewer's screen. Same apparent size and position, nothing in between to occlude it.
    /// </para>
    /// <para>
    /// The anchor distance adapts per target: a raycast finds the nearest obstruction along that
    /// direction and the marker is placed just in front of it, so the trick keeps working in tight
    /// corridors where a fixed distance would put the rectangle inside the wall.
    /// </para>
    /// </summary>
    public static class LineXray
    {
        public const float SendInterval = 0.016f;

        /// <summary>
        /// Lifetime per line. Slightly longer than <see cref="SendInterval"/> so the previous batch
        /// is still alive when the next arrives, otherwise markers strobe.
        /// </summary>
        public const float LineDuration = SendInterval * 3.3f;

        public const float DefaultRange = 60f;

        /// <summary>
        /// Preferred anchor distance when nothing is in the way.
        /// <para>
        /// Line thickness is hardcoded to 0.05 world units in <c>DrawableLines.ClientGenerateLine</c>
        /// and cannot be changed from the server, so the marker's apparent line weight is set purely
        /// by how close it is drawn. At 4 m that is about 0.7 degrees, roughly 1% of screen height
        /// at default FOV. Much below 2 m and the outline becomes a fat bar.
        /// </para>
        /// </summary>
        public const float MaxAnchorDistance = 4f;

        /// <summary>Never anchor closer than this — below it the lines are unreadably thick.</summary>
        public const float MinAnchorDistance = 0.6f;

        /// <summary>Fraction of the obstruction distance to sit in front of.</summary>
        public const float ObstacleClearance = 0.85f;

        public const int MaxMarkers = 60;

        /// <summary>Extra margin around the target, as a fraction of the projected rect.</summary>
        public const float Padding = 0.08f;

        private sealed class ViewerSettings
        {
            public float Range = DefaultRange;
            public bool EnemiesOnly = true;
        }

        private static readonly Dictionary<ReferenceHub, ViewerSettings> Viewers = new();
        private static readonly List<ReferenceHub> Dead = new();

        // Closed rectangle: 4 corners plus a repeat of the first, so one message draws 4 segments.
        private static readonly Vector3[] RectBuffer = new Vector3[5];

        private static float _cooldown;
        private static bool _hooked;

        public static void Enable(ReferenceHub viewer, float range = DefaultRange, bool enemiesOnly = true)
        {
            if (viewer == null)
            {
                return;
            }

            EnsureHooked();
            Viewers[viewer] = new ViewerSettings { Range = range, EnemiesOnly = enemiesOnly };
        }

        /// <summary>
        /// Stops drawing. Lines already sent cannot be retracted — there is no such message — so
        /// they fade after <see cref="LineDuration"/>. That is why the duration is kept short.
        /// </summary>
        public static void Disable(ReferenceHub viewer)
        {
            if (viewer != null)
            {
                _ = Viewers.Remove(viewer);
            }
        }

        public static bool IsEnabled(ReferenceHub viewer)
        {
            return viewer != null && Viewers.ContainsKey(viewer);
        }

        /// <summary>Call on round restart and plugin disable.</summary>
        public static void DisableAll()
        {
            Viewers.Clear();
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

        private static void OnUpdate()
        {
            if (!NetworkServer.active || Viewers.Count == 0)
            {
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            _cooldown = SendInterval;
            Dead.Clear();

            foreach (KeyValuePair<ReferenceHub, ViewerSettings> kv in Viewers)
            {
                if (!IsValidViewer(kv.Key))
                {
                    Dead.Add(kv.Key);
                    continue;
                }

                DrawFor(kv.Key, kv.Value);
            }

            foreach (ReferenceHub dead in Dead)
            {
                _ = Viewers.Remove(dead);
            }

            Dead.Clear();
        }

        private static void DrawFor(ReferenceHub viewer, ViewerSettings settings)
        {
            if (!(viewer.roleManager.CurrentRole is IFpcRole viewerFpc))
            {
                return;
            }

            Transform cam = viewer.PlayerCameraReference;
            Vector3 camPos = cam.position;
            Quaternion camRot = cam.rotation;
            Quaternion invRot = Quaternion.Inverse(camRot);

            Vector3 origin = viewerFpc.FpcModule.Position;
            float sqrRange = settings.Range * settings.Range;
            float tanHalfV = Mathf.Tan(ScreenProjection.GetVerticalFov(viewer) * 0.5f * Mathf.Deg2Rad);

            int drawn = 0;

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

                if (!(target.roleManager.CurrentRole is IFpcRole targetFpc))
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

                if (TryBuildMarker(camPos, camRot, invRot, tanHalfV, bounds))
                {
                    Send(viewer, RectBuffer, ColorFor(target, enemy));
                    drawn++;
                }
            }
        }

        /// <summary>
        /// Fills <see cref="RectBuffer"/> with a camera-facing rectangle that covers the same screen
        /// area as <paramref name="bounds"/>, but sits close enough to the camera that geometry
        /// cannot occlude it.
        /// </summary>
        private static bool TryBuildMarker(Vector3 camPos,
                                           Quaternion camRot,
                                           Quaternion invRot,
                                           float tanHalfV,
                                           Bounds bounds)
        {
            Vector3 toTarget = bounds.center - camPos;
            float targetDistance = toTarget.magnitude;

            if (targetDistance < 0.05f)
            {
                return false;
            }

            Vector3 dir = toTarget / targetDistance;

            // Angular extent of the target, measured in camera space. Both axes are divided by
            // tanHalfV, so the result is in half-screen-height units and needs no aspect ratio —
            // the client's own projection supplies that when it renders the world-space quad.
            if (!TryGetAngularExtent(camPos, invRot, tanHalfV, bounds, out float hMin, out float hMax,
                                     out float vMin, out float vMax))
            {
                return false;
            }

            // Sit in front of whatever is actually blocking the view, not at a fixed distance —
            // otherwise the marker ends up inside the wall again in a tight corridor.
            float anchor = MaxAnchorDistance;

            if (Physics.Raycast(camPos, dir, out RaycastHit hit, MaxAnchorDistance,
                                VisionInformation.VisionLayerMask))
            {
                anchor = hit.distance * ObstacleClearance;
            }

            anchor = Mathf.Clamp(anchor, MinAnchorDistance, Mathf.Min(MaxAnchorDistance, targetDistance));

            // Half of the screen's vertical extent, in world units, at the anchor plane. Everything
            // in half-height units scales by this and lands on the correct screen position.
            float halfHeight = anchor * tanHalfV;

            if (Padding > 0f)
            {
                float px = (hMax - hMin) * Padding;
                float py = (vMax - vMin) * Padding;
                hMin -= px; hMax += px;
                vMin -= py; vMax += py;
            }

            RectBuffer[0] = CameraToWorld(camPos, camRot, hMin, vMin, anchor, halfHeight);
            RectBuffer[1] = CameraToWorld(camPos, camRot, hMax, vMin, anchor, halfHeight);
            RectBuffer[2] = CameraToWorld(camPos, camRot, hMax, vMax, anchor, halfHeight);
            RectBuffer[3] = CameraToWorld(camPos, camRot, hMin, vMax, anchor, halfHeight);
            RectBuffer[4] = RectBuffer[0];

            return true;
        }

        /// <summary>
        /// Projects the 8 corners of the AABB into camera space and returns the angular bounding
        /// box, in half-screen-height units on both axes.
        /// </summary>
        private static bool TryGetAngularExtent(Vector3 camPos,
                                                Quaternion invRot,
                                                float tanHalfV,
                                                Bounds bounds,
                                                out float hMin, out float hMax,
                                                out float vMin, out float vMax)
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

                // Behind the camera the perspective divide flips sign and mirrors the corner.
                if (local.z <= 0.01f)
                {
                    continue;
                }

                float h = local.x / local.z / tanHalfV;
                float v = local.y / local.z / tanHalfV;

                any = true;
                if (h < hMin) { hMin = h; }
                if (h > hMax) { hMax = h; }
                if (v < vMin) { vMin = v; }
                if (v > vMax) { vMax = v; }
            }

            return any;
        }

        private static Vector3 CameraToWorld(Vector3 camPos, Quaternion camRot,
                                             float h, float v, float depth, float halfHeight)
        {
            return camPos + (camRot * new Vector3(h * halfHeight, v * halfHeight, depth));
        }

        private static Color ColorFor(ReferenceHub target, bool enemy)
        {
            if (!enemy)
            {
                return new Color(0.31f, 0.78f, 0.47f);
            }

            return target.GetTeam() switch
            {
                Team.SCPs => new Color(0.78f, 0.16f, 0.78f),
                Team.FoundationForces => new Color(0.24f, 0.55f, 1f),
                Team.ChaosInsurgency => new Color(0.20f, 0.75f, 0.24f),
                Team.Scientists => new Color(0.94f, 0.90f, 0.55f),
                Team.ClassD => new Color(1f, 0.55f, 0.16f),
                _ => new Color(0.90f, 0.24f, 0.24f),
            };
        }

        private static bool IsValidViewer(ReferenceHub viewer)
        {
            return viewer != null
                && viewer.Mode != ClientInstanceMode.Unverified
                && viewer.connectionToClient != null
                && viewer.roleManager.CurrentRole is IFpcRole;
        }

        private static void Send(ReferenceHub viewer, Vector3[] points, Color color)
        {
            // Built by hand rather than via DrawableLines.ServerGenerateLine: that helper is gated
            // behind IsDebugModeEnabled AND sends on hub.connectionToServer, which is null for a
            // remote player on the server.
            viewer.connectionToClient.Send(new DrawableLineMessage(LineDuration, color, points));
        }
    }
}
