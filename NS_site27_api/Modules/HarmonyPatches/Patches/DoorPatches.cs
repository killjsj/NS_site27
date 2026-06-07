using HarmonyLib;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch]
    public class DoorPermissionsPolicyPatch
    {
        private static readonly Type[] TargetMethodArgs = new Type[]
        {
            typeof(ReferenceHub), typeof(IDoorPermissionRequester), typeof(PermissionUsed).MakeByRefType()
        };

        private static MethodBase TargetMethod()
        {
            return typeof(DoorPermissionsPolicy).GetMethod("CheckPermissions", TargetMethodArgs);
        }

        [HarmonyPostfix]
        public static void Postfix(ReferenceHub hub, IDoorPermissionRequester requester, out PermissionUsed callback, ref bool __result)
        {
            callback = null;
            if (__result) return;

            var player = Exiled.API.Features.Player.Get(hub);
            if (player == null) return;

            foreach (var item in hub.inventory.UserInventory.Items.Values)
            {
                if (item is IDoorPermissionProvider dp)
                {
                    if (requester.PermissionsPolicy.CheckPermissions(dp, requester, out PermissionUsed temp))
                    {
                        __result = true;
                        callback = temp;
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(EmergencyDoorRelease))]
    public class EmergencyDoorReleasePatch
    {
        [HarmonyPatch("ServerInteract")]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }
}
