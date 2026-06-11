using HarmonyLib;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.Visibility;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(FpcStateProcessor))]
    public static class FpcStateProcessorPatch
    {
            static PropertyInfo hubField = typeof(FpcStateProcessor).GetProperty("Hub", BindingFlags.NonPublic | BindingFlags.Instance);
        [HarmonyPatch("get_ServerUseRate")]
        [HarmonyPrefix]
        public static bool Prefix(FpcStateProcessor __instance, ref float __result)
        {
            var hub = (ReferenceHub)hubField?.GetValue(__instance);

            if (hub != null)
            {
                var role = hub.roleManager.CurrentRole.RoleTypeId;
                if (role == RoleTypeId.Scp939 || role == RoleTypeId.Scp106)
                    return true;
            }

            __result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(FpcServerPositionDistributor))]
    public static class FpcServerPositionDistributorPatch
    {
        [HarmonyPatch("GetVisibleRole")]
        [HarmonyPrefix]
        public static bool Prefix(ReferenceHub receiver, ReferenceHub target, ref RoleTypeId __result)
        {
            RoleTypeId result = target.GetRoleId();
            if (target.isLocalPlayer || receiver.isLocalPlayer)
            {
                __result = result;
                return false;
            }

            if (target.roleManager.CurrentRole is IObfuscatedRole obfuscated)
                result = obfuscated.GetRoleForUser(receiver);

            if (receiver == target)
            {
                __result = result;
                return false;
            }

            bool visible = false;
            if (receiver.roleManager.CurrentRole is ICustomVisibilityRole cvr)
                visible = cvr.VisibilityController.ValidateVisibility(target);

            bool perm = PermissionsHandler.IsPermitted(receiver.serverRoles.Permissions, PlayerPermissions.GameplayData);
            bool isSpec = receiver.GetRoleId() == RoleTypeId.Spectator;

            if (target.GetTeam() == Team.SCPs)
            {
                __result = result;
                return false;
            }

            __result = (visible && perm && !isSpec) ? result : RoleTypeId.Spectator;
            return false;
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
            LobbyMusic.LobbyMusicManager.Instance.RoundStarted();
            return true;
        }
    }
}
