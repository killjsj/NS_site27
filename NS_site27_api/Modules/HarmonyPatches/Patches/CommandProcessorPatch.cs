using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    //[HarmonyPatch]
    //public class CommandProcessorPatch
    //{
    //    private static readonly Type[] TargetMethodArgs = new Type[]
    //    {
    //        typeof(string), typeof(CommandSender),
    //    };

    //    private static MethodBase TargetMethod()
    //    {
    //        return typeof(CommandSender).GetMethod("ProcessQuery", TargetMethodArgs);
    //    }

    //    [HarmonyPostfix]
    //    public static void Postfix(string q, CommandSender sender)
    //    {
    //        LastCommand.LastCommandQuery[sender] = q;
    //    }
    //}
    //public static class LastCommand
    //{
    //    public static Dictionary<CommandSender, string> LastCommandQuery = new();
    //}
}
