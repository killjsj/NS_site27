using System.Reflection;
using HarmonyLib;
using InventorySystem.Items.Firearms.Modules;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    /// <summary>
    /// Removes bullet spread and redirects the ray, for players holding the ability only.
    /// <para>
    /// <c>HitscanHitregModuleBase.RandomizeRay(ray, angle)</c> is the single chokepoint both
    /// <c>SingleBulletHitscan.Fire()</c> and <c>BuckshotHitreg.Fire()</c> pass through:
    /// </para>
    /// <code>
    /// float num = Mathf.Max(Random.value, Random.value);
    /// Vector3 vector = Random.insideUnitSphere * num;
    /// ray.direction = Quaternion.Euler(angle * vector) * ray.direction;
    /// </code>
    /// <para>
    /// The cone width is <c>CurrentInaccuracy</c>, the sum of every IInaccuracyProviderModule on the
    /// firearm (base bullet inaccuracy, movement, subsequent shots, ADS). Most of those have private
    /// setters, so zeroing the inputs is not practical — replacing the output is.
    /// </para>
    /// <para>
    /// NOTE for shotguns: this only de-randomizes the <em>base</em> ray. BuckshotHitreg then applies
    /// independent per-pellet randomness in <c>GetPelletDirection</c> via Random.insideUnitCircle.
    /// If this ability needs to work with a shotgun, add a matching prefix there.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class ExactRayPatch
    {
        // RandomizeRay is protected -> resolve manually.
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(HitscanHitregModuleBase), "RandomizeRay");

        private static bool Prefix(HitscanHitregModuleBase __instance, Ray ray, float angle, ref Ray __result)
        {
            ReferenceHub owner = __instance.Firearm?.Owner;

            if (owner == null || !AimOverride.TryConsume(owner, ray.origin, out Vector3 dir))
                return true;                        // vanilla: randomize as normal

            __result = new Ray(ray.origin, dir);    // exact, no cone
            return false;
        }
    }
}
