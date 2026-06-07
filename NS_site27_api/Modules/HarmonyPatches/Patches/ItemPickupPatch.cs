using HarmonyLib;
using InventorySystem.Items.Pickups;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(ItemPickupBase))]
    public class ItemPickupBasePatch
    {
        [HarmonyPatch("DestroySelf")]
        [HarmonyPrefix]
        public static bool Prefix(ItemPickupBase __instance)
        {
            if (__instance == null) return false;
            if (__instance.GetInstanceID() == 0) return false;
            return true;
        }
    }
}
