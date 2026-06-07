using HarmonyLib;
using InventorySystem.Configs;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Usables;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(InventoryLimits))]
    public class InventoryLimitsPatch
    {
        [HarmonyPatch(nameof(InventoryLimits.GetAmmoLimit), typeof(BodyArmor), typeof(ItemType))]
        [HarmonyPrefix]
        public static bool Prefix(BodyArmor armor, ItemType ammoType, ref ushort __result)
        {
            __result = 150;
            return false;
        }
    }

    [HarmonyPatch(typeof(Scp207))]
    public class Scp207Patch
    {
        [HarmonyPatch("OnEffectsActivated")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_4)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)127);
                    break;
                }
            }
            return codes;
        }
    }
}
