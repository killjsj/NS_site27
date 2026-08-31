using HarmonyLib;
using PlayerRoles.Spectating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_heavy.heavy.Module.commanderBattle
{
    [HarmonyPatch(typeof(SpectatorRole))]
    class Patch_ReadyToRespawn
    {
        [HarmonyPatch("get_ReadyToRespawn")]
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, SpectatorRole __instance)
        {
            if (__result)
            {
                if (__instance.TryGetOwner(out var referenceHub))
                {
                    __result = !CommanderGlobalVar.DoNotRespawn.Contains(referenceHub);

                }
            }
        }
    }
}
