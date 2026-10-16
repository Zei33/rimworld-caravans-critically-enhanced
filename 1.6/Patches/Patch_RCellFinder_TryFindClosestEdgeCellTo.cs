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
    /// Answers the formation window's exit search with the mod's plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The window's only call to this search is the first of its exit attempts, and the mod's
    /// answer then goes through vanilla's own meeting-point choice and pen-animal check. With every
    /// edge out of reach, or none usable, vanilla answers as before. When areas allow no exit, the
    /// answer is the best exit ignoring areas, and the new caravan is held at once
    /// (<see cref="ExitDirector.Begin"/>), so it packs and then waits.
    /// </para>
    /// <para>
    /// Only during a send (<see cref="ExitHandoff"/>) and only once in it: any other caller gets vanilla.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(RCellFinder), nameof(RCellFinder.TryFindClosestEdgeCellTo))]
    public static class Patch_RCellFinder_TryFindClosestEdgeCellTo
    {
        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch.
        /// </summary>
        public const int ErrorKey = 0x43434506;

        /// <summary>
        /// Plans the exit.
        /// </summary>
        /// <param name="root">Where the caravan's pawns stand on average.</param>
        /// <param name="map">The map.</param>
        /// <param name="result">The exit cell.</param>
        /// <param name="__result">Whether an exit was found.</param>
        /// <returns><c>false</c> when the mod answered.</returns>
        public static bool Prefix(IntVec3 root, Map map, ref IntVec3 result, ref bool __result)
        {
            if (ExitHandoff.Map == null || ExitHandoff.Map != map || ExitHandoff.Planned)
            {
                return true;
            }

            ExitHandoff.Planned = true;
            try
            {
                List<Pawn> pawns = TransferableUtility.GetPawnsFromTransferables(ExitHandoff.Transferables);
                ExitReading reading = ExitReadings.Read(map, pawns, root, ExitHandoff.StartingTile, IntVec3.Invalid, CaravansCriticallyEnhancedMod.Settings);
                ExitPlan plan = ExitPlanner.Plan(reading.Input);
                if (plan.Blocker == ExitBlocker.None)
                {
                    result = reading.Cells[plan.CandidateIndex];
                    __result = true;
                    return false;
                }

                bool areas = plan.Blocker == ExitBlocker.AreaExcludesReachableEdges || plan.Blocker == ExitBlocker.AreasDisjoint;
                if (areas && plan.FallbackIndex >= 0 && CaravanLordAccess.AllExitFieldsAvailable)
                {
                    result = reading.Cells[plan.FallbackIndex];
                    __result = true;
                    ExitHandoff.HoldOnStart = true;
                    reading.PawnsOf(plan.BlockingMoverIds, ExitHandoff.Blocking);
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[CaravansCriticallyEnhanced] Choosing the caravan's exit failed, so vanilla chooses: " + exception, ErrorKey);
                ExitHandoff.HoldOnStart = false;
                return true;
            }
        }
    }
}
