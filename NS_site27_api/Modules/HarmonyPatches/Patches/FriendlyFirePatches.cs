using Exiled.API.Features;
using HarmonyLib;
using NS_site27_api.Core;
using NS_site27_api.Modules.EventHandle;
using PlayerRoles;
using PlayerStatsSystem;
using Respawning.Waves;
using System.Linq;

namespace NS_site27_api.Modules.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(HitboxIdentity))]
    public static class HitboxIdentityPatch
    {
        [HarmonyPatch("IsDamageable", typeof(ReferenceHub), typeof(ReferenceHub))]
        [HarmonyPrefix]
        public static bool IsDamageablePrefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result)
        {
            var ffManager = GetFFManager();
            if (ffManager != null)
            {
                __result = (ServerConfigSynchronizer.Singleton.MainBoolsSync & 1) == 1 || FFIsEnemy(attacker, victim);
                return false;
            }
            return true;
        }

        [HarmonyPatch("IsEnemy", typeof(ReferenceHub), typeof(ReferenceHub))]
        [HarmonyPrefix]
        public static bool IsEnemyPrefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result)
        {
            var ffManager = GetFFManager();
            if (ffManager != null)
            {
                __result = FFIsEnemy(attacker, victim);
                return false;
            }
            return true;
        }

        private static bool FFIsEnemy(ReferenceHub attacker, ReferenceHub victim)
        {
            var ffManager = GetFFManager();
            if (ffManager == null)
            {
                return IsEnemyDefault(attacker?.GetTeam() ?? Team.Dead, victim?.GetTeam() ?? Team.Dead);
            }

            if (attacker == Server.Host.ReferenceHub)
            {
                return true;
            }

            if ((victim.isServer || victim == Server.Host.ReferenceHub) && !victim.IsDummy)
            {
                return false;
            }

            var a = Player.Get(attacker);
            var v = Player.Get(victim);
            return a == null || v == null || ffManager.IsDamaging(a, v);
        }

        private static IFFManager GetFFManager()
        {
            var ehModule = CorePlugin.Modules.OfType<ItemCleanerModule>().FirstOrDefault();
            return ehModule?.CurrentFFManager;
        }

        private static bool IsEnemyDefault(Team a, Team b)
        {
            return a != Team.Dead && b != Team.Dead && (a != Team.SCPs || b != Team.SCPs) && a.GetFaction() != b.GetFaction();
        }
    }

    [HarmonyPatch(typeof(AttackerDamageHandler))]
    public static class AttackerDamageHandlerPatch
    {
        [HarmonyPatch("get_IgnoreFriendlyFireDetector")]
        [HarmonyPrefix]
        public static bool GetIgnoreFFPrefix(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPatch("ProcessDamage")]
        [HarmonyPrefix]
        public static bool ProcessDamagePrefix(AttackerDamageHandler __instance, ReferenceHub ply)
        {
            var ffManager = GetFFManager();
            return ffManager == null || __instance.Attacker.Hub == null || ply == null;
        }

        private static IFFManager GetFFManager()
        {
            var ehModule = CorePlugin.Modules.OfType<ItemCleanerModule>().FirstOrDefault();
            return ehModule?.CurrentFFManager;
        }
    }

    [HarmonyPatch(typeof(NtfSpawnWave))]
    public static class NtfSpawnWavePatch
    {
        [HarmonyPatch("PopulateQueue")]
        [HarmonyPostfix]
        public static void Postfix(System.Collections.Generic.Queue<RoleTypeId> queueToFill, int playersToSpawn)
        {
            queueToFill.Enqueue(RoleTypeId.NtfCaptain);
        }
    }
}
