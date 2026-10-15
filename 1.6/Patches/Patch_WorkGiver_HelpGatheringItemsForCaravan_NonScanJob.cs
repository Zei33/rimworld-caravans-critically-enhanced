using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Stands vanilla's caravan helper down while the Load work type does its job.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Switched off in code rather than moved by an XML patch. FSF Complex Jobs, with nearly two
    /// hundred thousand subscribers, moves this giver into its own Transport column with a
    /// <c>PatchOperationReplace</c> on the def, which would collide with a second XML patch on the
    /// same node. A prefix works wherever another mod has put the def.
    /// </para>
    /// <para>
    /// It stands down only for the vanilla lord. While a subclass lord is forming on the map, a
    /// Vehicle Framework vehicle caravan for instance, vanilla's helper runs as before, because this
    /// mod's giver does not serve those lords and nothing else would.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(WorkGiver_HelpGatheringItemsForCaravan), nameof(WorkGiver_HelpGatheringItemsForCaravan.NonScanJob))]
    public static class Patch_WorkGiver_HelpGatheringItemsForCaravan_NonScanJob
    {
        /// <summary>
        /// Skips vanilla's helper when the Load work type covers every forming caravan on the map.
        /// </summary>
        /// <param name="pawn">The pawn looking for work.</param>
        /// <param name="__result">The job, set to none when vanilla is skipped.</param>
        /// <returns><c>false</c> to skip vanilla.</returns>
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LoadingWorkType) || AnySubclassFormingLord(pawn.Map))
            {
                return true;
            }

            __result = null;
            return false;
        }

        private static bool AnySubclassFormingLord(Map map)
        {
            if (map == null)
            {
                return false;
            }

            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                LordJob job = lords[i].LordJob;
                if (job is LordJob_FormAndSendCaravan && !FormingLords.IsVanillaJob(job))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
