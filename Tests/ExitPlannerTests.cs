using System;
using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The exit planner over hand-built readings: a 20 by 20 map's edge cells in a few regions.
    /// </summary>
    [TestFixture]
    public class ExitPlannerTests
    {
        private const int North = 0;
        private const int East = 1;
        private const int South = 2;
        private const int West = 3;

        [Test]
        public void TheExitFewestRegionsAwayWinsAndTiesGoToTheNearest()
        {
            ExitPlanInput input = Map();
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Movers.Add(new MoverReading(7, 0, null));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.None));
            Assert.That(Cell(input, plan.CandidateIndex), Is.EqualTo((9, 19)), "Region 1 is met first; of its cells, the one nearest (10, 10).");
            Assert.That(plan.FallbackIndex, Is.EqualTo(plan.CandidateIndex), "With no areas the fallback is the choice.");
        }

        [Test]
        public void AnUnusableCellIsNeverChosen()
        {
            ExitPlanInput input = Map();
            input.Groups.Add(Reach((1, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            Assert.That(Cell(input, ExitPlanner.Plan(input).CandidateIndex), Is.EqualTo((9, 19)), "The control: usable, it wins.");

            input.Candidates[Index(input, 9, 19)] = new EdgeCandidate(9, 19, 1, false, North);
            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(Cell(input, plan.CandidateIndex), Is.Not.EqualTo((9, 19)), "Its region is reachable; only being unusable rules it out.");
            Assert.That(input.Candidates[plan.CandidateIndex].RegionId, Is.EqualTo(1));
        }

        [Test]
        public void AnExitOneGroupCannotReachIsLeftOut()
        {
            ExitPlanInput input = Map();
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Groups.Add(Reach((2, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Movers.Add(new MoverReading(8, 1, null));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(input.Candidates[plan.CandidateIndex].RegionId, Is.EqualTo(2), "Only region 2 suits both; its rank is the worse of the two groups'.");
        }

        [Test]
        public void AMoverThatReachesNoEdgeIsReportedAndBlocksNobody()
        {
            ExitPlanInput input = Map();
            input.Groups.Add(Reach((1, 0)));
            input.Groups.Add(Reach());
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Movers.Add(new MoverReading(8, 1, AllowNone(input)));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.None));
            Assert.That(plan.StrandedMoverIds, Is.EqualTo(new[] { 8 }));
        }

        [Test]
        public void NobodyReachingAnyEdgeIsUnreachableAndSoIsNoSharedEdge()
        {
            ExitPlanInput walledIn = Map();
            walledIn.Groups.Add(Reach());
            walledIn.Movers.Add(new MoverReading(7, 0, null));
            Assert.That(ExitPlanner.Plan(walledIn).Blocker, Is.EqualTo(ExitBlocker.Unreachable));

            ExitPlanInput apart = Map();
            apart.Groups.Add(Reach((1, 0)));
            apart.Groups.Add(Reach((2, 0)));
            apart.Movers.Add(new MoverReading(7, 0, null));
            apart.Movers.Add(new MoverReading(8, 1, null));
            ExitPlan plan = ExitPlanner.Plan(apart);
            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.Unreachable));
            Assert.That(plan.FallbackIndex, Is.EqualTo(-1));
        }

        [Test]
        public void NoUsableEdgeCellIsItsOwnBlocker()
        {
            var input = new ExitPlanInput();
            input.Candidates.Add(new EdgeCandidate(0, 0, 1, false, West));
            Assert.That(ExitPlanner.Plan(input).Blocker, Is.EqualTo(ExitBlocker.NoEdgeCells));
            Assert.That(ExitPlanner.Plan(new ExitPlanInput()).Blocker, Is.EqualTo(ExitBlocker.NoEdgeCells));
        }

        [Test]
        public void ARestrictedPawnSteersTheExitToAFartherCellInsideItsArea()
        {
            ExitPlanInput input = Map();
            input.ApplyAreas = true;
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Movers.Add(new MoverReading(8, 0, Allow(input, c => c.RegionId == 2)));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.None));
            Assert.That(input.Candidates[plan.CandidateIndex].RegionId, Is.EqualTo(2));
            Assert.That(input.Candidates[plan.FallbackIndex].RegionId, Is.EqualTo(1), "The fallback ignores areas.");
        }

        [Test]
        public void AnAreaWithNoReachableEdgeNamesItsPawnAndStillGivesAFallback()
        {
            ExitPlanInput input = Map();
            input.ApplyAreas = true;
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Movers.Add(new MoverReading(8, 0, AllowNone(input)));
            input.Movers.Add(new MoverReading(9, 0, Allow(input, c => c.RegionId == 1)));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.AreaExcludesReachableEdges));
            Assert.That(plan.BlockingMoverIds, Is.EqualTo(new[] { 8 }), "Exactly the pawn nothing can help.");
            Assert.That(plan.CandidateIndex, Is.EqualTo(-1));
            Assert.That(input.Candidates[plan.FallbackIndex].RegionId, Is.EqualTo(1));
        }

        [Test]
        public void AnAreaWhoseOnlyEdgesAreOutOfReachHoldsNoReachableEdge()
        {
            ExitPlanInput input = Map();
            input.ApplyAreas = true;
            input.Groups.Add(Reach((1, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Movers.Add(new MoverReading(8, 0, Allow(input, c => c.RegionId == 2)));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.AreaExcludesReachableEdges), "Its area does touch an edge, one nobody can get to.");
            Assert.That(plan.BlockingMoverIds, Is.EqualTo(new[] { 8 }));
        }

        [Test]
        public void AreasThatEachHoldAnEdgeButShareNoneNameThoseRejectingTheWidestCell()
        {
            ExitPlanInput input = Map();
            input.ApplyAreas = true;
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Movers.Add(new MoverReading(7, 0, Allow(input, c => c.RegionId == 1)));
            input.Movers.Add(new MoverReading(8, 0, Allow(input, c => c.RegionId == 1)));
            input.Movers.Add(new MoverReading(9, 0, Allow(input, c => c.RegionId == 2)));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Blocker, Is.EqualTo(ExitBlocker.AreasDisjoint));
            Assert.That(plan.BlockingMoverIds, Is.EqualTo(new[] { 9 }));
        }

        [Test]
        public void AreasAreIgnoredWhenTheRuleIsOff()
        {
            ExitPlanInput input = Map();
            input.Groups.Add(Reach((1, 0)));
            input.Movers.Add(new MoverReading(8, 0, AllowNone(input)));

            Assert.That(ExitPlanner.Plan(input).Blocker, Is.EqualTo(ExitBlocker.None));
        }

        [Test]
        public void AnUnthreatenedExitBeatsANearerThreatenedOne()
        {
            ExitPlanInput input = Map();
            input.ApplyThreats = true;
            input.ThreatRadius = 5;
            input.RouteThreatRadius = 2;
            input.Groups.Add(Reach((1, 0), (2, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            Assert.That(Cell(input, ExitPlanner.Plan(input).CandidateIndex), Is.EqualTo((19, 10)), "The control: the nearest exit.");

            input.Threats.Add(new ThreatReading(19, 10));
            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Threatened, Is.False);
            Assert.That(Cell(input, plan.CandidateIndex), Is.EqualTo((10, 19)), "The nearest of those far enough from the hostile and its way.");
        }

        [Test]
        public void WhenEveryExitIsThreatenedTheLeastThreatenedIsChosenAndSaidToBe()
        {
            ExitPlanInput input = Map();
            input.ApplyThreats = true;
            input.ThreatRadius = 60;
            input.RouteThreatRadius = 1;
            input.Groups.Add(Reach((1, 0), (2, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Threats.Add(new ThreatReading(0, 10));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(plan.Threatened, Is.True);
            Assert.That(Cell(input, plan.CandidateIndex), Is.EqualTo((19, 0)), "The farthest corner from the threat.");
        }

        [Test]
        public void AThreatNearTheRouteAloneCounts()
        {
            ExitPlanInput input = Map();
            input.ApplyThreats = true;
            input.ThreatRadius = 1;
            input.RouteThreatRadius = 0;
            input.Groups.Add(Reach((1, 0), (2, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Threats.Add(new ThreatReading(14, 10));
            Assert.That(Cell(input, ExitPlanner.Plan(input).CandidateIndex), Is.EqualTo((19, 10)), "The control: five cells from the hostile, the nearest exit.");

            input.RouteThreatRadius = 3;
            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(Cell(input, plan.CandidateIndex), Is.Not.EqualTo((19, 10)), "The hostile stands on the way there.");
            Assert.That(plan.Threatened, Is.False);
        }

        [Test]
        public void ThreatsAreIgnoredWhenTheSwitchIsOff()
        {
            ExitPlanInput input = Map();
            input.ThreatRadius = 25;
            input.RouteThreatRadius = 12;
            input.Groups.Add(Reach((1, 0), (2, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Threats.Add(new ThreatReading(18, 10));

            ExitPlan plan = ExitPlanner.Plan(input);

            Assert.That(Cell(input, plan.CandidateIndex), Is.EqualTo((19, 10)), "The nearest exit, a cell from the hostile.");
            Assert.That(plan.Threatened, Is.False);
        }

        [Test]
        public void AThreatScoresMoreTheCloserItIsAndNothingFromTheRadiusOut()
        {
            ExitPlanInput input = Map();
            input.ThreatRadius = 3;
            input.RouteThreatRadius = 0;
            var candidate = new EdgeCandidate(0, 0, 1, true, West);
            input.OriginX = 0;
            input.OriginZ = 0;

            input.Threats.Add(new ThreatReading(3, 0));
            Assert.That(ThreatGeometry.Score(input, candidate), Is.EqualTo(0f), "Exactly at the radius: not counted.");

            input.Threats[0] = new ThreatReading(2, 0);
            Assert.That(ThreatGeometry.Score(input, candidate), Is.EqualTo(1f));
        }

        [Test]
        public void TheDistanceToARouteMeasuresToItsNearestPoint()
        {
            Assert.That(ThreatGeometry.DistanceSquaredToSegment(5, 5, 0, 0, 0, 0), Is.EqualTo(50), "A segment of no length is a point.");
            Assert.That(ThreatGeometry.DistanceSquaredToSegment(-3, 0, 0, 0, 10, 0), Is.EqualTo(9), "Before the first end.");
            Assert.That(ThreatGeometry.DistanceSquaredToSegment(14, 0, 0, 0, 10, 0), Is.EqualTo(16), "Past the second end.");
            Assert.That(ThreatGeometry.DistanceSquaredToSegment(5, 4, 0, 0, 10, 0), Is.EqualTo(16), "Beside the middle.");
        }

        [Test]
        public void TheWorldRouteSettlesOnlyAnOtherwiseExactTie()
        {
            var input = new ExitPlanInput { OriginX = 10, OriginZ = 10 };
            input.Candidates.Add(new EdgeCandidate(10, 0, 1, true, South));
            input.Candidates.Add(new EdgeCandidate(10, 20, 1, true, North));
            input.Groups.Add(Reach((1, 0)));
            input.Movers.Add(new MoverReading(7, 0, null));

            input.PreferredSide = North;
            Assert.That(ExitPlanner.Plan(input).CandidateIndex, Is.EqualTo(1));
            input.PreferredSide = South;
            Assert.That(ExitPlanner.Plan(input).CandidateIndex, Is.EqualTo(0));
            input.PreferredSide = -1;
            Assert.That(ExitPlanner.Plan(input).CandidateIndex, Is.EqualTo(0), "No route: the first.");

            input.Candidates.Add(new EdgeCandidate(10, 19, 1, true, North));
            input.PreferredSide = South;
            Assert.That(ExitPlanner.Plan(input).CandidateIndex, Is.EqualTo(2), "A nearer exit beats the route's side.");

            input.Groups[0] = Reach((1, 1), (2, 0));
            input.Candidates.Add(new EdgeCandidate(0, 10, 2, true, West));
            Assert.That(ExitPlanner.Plan(input).CandidateIndex, Is.EqualTo(3), "Fewer regions away beats a nearer exit and the route's side.");
        }

        [Test]
        public void TheCurrentExitIsJudgedByTheSameRules()
        {
            ExitPlanInput input = Map();
            input.ApplyAreas = true;
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Movers.Add(new MoverReading(8, 0, Allow(input, c => c.RegionId == 2)));

            input.CurrentIndex = Index(input, 0, 10);
            Assert.That(ExitPlanner.Plan(input).CurrentValid, Is.False, "Outside the area.");
            Assert.That(ExitPlanner.Plan(input).CurrentReachable, Is.True, "Reachable all the same.");
            input.CurrentIndex = Index(input, 19, 10);
            Assert.That(ExitPlanner.Plan(input).CurrentValid, Is.True);
            input.Candidates[Index(input, 19, 10)] = new EdgeCandidate(19, 10, 2, false, East);
            Assert.That(ExitPlanner.Plan(input).CurrentValid, Is.False, "Walled or fogged.");
            Assert.That(ExitPlanner.Plan(input).CurrentReachable, Is.False);
            input.CurrentIndex = -1;
            Assert.That(ExitPlanner.Plan(input).CurrentValid, Is.False, "Not an edge cell at all.");
            Assert.That(ExitPlanner.Plan(input).CurrentReachable, Is.False);
        }

        [Test]
        public void TheSameReadingsAlwaysGiveTheSameExit()
        {
            ExitPlanInput input = Map();
            input.ApplyThreats = true;
            input.ThreatRadius = 25;
            input.RouteThreatRadius = 12;
            input.Groups.Add(Reach((1, 0), (2, 1)));
            input.Movers.Add(new MoverReading(7, 0, null));
            input.Threats.Add(new ThreatReading(3, 3));

            int first = ExitPlanner.Plan(input).CandidateIndex;
            Assert.That(Enumerable.Range(0, 5).Select(_ => ExitPlanner.Plan(input).CandidateIndex), Is.All.EqualTo(first));
        }

        /// <summary>
        /// A 20 by 20 map seen from (10, 10): the west edge is region 1, the east edge region 2, and
        /// the north and south edges are split between them at x 10.
        /// </summary>
        private static ExitPlanInput Map()
        {
            var input = new ExitPlanInput { OriginX = 10, OriginZ = 10 };
            for (int z = 0; z < 20; z++)
            {
                input.Candidates.Add(new EdgeCandidate(0, z, 1, true, West));
                input.Candidates.Add(new EdgeCandidate(19, z, 2, true, East));
            }

            for (int x = 1; x < 19; x++)
            {
                int region = x < 10 ? 1 : 2;
                input.Candidates.Add(new EdgeCandidate(x, 0, region, true, South));
                input.Candidates.Add(new EdgeCandidate(x, 19, region, true, North));
            }

            return input;
        }

        private static IDictionary<int, int> Reach(params (int Region, int Rank)[] regions)
        {
            return regions.ToDictionary(r => r.Region, r => r.Rank);
        }

        private static bool[] Allow(ExitPlanInput input, Func<EdgeCandidate, bool> rule)
        {
            return input.Candidates.Select(rule).ToArray();
        }

        private static bool[] AllowNone(ExitPlanInput input)
        {
            return new bool[input.Candidates.Count];
        }

        private static int Index(ExitPlanInput input, int x, int z)
        {
            for (int i = 0; i < input.Candidates.Count; i++)
            {
                if (input.Candidates[i].X == x && input.Candidates[i].Z == z)
                {
                    return i;
                }
            }

            throw new ArgumentException("No candidate at " + x + ", " + z);
        }

        private static (int, int) Cell(ExitPlanInput input, int index)
        {
            return (input.Candidates[index].X, input.Candidates[index].Z);
        }
    }
}
