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
            return __instance != null && __instance.GetInstanceID() != 0;
        }
    }
}
