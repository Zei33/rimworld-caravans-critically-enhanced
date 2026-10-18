using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// What the mod knows about one forming caravan's exit.
    /// </summary>
    /// <remarks>
    /// The public fields but <see cref="Passed"/> and <see cref="ExitThreatened"/> are saved with the lord job (<see cref="CaravanExitStates.Expose"/>), as
    /// <c>cce_exit*</c> nodes vanilla ignores if the mod is removed. Fields rather than properties,
    /// because <c>Scribe_Values.Look</c> takes them by reference.
    /// </remarks>
    public sealed class CaravanExitState
    {
        /// <summary>Whether the fields below have been set up for this caravan.</summary>
        public bool Initialised;

        /// <summary>Whether the caravan is held at its spot because no exit suits every allowed area.</summary>
        public bool Holding;

        /// <summary>The exit the mod last wrote, or vanilla's at the start; anything else means another mod wrote it.</summary>
        public IntVec3 LastWrittenSpot = IntVec3.Invalid;

        /// <summary>Whether the player sent the caravan off regardless, with Force depart.</summary>
        public bool PlayerOverride;

        /// <summary>Whether another mod has written the exit, so the mod keeps out of it for good.</summary>
        public bool Foreign;

        /// <summary>Whether the mod wrote the exit while the caravan waited, so its downed pawns may need gathering again.</summary>
        public bool RegatherPending;

        /// <summary>The tick of the last move away from a threat, or -1 for none, for the cooldown.</summary>
        public int LastThreatMoveTick = -1;

        /// <summary>
        /// Whether the last pass found the exit the caravan keeps threatened. Not saved: the first pass
        /// after a load sets it again.
        /// </summary>
        public bool ExitThreatened;

        /// <summary>
        /// Whether a pass has run since this state was made. Not saved, so it is false after a load,
        /// which is what <see cref="CaravanExitStates.NeedsFirstPass"/> asks.
        /// </summary>
        public bool Passed;

        /// <summary>
        /// Gets the pawns whose areas hold the caravan, from the latest plan that could name them. Not
        /// saved: the first pass after a load fills it again.
        /// </summary>
        public List<Pawn> BlockingPawns { get; } = new List<Pawn>();
    }

    /// <summary>
    /// The exit state of every forming caravan, kept beside its lord job.
    /// </summary>
    /// <remarks>
    /// A <c>ConditionalWeakTable</c>, so an entry goes when its lord job does and nothing has to be
    /// cleaned up, and there is no component for the save to name.
    /// </remarks>
    public static class CaravanExitStates
    {
        private static readonly ConditionalWeakTable<LordJob_FormAndSendCaravan, CaravanExitState> States =
            new ConditionalWeakTable<LordJob_FormAndSendCaravan, CaravanExitState>();

        /// <summary>
        /// Gets a caravan's state, creating it when there is none.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <returns>Its state.</returns>
        public static CaravanExitState For(LordJob_FormAndSendCaravan job)
        {
            return States.GetValue(job, _ => new CaravanExitState());
        }

        /// <summary>
        /// Determines whether a caravan is being held, without creating a state for it.
        /// </summary>
        /// <param name="job">The caravan's lord job, or <c>null</c>.</param>
        /// <returns><c>true</c> when it is held.</returns>
        public static bool IsHolding(LordJob_FormAndSendCaravan job)
        {
            return job != null && States.TryGetValue(job, out CaravanExitState state) && state.Holding;
        }

        /// <summary>
        /// Determines whether the exit a caravan keeps was threatened at its last pass, without creating
        /// a state for it.
        /// </summary>
        /// <param name="job">The caravan's lord job, or <c>null</c>.</param>
        /// <returns><c>true</c> when it was.</returns>
        public static bool IsExitThreatened(LordJob_FormAndSendCaravan job)
        {
            return job != null && States.TryGetValue(job, out CaravanExitState state) && state.ExitThreatened;
        }

        /// <summary>
        /// Determines whether a held caravan has had no pass since it was loaded, without creating a
        /// state for it.
        /// </summary>
        /// <param name="job">The caravan's lord job, or <c>null</c>.</param>
        /// <returns><c>true</c> when it is held and its toils may still point at the fallback.</returns>
        public static bool NeedsFirstPass(LordJob_FormAndSendCaravan job)
        {
            return job != null && States.TryGetValue(job, out CaravanExitState state) && state.Holding && !state.Passed;
        }

        /// <summary>
        /// Saves or loads a caravan's state with its lord job.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        public static void Expose(LordJob_FormAndSendCaravan job)
        {
            CaravanExitState state = For(job);
            Scribe_Values.Look(ref state.Initialised, "cce_exitInitialised");
            Scribe_Values.Look(ref state.Holding, "cce_exitHolding");
            Scribe_Values.Look(ref state.LastWrittenSpot, "cce_exitLastWritten", IntVec3.Invalid);
            Scribe_Values.Look(ref state.PlayerOverride, "cce_exitOverride");
            Scribe_Values.Look(ref state.Foreign, "cce_exitForeign");
            Scribe_Values.Look(ref state.RegatherPending, "cce_exitRegather");
            Scribe_Values.Look(ref state.LastThreatMoveTick, "cce_exitThreatMoved", -1);
        }
    }
}
