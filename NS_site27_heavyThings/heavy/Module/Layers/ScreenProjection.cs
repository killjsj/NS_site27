using InventorySystem.Items;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using UnityEngine;

namespace NS_site27_api.Core.UI
{
    /// <summary>
    /// Aspect-free screen rectangle produced by <see cref="ScreenProjection"/>.
    ///
    /// <para>
    /// <b>Y</b> is final: 0..1 from the top of the screen, ready for UI Toolkit.
    /// </para>
    /// <para>
    /// <b>H</b> is horizontal offset from screen centre measured in <i>half-screen-height</i> units,
    /// not half-widths. A Unity perspective camera keeps vertical FOV fixed and widens horizontally
    /// with the aspect ratio, DropSo the vertical axis is aspect-independent while the horizontal axis
    /// needs exactly one division by the aspect ratio. Finish it with <see cref="ToUiRect"/>.
    /// </para>
    /// </summary>
    public readonly struct ScreenRect
    {
        public readonly float HMin;
        public readonly float HMax;
        public readonly float YMin;
        public readonly float YMax;

        public ScreenRect(float hMin, float hMax, float yMin, float yMax)
        {
            HMin = hMin;
            HMax = hMax;
            YMin = yMin;
            YMax = yMax;
        }

        /// <summary>
        /// Converts to normalized UI coordinates (0..1, top-left origin) for a given aspect ratio.
        /// Pass <see cref="ScreenProjection.GetAspectRatio"/> for the viewer.
        /// </summary>
        public Rect ToUiRect(float aspectRatio)
        {
            if (aspectRatio <= 0f)
            {
                aspectRatio = 16f / 9f;
            }

            float xMin = 0.5f + (HMin / (2f * aspectRatio));
            float xMax = 0.5f + (HMax / (2f * aspectRatio));

            return new Rect(xMin, YMin, xMax - xMin, YMax - YMin);
        }
    }

    /// <summary>
    /// World -> screen projection with no <see cref="Camera"/> involved, DropSo it runs on a headless
    /// server. The perspective divide is done by hand from the observer's camera transform.
    /// </summary>
    public static class ScreenProjection
    {
        /// <summary>
        /// Unzoomed vertical FOV of the main world camera. <c>CameraShakeController</c> and
        /// <c>AspectRatioSync.Start</c> both use this literal.
        /// </summary>
        public const float BaseVerticalFov = 70f;

        /// <summary>Pass as <c>verticalFovDegrees</c> to resolve the FOV from the held item.</summary>
        public const float AutoFov = -1f;

        private static readonly Vector3[] CornerBuffer = new Vector3[8];

        /// <summary>
        /// Actual vertical FOV of a player's world camera, derived from what they are holding.
        ///
        /// <para>
        /// <c>CameraShakeController.LateUpdate</c> sets the main camera to
        /// <c>70f / IZoomModifyingItem.ZoomAmount</c>, and every input is available on the server:
        /// <c>LinearAdsModule.AdsAmount</c> falls back to <c>GetAdsAmountForSerial(serial)</c>
        /// whenever the module is not the local player, and the server itself populates that
        /// <c>SyncData</c> entry in <c>EquipUpdate</c> from the ADS input the client sends.
        /// </para>
        /// <para>
        /// Not modelled: transient recoil shake, which multiplies the base FOV for a few frames
        /// after a shot. Client-only, and small relative to zoom.
        /// </para>
        /// </summary>
        public static float GetVerticalFov(ReferenceHub hub)
        {
            float zoom = 1f;

            if (hub?.inventory != null && hub.inventory.CurInstance is IZoomModifyingItem zoomItem)
            {
                zoom = zoomItem.ZoomAmount;
            }

            if (zoom <= 0.01f || float.IsNaN(zoom))
            {
                zoom = 1f;
            }

            return BaseVerticalFov / zoom;
        }

        /// <summary>
        /// The viewer's real aspect ratio, as reported by their client through
        /// <c>AspectRatioSync.CmdSetAspectRatio</c>.
        ///
        /// <para>
        /// Values at or below 1 are treated as "not reported yet" and replaced by
        /// <paramref name="fallback"/>: the property reads its constructor default of exactly 1f
        /// before the first command arrives, and the server clamps
        /// <c>if (aspectRatio &lt; 1f) aspectRatio = 1f</c>, DropSo a portrait window is
        /// indistinguishable from unset.
        /// </para>
        /// <para>
        /// Read only <c>AspectRatio</c> from that component. <c>XScreenEdge</c> and <c>XplusY</c>
        /// derive from <c>_defaultCameraFieldOfView</c>, which is only assigned in <c>Start()</c>
        /// behind an <c>isLocalPlayer</c> check that never passes on a dedicated server, DropSo they
        /// evaluate to 0 and 35 respectively.
        /// </para>
        /// </summary>
        public static float GetAspectRatio(ReferenceHub hub, float fallback = 16f / 9f)
        {
            AspectRatioSync sync = hub?.aspectRatioSync;
            if (sync == null)
            {
                return fallback;
            }

            float reported = sync.AspectRatio;
            return reported > 1.0001f ? reported : fallback;
        }

