using System;
using System.Collections.Generic;
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
    /// Sets up a new forming caravan's state as it starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One class for every feature that needs the start: so far the exit, which records the exit it
    /// was given and holds the caravan at once when the send found none inside every area.
    /// </para>
    /// <para>
    /// The new lord is the first pawn's lord afterwards, but only if it is not the lord that pawn had
    /// before: vanilla returns early, logging, on a bad tile or an empty list, and
    /// <c>LordMaker.MakeNewLord</c> returns null for a pawn with no map, and in both cases the first
    /// pawn's lord is an older one.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.StartFormingCaravan))]
    public static class Patch_CaravanFormingUtility_StartFormingCaravan
    {
        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch.
        /// </summary>
        public const int ErrorKey = 0x43434508;

        /// <summary>
        /// Notes the first pawn's lord before vanilla runs.
        /// </summary>
        /// <param name="pawns">The pawns going.</param>
        /// <param name="__state">That lord, or <c>null</c>.</param>
        public static void Prefix(List<Pawn> pawns, out Lord __state)
        {
            __state = pawns != null && pawns.Count > 0 ? pawns[0].GetLord() : null;
        }

        /// <summary>
        /// Sets up the new caravan.
        /// </summary>
        /// <param name="pawns">The pawns going.</param>
        /// <param name="exitSpot">The exit vanilla was given.</param>
        /// <param name="__state">The first pawn's lord before.</param>
        public static void Postfix(List<Pawn> pawns, IntVec3 exitSpot, Lord __state)
        {
            Lord lord = pawns != null && pawns.Count > 0 ? pawns[0].GetLord() : null;
            if (lord == null || lord == __state || !FormingLords.IsVanilla(lord))
            {
                return;
            }

            try
            {
                ExitDirector.Begin((LordJob_FormAndSendCaravan)lord.LordJob, exitSpot);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[CaravansCriticallyEnhanced] Setting up a new caravan's exit failed: " + exception, ErrorKey);
            }
        }
    }
}
