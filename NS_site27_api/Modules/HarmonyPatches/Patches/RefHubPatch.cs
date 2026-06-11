using CentralAuth;
using HarmonyLib;
using NS_site27_api.Modules.EventHandle.Handlers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.Visibility;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(ReferenceHub))]
    public static class MReferenceHubPatch
    {
        [HarmonyPatch("GetPlayerCount", new Type[] {
        typeof(ClientInstanceMode),
        typeof(ClientInstanceMode),
        typeof(ClientInstanceMode)
    })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix1(
            ClientInstanceMode allowedState,
            ClientInstanceMode allowedState2,
            ClientInstanceMode allowedState3,
            ref int __result)
        {
            int num = 0;
            foreach (ReferenceHub referenceHub in ReferenceHub.AllHubs)
            {
                if (allowedState == referenceHub.Mode ||
                    allowedState2 == referenceHub.Mode ||
                    allowedState3 == referenceHub.Mode)
                {
                    num++;
                }
            }
            __result = num - RoundEventHandler.DoNotCountDummyHubs.Count;
            //Log.Info($"__result={__result}");
            return false;
        }
    }
}
