using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Leaves a forming caravan's drafted colonists to their orders when the caravan moves on a stage.
    /// </summary>
    /// <remarks>
    /// Vanilla ends every member's job on most of the forming caravan's transitions, drafted ones
    /// included, so a drafted colonist walking animals to the spot stops dead whenever a stage changes.
    /// For a vanilla forming caravan with "Keep leads when drafted" on, this runs vanilla's loop but
    /// skips the drafted. Every other lord is left to vanilla.
    /// </remarks>
    [HarmonyPatch(typeof(TransitionAction_EndAllJobs), nameof(TransitionAction_EndAllJobs.DoAction))]
    public static class Patch_TransitionAction_EndAllJobs_DoAction
    {
        /// <summary>
        /// Ends every undrafted member's job, in place of vanilla.
        /// </summary>
        /// <param name="trans">The transition.</param>
        /// <returns><c>false</c> when the mod did it.</returns>
        public static bool Prefix(Transition trans)
        {
            Lord lord = trans?.target?.lord;
            if (!FormingLords.IsVanilla(lord) || !CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting))
            {
                return true;
            }

            List<Pawn> owned = lord.ownedPawns;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i];
                if (pawn.jobs?.curJob != null && !pawn.Drafted)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            return false;
        }
    }
}
