using System;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Lets Force depart send a held caravan off by its fallback exit.
    /// </summary>
    /// <remarks>
    /// Force depart is the player overriding the area rule, so the caravan stops being held, its exit
    /// copies go back to the fallback, and the rule leaves it alone from then on. Vanilla then runs as
    /// usual.
    /// </remarks>
    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.ForceCaravanDepart))]
    public static class Patch_CaravanFormingUtility_ForceCaravanDepart
    {
        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch.
        /// </summary>
        public const int ErrorKey = 0x43434509;

        /// <summary>
        /// Records the override and releases a held caravan.
        /// </summary>
        /// <param name="lord">The caravan.</param>
        public static void Prefix(Lord lord)
        {
            LordJob_FormAndSendCaravan job = FormingLords.VanillaJobOf(lord);
            if (job == null)
            {
                return;
            }

            try
            {
                ExitDirector.Override(job);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[CaravansCriticallyEnhanced] Releasing a held caravan for Force depart failed: " + exception, ErrorKey);
            }
        }
    }
}
