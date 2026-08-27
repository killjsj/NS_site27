using HarmonyLib;
using InventorySystem.Items.Firearms.Modules;
using System.Reflection;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    [HarmonyPatch]
    internal static class ExactRayPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HitscanHitregModuleBase), "RandomizeRay");
        }

        private static bool Prefix(HitscanHitregModuleBase __instance, Ray ray, float angle, ref Ray __result)
        {
            ReferenceHub owner = __instance.Firearm?.Owner;

            if (owner == null || !AimOverride.TryConsume(owner, ray.origin, out Vector3 dir))
            {
                return true;
            }

            __result = new Ray(ray.origin, dir); return false;
        }
    }
}
