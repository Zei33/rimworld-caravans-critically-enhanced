using System;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// The forming caravan's own tick, shared by every feature that watches a caravan over time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs every tick for every forming lord, so the type test and the interval test come first and
    /// are all most ticks cost. The interval is staggered by the lord's load id, so two caravans
    /// forming at once do not do their work on the same tick.
    /// </para>
    /// <para>
    /// <c>LordManager.LordManagerTick</c> wraps each lord in a <c>try</c> that logs <c>Log.Error</c>
    /// every tick, so an exception escaping here would print a red line sixty times a second. This
    /// catches its own and reports it once.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(LordJob_FormAndSendCaravan), nameof(LordJob_FormAndSendCaravan.LordJobTick))]
    public static class Patch_LordJob_FormAndSendCaravan_LordJobTick
    {
        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch, chosen not to collide with vanilla's.
        /// </summary>
        public const int ErrorKey = 0x43434501;

        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from the leads pass, so that one failing pass
        /// does not hide the other's error.
        /// </summary>
        public const int LeadsErrorKey = 0x43434505;

        /// <summary>
        /// Runs each feature's periodic pass.
        /// </summary>
        /// <param name="__instance">The lord job.</param>
        public static void Postfix(LordJob_FormAndSendCaravan __instance)
        {
            if (!FormingLords.IsVanillaJob(__instance) || __instance.lord == null
                || (Find.TickManager.TicksGame + __instance.lord.loadID) % LoadingCompletion.TickInterval != 0)
            {
                return;
            }

            try
            {
                LoadingCompletion.Tick(__instance, CaravansCriticallyEnhancedMod.Settings);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[CaravansCriticallyEnhanced] The forming caravan pass failed: " + exception, ErrorKey);
            }

            try
            {
                LeadUtility.RecollectStrandedAnimals(__instance, CaravansCriticallyEnhancedMod.Settings);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[CaravansCriticallyEnhanced] Looking for animals stranded while leaving failed: " + exception, LeadsErrorKey);
            }
        }
    }
}
