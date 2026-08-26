using HarmonyLib;
using InventorySystem.Configs;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Usables;
using PlayerRoles.Voice;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(Intercom))]
    public class IntercomPatch
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
