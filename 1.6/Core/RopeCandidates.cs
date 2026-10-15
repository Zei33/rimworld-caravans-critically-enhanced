namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Decides which caravan animals a helper may lead to the gathering spot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same animals vanilla's own members fetch through
    /// <c>JobGiver_PrepareCaravan_GatherPawns.DoesAnimalNeedGathering</c>: pen animals that have to be
    /// led on a rope, not already on somebody's rope, and not already tied at the spot. Elephants,
    /// dogs and anything else that walks to the spot by itself are left alone, as in vanilla.
    /// </para>
    /// <para>
    /// Members and helpers never contend for one animal, because both sides require it not to be
    /// on a rope and both reserve it before taking it.
    /// </para>
    /// </remarks>
    public static class RopeCandidates
    {
        /// <summary>
        /// Determines whether an animal in a forming caravan is waiting to be led to the gathering spot.
        /// </summary>
        /// <param name="stage">The caravan's stage.</param>
        /// <param name="needsRope">Whether the animal has to be led on a rope, <c>AnimalPenUtility.NeedsToBeManagedByRope</c>.</param>
        /// <param name="onSomebodysRope">Whether a pawn is already leading it.</param>
        /// <param name="tiedAtSpot">Whether it is tied up at the spot it is being gathered to.</param>
        /// <param name="hasGatherDuty">Whether its duty is vanilla's gather-animals duty, which names the spot.</param>
        /// <returns><c>true</c> when a helper may offer to lead it.</returns>
        /// <remarks>
        /// Only during the gathering stage itself, not its pause. A member's mental break pauses the
        /// caravan, vanilla's members stop fetching, and a helper starting then would be the only one
        /// working while the caravan waits.
        /// </remarks>
        public static bool NeedsLeading(FormingStage stage, bool needsRope, bool onSomebodysRope, bool tiedAtSpot, bool hasGatherDuty)
        {
            return stage == FormingStage.GatherAnimals && needsRope && !onSomebodysRope && !tiedAtSpot && hasGatherDuty;
        }
    }
}
