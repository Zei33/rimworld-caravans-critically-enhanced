using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// A helper leads a caravan's animal to the gathering spot and ties it there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's <c>JobDriver_PrepareCaravan_GatherAnimals</c> with the spot taken from target B
    /// instead of the roper's duty, which a helper does not have. Everything else is the base
    /// <c>JobDriver_RopeToDestination</c>: the walk, the opportunistic pick-up of up to ten more
    /// animals nearby, the speed of the slowest animal, and the finish action that drops every rope
    /// the helper still holds.
    /// </para>
    /// <para>
    /// The ropees follow because <c>JobGiver_FollowRoper</c> and <c>JobDriver_FollowRoper</c> test
    /// the roper's driver with <c>is JobDriver_RopeToDestination</c>, which a subclass passes, and read
    /// the destination from its target B.
    /// </para>
    /// </remarks>
    public class JobDriver_CCE_RopeAnimalToCaravan : JobDriver_RopeToDestination
    {
        /// <inheritdoc />
        /// <remarks>
        /// The same test as vanilla's: within two cells of the spot, in the spot's district, and the
        /// animal in that district too.
        /// </remarks>
        protected override bool HasRopeeArrived(Pawn ropee, bool roperWaitingAtDest)
        {
            IntVec3 spot = job.GetTarget(TargetIndex.B).Cell;
            if (!spot.IsValid || !pawn.Position.InHorDistOf(spot, 2f))
            {
                return false;
            }

            District district = spot.GetDistrict(pawn.Map);
            return district == pawn.GetDistrict() && district == ropee.GetDistrict();
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// Ties the animal at target B, the caravan's spot. Vanilla reads the animal's own duty, which
        /// names the same spot while animals gather, but this job carries on through a pause for a
        /// member's mental break, and there every duty is the pause duty, with no spot: the animal would
        /// be let off the rope at the hitching spot and wander away.
        /// </para>
        /// <para>
        /// Only an animal still in the job's caravan is tied. One the player took out of the caravan
        /// on the way has lost its duty, and vanilla's driver lets such an animal go; tying it would
        /// leave it at the hitching spot belonging to nobody.
        /// </para>
        /// </remarks>
        protected override void ProcessArrivedRopee(Pawn ropee)
        {
            IntVec3 spot = job.GetTarget(TargetIndex.B).Cell;
            if (spot.IsValid && ropee.GetLord() == job.lord)
            {
                ropee.roping.RopeToSpot(spot);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// The rule the work giver's search applies, so a helper picks up nearby only what it could
        /// have been sent for, and only for the caravan it is working for. That includes the allowed
        /// area: <c>JobGiver_Work</c> tests it on every candidate, and the base class's search for
        /// more animals does not.
        /// </remarks>
        protected override bool ShouldOpportunisticallyRopeAnimal(Pawn animal)
        {
            return animal.GetLord() == job.lord
                && AnimalGatherIndex.NeedsLeading(animal)
                && !animal.IsForbidden(pawn)
                && pawn.CanReserve(animal)
                && GatherAnimalsAndSlavesForCaravanUtility.CanRoperTakeAnimalToDest(pawn, animal, job.GetTarget(TargetIndex.B).Cell);
        }

        /// <summary>
        /// Ends every helper's rope job for a caravan, so each lets go of its animals.
        /// </summary>
        /// <param name="lord">The caravan, a lord <see cref="FormingLords.IsVanilla(Lord)"/> accepts.</param>
        /// <remarks>
        /// Called as the caravan is cancelled, before vanilla's <c>StopFormingCaravan</c> runs. Vanilla
        /// then has the caravan's colonists lead its pen animals back to their pen, but only animals
        /// nobody is holding (<c>CaravanFormingUtility.LeadAnimalsToPen</c> skips any that are roped),
        /// and a helper is not a member, so nothing else ends its job in time. Doing it here rather
        /// than from the job's own end avoids re-entering it: vanilla's pen lord ends the current job
        /// of every pawn it takes.
        /// <para>
        /// A rope job waiting in a pawn's queue is ended too. It still holds its reservations: one
        /// is queued behind an opportunistic haul started first, behind a suspension such as
        /// vomiting, or by a shift-click order. Left in place, those reservations make the pen lord's
        /// rope job fail for the member who takes the animal, and the member then lets go of every
        /// animal it is leading.
        /// </para>
        /// <para>
        /// No new job is started in its place. The caravan still exists at this point and this tick's
        /// search set still lists its animals, so a helper choosing again at once could take the same
        /// animal straight back. It chooses at its next job search, which comes after vanilla has
        /// removed the caravan.
        /// </para>
        /// </remarks>
        public static void LetGoFor(Lord lord)
        {
            var found = new List<(Pawn Helper, Job Job)>();
            IReadOnlyList<Pawn> pawns = lord.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn_JobTracker jobs = pawns[i].jobs;
                if (jobs == null)
                {
                    continue;
                }

                if (IsRopeJobFor(jobs.curJob, lord))
                {
                    found.Add((pawns[i], jobs.curJob));
                }

                for (int q = 0; q < jobs.jobQueue.Count; q++)
                {
                    if (IsRopeJobFor(jobs.jobQueue[q].job, lord))
                    {
                        found.Add((pawns[i], jobs.jobQueue[q].job));
                    }
                }
            }

            for (int i = 0; i < found.Count; i++)
            {
                found[i].Helper.jobs.EndCurrentOrQueuedJob(found[i].Job, JobCondition.InterruptForced, startNewJob: false);
            }
        }

        /// <summary>
        /// Determines whether a job is a helper's rope job for a caravan.
        /// </summary>
        private static bool IsRopeJobFor(Job job, Lord lord)
        {
            return job != null && job.def == CaravansCriticallyEnhancedDefOf.CCE_RopeAnimalToCaravan && job.lord == lord;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// Adds one fail condition ahead of the base toils: the caravan is gone, or has moved past
        /// gathering its animals. Ending the job runs the base finish action, which drops every rope.
        /// That matters in the collect stage, where vanilla only counts an animal as led when its
        /// roper belongs to the caravan, so a helper still holding one there would stall it.
        /// </para>
        /// <para>
        /// Not an iterator, so the condition is added when the toils are set up, before any of them
        /// runs, and the base method's own iterator is returned untouched.
        /// </para>
        /// </remarks>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(CaravanMovedOn);
            return base.MakeNewToils();
        }

        /// <summary>
        /// Determines whether the caravan this job works for has stopped gathering animals.
        /// </summary>
        /// <returns><c>true</c> when the job should end.</returns>
        /// <remarks>
        /// <para>
        /// A cancelled or departed caravan is caught by the lord leaving the map's list, the test
        /// vanilla's gather-items driver makes. Nothing on the lord itself says it is gone:
        /// <c>Lord.Cleanup</c> leaves its toil where it was and its member list as it was.
        /// </para>
        /// <para>
        /// A pause during gathering does not end the job: the animal already on the rope still needs
        /// to reach the spot, and the caravan resumes gathering after the pause.
        /// </para>
        /// </remarks>
        private bool CaravanMovedOn()
        {
            Lord lord = job.lord;
            return lord == null
                || !Map.lordManager.lords.Contains(lord)
                || FormingStages.WorkingStageOf(FormingLords.StageOf(lord)) != FormingStage.GatherAnimals;
        }
    }
}
