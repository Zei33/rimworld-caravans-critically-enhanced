using System.Collections.Generic;
using CaravansCriticallyEnhanced.Jobs;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Adds the mod's buttons to a pawn's gizmos.
    /// </summary>
    /// <remarks>
    /// One postfix for every button the mod adds to pawns. So far that is the drop-lead button on a
    /// drafted caravan colonist holding leads.
    /// </remarks>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        /// <summary>
        /// Passes vanilla's gizmos through and adds the mod's.
        /// </summary>
        /// <param name="__result">Vanilla's gizmos.</param>
        /// <param name="__instance">The pawn.</param>
        /// <returns>Vanilla's gizmos, then the mod's.</returns>
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }

            if (LeadUtility.ShowsDropLead(__instance))
            {
                yield return new Command_DropLead(__instance);
            }
        }
    }
}
