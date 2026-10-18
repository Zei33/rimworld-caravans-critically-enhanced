using System;
using System.Collections.Generic;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Why no exit could be chosen.
    /// </summary>
    public enum ExitBlocker
    {
        /// <summary>An exit was chosen.</summary>
        None,

        /// <summary>No edge cell is standable and visible.</summary>
        NoEdgeCells,

        /// <summary>No edge cell is reachable by every mover that can reach an edge at all.</summary>
        Unreachable,

        /// <summary>Some restricted movers' areas hold no reachable edge cell at all.</summary>
        AreaExcludesReachableEdges,

        /// <summary>Every restricted mover's area holds a reachable edge cell, but no one cell is in all of them.</summary>
        AreasDisjoint,
    }

    /// <summary>
    /// One map edge cell the caravan could leave by.
    /// </summary>
    public struct EdgeCandidate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeCandidate"/> struct.
        /// </summary>
        /// <param name="x">Its x coordinate.</param>
        /// <param name="z">Its z coordinate.</param>
        /// <param name="regionId">The id of its region, or -1 when it has none.</param>
        /// <param name="usable">Whether it is standable and not fogged.</param>
        /// <param name="side">The map edge it is on, as <c>Rot4.AsInt</c>: 0 north, 1 east, 2 south, 3 west.</param>
        public EdgeCandidate(int x, int z, int regionId, bool usable, int side)
        {
            X = x;
            Z = z;
            RegionId = regionId;
            Usable = usable;
            Side = side;
        }

        /// <summary>Gets its x coordinate.</summary>
        public int X { get; }

        /// <summary>Gets its z coordinate.</summary>
        public int Z { get; }

        /// <summary>Gets the id of its region, or -1 when it has none.</summary>
        public int RegionId { get; }

        /// <summary>Gets whether it is standable and not fogged.</summary>
        public bool Usable { get; }

        /// <summary>Gets the map edge it is on: 0 north, 1 east, 2 south, 3 west.</summary>
        public int Side { get; }
    }

    /// <summary>
    /// A hostile the caravan should keep away from.
    /// </summary>
    public struct ThreatReading
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreatReading"/> struct.
        /// </summary>
        /// <param name="x">Its x coordinate.</param>
        /// <param name="z">Its z coordinate.</param>
        public ThreatReading(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>Gets its x coordinate.</summary>
        public int X { get; }

        /// <summary>Gets its z coordinate.</summary>
        public int Z { get; }
    }

    /// <summary>
    /// One mover the exit has to suit.
    /// </summary>
    public sealed class MoverReading
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MoverReading"/> class.
        /// </summary>
        /// <param name="id">An id the caller maps back to the pawn.</param>
        /// <param name="group">The index of its reach group in <see cref="ExitPlanInput.Groups"/>.</param>
        /// <param name="allowed">For each candidate, whether its allowed area holds that cell; <c>null</c> when it is unrestricted.</param>
        public MoverReading(int id, int group, bool[] allowed)
        {
            Id = id;
            Group = group;
            Allowed = allowed;
        }

        /// <summary>Gets the id the caller maps back to the pawn.</summary>
        public int Id { get; }

        /// <summary>Gets the index of its reach group.</summary>
        public int Group { get; }

        /// <summary>Gets, for each candidate, whether its area holds the cell, or <c>null</c> when unrestricted.</summary>
        public bool[] Allowed { get; }
    }

    /// <summary>
    /// Everything the planner reads, as plain values.
    /// </summary>
    public sealed class ExitPlanInput
    {
        /// <summary>Gets or sets the edge cells.</summary>
        public IList<EdgeCandidate> Candidates { get; set; } = new List<EdgeCandidate>();

        /// <summary>
        /// Gets or sets, per reach group, the edge regions the group reaches and how many regions away
        /// from its start its search met each one.
        /// </summary>
        public IList<IDictionary<int, int>> Groups { get; set; } = new List<IDictionary<int, int>>();

        /// <summary>Gets or sets the movers.</summary>
        public IList<MoverReading> Movers { get; set; } = new List<MoverReading>();

        /// <summary>Gets or sets the hostiles to keep away from.</summary>
        public IList<ThreatReading> Threats { get; set; } = new List<ThreatReading>();

        /// <summary>Gets or sets the x coordinate the caravan sets out from.</summary>
        public int OriginX { get; set; }

        /// <summary>Gets or sets the z coordinate the caravan sets out from.</summary>
        public int OriginZ { get; set; }

        /// <summary>Gets or sets the map edge facing the caravan's world route, or -1 for none.</summary>
        public int PreferredSide { get; set; } = -1;

        /// <summary>Gets or sets the candidate the caravan uses now, or -1.</summary>
        public int CurrentIndex { get; set; } = -1;

        /// <summary>Gets or sets whether allowed areas constrain the choice.</summary>
        public bool ApplyAreas { get; set; }

        /// <summary>Gets or sets whether threats rank the choice.</summary>
        public bool ApplyThreats { get; set; }

        /// <summary>Gets or sets how close to an exit cell a hostile counts, in cells.</summary>
        public int ThreatRadius { get; set; }

        /// <summary>Gets or sets how close to the straight route to the exit a hostile counts, in cells.</summary>
        public int RouteThreatRadius { get; set; }
    }

    /// <summary>
    /// What the planner chose.
    /// </summary>
    public sealed class ExitPlan
    {
        /// <summary>Gets or sets why no exit was chosen, or <see cref="ExitBlocker.None"/>.</summary>
        public ExitBlocker Blocker { get; set; }

        /// <summary>Gets or sets the chosen candidate, or -1.</summary>
        public int CandidateIndex { get; set; } = -1;

        /// <summary>Gets or sets the best reachable candidate ignoring areas, or -1.</summary>
        public int FallbackIndex { get; set; } = -1;

        /// <summary>Gets or sets whether the chosen candidate is threatened, which means every valid one is.</summary>
        public bool Threatened { get; set; }

        /// <summary>Gets or sets whether the current candidate is still valid.</summary>
        public bool CurrentValid { get; set; }

        /// <summary>Gets or sets whether the current candidate is still usable and reachable, areas aside.</summary>
        public bool CurrentReachable { get; set; }

        /// <summary>Gets or sets the current candidate's threat score, or 0 when it is not reachable or threats are off.</summary>
        public float CurrentScore { get; set; }

        /// <summary>Gets or sets the chosen candidate's threat score, or 0 when none was chosen.</summary>
        public float CandidateScore { get; set; }

        /// <summary>
        /// Gets or sets the safe candidate for a move away from a threat, or -1 when none is safe or
        /// threats are off: of the valid candidates with no threat at the set distances, the one ranked
        /// first with both distances widened (<see cref="ThreatPolicy.Clearance"/>), so the nearest
        /// (fewest regions away, then fewest cells) clear of the wider distances where there is one,
        /// and otherwise the one least inside them.
        /// </summary>
        public int SafeIndex { get; set; } = -1;

        /// <summary>Gets a value indicating whether a safe candidate was found (<see cref="SafeIndex"/>).</summary>
        public bool HasSafeExit => SafeIndex >= 0;

        /// <summary>Gets or sets the movers whose areas stand in the way.</summary>
        public IList<int> BlockingMoverIds { get; set; } = new List<int>();

        /// <summary>Gets or sets the movers that can reach no edge at all and were left out.</summary>
        public IList<int> StrandedMoverIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// Chooses the edge cell a caravan leaves by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla takes a random cell of the first edge region a search from the pawns' centre meets,
    /// with no pawn, fences passable, and no test of fog, standing room, threats or areas
    /// (<c>RCellFinder.TryFindClosestEdgeCellTo</c>). This chooses among every edge cell instead:
    /// one each mover that can reach an edge at all can reach, inside every restricted mover's
    /// allowed area, and then the least threatened, fewest regions away, nearest, facing the world
    /// route. Nothing is random, so the same readings always give the same exit.
    /// </para>
    /// <para>
    /// A mover that reaches no edge at all, such as one walled in, is left out and reported rather
    /// than blocking everyone; vanilla's own send check deals with it.
    /// </para>
    /// </remarks>
    public static class ExitPlanner
    {
        /// <summary>
        /// Chooses an exit.
        /// </summary>
        /// <param name="input">The readings.</param>
        /// <returns>The plan.</returns>
        public static ExitPlan Plan(ExitPlanInput input)
        {
            var plan = new ExitPlan();
            IList<EdgeCandidate> candidates = input.Candidates;

            bool anyUsable = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                anyUsable |= candidates[i].Usable;
            }

            if (!anyUsable)
            {
                plan.Blocker = ExitBlocker.NoEdgeCells;
                return plan;
            }

            var counted = new List<MoverReading>();
            for (int m = 0; m < input.Movers.Count; m++)
            {
                MoverReading mover = input.Movers[m];
                if (input.Groups[mover.Group].Count == 0)
                {
                    plan.StrandedMoverIds.Add(mover.Id);
                }
                else
                {
                    counted.Add(mover);
                }
            }

            if (input.Movers.Count > 0 && counted.Count == 0)
            {
                plan.Blocker = ExitBlocker.Unreachable;
                return plan;
            }

            var reachable = new bool[candidates.Count];
            var rank = new int[candidates.Count];
            bool anyReachable = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                reachable[i] = candidates[i].Usable && Reachable(candidates[i].RegionId, counted, input.Groups, out rank[i]);
                anyReachable |= reachable[i];
            }

            if (!anyReachable)
            {
                plan.Blocker = ExitBlocker.Unreachable;
                return plan;
            }

            var score = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                score[i] = input.ApplyThreats && reachable[i] ? ThreatGeometry.Score(input, candidates[i]) : 0f;
            }

            var valid = new bool[candidates.Count];
            bool anyValid = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                valid[i] = reachable[i] && (!input.ApplyAreas || AllowedByEvery(counted, i));
                anyValid |= valid[i];
            }

            plan.FallbackIndex = Best(input, reachable, rank, score);
            bool current = input.CurrentIndex >= 0 && input.CurrentIndex < candidates.Count;
            plan.CurrentValid = current && valid[input.CurrentIndex];
            plan.CurrentReachable = current && reachable[input.CurrentIndex];
            plan.CurrentScore = current ? score[input.CurrentIndex] : 0f;

            if (!anyValid)
            {
                Explain(plan, counted, reachable);
                return plan;
            }

            plan.CandidateIndex = Best(input, valid, rank, score);
            plan.CandidateScore = score[plan.CandidateIndex];
            plan.Threatened = score[plan.CandidateIndex] > 0f;
            plan.SafeIndex = Safest(input, valid, rank, score);
            return plan;
        }

        private static int Safest(ExitPlanInput input, bool[] valid, int[] rank, float[] score)
        {
            if (!input.ApplyThreats)
            {
                return -1;
            }

            // Only exits safe at the set distances; among them the wider distances rank, so a move goes
            // to one clear of those where it can, rather than to the cell just outside the danger.
            var safe = new bool[valid.Length];
            var wide = new float[valid.Length];
            var wider = new ExitPlanInput
            {
                Threats = input.Threats,
                OriginX = input.OriginX,
                OriginZ = input.OriginZ,
                ThreatRadius = ThreatPolicy.Clearance(input.ThreatRadius),
                RouteThreatRadius = ThreatPolicy.Clearance(input.RouteThreatRadius),
            };
            bool any = false;
            for (int i = 0; i < valid.Length; i++)
            {
                safe[i] = valid[i] && score[i] == 0f;
                wide[i] = safe[i] ? ThreatGeometry.Score(wider, input.Candidates[i]) : 0f;
                any |= safe[i];
            }

            return any ? Best(input, safe, rank, wide) : -1;
        }

        private static bool Reachable(int regionId, List<MoverReading> counted, IList<IDictionary<int, int>> groups, out int rank)
        {
            rank = 0;
            if (regionId < 0)
            {
                return false;
            }

            for (int m = 0; m < counted.Count; m++)
            {
                if (!groups[counted[m].Group].TryGetValue(regionId, out int order))
                {
                    return false;
                }

                rank = Math.Max(rank, order);
            }

            return true;
        }

        private static bool AllowedByEvery(List<MoverReading> counted, int candidate)
        {
            for (int m = 0; m < counted.Count; m++)
            {
                bool[] allowed = counted[m].Allowed;
                if (allowed != null && !allowed[candidate])
                {
                    return false;
                }
            }

            return true;
        }

        private static int Best(ExitPlanInput input, bool[] eligible, int[] rank, float[] score)
        {
            int best = -1;
            for (int i = 0; i < eligible.Length; i++)
            {
                if (eligible[i] && (best < 0 || Better(input, i, best, rank, score)))
                {
                    best = i;
                }
            }

            return best;
        }

        private static bool Better(ExitPlanInput input, int a, int b, int[] rank, float[] score)
        {
            bool threatenedA = score[a] > 0f;
            bool threatenedB = score[b] > 0f;
            if (threatenedA != threatenedB)
            {
                return !threatenedA;
            }

            if (score[a] != score[b])
            {
                return score[a] < score[b];
            }

            if (rank[a] != rank[b])
            {
                return rank[a] < rank[b];
            }

            long distanceA = DistanceSquared(input, input.Candidates[a]);
            long distanceB = DistanceSquared(input, input.Candidates[b]);
            if (distanceA != distanceB)
            {
                return distanceA < distanceB;
            }

            bool facesA = input.Candidates[a].Side == input.PreferredSide;
            bool facesB = input.Candidates[b].Side == input.PreferredSide;
            return facesA && !facesB;
        }

        private static long DistanceSquared(ExitPlanInput input, EdgeCandidate candidate)
        {
            long dx = candidate.X - input.OriginX;
            long dz = candidate.Z - input.OriginZ;
            return (dx * dx) + (dz * dz);
        }

        private static void Explain(ExitPlan plan, List<MoverReading> counted, bool[] reachable)
        {
            // Movers whose area holds no reachable cell at all are the ones to name first: nothing
            // anybody else does can help them.
            for (int m = 0; m < counted.Count; m++)
            {
                bool[] allowed = counted[m].Allowed;
                if (allowed == null)
                {
                    continue;
                }

                bool any = false;
                for (int i = 0; i < reachable.Length && !any; i++)
                {
                    any = reachable[i] && allowed[i];
                }

                if (!any)
                {
                    plan.BlockingMoverIds.Add(counted[m].Id);
                }
            }

            if (plan.BlockingMoverIds.Count > 0)
            {
                plan.Blocker = ExitBlocker.AreaExcludesReachableEdges;
                return;
            }

            // Otherwise the areas each hold an edge but share none: name those that reject the cell
            // most of them accept.
            int widest = -1;
            int widestCount = -1;
            for (int i = 0; i < reachable.Length; i++)
            {
                if (!reachable[i])
                {
                    continue;
                }

                int count = 0;
                for (int m = 0; m < counted.Count; m++)
                {
                    bool[] allowed = counted[m].Allowed;
                    count += allowed == null || allowed[i] ? 1 : 0;
                }

                if (count > widestCount)
                {
                    widest = i;
                    widestCount = count;
                }
            }

            for (int m = 0; m < counted.Count; m++)
            {
                bool[] allowed = counted[m].Allowed;
                if (allowed != null && !allowed[widest])
                {
                    plan.BlockingMoverIds.Add(counted[m].Id);
                }
            }

            plan.Blocker = ExitBlocker.AreasDisjoint;
        }
    }

    /// <summary>
    /// How threatening an exit is.
    /// </summary>
    /// <remarks>
    /// A hostile counts when it is within the threat radius of the exit cell, or within the route
    /// radius of the straight line from where the caravan sets out to the exit. Walls are ignored and
    /// hostiles move, so this ranks exits; it promises nothing.
    /// </remarks>
    public static class ThreatGeometry
    {
        /// <summary>
        /// Scores an exit: 0 when no hostile counts, and more the closer they are.
        /// </summary>
        /// <param name="input">The readings, for the origin, the threats and the radii.</param>
        /// <param name="candidate">The exit cell.</param>
        /// <returns>The score; lower is safer.</returns>
        public static float Score(ExitPlanInput input, EdgeCandidate candidate)
        {
            float score = 0f;
            for (int t = 0; t < input.Threats.Count; t++)
            {
                ThreatReading threat = input.Threats[t];
                float atExit = (float)Math.Sqrt(DistanceSquared(threat.X, threat.Z, candidate.X, candidate.Z));
                if (atExit < input.ThreatRadius)
                {
                    score += input.ThreatRadius - atExit;
                }

                float onRoute = (float)Math.Sqrt(DistanceSquaredToSegment(threat.X, threat.Z, input.OriginX, input.OriginZ, candidate.X, candidate.Z));
                if (onRoute < input.RouteThreatRadius)
                {
                    score += input.RouteThreatRadius - onRoute;
                }
            }

            return score;
        }

        /// <summary>
        /// Measures the squared distance from a point to a segment.
        /// </summary>
        /// <param name="px">The point's x.</param>
        /// <param name="pz">The point's z.</param>
        /// <param name="ax">The segment's first end, x.</param>
        /// <param name="az">The segment's first end, z.</param>
        /// <param name="bx">The segment's second end, x.</param>
        /// <param name="bz">The segment's second end, z.</param>
        /// <returns>The squared distance.</returns>
        public static double DistanceSquaredToSegment(int px, int pz, int ax, int az, int bx, int bz)
        {
            double dx = bx - ax;
            double dz = bz - az;
            double lengthSquared = (dx * dx) + (dz * dz);
            if (lengthSquared == 0)
            {
                return DistanceSquared(px, pz, ax, az);
            }

            double t = (((px - ax) * dx) + ((pz - az) * dz)) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            double cx = ax + (t * dx) - px;
            double cz = az + (t * dz) - pz;
            return (cx * cx) + (cz * cz);
        }

        private static double DistanceSquared(int ax, int az, int bx, int bz)
        {
            double dx = ax - bx;
            double dz = az - bz;
            return (dx * dx) + (dz * dz);
        }
    }
}
