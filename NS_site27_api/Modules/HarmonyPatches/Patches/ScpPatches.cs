using HarmonyLib;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.PlayableScps.Scp079;
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

    //[HarmonyPatch(typeof(FpcServerPositionDistributor))]
    //public static class FpcServerPositionDistributorPatch
    //{
    //    [HarmonyPatch("GetVisibleRole")]
    //    [HarmonyPrefix]
    //    public static bool Prefix(ReferenceHub receiver, ReferenceHub target, ref RoleTypeId __result)
    //    {
    //        RoleTypeId CurrentRole = target.GetRoleId();
    //        if (target.isLocalPlayer || receiver.isLocalPlayer)
    //        {
    //            __result = CurrentRole;
    //            return false;
    //        }
    //        if (target.roleManager.CurrentRole is IObfuscatedRole obfuscatedRole)
    //        {
    //            CurrentRole = obfuscatedRole.GetRoleForUser(receiver);
    //        }
    //        if (receiver == target)
    //        {
    //            __result = CurrentRole;
    //            return false;
    //        }
    //        bool IsVisable = false;
    //        if (receiver.roleManager.CurrentRole is ICustomVisibilityRole customVisibilityRole)
    //        {
    //            IsVisable = !customVisibilityRole.VisibilityController.ValidateVisibility(target);
    //        }
    //        float distant = Vector3.Distance(receiver.transform.position, target.transform.position);
    //        if (receiver.roleManager.CurrentRole is Scp079Role scp079Role)
    //        {
    //            distant = Vector3.Distance(scp079Role.CameraPosition, target.transform.position);
    //        }
    //        bool RAPermission = PermissionsHandler.IsPermitted(receiver.serverRoles.Permissions, PlayerPermissions.GameplayData);
    //        bool distantFlag = (receiver.GetTeam() == Team.SCPs) ? (distant <= 110f) : (distant <= 50f);
    //        bool IsDied = receiver.GetRoleId() == RoleTypeId.Spectator;
    //        if (target.GetTeam() == Team.SCPs)
    //        {
    //            __result = CurrentRole;
    //            return false;
    //        }
    //        if (target.IsCommunicatingGlobally())
    //        {
    //            __result = CurrentRole;
    //            return false;
    //        }
    //        if (IsVisable && !distantFlag && !RAPermission && !IsDied)
    //        {
    //            CurrentRole = RoleTypeId.Spectator;
    //        }
    //        __result = CurrentRole;
    //        return false;
    //    }
    //}

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
            //LobbyMusic.LobbyMusicManager.Instance.RoundStarted();
            return true;
        }
    }
}
