namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The extra limits on which caravan member a helper may hand an item to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A helper is a pawn that is not in the caravan and loads it through the Load work type.
    /// Vanilla's <c>JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier</c> decides who may carry,
    /// and its <c>allowColonists</c> branch accepts any colonist member with no weight check at all,
    /// so a helper falling back to a colonist could load one far past its capacity. Being over
    /// capacity does not slow a pawn on the map, but it is mass the caravan has to carry once it
    /// leaves, and it switches the member's own gather jobs to carrying items in its hands. That was
    /// rare in vanilla, because vanilla's helper never started without a pack animal or prisoner with
    /// room. Here, where a caravan of colonists alone is the case being fixed, it is the common case.
    /// </para>
    /// <para>
    /// This only ever narrows vanilla's answer. The postfix that applies it runs only when vanilla
    /// said yes, only for a helper, and only on a lord <see cref="FormingLords.IsVanilla(Verse.AI.Group.Lord)"/>
    /// accepts. Members choosing carriers for themselves are untouched.
    /// </para>
    /// </remarks>
    public static class CarrierRule
    {
        /// <summary>
        /// Decides whether a caravan member vanilla accepted as a carrier may take a helper's item.
        /// </summary>
        /// <param name="stage">Where the caravan has got to.</param>
        /// <param name="carrierIsColonist">Whether the member is a colonist, as <c>Pawn.IsColonist</c> answers.</param>
        /// <param name="carrierUnavailable">Whether the member is drafted, in a mental state, or outside the helper's allowed area.</param>
        /// <param name="carrierOverEncumbered">Whether the member already carries more than its capacity.</param>
        /// <param name="carrierIsRopeAnimal">Whether the member is an animal that has to be led on a rope.</param>
        /// <param name="carrierTiedAtSpot">Whether that animal is tied up, which during gathering means at the hitching spot.</param>
        /// <param name="colonistCarriers">Whether the "Load into colonists" setting is on.</param>
        /// <returns><c>true</c> when the helper may give the member the item.</returns>
        /// <remarks>
        /// While animals are still being gathered, colonist members are out fetching them, and a rope
        /// animal still on its way would drag the helper round the map. Both wait until gathering ends,
        /// or, for an animal, until it is tied up.
        /// <para>
        /// A drafted member or one in a mental state is never a carrier for a helper. Vanilla's
        /// <c>allowColonists</c> branch does not ask, so without this a helper would follow a drafted
        /// colonist through a fight, or walk up to a berserk one, to hand over a stack of steel. Nor is
        /// a member standing outside the helper's allowed area: vanilla's carrier choice never asks
        /// about areas, so this is what keeps a helper inside its own.
        /// </para>
        /// </remarks>
        public static bool Allows(
            FormingStage stage,
            bool carrierIsColonist,
            bool carrierUnavailable,
            bool carrierOverEncumbered,
            bool carrierIsRopeAnimal,
            bool carrierTiedAtSpot,
            bool colonistCarriers)
        {
            if (carrierUnavailable)
            {
                return false;
            }

            bool gatheringAnimals = FormingStages.WorkingStageOf(stage) == FormingStage.GatherAnimals;

            if (carrierIsColonist)
            {
                return colonistCarriers && !carrierOverEncumbered && !gatheringAnimals;
            }

            if (carrierIsRopeAnimal && gatheringAnimals)
            {
                return carrierTiedAtSpot;
            }

            return true;
        }
    }
}
