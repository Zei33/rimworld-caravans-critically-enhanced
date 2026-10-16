using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// Reads and writes the private fields of the vanilla caravan forming lord and its toils.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every field this mod reaches into is named here once, as a <c>const</c>, so the tests can pin
    /// the same string the accessor uses. A field Ludeon renames is then a failing test on the day the
    /// game updates.
    /// </para>
    /// <para>
    /// Each accessor is resolved on first use and on its own. If one field has gone, that accessor
    /// logs once and reads as null, and the features that need it switch themselves off; the others
    /// keep working. Resolving all five in one static initialiser would have let a single rename
    /// throw <c>TypeInitializationException</c> from every one of them for the rest of the session.
    /// </para>
    /// <para>
    /// The accessors are only ever used on a lord for which <c>FormingLords.IsVanilla</c> holds, and
    /// on that lord's own toils. A subclass lord's graph is built by its own <c>CreateGraph</c> and
    /// these fields may not mean the same thing there.
    /// </para>
    /// <para>
    /// The exit cell lives in three kinds of place, and moving it means writing all three. The lord
    /// job's <c>exitSpot</c> is saved, and <c>Lord.ExposeData</c> rebuilds the graph from it on load,
    /// so the toils' own copies (<c>LordToil_PrepareCaravan_Leave.exitSpot</c>,
    /// <c>LordToil_PrepareCaravan_GatherDownedPawns.exitSpot</c>, and
    /// <c>LordToil_PrepareCaravan_CollectAnimals</c>' <c>destinationPoint</c>) are not saved and have
    /// to be written for the live graph. And every member's <c>PawnDuty</c> carries the cell as its
    /// focus, is saved with the pawn, and is what the duty's job givers actually walk to
    /// (<c>JobGiver_PrepareCaravan_GatherDownedPawns</c> reads <c>duty.focusSecond</c>), so the
    /// current toil's <c>UpdateAllDuties</c> has to run after the fields are written.
    /// </para>
    /// <para>
    /// The meeting point is read only. The mod never moves it, and three toils hold private copies of
    /// it (gather items, gather downed pawns, wait) that have no accessor here. It is where the
    /// caravan gathers: a caravan hitching spot when one can reach the exit, otherwise a spot just
    /// outside the colony that <c>Dialog_FormCaravan.TryFindRandomPackingSpot</c> picks.
    /// </para>
    /// </remarks>
    public static class CaravanLordAccess
    {
        /// <summary>The name of the exit cell field, on the lord job and on two of its toils.</summary>
        public const string ExitSpotField = "exitSpot";

        /// <summary>The name of the meeting point field on the lord job.</summary>
        public const string MeetingPointField = "meetingPoint";

        /// <summary>The name of the rope toils' destination field.</summary>
        public const string DestinationPointField = "destinationPoint";

        /// <summary>
        /// The name of the lord job's private first tile of the world route, which faces the edge the
        /// caravan would rather leave by.
        /// </summary>
        public const string StartingTileField = "startingTile";

        /// <summary>The <c>Log.ErrorOnce</c> key base for an accessor that cannot be resolved.</summary>
        public const int ErrorKeyBase = 0x43434510;

        private static readonly Lazy<AccessTools.FieldRef<LordJob_FormAndSendCaravan, IntVec3>> jobExitSpot =
            Accessor<LordJob_FormAndSendCaravan>(ExitSpotField, 0);

        private static readonly Lazy<AccessTools.FieldRef<LordJob_FormAndSendCaravan, PlanetTile>> jobStartingTile =
            new Lazy<AccessTools.FieldRef<LordJob_FormAndSendCaravan, PlanetTile>>(() => Resolve<LordJob_FormAndSendCaravan, PlanetTile>(StartingTileField, 5));

        private static readonly Lazy<AccessTools.FieldRef<LordJob_FormAndSendCaravan, IntVec3>> jobMeetingPoint =
            Accessor<LordJob_FormAndSendCaravan>(MeetingPointField, 1);

        private static readonly Lazy<AccessTools.FieldRef<LordToil_PrepareCaravan_Leave, IntVec3>> leaveExitSpot =
            Accessor<LordToil_PrepareCaravan_Leave>(ExitSpotField, 2);

        private static readonly Lazy<AccessTools.FieldRef<LordToil_PrepareCaravan_GatherDownedPawns, IntVec3>> gatherDownedPawnsExitSpot =
            Accessor<LordToil_PrepareCaravan_GatherDownedPawns>(ExitSpotField, 3);

        private static readonly Lazy<AccessTools.FieldRef<LordToil_PrepareCaravan_RopeAnimals, IntVec3>> ropeDestinationPoint =
            Accessor<LordToil_PrepareCaravan_RopeAnimals>(DestinationPointField, 4);

        /// <summary>
        /// The lord job's exit cell, or null if the field has gone. The public <c>ExitSpot</c> getter
        /// reads it too; this is for writing.
        /// </summary>
        public static AccessTools.FieldRef<LordJob_FormAndSendCaravan, IntVec3> JobExitSpot => jobExitSpot.Value;

        /// <summary>
        /// The lord job's meeting point, where animals and items gather, or null if the field has gone.
        /// </summary>
        public static AccessTools.FieldRef<LordJob_FormAndSendCaravan, IntVec3> JobMeetingPoint => jobMeetingPoint.Value;

        /// <summary>
        /// The lord job's first tile of the world route, or null if the field has gone. Read only, to
        /// break ties between equally good exits.
        /// </summary>
        public static AccessTools.FieldRef<LordJob_FormAndSendCaravan, PlanetTile> JobStartingTile => jobStartingTile.Value;

        /// <summary>
        /// The leave toil's copy of the exit cell, which it tests arrival against, or null.
        /// </summary>
        public static AccessTools.FieldRef<LordToil_PrepareCaravan_Leave, IntVec3> LeaveExitSpot => leaveExitSpot.Value;

        /// <summary>
        /// The downed-pawn toil's copy of the exit cell, which it hands to its duties, or null.
        /// </summary>
        public static AccessTools.FieldRef<LordToil_PrepareCaravan_GatherDownedPawns, IntVec3> GatherDownedPawnsExitSpot => gatherDownedPawnsExitSpot.Value;

        /// <summary>
        /// Where a rope toil leads its animals: the meeting point while gathering, the exit while
        /// collecting. Null if the field has gone.
        /// </summary>
        public static AccessTools.FieldRef<LordToil_PrepareCaravan_RopeAnimals, IntVec3> RopeDestinationPoint => ropeDestinationPoint.Value;

        /// <summary>
        /// Gets whether every accessor resolved, which a feature that writes the exit asks before
        /// doing anything.
        /// </summary>
        public static bool AllExitFieldsAvailable =>
            JobExitSpot != null && JobMeetingPoint != null && LeaveExitSpot != null && GatherDownedPawnsExitSpot != null && RopeDestinationPoint != null;

        private static Lazy<AccessTools.FieldRef<T, IntVec3>> Accessor<T>(string field, int index)
            where T : class
        {
            return new Lazy<AccessTools.FieldRef<T, IntVec3>>(() => Resolve<T, IntVec3>(field, index));
        }

        private static AccessTools.FieldRef<T, TValue> Resolve<T, TValue>(string field, int index)
            where T : class
        {
            try
            {
                return AccessTools.FieldRefAccess<T, TValue>(field);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce(
                    "[CaravansCriticallyEnhanced] " + typeof(T).Name + "." + field + " is gone, so the features that move a caravan's exit are off: " + exception.Message,
                    ErrorKeyBase + index);
                return null;
            }
        }
    }
}
