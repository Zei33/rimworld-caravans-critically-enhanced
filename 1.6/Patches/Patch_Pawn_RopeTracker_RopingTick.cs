using System;
using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Keeps a drafted caravan colonist's leads through vanilla's rope tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's <c>Pawn_RopeTracker.RopingTick</c> breaks every rope of a roper that is drafted, and
    /// again of one with animals that is not in a rope job, which a drafted colonist never is. For a
    /// roper <see cref="LeadUtility.KeepsLeads"/> accepts, this prefix runs the checks that would
    /// still make it let go (<see cref="Core.LeadPolicy.IsFatal"/>: dead, downed, asleep, in a mental
    /// break, burning) and skips the rest; the roper is not on a rope itself, so vanilla's remaining
    /// checks, which are about its own rope, have nothing to do.
    /// </para>
    /// <para>
    /// The hottest patch in the mod: it runs every tick for every spawned pawn. Anything not leading
    /// an animal leaves at the first comparison, before the settings are read.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(Pawn_RopeTracker), nameof(Pawn_RopeTracker.RopingTick))]
    public static class Patch_Pawn_RopeTracker_RopingTick
    {
        /// <summary>
        /// How often held animals are checked for hunger and tiredness, in ticks.
        /// </summary>
        public const int NeedsInterval = 250;

        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch.
        /// </summary>
        public const int ErrorKey = 0x43434504;

        /// <summary>
        /// Takes over the tick for a held roper.
        /// </summary>
        /// <param name="__instance">The tracker.</param>
        /// <param name="___pawn">Its pawn.</param>
        /// <param name="___ropees">The animals it leads.</param>
        /// <returns><c>false</c> when the mod handled the tick.</returns>
        public static bool Prefix(Pawn_RopeTracker __instance, Pawn ___pawn, List<Pawn> ___ropees)
        {
            if (___ropees == null || ___ropees.Count == 0)
            {
                return true;
            }

            try
            {
                if (!LeadUtility.KeepsLeads(___pawn))
                {
                    if (LeadUndraftNotices.Count > 0)
                    {
                        LeadUtility.TellUndraftDroppedLeads(___pawn);
                    }

                    return true;
                }

                if (LeadUtility.IsFatal(___pawn))
                {
                    __instance.BreakAllRopes();
                    return false;
                }

                bool interval = ___pawn.IsHashIntervalTick(NeedsInterval);
                if (interval)
                {
                    LeadUtility.ReleaseNeedyRopees(___pawn);
                }

                LeadUtility.MatchSpeed(___pawn, interval);
                return false;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[CaravansCriticallyEnhanced] Keeping a drafted colonist's leads failed, so vanilla decides: " + exception, ErrorKey);
                return true;
            }
        }
    }
}
