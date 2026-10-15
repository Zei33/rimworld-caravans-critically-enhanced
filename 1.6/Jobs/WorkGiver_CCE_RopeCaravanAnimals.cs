using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// Lets a pawn who is not going with a caravan lead its pen animals to the gathering spot. The Load
    /// work type's second giver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In vanilla only the caravan's own colonists fetch its animals, so fifteen muffalo grazing
    /// across the map hold a caravan of two colonists up for a long time. This giver offers
    /// the same job to everyone else with the Load work type switched on, during the gathering stage
    /// only, and ties each animal at the spot exactly as a member would.
    /// </para>
    /// <para>
    /// It cannot hand out vanilla's own rope job. <c>JobDriver_PrepareCaravan_GatherAnimals</c> reads
    /// the gathering spot from the roper's duty, and a helper has none, so this giver uses its own job,
    /// <see cref="JobDriver_CCE_RopeAnimalToCaravan"/>, which carries the spot as its second target.
    /// </para>
    /// <para>
    /// The shape is the Load giver's: no thing request, <see cref="ShouldSkip"/> free when nothing is
    /// gathering, the set a <c>List&lt;Thing&gt;</c>, and one memoised decision behind both
    /// <see cref="HasJobOnThing"/> and <see cref="JobOnThing"/>.
    /// </para>
    /// </remarks>
    public class WorkGiver_CCE_RopeCaravanAnimals : WorkGiver_Scanner
    {
        private readonly JobMemo<Pawn, Thing, Job> memo;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkGiver_CCE_RopeCaravanAnimals"/> class.
        /// </summary>
        /// <remarks>
        /// Nothing else is cached against the memo's key, so there is nothing to forget when it changes.
        /// </remarks>
        public WorkGiver_CCE_RopeCaravanAnimals()
        {
            memo = new JobMemo<Pawn, Thing, Job>(BuildJob, () => { });
        }

        /// <inheritdoc />
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// A pawn in any lord is skipped, as in the Load giver, because its duty would overwrite the
        /// job's lord; caravan members are also already sent after the caravan's animals by their own
        /// duty. Only colonists rope, as in vanilla, where the gather duty's roping branch sits under
        /// <c>ThinkNode_ConditionalColonist</c>.
        /// </para>
        /// <para>
        /// With nothing gathering, the giver also lets go of what its memo held, which would otherwise
        /// keep the last save's map in memory after another is loaded.
        /// </para>
        /// </remarks>
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!pawn.IsColonist || pawn.GetLord() != null)
            {
                return true;
            }

            if (!CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.HelpersRopeAnimals) || !AnimalGatherIndex.AnyGathering(pawn.Map))
            {
                memo.Rekey(null, false, -1);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Has its own early exit, because the right-click menu asks for this set before it asks
        /// <see cref="ShouldSkip"/>.
        /// </remarks>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (!CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.HelpersRopeAnimals) || !AnimalGatherIndex.AnyGathering(pawn.Map))
            {
                return AnimalGatherIndex.None;
            }

            return AnimalGatherIndex.AnimalsOn(pawn.Map);
        }

        /// <inheritdoc />
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobFor(pawn, t, forced) != null;
        }

        /// <inheritdoc />
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobFor(pawn, t, forced);
        }

        /// <summary>
        /// The one decision behind both scan entry points.
        /// </summary>
        private Job JobFor(Pawn pawn, Thing t, bool forced)
        {
            return memo.Answer(pawn, forced, Find.TickManager.TicksGame, t);
        }

        /// <summary>
        /// Builds the helper's rope job for one animal, or refuses.
        /// </summary>
        /// <param name="pawn">The helper.</param>
        /// <param name="t">The animal.</param>
        /// <param name="forced">Whether the player ordered it from the right-click menu.</param>
        /// <returns>The rope job, or <c>null</c>.</returns>
        /// <remarks>
        /// The tests run cheapest first. The reachability test is vanilla's own,
        /// <c>CanRoperTakeAnimalToDest</c>, which also checks that the animal can be walked through
        /// the fences on the way. A refusal's reason is built only for a forced ask, which is how the
        /// right-click menu, the one place that shows it, always asks. Unlike most givers, a forced ask
        /// does not take over another pawn's reservation; the comment at that check says why.
        /// </remarks>
        private Job BuildJob(Pawn pawn, Thing t, bool forced)
        {
            if (!AnimalGatherIndex.TryGetDestination(pawn.Map, t, out Lord lord, out IntVec3 spot))
            {
                return null;
            }

            if (spot.IsForbidden(pawn))
            {
                if (forced)
                {
                    JobFailReason.Is("CaravansCriticallyEnhanced_SpotOutsideArea".Translate());
                }

                return null;
            }

            // Never a forced takeover. An animal reserved by somebody is one a member or another helper
            // is walking to, and taking it over ends that pawn's rope job, which lets go of every
            // animal it is already leading, wherever it is.
            if (!pawn.CanReserve(t))
            {
                Pawn reserver = forced ? pawn.Map.reservationManager.FirstRespectedReserver(t, pawn) : null;
                if (reserver != null)
                {
                    JobFailReason.Is("ReservedBy".Translate(reserver.LabelShort, reserver));
                }

                return null;
            }

            Pawn animal = (Pawn)t;
            if (!GatherAnimalsAndSlavesForCaravanUtility.CanRoperTakeAnimalToDest(pawn, animal, spot))
            {
                if (forced)
                {
                    JobFailReason.Is("NoPath".Translate());
                }

                return null;
            }

            Job job = JobMaker.MakeJob(CaravansCriticallyEnhancedDefOf.CCE_RopeAnimalToCaravan, animal, spot);
            job.lord = lord;
            return job;
        }
    }
}
