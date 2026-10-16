using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// Carries what the formation window knows to the exit search inside it, and the result on to the
    /// new caravan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Dialog_FormCaravan.TryFormAndSendCaravan</c> checks the caravan for errors, then asks
    /// <c>RCellFinder.TryFindClosestEdgeCellTo</c> for an exit, then picks the meeting point and checks
    /// every pen animal can be led to both, then calls <c>CaravanFormingUtility.StartFormingCaravan</c>.
    /// Planning in the dialog's prefix would run before the error check, so the prefix only opens this,
    /// the exit search plans, and the start of the caravan reads the result. The dialog's finalizer
    /// closes it whatever happened, so nothing leaks into a later search.
    /// </para>
    /// <para>
    /// The search is answered once per send: vanilla calls it once, and anything else calling it in
    /// between gets vanilla's answer.
    /// </para>
    /// </remarks>
    public static class ExitHandoff
    {
        /// <summary>Gets the map of the caravan being sent, or <c>null</c> when no send is under way.</summary>
        public static Map Map { get; private set; }

        /// <summary>Gets the window's list, which names every pawn being sent.</summary>
        public static List<TransferableOneWay> Transferables { get; private set; }

        /// <summary>Gets the first tile of the chosen world route.</summary>
        public static PlanetTile StartingTile { get; private set; }

        /// <summary>Gets or sets whether the exit search has already been answered for this send.</summary>
        public static bool Planned { get; set; }

        /// <summary>Gets or sets whether the caravan has to be held as soon as it starts, because no exit suits every area.</summary>
        public static bool HoldOnStart { get; set; }

        /// <summary>Gets the pawns whose areas hold it.</summary>
        public static List<Pawn> Blocking { get; } = new List<Pawn>();

        /// <summary>
        /// Opens the handoff for a send.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="transferables">The window's list.</param>
        /// <param name="startingTile">The first tile of the route.</param>
        public static void Open(Map map, List<TransferableOneWay> transferables, PlanetTile startingTile)
        {
            Close();
            Map = map;
            Transferables = transferables;
            StartingTile = startingTile;
        }

        /// <summary>
        /// Closes the handoff.
        /// </summary>
        public static void Close()
        {
            Map = null;
            Transferables = null;
            StartingTile = PlanetTile.Invalid;
            Planned = false;
            HoldOnStart = false;
            Blocking.Clear();
        }
    }
}
