using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Which caravan, and which row of its list, an item a helper might fetch belongs to.
    /// </summary>
    public sealed class LoadingClaim
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoadingClaim"/> class.
        /// </summary>
        /// <param name="lord">The forming caravan.</param>
        /// <param name="row">The row of its item list the thing is in.</param>
        public LoadingClaim(Lord lord, TransferableOneWay row)
        {
            Lord = lord;
            Row = row;
        }

        /// <summary>Gets the forming caravan.</summary>
        public Lord Lord { get; }

        /// <summary>Gets the row of its item list.</summary>
        public TransferableOneWay Row { get; }
    }

    /// <summary>
    /// The items helpers may fetch on a map, rebuilt at most once per tick and once per frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keyed on the game tick and the frame together. The tick alone goes stale while the game is
    /// paused, and the float menu, which asks for this set twice per right-click through
    /// <c>FloatMenuOptionProvider_WorkGivers.ScannerShouldSkip</c>, runs exactly then; a player who
    /// removes an item from the caravan while paused would otherwise be offered the old one. The frame
    /// alone rebuilds once per frame while running at speed, when several ticks pass per frame and
    /// pawns search on each.
    /// </para>
    /// <para>
    /// Held per map in a <see cref="ConditionalWeakTable{TKey, TValue}"/>, so a map that goes away
    /// takes its index with it, and nothing is saved.
    /// </para>
    /// </remarks>
    public static class LoadingIndex
    {
        /// <summary>
        /// The empty search set, shared. Nothing may add to it; the scanner only reads its set.
        /// </summary>
        public static readonly List<Thing> None = new List<Thing>();

        private static readonly ConditionalWeakTable<Map, MapIndex> Indices = new ConditionalWeakTable<Map, MapIndex>();

        /// <summary>
        /// Determines whether any caravan on a map is taking help with loading right now.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="earlyLoading">Whether the early loading setting is on.</param>
        /// <returns><c>true</c> when at least one vanilla forming caravan is at a stage open to helpers.</returns>
        /// <remarks>
        /// Not cached and needs no cache: one pass over the map's lords, a type test and a list search
        /// of thirteen toils for each forming one. This is what makes the no-caravan case free in
        /// <c>ShouldSkip</c>.
        /// </remarks>
        public static bool AnyOpen(Map map, bool earlyLoading)
        {
            if (map == null)
            {
                return false;
            }

            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (IsOpen(lords[i], earlyLoading))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the items helpers may fetch on a map.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="earlyLoading">Whether the early loading setting is on.</param>
        /// <returns>The set, which callers must not change.</returns>
        public static List<Thing> ThingsOn(Map map, bool earlyLoading)
        {
            return map == null ? None : Fresh(map, earlyLoading).Things;
        }

        /// <summary>
        /// Finds which caravan and row an item in the set belongs to.
        /// </summary>
        /// <param name="map">The map the asking pawn is on.</param>
        /// <param name="thing">The item.</param>
        /// <param name="earlyLoading">Whether the early loading setting is on.</param>
        /// <returns>The claim, or <c>null</c> when the item is not in the set.</returns>
        public static LoadingClaim ClaimOf(Map map, Thing thing, bool earlyLoading)
        {
            if (map == null || thing == null)
            {
                return null;
            }

            return Fresh(map, earlyLoading).Owners.TryGetValue(thing, out LoadingClaim claim) ? claim : null;
        }

        private static bool IsOpen(Lord lord, bool earlyLoading)
        {
            return FormingLords.IsVanilla(lord) && FormingStages.OpenForLoading(FormingLords.StageOf(lord), earlyLoading);
        }

        private static MapIndex Fresh(Map map, bool earlyLoading)
        {
            MapIndex index = Indices.GetValue(map, _ => new MapIndex());
            int tick = Find.TickManager.TicksGame;
            int frame = Time.frameCount;

            if (index.Tick == tick && index.Frame == frame && index.EarlyLoading == earlyLoading)
            {
                return index;
            }

            index.Tick = tick;
            index.Frame = frame;
            index.EarlyLoading = earlyLoading;
            Rebuild(index, map, earlyLoading);
            return index;
        }

        /// <summary>
        /// Refills an index from the map's forming caravans.
        /// </summary>
        /// <remarks>
        /// Kept apart from <see cref="Fresh"/> because of the eligibility lambda: it captures the map,
        /// and the compiler allocates a captured variable's closure on entry to the method that
        /// declares it, cache hit or not. Here it is paid only on a rebuild.
        /// </remarks>
        private static void Rebuild(MapIndex index, Map map, bool earlyLoading)
        {
            index.Things.Clear();
            index.Owners.Clear();

            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                Lord lord = lords[i];
                if (!IsOpen(lord, earlyLoading))
                {
                    continue;
                }

                List<TransferableOneWay> rows = ((LordJob_FormAndSendCaravan)lord.LordJob).transferables;
                for (int r = 0; r < rows.Count; r++)
                {
                    TransferableOneWay row = rows[r];

                    // A null pawn counts every pawn's in-flight haul, so a row already fully covered by
                    // hauls in progress contributes nothing. Each pawn asks again for itself in
                    // BuildJob, where its own haul does not count against it.
                    if (GatherItemsForCaravanUtility.CountLeftToTransfer(null, row, lord) <= 0)
                    {
                        continue;
                    }

                    LoadingCandidates.Collect(new LoadingClaim(lord, row), row.things, t => CanBeFetchedOn(t, map), index.Things, index.Owners);
                }
            }
        }

        /// <summary>
        /// Whether a listed thing is somewhere a pawn on this map could pick it up.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Spawned on the map, or inside a haul source on it (a bookcase or an outfit stand), which
        /// <c>GenClosest.ClosestThing_Global</c> and the vanilla gather driver both handle. Things in a
        /// pawn's inventory or hands are skipped here, as vanilla's own search skips them.
        /// </para>
        /// <para>
        /// A haul source the player has closed is skipped too: an outfit stand defaults to not
        /// allowing items to be taken off it. Vanilla's region search for members skips those, and
        /// <c>ClosestThing_Global</c>, which the helpers' search uses, does not, so without this a
        /// helper would strip a stand a member would leave alone.
        /// </para>
        /// </remarks>
        private static bool CanBeFetchedOn(Thing thing, Map map)
        {
            if (thing.Destroyed)
            {
                return false;
            }

            if (thing.Spawned)
            {
                return thing.Map == map;
            }

            return thing.ParentHolder is IHaulSource source && source.HaulSourceEnabled && thing.MapHeld == map;
        }

        private sealed class MapIndex
        {
            public readonly List<Thing> Things = new List<Thing>();

            public readonly Dictionary<Thing, LoadingClaim> Owners = new Dictionary<Thing, LoadingClaim>();

            public int Tick = -1;

            public int Frame = -1;

            public bool EarlyLoading;
        }
    }
}
