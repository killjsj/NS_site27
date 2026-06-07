using CentralAuth;
using HarmonyLib;
using Mirror;
using System;
using System.Collections.Generic;
using System.Reflection;
using Log = Exiled.API.Features.Log;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch]
    public static class DedicatedServerDisconnectPatch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(NetworkConnectionToClient), nameof(NetworkConnectionToClient.Disconnect));
            yield return AccessTools.Method(typeof(NetworkConnectionToServer), nameof(NetworkConnectionToServer.Disconnect));
            yield return AccessTools.Method(typeof(LocalConnectionToClient), nameof(LocalConnectionToClient.Disconnect));
            yield return AccessTools.Method(typeof(LocalConnectionToServer), nameof(LocalConnectionToServer.Disconnect));
        }

        public static bool Prefix(NetworkConnection __instance)
        {
            if (__instance.connectionId == 0)
            {
                Log.Info("Critical: attempted to disconnect the dedicated server player.");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(NetworkServer), "UnpackAndInvoke")]
    public static class HostDisconnectPatch
    {
        public static void Postfix(ref bool __result, NetworkConnectionToClient connection, NetworkReader reader, int channelId)
        {
            if (connection.connectionId == 0)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ReferenceHub))]
    public static class ReferenceHubPatch
    {
        [HarmonyPatch("GetPlayerCount", new Type[] { typeof(ClientInstanceMode), typeof(ClientInstanceMode), typeof(ClientInstanceMode) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(ClientInstanceMode allowedState, ClientInstanceMode allowedState2, ClientInstanceMode allowedState3, ref int __result)
        {
            int num = 0;
            foreach (var hub in ReferenceHub.AllHubs)
            {
                if (allowedState == hub.Mode || allowedState2 == hub.Mode || allowedState3 == hub.Mode)
                    num++;
            }
            __result = num;
            return false;
        }
    }
}
