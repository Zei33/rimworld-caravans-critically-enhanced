using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Keeps a forming caravan's colonist from walking off the map alone at the player's order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a map that is not a home, with enemies about, a drafted colonist ordered onto an exit cell
    /// leaves the map by itself and makes a one-pawn caravan, abandoning the caravan it was forming,
    /// its animals included. Ordered by the player (drafted, or a forced job), the walk is stopped at
    /// the edge and a message points at Leave from here. A colonist leaving in a mental break
    /// (<c>JobGiver_ExitMap</c>) is left to vanilla.
    /// </para>
    /// <para>
    /// Asked every tick while the pawn stands on an exit cell, so the message is throttled. Clearing
    /// the job's exit flag ends the asking: the walk then simply finishes. <c>Pawn.ExitMap</c> itself
    /// is never touched, since vanilla's own departure calls it while the pawns are still members.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(JobDriver_Goto), "TryExitMap")]
    public static class Patch_JobDriver_Goto_TryExitMap
    {
        /// <summary>
        /// Stops the walk-off.
        /// </summary>
        /// <param name="__instance">The driver.</param>
        /// <returns><c>false</c> when the walk-off was stopped.</returns>
        public static bool Prefix(JobDriver_Goto __instance)
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;
            if (job == null || pawn == null || !(pawn.Drafted || job.playerForced)
                || FormingLords.VanillaJobOf(pawn.GetLord()) == null
                || !CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeaveFromHere))
            {
                return true;
            }

            job.exitMapOnArrival = false;
            CaravanMessages.PostThrottled(
                "CaravansCriticallyEnhanced_WalkOffBlocked" + pawn.thingIDNumber,
                5f,
                "CaravansCriticallyEnhanced_WalkOffBlocked".Translate(pawn.LabelShort),
                new LookTargets(pawn),
                MessageTypeDefOf.RejectInput);
            return false;
        }
    }
}
