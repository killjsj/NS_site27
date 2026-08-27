using Exiled.API.Features;
using HarmonyLib;
using NS_site27_api.Modules.PlayerManagement;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(FpcStateProcessor))]
    public static class FpcStateProcessorPatch
    {
        private static readonly PropertyInfo hubField = typeof(FpcStateProcessor).GetProperty("Hub", BindingFlags.NonPublic | BindingFlags.Instance);
        [HarmonyPatch("get_ServerUseRate")]
        [HarmonyPrefix]
        public static bool Prefix(FpcStateProcessor __instance, ref float __result)
        {
            var hub = (ReferenceHub)hubField?.GetValue(__instance);

            if (hub != null)
            {
                var role = hub.roleManager.CurrentRole.RoleTypeId;
                if (role is RoleTypeId.Scp939 or RoleTypeId.Scp106)
                {
                    return true;
                }
            }

            __result = 0;
            return false;
        }
    }

                                                                                                                                                                                                                    
    [HarmonyPatch(typeof(PlayerRoles.Voice.Intercom))]
    public class IntercomPatch
    {

        [HarmonyPatch("CheckPlayer", typeof(ReferenceHub))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (
                    codes[i].opcode == OpCodes.Call &&
                    codes[i+1].opcode == OpCodes.Brfalse_S &&
                    codes[i+2].opcode == OpCodes.Ldloc_0 &&
                    codes[i+3].opcode == OpCodes.Isinst
                    )
                {
                    codes[i] = new CodeInstruction(OpCodes.Call, typeof(IntercomPatch).Method(nameof(CheckScpAndHuman)));
                    break;
                }
            }
            return codes;
        }
        public static bool CheckScpAndHuman(ReferenceHub hub)
        {
            if (hub == null) return false;
            if(hub.IsHuman()) return true;
            if(Player.TryGet(hub,out var player))
            {
                return ScpToPlayerChat.TalkTohumanScp.Contains(player) || (player.Role.Type == RoleTypeId.Scp079 && ScpToPlayerChat.Scp079AllowIntercom.Contains(hub));
            }
            return false;
        }
        [HarmonyPatch("CheckRange")]
        [HarmonyPostfix]
        public static void Postfix(ReferenceHub hub,ref bool __result)
        {
            if (!__result)
            {
                __result = hub.roleManager.CurrentRole.RoleTypeId == RoleTypeId.Scp079 && ScpToPlayerChat.Scp079AllowIntercom.Contains(hub);
            }
        }
    }

    [HarmonyPatch(typeof(Scp914.Scp914Upgrader))]
    public static class Scp914Patch
    {
        [HarmonyPatch("ProcessPlayer")]
        [HarmonyPrefix]
        public static bool ProcessPlayerPrefix(ReferenceHub ply, bool upgradeInventory, bool heldOnly, Scp914.Scp914KnobSetting setting)
        {
            return true;
        }

        [HarmonyPatch("ProcessPickup")]
        [HarmonyPrefix]
        public static bool ProcessPickupPrefix(ref InventorySystem.Items.Pickups.ItemPickupBase pickup, bool upgradeDropped, Scp914.Scp914KnobSetting setting)
        {
            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterClassManager))]
    public static class CharacterClassManagerPatch
    {
        [HarmonyPatch("ForceRoundStart")]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            foreach (var item in Player.Enumerable)
            {
                if (item.Role.Type != RoleTypeId.Overwatch)
                {
                    item.RoleManager.ServerSetRole(RoleTypeId.Spectator, RoleChangeReason.RoundStart);
                }
            }
            return true;
        }
    }
}
