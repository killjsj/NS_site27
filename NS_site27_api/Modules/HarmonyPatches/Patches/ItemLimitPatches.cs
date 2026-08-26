using HarmonyLib;
using InventorySystem;
using InventorySystem.Configs;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Usables;
using System.Collections.Generic;
using System.Reflection;
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
        [HarmonyPatch(nameof(InventoryLimits.GetCategoryLimit), typeof(BodyArmor), typeof(ItemCategory))]
        [HarmonyPrefix]
        public static bool FireArmnPrefix(BodyArmor armor, ItemCategory category, ref sbyte __result)
        {
            __result = 8;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory))]
    public class InventoryPatch
    {
        [HarmonyPatch("RefreshModifiers")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 1; i < codes.Count; i++)
            {
                
                if (codes[i - 1].opcode == OpCodes.Call && codes[i  - 1].operand is MethodInfo m && m.Name.Contains("NetworkServer"))
                {
                    codes[i].opcode = OpCodes.Brtrue_S;
                    break;
                }
            }
            return codes;
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