        /// <summary>
        /// Screen rect covering <paramref name="target"/> as seen by <paramref name="observer"/>.
        /// </summary>
        /// <param name="verticalFovDegrees">
        /// Leave at <see cref="AutoFov"/> to resolve it from the observer's held item.
        /// </param>
        /// <returns>False if either player has no model, or the target is behind the observer.</returns>
        public static bool TryGetPlayerRect(ReferenceHub observer,
                                            ReferenceHub target,
                                            out ScreenRect rect,
                                            float verticalFovDegrees = AutoFov)
        {
            rect = default;

            if (observer == null || target == null || observer == target)
            {
                return false;
            }

            Transform cam = observer.PlayerCameraReference;
            if (cam == null)
            {
                return false;
            }

            if (!TryGetWorldBounds(target, out Bounds bounds))
            {
                return false;
            }

            if (verticalFovDegrees <= 0f)
            {
                verticalFovDegrees = GetVerticalFov(observer);
            }

            return TryProjectBounds(cam.position, cam.rotation, bounds, verticalFovDegrees, out rect);
        }

        /// <summary>Combined world-space AABB of every hitbox on the player's model.</summary>
        public static bool TryGetWorldBounds(ReferenceHub target, out Bounds bounds)
        {
            bounds = default;

            if (target.roleManager.CurrentRole is not IFpcRole fpc)
            {
                return false;
            }

            CharacterModel model = fpc.FpcModule.CharacterModelInstance;
            if (model == null)
            {
                return false;
            }

            HitboxIdentity[] hitboxes = model.Hitboxes;
            if (hitboxes == null || hitboxes.Length == 0)
            {
                return false;
            }

            bool any = false;

            foreach (HitboxIdentity hb in hitboxes)
            {
                if (hb == null || hb.TargetColliders == null)
                {
                    continue;
                }

                foreach (Collider col in hb.TargetColliders)
                {
                    if (col == null)
                    {
                        continue;
                    }

                    if (!any)
                    {
                        bounds = col.bounds;
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                }
            }

            return any;
        }

        /// <summary>
        /// Projects all 8 corners of a world AABB and returns their screen-space bounding box.
        /// Projecting the box rather than a single point is what makes the rect actually
        /// <i>cover</i> the target instead of merely pointing at it.
        /// </summary>
        public static bool TryProjectBounds(Vector3 cameraPosition,
                                            Quaternion cameraRotation,
                                            Bounds bounds,
                                            float verticalFovDegrees,
                                            out ScreenRect rect)
        {
            rect = default;

            float tanHalfV = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            if (tanHalfV <= 0f)
            {
                return false;
            }

            Quaternion invRot = Quaternion.Inverse(cameraRotation);

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            CornerBuffer[0] = new Vector3(c.x - e.x, c.y - e.y, c.z - e.z);
            CornerBuffer[1] = new Vector3(c.x + e.x, c.y - e.y, c.z - e.z);
            CornerBuffer[2] = new Vector3(c.x - e.x, c.y + e.y, c.z - e.z);
            CornerBuffer[3] = new Vector3(c.x + e.x, c.y + e.y, c.z - e.z);
            CornerBuffer[4] = new Vector3(c.x - e.x, c.y - e.y, c.z + e.z);
            CornerBuffer[5] = new Vector3(c.x + e.x, c.y - e.y, c.z + e.z);
            CornerBuffer[6] = new Vector3(c.x - e.x, c.y + e.y, c.z + e.z);
            CornerBuffer[7] = new Vector3(c.x + e.x, c.y + e.y, c.z + e.z);

            float hMin = float.MaxValue, hMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            bool any = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 local = invRot * (CornerBuffer[i] - cameraPosition);

                // Behind the camera the perspective divide flips sign and would place the corner on
                // the opposite side of the screen. Drop it rather than produce a mirrored rect.
                if (local.z <= 0.01f)
                {
                    continue;
                }

                float h = local.x / local.z / tanHalfV;   // half-screen-HEIGHT units, aspect-free
                float v = local.y / local.z / tanHalfV;   // -1 = bottom edge, +1 = top edge
                float y = 0.5f - (v * 0.5f);              // UI Toolkit measures down from the top

                any = true;
                if (h < hMin) { hMin = h; }
                if (h > hMax) { hMax = h; }
                if (y < yMin) { yMin = y; }
                if (y > yMax) { yMax = y; }
            }

            if (!any)
            {
                return false;
            }

            rect = new ScreenRect(hMin, hMax, yMin, yMax);
            return true;
        }

        /// <summary>
        /// Single world point, aspect-free. <c>x</c> is in half-screen-height units from centre,
        /// <c>y</c> is final UI 0..1 from the top.
        /// </summary>
        public static bool TryProjectPoint(Vector3 cameraPosition,
                                           Quaternion cameraRotation,
                                           Vector3 worldPoint,
                                           out Vector2 point,
                                           float verticalFovDegrees = BaseVerticalFov)
        {
            point = default;

            float tanHalfV = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            if (tanHalfV <= 0f)
            {
                return false;
            }

            Vector3 local = Quaternion.Inverse(cameraRotation) * (worldPoint - cameraPosition);
            if (local.z <= 0.01f)
            {
                return false;
            }

            point = new Vector2(
                local.x / local.z / tanHalfV,
                0.5f - (local.y / local.z / tanHalfV * 0.5f));

            return true;
        }
    }
}
