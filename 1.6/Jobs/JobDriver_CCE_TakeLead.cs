using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// A drafted caravan colonist walks to an animal of its caravan and takes its lead.
    /// </summary>
    /// <remarks>
    /// Vanilla's own roping toils, so the walk to the rope cell and the roping itself behave as a
    /// vanilla roper's. The job fails if the colonist is undrafted, if the animal leaves the
    /// colonist's caravan, or if somebody else takes its lead first. Once it is roped the job ends and
    /// the colonist simply holds the lead, as the rope patches allow while it is drafted.
    /// </remarks>
    public class JobDriver_CCE_TakeLead : JobDriver
    {
        private Pawn Animal => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        /// <inheritdoc />
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        /// <inheritdoc />
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !pawn.Drafted);
            this.FailOn(AnimalNoLongerTakeable);
            yield return Toils_Rope.GotoRopeAttachmentInteractionCell(TargetIndex.A);
            yield return Toils_Rope.RopePawn(TargetIndex.A);
        }

        /// <summary>
        /// Determines whether the animal has left the colonist's caravan or somebody else holds it.
        /// </summary>
        private bool AnimalNoLongerTakeable()
        {
            Pawn animal = Animal;
            if (animal?.roping == null)
            {
                return true;
            }

            if (animal.roping.RopedByPawn == pawn)
            {
                return false;
            }

            return FormingLords.VanillaLordOf(animal) == null
                || animal.GetLord() != pawn.GetLord()
                || !LeadUtility.CanBeTaken(animal);
        }
    }
}
