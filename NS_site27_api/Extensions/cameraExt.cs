using Exiled.API.Features;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NS_site27_api.Extensions
{
    public static class cameraExt
    {
        public static bool TryLookDirection(this Player hub, Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            dir.Normalize();

                        float vertical = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            vertical = Mathf.Clamp(vertical, -88f, 88f);          
                        float horizontal = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (horizontal < 0f)
            {
                horizontal += 360f;
            }

            return hub.ReferenceHub.TryOverrideRotation(new Vector2(vertical, horizontal));
        }

        public static bool TryLookAt(this Player hub, Vector3 worldPoint)
        {
            return TryLookDirection(hub, worldPoint - hub.CameraTransform.position);
        }
    }
}
