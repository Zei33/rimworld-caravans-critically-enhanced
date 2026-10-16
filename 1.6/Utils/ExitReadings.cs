using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// The planner's readings of one map, and what each index in them stands for.
    /// </summary>
    public sealed class ExitReading
    {
        /// <summary>Gets the readings.</summary>
        public ExitPlanInput Input { get; } = new ExitPlanInput();

        /// <summary>Gets the edge cells, in the order of <see cref="ExitPlanInput.Candidates"/>.</summary>
        public List<IntVec3> Cells { get; } = new List<IntVec3>();

        /// <summary>Gets the movers, indexed by <see cref="MoverReading.Id"/>.</summary>
        public List<Pawn> Movers { get; } = new List<Pawn>();

        /// <summary>
        /// Lists the movers a plan names.
        /// </summary>
        /// <param name="ids">The ids.</param>
        /// <param name="into">The list to fill; it is cleared first.</param>
        public void PawnsOf(IList<int> ids, List<Pawn> into)
        {
            into.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                into.Add(Movers[ids[i]]);
            }
        }
    }

    /// <summary>
    /// Reads a map for <see cref="ExitPlanner"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reachability is one region search per group of movers that pass the same doors and fences
    /// from the same region, never a path test per cell. A region search answers what
    /// <c>Region.Allows</c> lets a pawn into: at <c>Danger.Deadly</c> it reads the fence rule, the
    /// pawn's doors, forbidden doors, its avoid grid and door hostility, and neither drafting nor
    /// colonist status, so the key is the start region, the fence rule, whether the pawn opens
    /// doors, whether it minds forbidden doors, and its faction.
    /// </para>
    /// <para>
    /// A pen animal is searched for as vanilla's own send check reaches it
    /// (<c>GatherAnimalsAndSlavesForCaravanUtility.CanRoperTakeAnimalToDest</c>): led by a colonist,
    /// through doors the colonist opens, kept out of fences. Searched as itself it could never leave
    /// its pen.
    /// </para>
    /// </remarks>
    public static class ExitReadings
    {
        /// <summary>
        /// Reads a map for a caravan about to be sent.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="pawns">Every pawn being sent; the downed are carried and left out.</param>
        /// <param name="origin">Where the caravan sets out from.</param>
        /// <param name="startingTile">The first tile of its world route, or invalid.</param>
        /// <param name="current">The exit it has now, or <see cref="IntVec3.Invalid"/>.</param>
        /// <param name="settings">The mod's settings.</param>
        /// <returns>The readings.</returns>
        public static ExitReading Read(Map map, List<Pawn> pawns, IntVec3 origin, PlanetTile startingTile, IntVec3 current, CaravansCriticallyEnhancedSettings settings)
        {
            var reading = new ExitReading();
            ExitPlanInput input = reading.Input;
            input.OriginX = origin.x;
            input.OriginZ = origin.z;
            input.ApplyAreas = settings.IsOn(Toggle.AreaAwareExit);
            input.ApplyThreats = settings.IsOn(Toggle.ThreatAwareExit);
            input.ThreatRadius = settings.ThreatRadius;
            input.RouteThreatRadius = settings.RouteThreatRadius;
            input.PreferredSide = startingTile.Valid ? Find.WorldGrid.GetRotFromTo(map.Tile, startingTile).AsInt : -1;

            ReadCandidates(map, current, reading);

            Pawn roper = null;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Spawned && !pawn.Downed && pawn.Map == map)
                {
                    reading.Movers.Add(pawn);
                    if (roper == null && pawn.IsColonist)
                    {
                        roper = pawn;
                    }
                }
            }

            var groups = new Dictionary<(int, bool, bool, bool, Faction), int>();
            for (int id = 0; id < reading.Movers.Count; id++)
            {
                Pawn mover = reading.Movers[id];
                input.Movers.Add(new MoverReading(id, GroupOf(mover, roper, map, groups, input), AllowedFor(mover, reading.Cells, input.ApplyAreas)));
            }

            if (input.ApplyThreats)
            {
                ReadThreats(map, reading.Movers, input);
            }

            return reading;
        }

        private static void ReadCandidates(Map map, IntVec3 current, ExitReading reading)
        {
            IntVec3 size = map.Size;
            foreach (IntVec3 cell in CellRect.WholeMap(map).EdgeCells)
            {
                Region region = cell.GetRegion(map);
                int side = cell.z == size.z - 1 ? Rot4.North.AsInt
                    : cell.x == size.x - 1 ? Rot4.East.AsInt
                    : cell.z == 0 ? Rot4.South.AsInt
                    : Rot4.West.AsInt;

                if (cell == current)
                {
                    reading.Input.CurrentIndex = reading.Cells.Count;
                }

                reading.Cells.Add(cell);
                reading.Input.Candidates.Add(new EdgeCandidate(cell.x, cell.z, region?.id ?? -1, region != null && cell.Standable(map) && !cell.Fogged(map), side));
            }
        }

        private static int GroupOf(Pawn mover, Pawn roper, Map map, Dictionary<(int, bool, bool, bool, Faction), int> groups, ExitPlanInput input)
        {
            Region root = mover.Position.GetRegion(map);
            bool led = AnimalPenUtility.NeedsToBeManagedByRope(mover);
            if (root == null || (led && roper == null))
            {
                input.Groups.Add(new Dictionary<int, int>());
                return input.Groups.Count - 1;
            }

            TraverseParms parms = led ? TraverseParms.For(roper).WithFenceblockedOf(mover) : TraverseParms.For(mover);
            Pawn walker = parms.pawn;
            var key = (root.id, parms.fenceBlocked, walker.CanOpenDoors, ForbidUtility.CaresAboutForbidden(walker, false, true), walker.Faction);
            if (groups.TryGetValue(key, out int existing))
            {
                return existing;
            }

            // How many regions from the start each region is: the first entry into a region in a
            // breadth-first search comes from a region one nearer, so the first depth recorded is the least.
            var depths = new Dictionary<int, int> { { root.id, 0 } };
            var ranks = new Dictionary<int, int>();
            RegionTraverser.BreadthFirstTraverse(
                root,
                (from, to) =>
                {
                    if (!to.Allows(parms, false))
                    {
                        return false;
                    }

                    if (!depths.ContainsKey(to.id))
                    {
                        depths.Add(to.id, depths[from.id] + 1);
                    }

                    return true;
                },
                region =>
                {
                    if (region.touchesMapEdge && !ranks.ContainsKey(region.id))
                    {
                        ranks.Add(region.id, depths[region.id]);
                    }

                    return false;
                });

            input.Groups.Add(ranks);
            groups.Add(key, input.Groups.Count - 1);
            return input.Groups.Count - 1;
        }

        private static bool[] AllowedFor(Pawn mover, List<IntVec3> cells, bool applyAreas)
        {
            if (!applyAreas)
            {
                return null;
            }

            Area area = mover.playerSettings?.AreaRestrictionInPawnCurrentMap;
            bool restricted = ExitPolicy.IsRestricted(
                mover.playerSettings != null,
                mover.playerSettings != null && mover.playerSettings.SupportsAllowedAreas,
                mover.Faction == Faction.OfPlayer,
                mover.HostFaction == null,
                mover.IsMutant && !mover.mutant.Def.respectsAllowedArea,
                mover.RaceProps.IsMechanoid,
                mover.GetOverseer() != null,
                area != null,
                area?.TrueCount ?? 0);
            if (!restricted)
            {
                return null;
            }

            var allowed = new bool[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                allowed[i] = area[cells[i]];
            }

            return allowed;
        }

        private static void ReadThreats(Map map, List<Pawn> movers, ExitPlanInput input)
        {
            foreach (IAttackTarget target in map.attackTargetsCache.TargetsHostileToColony)
            {
                // A member in a hostile mental state is in the set too; it is coming along, not lying in wait.
                if (target.Thing is Pawn pawn && IsAmong(pawn, movers))
                {
                    continue;
                }

                if (GenHostility.IsActiveThreatToPlayer(target))
                {
                    IntVec3 position = target.Thing.Position;
                    input.Threats.Add(new ThreatReading(position.x, position.z));
                }
            }
        }

        /// <summary>
        /// Determines whether a pawn is in a list, by reference.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="pawns">The list.</param>
        /// <returns><c>true</c> when it is.</returns>
        internal static bool IsAmong(Pawn pawn, List<Pawn> pawns)
        {
            // By reference: Pawn.Equals compares def names as strings.
            for (int i = 0; i < pawns.Count; i++)
            {
                if (ReferenceEquals(pawns[i], pawn))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
