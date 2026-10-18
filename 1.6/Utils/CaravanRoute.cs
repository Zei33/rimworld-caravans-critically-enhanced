using System;
using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// Sends a forming caravan, whose exit has just moved, through whichever of vanilla's own stages
    /// still has work for somebody undrafted: shared by Leave from here and the move away from a threat.
    /// </summary>
    /// <remarks>
    /// <see cref="LeaveFromHerePolicy.RouteFor"/> decides; this reads the lord for it and acts. Only an
    /// undrafted colonist with hands works a stage's duty (<see cref="ExitDirector.IsWorker"/>): a drafted
    /// pawn's orders come above the duty.
    /// </remarks>
    public static class CaravanRoute
    {
        /// <summary>
        /// Determines whether some member works the stages' duties.
        /// </summary>
        /// <param name="lord">The caravan's lord.</param>
        /// <returns><c>true</c> when one does.</returns>
        internal static bool AnyWorker(Lord lord)
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (ExitDirector.IsWorker(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a pen animal going is off every drafted member's lead, and whether one of
        /// those cannot walk to the exit by itself.
        /// </summary>
        /// <param name="lord">The caravan's lord.</param>
        /// <param name="spot">The exit.</param>
        /// <param name="penned">Whether one of them is fenced in.</param>
        /// <returns><c>true</c> when some pen animal needs leading.</returns>
        internal static bool NeedsRoping(Lord lord, IntVec3 spot, out bool penned)
        {
            // As vanilla's collecting stage finishes, every pen animal on a member's lead; but only a
            // drafted member's counts here, since the undrafted members' jobs are about to end, and
            // their leads with them.
            bool needs = false;
            penned = false;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (!AnimalPenUtility.NeedsToBeManagedByRope(pawn))
                {
                    continue;
                }

                Pawn roper = pawn.roping?.RopedByPawn;
                if (roper != null && roper.Drafted && roper.GetLord() == lord)
                {
                    continue;
                }

                needs = true;
                penned |= pawn.Spawned && !pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly);
            }

            return needs;
        }

        /// <summary>
        /// Unties every member tied at a spot, as vanilla's Force depart does before leaving.
        /// </summary>
        /// <param name="lord">The caravan's lord.</param>
        internal static void UntieFromSpots(Lord lord)
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn.roping != null && pawn.roping.IsRopedToSpot)
                {
                    pawn.roping.UnropeFromSpot();
                }
            }
        }

        /// <summary>
        /// Moves the caravan to the stage a route names, and ends every undrafted member's job as a
        /// stage change does.
        /// </summary>
        /// <param name="lord">The caravan's lord.</param>
        /// <param name="route">The route.</param>
        internal static void GoTo(Lord lord, LeaveRoute route)
        {
            // The graph's order is vanilla's and pinned by test.
            List<LordToil> toils = lord.Graph.lordToils;
            int target;
            Type expected;
            switch (route)
            {
                case LeaveRoute.DownedStage:
                    target = (int)FormingStage.GatherDownedPawns;
                    expected = typeof(LordToil_PrepareCaravan_GatherDownedPawns);
                    break;
                case LeaveRoute.CollectStage:
                    target = (int)FormingStage.CollectAnimals;
                    expected = typeof(LordToil_PrepareCaravan_CollectAnimals);
                    break;
                case LeaveRoute.LeaveStage:
                    target = (int)FormingStage.Leave;
                    expected = typeof(LordToil_PrepareCaravan_Leave);
                    break;
                default:
                    return;
            }

            if (toils.Count <= target || toils[target].GetType() != expected)
            {
                return;
            }

            lord.GotoToil(toils[target]);

            // What a stage change does, sparing the drafted, as the mod's transition patch does: collecting
            // jobs under way keep their old destination and never end by themselves.
            List<Pawn> owned = lord.ownedPawns;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i];
                if (pawn.jobs?.curJob != null && !pawn.Drafted)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }
    }
}
