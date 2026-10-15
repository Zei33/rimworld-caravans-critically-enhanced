namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Where a forming caravan has got to, named after the vanilla lord toil it is in.
    /// </summary>
    /// <remarks>
    /// The numeric values are the toil's index in the graph <c>LordJob_FormAndSendCaravan.CreateGraph</c>
    /// builds, and that is what makes them safe to rely on. <c>Lord.ExposeData</c> saves the current toil
    /// as <c>curLordToilIdx</c>, its index in <c>graph.lordToils</c>, and restores it by the same index
    /// on load, so the order is part of every save the game has ever written; changing it would need a
    /// save converter, and none exists for this lord job. <c>Tests/FormingStagesTests.cs</c> reads the order out of the shipped
    /// assembly's IL and fails if it moves.
    /// </remarks>
    public enum FormingStage
    {
        /// <summary>Not a toil of the vanilla graph, or no lord at all.</summary>
        Unknown = -1,

        /// <summary><c>LordToil_PrepareCaravan_GatherAnimals</c>: animals are being led to the meeting point.</summary>
        GatherAnimals = 0,

        /// <summary>The pause after <see cref="GatherAnimals"/>, entered while a member is in a mental state.</summary>
        GatherAnimalsPause = 1,

        /// <summary><c>LordToil_PrepareCaravan_GatherItems</c>: items are being loaded.</summary>
        GatherItems = 2,

        /// <summary>The pause after <see cref="GatherItems"/>.</summary>
        GatherItemsPause = 3,

        /// <summary><c>LordToil_PrepareCaravan_GatherDownedPawns</c>: downed pawns are carried to the exit.</summary>
        GatherDownedPawns = 4,

        /// <summary>The pause after <see cref="GatherDownedPawns"/>.</summary>
        GatherDownedPawnsPause = 5,

        /// <summary><c>LordToil_PrepareCaravan_Wait</c>: waiting for very tired sleepers to wake.</summary>
        Wait = 6,

        /// <summary>The pause after <see cref="Wait"/>, the one pause no field of the lord job holds.</summary>
        WaitPause = 7,

        /// <summary><c>LordToil_PrepareCaravan_CollectAnimals</c>: members take up the leads of the pen animals.</summary>
        CollectAnimals = 8,

        /// <summary>The pause after <see cref="CollectAnimals"/>.</summary>
        CollectAnimalsPause = 9,

        /// <summary><c>LordToil_PrepareCaravan_Leave</c>: walking to the exit spot.</summary>
        Leave = 10,

        /// <summary>The pause after <see cref="Leave"/>, which unpauses into <see cref="CollectAnimals"/> rather than back into <see cref="Leave"/>.</summary>
        LeavePause = 11,

        /// <summary><c>LordToil_End</c>: the caravan has been sent.</summary>
        End = 12,
    }

    /// <summary>
    /// Pure questions about a <see cref="FormingStage"/>.
    /// </summary>
    public static class FormingStages
    {
        /// <summary>
        /// How many toils the vanilla graph holds.
        /// </summary>
        public const int ToilCount = 13;

        /// <summary>
        /// Names the stage a toil index stands for.
        /// </summary>
        /// <param name="index">The current toil's index in <c>lord.Graph.lordToils</c>.</param>
        /// <returns>The stage, or <see cref="FormingStage.Unknown"/> for an index outside the graph.</returns>
        /// <remarks>
        /// <c>IndexOf</c> returns -1 for a toil that is not in the list, which this maps to
        /// <see cref="FormingStage.Unknown"/> along with anything past the end. Nothing in vanilla adds a
        /// toil to a player lord after <c>CreateGraph</c>: the panic flee toil <c>Lord.SetJob</c> appends
        /// is gated on a non-player faction and on <c>AddFleeToil</c>, which this lord job overrides to
        /// false.
        /// </remarks>
        public static FormingStage FromToilIndex(int index)
        {
            return index >= 0 && index < ToilCount ? (FormingStage)index : FormingStage.Unknown;
        }

        /// <summary>
        /// Determines whether a stage is one of the six pause toils.
        /// </summary>
        /// <param name="stage">The stage.</param>
        /// <returns><c>true</c> for a pause.</returns>
        public static bool IsPause(FormingStage stage)
        {
            switch (stage)
            {
                case FormingStage.GatherAnimalsPause:
                case FormingStage.GatherItemsPause:
                case FormingStage.GatherDownedPawnsPause:
                case FormingStage.WaitPause:
                case FormingStage.CollectAnimalsPause:
                case FormingStage.LeavePause:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Names the working toil a pause belongs to.
        /// </summary>
        /// <param name="stage">The stage.</param>
        /// <returns>
        /// The toil the pause sits after in the graph, or the stage itself when it is not a pause.
        /// </returns>
        /// <remarks>
        /// <see cref="FormingStage.LeavePause"/> answers <see cref="FormingStage.Leave"/>, the toil it
        /// interrupted, even though vanilla's unpause transition goes to
        /// <see cref="FormingStage.CollectAnimals"/>. This is about where the caravan had got to, not
        /// where it goes next.
        /// </remarks>
        public static FormingStage WorkingStageOf(FormingStage stage)
        {
            return IsPause(stage) ? stage - 1 : stage;
        }

        /// <summary>
        /// Determines whether pawns outside the caravan may load items onto it at this stage.
        /// </summary>
        /// <param name="stage">The stage.</param>
        /// <param name="earlyLoading">Whether the early loading setting is on.</param>
        /// <returns>
        /// <c>true</c> while items are being gathered, and also while animals are being gathered when
        /// <paramref name="earlyLoading"/> is set.
        /// </returns>
        /// <remarks>
        /// Never during a pause. Vanilla's helper asks <c>LordJob_FormAndSendCaravan.GatheringItemsNow</c>,
        /// which is the gather-items toil exactly, so a caravan paused for a mental break takes no help in
        /// vanilla either.
        /// </remarks>
        public static bool OpenForLoading(FormingStage stage, bool earlyLoading)
        {
            return stage == FormingStage.GatherItems || (earlyLoading && stage == FormingStage.GatherAnimals);
        }
    }
}
