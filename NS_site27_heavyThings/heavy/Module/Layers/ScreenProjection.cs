using InventorySystem.Items;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using UnityEngine;

namespace NS_site27_api.Core.UI
{
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

                    public static class ScreenProjection
    {
                                        public const float BaseVerticalFov = 70f;

                public const float AutoFov = -1f;

        private static readonly Vector3[] CornerBuffer = new Vector3[8];

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

                                                if (local.z <= 0.01f)
                {
                    continue;
                }

                float h = local.x / local.z / tanHalfV;                   float v = local.y / local.z / tanHalfV;                   float y = 0.5f - (v * 0.5f);              
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
