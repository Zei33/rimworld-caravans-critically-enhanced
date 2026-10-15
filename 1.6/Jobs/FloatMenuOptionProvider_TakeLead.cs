using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// The right-click option that has a drafted caravan colonist take the lead of a caravan animal
    /// nobody is holding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game builds one instance of every provider by reflection and asks it once per click with
    /// every selected pawn, so the order goes to the nearest selected drafted colonist that can do
    /// it: a member of the animal's caravan, with a cell beside the animal to tie a rope from, able
    /// to reserve and reach it (<see cref="LeadPolicy.TakeLead"/>). If none can, the option is shown
    /// greyed with the reason from the nearest.
    /// </para>
    /// <para>
    /// Only an animal that has to be led and could keep the lead is offered
    /// (<see cref="LeadPolicy.CanBeTaken"/>): an animal that walks by itself, once tied at the spot,
    /// would never be untied, and a downed, broken, starving or exhausted one would be let go again
    /// at once.
    /// </para>
    /// <para>
    /// Only <c>GetSingleOptionFor(Pawn)</c> is overridden: a clicked pawn is listed among both the
    /// clicked things and the clicked pawns, and answering both would show the option twice. The
    /// option is not shown at all for an animal somebody is already leading.
    /// </para>
    /// </remarks>
    public class FloatMenuOptionProvider_TakeLead : FloatMenuOptionProvider
    {
        /// <inheritdoc />
        protected override bool Drafted => true;

        /// <inheritdoc />
        protected override bool Undrafted => false;

        /// <inheritdoc />
        protected override bool Multiselect => true;

        /// <inheritdoc />
        protected override bool RequiresManipulation => true;

        /// <inheritdoc />
        protected override bool AppliesInt(FloatMenuContext context)
        {
            return CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting);
        }

        /// <inheritdoc />
        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Lord lord = FormingLords.VanillaLordOf(clickedPawn);
            if (lord == null || !clickedPawn.RaceProps.Animal || clickedPawn.roping == null || !LeadUtility.CanBeTaken(clickedPawn))
            {
                return null;
            }

            string label = "CaravansCriticallyEnhanced_TakeLead".Translate(clickedPawn.LabelShort, clickedPawn);
            TakeLeadBlocker firstBlocker = TakeLeadBlocker.None;
            Pawn blockedPawn = null;

            foreach (Pawn pawn in context.ValidSelectedPawns.OrderBy(p => p.Position.DistanceToSquared(clickedPawn.Position)))
            {
                TakeLeadBlocker blocker = LeadPolicy.TakeLead(
                    pawn.GetLord() == lord,
                    AnimalPenUtility.RopeAttachmentInteractionCell(pawn, clickedPawn).IsValid,
                    pawn.CanReserve(clickedPawn),
                    pawn.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly));

                if (blocker == TakeLeadBlocker.None)
                {
                    Pawn taker = pawn;
                    return FloatMenuUtility.DecoratePrioritizedTask(
                        new FloatMenuOption(label, () => taker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(CaravansCriticallyEnhancedDefOf.CCE_TakeLead, clickedPawn), JobTag.Misc)),
                        taker,
                        clickedPawn);
                }

                if (blockedPawn == null)
                {
                    firstBlocker = blocker;
                    blockedPawn = pawn;
                }
            }

            return blockedPawn == null ? null : new FloatMenuOption(label + ": " + ReasonFor(firstBlocker, blockedPawn, clickedPawn), null);
        }

        private static string ReasonFor(TakeLeadBlocker blocker, Pawn pawn, Pawn animal)
        {
            switch (blocker)
            {
                case TakeLeadBlocker.NotInCaravan:
                    return "CaravansCriticallyEnhanced_TakeLeadNotMember".Translate();
                case TakeLeadBlocker.Reserved:
                    Pawn reserver = pawn.Map.reservationManager.FirstRespectedReserver(animal, pawn);
                    return reserver != null ? "ReservedBy".Translate(reserver.LabelShort, reserver).Resolve() : "Reserved".Translate().Resolve();
                default:
                    return "NoPath".Translate();
            }
        }
    }
}
