using CommandSystem;
using HarmonyLib;
using RemoteAdmin;
using System;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(CommandSystem.Commands.RemoteAdmin.BanCommand))]
    public class BanCommandPatch
    {
        [HarmonyPatch("Execute")]
        [HarmonyPrefix]
        public static bool Prefix(ArraySegment<string> arguments, ICommandSender sender, ref string response, ref bool __result)
        {
            if (CommandProcessor.RemoteAdminCommandHandler.TryGetCommand("sban", out var command))
            {
                var b = command as Modules.BanSystem.Site27BanCommand;
                __result = b.Execute(arguments, sender, out response);
                return false;
            }
            return true;
        }
    }
}
