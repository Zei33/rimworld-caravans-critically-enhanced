using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Jobs;
using CaravansCriticallyEnhanced.Patches;
using CaravansCriticallyEnhanced.Utils;
using NUnit.Framework;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the exit's game-facing half to the rules it hands over to: the toil writes run on real
    /// toils, the saved node names are pinned, and the rest is read from the IL.
    /// </summary>
    /// <remarks>
    /// A <c>Lord</c> and a <c>Map</c> cannot be had here, so the director, the readings and the
    /// patches that need one are pinned by which calls they make and in what order, never by their
    /// arguments or branches; <c>Tests/README.md</c> lists the in-game checks for the rest.
    /// </remarks>
    [TestFixture]
    public class ExitSurfaceTests
    {
        [TearDown]
        public void CloseTheHandoff()
        {
            ExitHandoff.Close();
        }

        [Test]
        public void MovingTheExitWritesEveryToilCopyButNeverTheMeetingPoint()
        {
            var meeting = new IntVec3(7, 0, 8);
            var exit = new IntVec3(3, 0, 4);
            var moved = new IntVec3(9, 0, 1);
            var gather = new LordToil_PrepareCaravan_GatherAnimals(meeting);
            var downed = new LordToil_PrepareCaravan_GatherDownedPawns(meeting, exit);
            var collect = new LordToil_PrepareCaravan_CollectAnimals(exit);
            var leave = new LordToil_PrepareCaravan_Leave(exit);
            var toils = new List<LordToil> { gather, new LordToil_PrepareCaravan_Pause(), downed, collect, leave };

            Assert.That(ExitDirector.CopiesAt(toils, exit, exit), Is.True, "The control: as built.");
            ExitDirector.WriteCopies(toils, moved, moved);

            Assert.That(CaravanLordAccess.LeaveExitSpot(leave), Is.EqualTo(moved));
            Assert.That(CaravanLordAccess.GatherDownedPawnsExitSpot(downed), Is.EqualTo(moved));
            Assert.That(CaravanLordAccess.RopeDestinationPoint(collect), Is.EqualTo(moved));
            Assert.That(CaravanLordAccess.RopeDestinationPoint(gather), Is.EqualTo(meeting), "Gathering animals is a rope toil too; its destination is the meeting point.");
            Assert.That(ExitDirector.CopiesAt(toils, moved, moved), Is.True);
            Assert.That(ExitDirector.CopiesAt(toils, exit, exit), Is.False);

            CaravanLordAccess.LeaveExitSpot(leave) = exit;
            Assert.That(ExitDirector.CopiesAt(toils, moved, moved), Is.False, "One copy out of step is enough to rewrite them.");
        }

        [Test]
        public void HeldOnlyTheDownedStagesCopyPointsAtTheMeetingPoint()
        {
            var meeting = new IntVec3(7, 0, 8);
            var exit = new IntVec3(3, 0, 4);
            var fallback = new IntVec3(0, 0, 9);
            var downed = new LordToil_PrepareCaravan_GatherDownedPawns(meeting, exit);
            var collect = new LordToil_PrepareCaravan_CollectAnimals(exit);
            var leave = new LordToil_PrepareCaravan_Leave(exit);
            var toils = new List<LordToil> { downed, collect, leave };

            ExitDirector.WriteCopies(toils, fallback, meeting);

            Assert.That(CaravanLordAccess.GatherDownedPawnsExitSpot(downed), Is.EqualTo(meeting), "Its idle colonists walk to this copy.");
            Assert.That(CaravanLordAccess.RopeDestinationPoint(collect), Is.EqualTo(fallback), "Anything that sends the caravan on regardless leaves by the fallback.");
            Assert.That(CaravanLordAccess.LeaveExitSpot(leave), Is.EqualTo(fallback));
            Assert.That(ExitDirector.CopiesAt(toils, fallback, meeting), Is.True);
            Assert.That(ExitDirector.CopiesAt(toils, fallback, fallback), Is.False);
            Assert.That(ExitDirector.CopiesAt(toils, meeting, meeting), Is.False);
        }

        [Test]
        public void TheStartingTileAccessorReadsAndWritesTheLordJobsField()
        {
            var job = new LordJob_FormAndSendCaravan(null, null, IntVec3.Zero, IntVec3.Zero, new PlanetTile(42), PlanetTile.Invalid);

            Assert.That(CaravanLordAccess.JobStartingTile, Is.Not.Null);
            Assert.That(CaravanLordAccess.JobStartingTile(job), Is.EqualTo(new PlanetTile(42)));
            Assert.That(ExitDirector.StartingTile(job), Is.EqualTo(new PlanetTile(42)));

            FieldInfo field = typeof(LordJob_FormAndSendCaravan).GetField(CaravanLordAccess.StartingTileField, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field?.FieldType, Is.EqualTo(typeof(PlanetTile)));
        }

        [Test]
        public void TheExitIsMovedOnlyWithEveryFieldItWrites()
        {
            Assert.That(Calls(typeof(CaravanLordAccess), "get_" + nameof(CaravanLordAccess.AllExitFieldsAvailable)), Is.EquivalentTo(new[]
            {
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_JobExitSpot",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_JobMeetingPoint",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_LeaveExitSpot",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_GatherDownedPawnsExitSpot",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_RopeDestinationPoint",
            }));
            Assert.That(CaravanLordAccess.AllExitFieldsAvailable, Is.True);
        }

        [Test]
        public void ACaravansStateIsMadeOnceAndAskingWhetherItIsHeldMakesNone()
        {
            var job = new LordJob_FormAndSendCaravan(null, null, IntVec3.Zero, IntVec3.Zero, PlanetTile.Invalid, PlanetTile.Invalid);
            var other = new LordJob_FormAndSendCaravan(null, null, IntVec3.Zero, IntVec3.Zero, PlanetTile.Invalid, PlanetTile.Invalid);

            Assert.That(CaravanExitStates.IsHolding(null), Is.False);
            Assert.That(CaravanExitStates.IsHolding(job), Is.False);
            Assert.That(CaravanExitStates.For(job), Is.SameAs(CaravanExitStates.For(job)));

            CaravanExitStates.For(job).Holding = true;
            Assert.That(CaravanExitStates.IsHolding(job), Is.True);
            Assert.That(CaravanExitStates.IsHolding(other), Is.False, "Another caravan's state is its own.");
            Assert.That(CaravanExitStates.For(job).LastWrittenSpot, Is.EqualTo(IntVec3.Invalid), "Nothing is known until the first pass.");

            // The table cannot be counted, so the readers are held to looking rather than making.
            foreach (string reader in new[] { nameof(CaravanExitStates.IsHolding), nameof(CaravanExitStates.NeedsFirstPass) })
            {
                List<string> calls = Calls(typeof(CaravanExitStates), reader);
                Assert.That(calls, Does.Contain("System.Runtime.CompilerServices.ConditionalWeakTable`2.TryGetValue"), reader);
                Assert.That(calls, Does.Not.Contain("System.Runtime.CompilerServices.ConditionalWeakTable`2.GetValue"), reader);
            }

            Assert.That(CaravanExitStates.NeedsFirstPass(job), Is.True, "Held, and no pass since it was made: as after a load.");
            CaravanExitStates.For(job).Passed = true;
            Assert.That(CaravanExitStates.NeedsFirstPass(job), Is.False);
            CaravanExitStates.For(other);
            Assert.That(CaravanExitStates.NeedsFirstPass(other), Is.False, "Not held, though no pass has run.");
        }

        [Test]
        public void TheSavedNodeNamesNeverChange()
        {
            // A renamed node reads as absent: a held caravan would load as free, with its toils
            // pointing at the meeting point, and leave by an edge outside the areas.
            Assert.That(ILCalls.Read(typeof(CaravanExitStates).GetMethod(nameof(CaravanExitStates.Expose))).Strings, Is.EqualTo(new[]
            {
                "cce_exitInitialised",
                "cce_exitHolding",
                "cce_exitLastWritten",
                "cce_exitOverride",
                "cce_exitForeign",
                "cce_exitRegather",
            }));
            Assert.That(Calls(typeof(Patch_LordJob_FormAndSendCaravan_ExposeData), "Postfix"), Is.EqualTo(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.IsVanillaJob",
                "CaravansCriticallyEnhanced.Core.CaravanExitStates.Expose",
            }));
        }

        [Test]
        public void ClosingTheHandoffForgetsEverySend()
        {
            var map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
            ExitHandoff.Open(map, new List<TransferableOneWay>(), new PlanetTile(3));
            ExitHandoff.Planned = true;
            ExitHandoff.HoldOnStart = true;
            ExitHandoff.Blocking.Add(new Pawn());

            ExitHandoff.Close();

            Assert.That(ExitHandoff.Map, Is.Null);
            Assert.That(ExitHandoff.Transferables, Is.Null);
            Assert.That(ExitHandoff.StartingTile, Is.EqualTo(PlanetTile.Invalid));
            Assert.That(new[] { ExitHandoff.Planned, ExitHandoff.HoldOnStart }, Is.All.False);
            Assert.That(ExitHandoff.Blocking, Is.Empty);
        }

        [Test]
        public void TheExitSearchIsVanillasOutsideASendOnAnotherMapAndAfterItsFirstAnswer()
        {
            IntVec3 result = IntVec3.Invalid;
            bool found = false;
            var map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
            var other = (Map)FormatterServices.GetUninitializedObject(typeof(Map));

            Assert.That(Patch_RCellFinder_TryFindClosestEdgeCellTo.Prefix(IntVec3.Zero, map, ref result, ref found), Is.True, "No send under way.");

            ExitHandoff.Open(map, new List<TransferableOneWay>(), PlanetTile.Invalid);
            Assert.That(Patch_RCellFinder_TryFindClosestEdgeCellTo.Prefix(IntVec3.Zero, other, ref result, ref found), Is.True, "Another map's search.");

            ExitHandoff.Planned = true;
            Assert.That(Patch_RCellFinder_TryFindClosestEdgeCellTo.Prefix(IntVec3.Zero, map, ref result, ref found), Is.True, "Answered once already.");
            Assert.That(result, Is.EqualTo(IntVec3.Invalid));
            Assert.That(found, Is.False);
        }

        [Test]
        public void TheSendOpensTheHandoffOnlyForAnExitSwitchAndAlwaysClosesIt()
        {
            List<string> prefix = Calls(typeof(Patch_Dialog_FormCaravan_TryFormAndSendCaravan), "Prefix");
            Assert.That(prefix, Does.Contain("CaravansCriticallyEnhanced.Utils.ExitHandoff.Open"));
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_Dialog_FormCaravan_TryFormAndSendCaravan), "Prefix")),
                Is.EqualTo(new Toggle?[] { Toggle.AreaAwareExit, Toggle.ThreatAwareExit }));
            Assert.That(Calls(typeof(Patch_Dialog_FormCaravan_TryFormAndSendCaravan), "Finalizer"), Is.EqualTo(new[] { "CaravansCriticallyEnhanced.Utils.ExitHandoff.Close" }));
        }

        [Test]
        public void TheExitSearchPlansFromTheWindowsPawnsAndHoldsWhenOnlyAreasStandInTheWay()
        {
            List<string> calls = Calls(typeof(Patch_RCellFinder_TryFindClosestEdgeCellTo), "Prefix");
            int planned = calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitHandoff.set_Planned");
            int pawns = calls.IndexOf("RimWorld.TransferableUtility.GetPawnsFromTransferables");
            int read = calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitReadings.Read");
            int plan = calls.IndexOf("CaravansCriticallyEnhanced.Core.ExitPlanner.Plan");
            int hold = calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitHandoff.set_HoldOnStart");
            Assert.That(new[] { planned, pawns, read, plan, hold }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { planned, pawns, read, plan, hold }, Is.Ordered, "Answered once, before anything that could throw.");
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_AllExitFieldsAvailable"), "No hold without the fields to hold with.");

            WalkResult walk = ILCalls.Read(Method(typeof(Patch_RCellFinder_TryFindClosestEdgeCellTo), "Prefix"));
            List<int?> holds = walk.Calls.Select((c, i) => c.Name == "set_HoldOnStart" ? walk.IntBeforeCall[i] : -1).Where(v => v != -1).ToList();
            Assert.That(holds, Is.EqualTo(new int?[] { 1, 0 }), "Held when areas block; the catch clears it.");
        }

        [Test]
        public void ANewCaravanIsSetUpOnlyWhenItIsANewVanillaLord()
        {
            List<string> post = Calls(typeof(Patch_CaravanFormingUtility_StartFormingCaravan), "Postfix");
            int vanilla = post.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla");
            int begin = post.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.Begin");
            Assert.That(new[] { vanilla, begin }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", post));
            Assert.That(vanilla, Is.LessThan(begin));

            Patch_CaravanFormingUtility_StartFormingCaravan.Prefix(null, out Lord none);
            Patch_CaravanFormingUtility_StartFormingCaravan.Prefix(new List<Pawn>(), out Lord empty);
            Assert.That(new[] { none, empty }, Is.All.Null);
            Assert.DoesNotThrow(() => Patch_CaravanFormingUtility_StartFormingCaravan.Postfix(new List<Pawn>(), IntVec3.Zero, null), "An empty list, as vanilla's early return leaves it.");

            List<string> beginCalls = Calls(typeof(ExitDirector), nameof(ExitDirector.Begin));
            Assert.That(beginCalls, Does.Contain("CaravansCriticallyEnhanced.Utils.ExitHandoff.get_HoldOnStart"));
            Assert.That(beginCalls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.Park"),
                Is.GreaterThan(beginCalls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitHandoff.get_HoldOnStart")));
        }

        [Test]
        public void ThePassReadsTheMapOnlyWhileTheRuleAppliesAndDecidesThroughThePolicy()
        {
            List<string> calls = Calls(typeof(ExitDirector), nameof(ExitDirector.Tick));
            int applies = calls.IndexOf("CaravansCriticallyEnhanced.Core.ExitPolicy.AreaRuleApplies");
            int read = calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitReadings.Read");
            int plan = calls.IndexOf("CaravansCriticallyEnhanced.Core.ExitPlanner.Plan");
            int decide = calls.IndexOf("CaravansCriticallyEnhanced.Core.ExitPolicy.Decide");
            Assert.That(new[] { applies, read, plan, decide }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { applies, read, plan, decide }, Is.Ordered, "The region searches cost something; only while the rule applies.");
            Assert.That(calls, Is.SupersetOf(new[]
            {
                "CaravansCriticallyEnhanced.Utils.ExitDirector.Park",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.Release",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.KeepParked",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_AllExitFieldsAvailable",
            }));
            Assert.That(ToggleReads.AskedBy(Method(typeof(ExitDirector), nameof(ExitDirector.Tick))), Is.EqualTo(new Toggle?[] { Toggle.AreaAwareExit }));

            List<string> postfix = Calls(typeof(Patch_LordJob_FormAndSendCaravan_LordJobTick), "Postfix");
            Assert.That(postfix, Does.Contain("CaravansCriticallyEnhanced.Utils.ExitDirector.Tick"));
            Assert.That(postfix.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.FirstTickAfterLoad"), Is.InRange(0, postfix.IndexOf("Verse.TickManager.get_TicksGame")),
                "A held caravan just loaded gets its pass on the first tick, ahead of the interval.");
            Assert.That(Calls(typeof(ExitDirector), nameof(ExitDirector.FirstTickAfterLoad)), Is.EqualTo(new[]
            {
                "CaravansCriticallyEnhanced.Core.CaravanExitStates.NeedsFirstPass",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.Tick",
            }));
        }

        [Test]
        public void EachActionTakesTheCellItsPlanMeans()
        {
            // The order of the plan's two index getters in the pass: the fallback for the moved test
            // and for holding, the choice for every release and move. Swapping one is a caravan held at
            // a cell it may not use, or released to -1.
            WalkResult tick = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Tick)));
            List<string> indices = tick.Calls.Select(c => c.Name).Where(n => n == "get_FallbackIndex" || n == "get_CandidateIndex").ToList();
            Assert.That(indices, Is.EqualTo(new[]
            {
                "get_FallbackIndex", "get_FallbackIndex",
                "get_FallbackIndex", "get_FallbackIndex",
                "get_FallbackIndex",
                "get_CandidateIndex", "get_CandidateIndex", "get_CandidateIndex",
            }));

            WalkResult search = ILCalls.Read(Method(typeof(Patch_RCellFinder_TryFindClosestEdgeCellTo), "Prefix"));
            Assert.That(search.Calls.Select(c => c.Name).Where(n => n == "get_FallbackIndex" || n == "get_CandidateIndex"), Is.EqualTo(new[]
            {
                "get_CandidateIndex", "get_FallbackIndex", "get_FallbackIndex",
            }), "The choice when nothing blocks; the fallback, and a hold, when areas do.");

            Assert.That(tick.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.Holding)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 0 }),
                "With a field gone nothing can be moved, so nothing may stay held.");
            Assert.That(tick.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.Passed)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }));
            Assert.That(tick.Strings, Is.EqualTo(new[]
            {
                "CaravansCriticallyEnhanced_ExitHeld",
                "CaravansCriticallyEnhanced_ExitReleased",
                "CaravansCriticallyEnhanced_ExitMoved",
            }), "One message each for holding, releasing and an area-driven move; a quiet move or fallback update says nothing.");
            Assert.That(Calls(typeof(ExitDirector), "KeepParked"), Is.SupersetOf(new[]
            {
                "RimWorld.LordJob_FormAndSendCaravan.get_ExitSpot",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_JobMeetingPoint",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.CopiesAt",
            }), "The copies are checked against the fallback and the meeting point, where a hold puts them.");
        }

        [Test]
        public void AWaitingCaravanGoesBackForDownedPawnsLeftAwayFromItsExit()
        {
            Assert.That(Calls(typeof(ExitDirector), nameof(ExitDirector.Tick)), Does.Contain("CaravansCriticallyEnhanced.Utils.ExitDirector.RegatherDowned"));
            List<string> calls = Calls(typeof(ExitDirector), "RegatherDowned");
            int count = calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.CountStrays");
            int decide = calls.IndexOf("CaravansCriticallyEnhanced.Core.ExitPolicy.Regather");
            int back = calls.IndexOf("Verse.AI.Group.Lord.GotoToil");
            Assert.That(new[] { count, decide, back }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { count, decide, back }, Is.Ordered, "Counted against the lord job's exit, the rule, then back.");
            Assert.That(calls, Does.Contain("RimWorld.LordJob_FormAndSendCaravan.get_ExitSpot"));

            List<string> strays = Calls(typeof(ExitDirector), nameof(ExitDirector.CountStrays));
            int carried = strays.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.CarrierOf");
            int broken = strays.IndexOf("Verse.Pawn.get_InMentalState");
            int works = strays.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.IsWorker");
            int near = strays.IndexOf("RimWorld.JobGiver_PrepareCaravan_GatherDownedPawns.IsDownedPawnNearExitPoint");
            int fetches = strays.LastIndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.IsWorker");
            int reach = strays.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.AnyMemberReaches");
            Assert.That(new[] { carried, broken, works, near, reach }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", strays));
            Assert.That(new[] { carried, broken, works, near }, Is.Ordered,
                "One a member holds that does not work the duties (drafted, or unable) goes with it; then vanilla's own 7-cell test, by the carrier's cell.");
            Assert.That(new[] { fetches, reach }, Is.All.GreaterThan(near), "Then fetchable: held by a worker, or reachable.");
            Assert.That(strays, Does.Not.Contain("Verse.DevelopmentalStageExtensions.Baby"), "Babies count: one member picks up one baby while collecting and leaving.");
            WalkResult regather = ILCalls.Read(Method(typeof(ExitDirector), "RegatherDowned"));
            Assert.That(regather.Fields.First().Field.Name, Is.EqualTo(nameof(CaravanExitState.RegatherPending)), "Nothing pending, nothing read.");
            Assert.That(regather.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.RegatherPending)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 0 }),
                "Settled once gone back or forgotten.");

            // Pending from a pass that wrote the exit while waiting, and kept until settled.
            List<string> tick = Calls(typeof(ExitDirector), nameof(ExitDirector.Tick));
            Assert.That(tick.IndexOf("CaravansCriticallyEnhanced.Core.ExitPolicy.WritesExit"), Is.InRange(0, tick.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.RegatherDowned")));
            WalkResult tickWalk = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Tick)));
            Assert.That(tickWalk.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.RegatherPending)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }),
                "The pass only ever sets it; never clears it before the reach test has had its say.");
            Assert.That(Calls(typeof(ExitDirector), nameof(ExitDirector.CarrierOf)), Is.SupersetOf(new[] { "Verse.Thing.get_ParentHolder", "Verse.AI.Group.LordUtility.GetLord" }));
            List<string> reaches = Calls(typeof(ExitDirector), "AnyMemberReaches");
            Assert.That(reaches.First(), Is.EqualTo("Verse.Thing.get_Spawned"), "The downed pawn itself first: one somebody else carries, or off the map, cannot be reached now.");
            Assert.That(reaches, Is.SupersetOf(new[]
            {
                "CaravansCriticallyEnhanced.Utils.ExitDirector.IsWorker",
                "Verse.Pawn.get_IsColonist",
                "Verse.PawnCapacitiesHandler.CapableOf",
                "Verse.ReachabilityUtility.CanReach",
            }), "A colonist with hands that can reach it; only one working the duties when asked for workers.");
            Assert.That(reaches, Has.None.Contains("CanReserve"), "Reach only: a reservation is a passing state, which the gathering stage waits out.");
            Assert.That(Calls(typeof(ExitDirector), nameof(ExitDirector.IsWorker)), Is.SupersetOf(new[]
            {
                "Verse.Pawn.get_IsColonist",
                "Verse.Pawn.get_Drafted",
                "Verse.Pawn.get_InMentalState",
                "Verse.PawnCapacitiesHandler.CapableOf",
            }), "A drafted pawn takes orders above its duty; a broken one is left out.");
        }

        [Test]
        public void EveryExitWriteKeepsTheCopiesDutiesAndWalksInStep()
        {
            List<string> write = Calls(typeof(ExitDirector), "Write");
            int jobExit = write.IndexOf("CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_JobExitSpot");
            int copies = write.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.WriteCopies");
            int duties = write.IndexOf("Verse.AI.Group.LordToil.UpdateAllDuties");
            int walks = write.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.EndStaleWalks");
            Assert.That(new[] { jobExit, copies, duties, walks }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", write));
            Assert.That(new[] { jobExit, copies, duties, walks }, Is.Ordered, "Duties are rebuilt from the copies, so the copies come first.");
            Assert.That(ILCalls.Read(Method(typeof(ExitDirector), "Write")).Fields.Count(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.LastWrittenSpot)), Is.EqualTo(1),
                "Unrecorded, the next pass would take the mod's own write for another mod's.");

            WalkResult park = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Park)));
            Assert.That(park.Calls.Select(ILCalls.Describe), Does.Contain("CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_JobMeetingPoint"), "Held: the copies point at the meeting point.");
            Assert.That(park.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.Holding)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }));
            WalkResult keep = ILCalls.Read(Method(typeof(ExitDirector), "KeepParked"));
            Assert.That(keep.Calls.Last().Name, Is.EqualTo("StopCarrying"), "On every held pass: a pick-up already under way when the hold began, a carrier back at the spot.");
            Assert.That(keep.IntBeforeCall[keep.Calls.Count - 1], Is.EqualTo(0), "Put down only at the spot.");
            WalkResult begin = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Begin)));
            int parked = begin.Calls.ToList().FindIndex(c => c.Name == nameof(ExitDirector.Park));
            int stopped = begin.Calls.ToList().FindIndex(c => c.Name == "StopCarrying");
            Assert.That(stopped, Is.GreaterThan(parked), "Held at the send: a baby a colonist beside the crib picked up inside the send is put down where it is.");
            Assert.That(begin.IntBeforeCall[stopped], Is.EqualTo(1), "Wherever the carrier stands.");
            List<string> stop = Calls(typeof(ExitDirector), "StopCarrying");
            Assert.That(stop, Is.SupersetOf(new[]
            {
                "Verse.Pawn.get_Drafted",
                "CaravansCriticallyEnhanced.Utils.ExitReadings.IsAmong",
                "Verse.AI.Pawn_JobTracker.EndCurrentJob",
                "Verse.IntVec3.InHorDistOf",
                "Verse.Pawn_CarryTracker.TryDropCarriedThing",
            }));
            Assert.That(stop.IndexOf("Verse.IntVec3.InHorDistOf"), Is.LessThan(stop.IndexOf("Verse.Pawn_CarryTracker.TryDropCarriedThing")), "Put down only back at the spot.");
            WalkResult release = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Release)));
            Assert.That(release.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.Holding)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 0 }));

            List<string> walksCalls = Calls(typeof(ExitDirector), "EndStaleWalks");
            Assert.That(walksCalls, Does.Contain("Verse.Pawn.get_Drafted"), "A drafted pawn is the player's.");
            Assert.That(walksCalls.Take(2), Is.EqualTo(new[] { "Verse.AI.Group.Lord.get_CurLordToil", "System.Object.GetType" }), "Only the stage that gathers downed pawns, tested first.");
            Assert.That(walksCalls, Does.Contain("Verse.AI.Pawn_JobTracker.EndCurrentJob"));
        }

        [Test]
        public void AForeignExitOrAPlayerOverrideIsNeverOverwritten()
        {
            WalkResult begin = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Begin)));
            Assert.That(begin.Fields.Where(f => f.IsStore).Select(f => f.Field.Name), Is.SupersetOf(new[]
            {
                nameof(CaravanExitState.Initialised),
                nameof(CaravanExitState.LastWrittenSpot),
                nameof(CaravanExitState.Foreign),
            }));

            Assert.That(begin.Calls.Select(ILCalls.Describe), Does.Contain("Verse.IntVec3.op_Inequality"), "Foreign when the graph came back with another exit.");

            WalkResult tick = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Tick)));
            Assert.That(tick.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.Foreign)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }),
                "Another mod's exit is noticed and never written back.");

            WalkResult over = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.Override)));
            Assert.That(over.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.PlayerOverride)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }));
            Assert.That(over.Calls.Select(ILCalls.Describe), Does.Contain("CaravansCriticallyEnhanced.Utils.ExitDirector.Release"));
            Assert.That(Calls(typeof(Patch_CaravanFormingUtility_ForceCaravanDepart), "Prefix"), Does.Contain("CaravansCriticallyEnhanced.Utils.ExitDirector.Override"));
        }

        [Test]
        public void AHeldCaravanNeitherSetsOffNorCarriesItsDownedToTheEdge()
        {
            // Asked every tick while the caravan waits for sleepers: vanilla's answer first.
            List<string> trigger = Calls(typeof(Patch_Trigger_NoPawnsVeryTiredAndSleeping_ActivateOn), "Postfix");
            Assert.That(trigger, Is.EqualTo(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Core.CaravanExitStates.IsHolding",
            }));
            bool yes = true;
            Patch_Trigger_NoPawnsVeryTiredAndSleeping_ActivateOn.Postfix(null, ref yes);
            Assert.That(yes, Is.True, "No caravan: vanilla's answer stands.");

            Assert.That(Calls(typeof(Patch_JobGiver_PrepareCaravan_GatherDownedPawns_TryGiveJob), "Postfix"), Is.SupersetOf(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Core.CaravanExitStates.IsHolding",
            }));
            Job none = null;
            Patch_JobGiver_PrepareCaravan_GatherDownedPawns_TryGiveJob.Postfix(new Pawn(), ref none);
            Assert.That(none, Is.Null, "No job: nothing to withdraw, and the lord is never looked up.");

            Assert.That(Calls(typeof(Patch_LordToil_PrepareCaravan_GatherDownedPawns_CheckAllPawnsArrived), "Prefix"), Is.EqualTo(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Core.CaravanExitStates.IsHolding",
            }));
            var toil = (LordToil_PrepareCaravan_GatherDownedPawns)FormatterServices.GetUninitializedObject(typeof(LordToil_PrepareCaravan_GatherDownedPawns));
            Assert.That(Patch_LordToil_PrepareCaravan_GatherDownedPawns_CheckAllPawnsArrived.Prefix(toil), Is.True, "No held caravan: vanilla tests as ever.");

            Assert.That(Calls(typeof(Patch_JobGiver_GotoTravelDestination_CaravanBabyToCarry), "Postfix"), Is.SupersetOf(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Core.CaravanExitStates.IsHolding",
            }));
            Pawn baby = new Pawn();
            Pawn kept = baby;
            Patch_JobGiver_GotoTravelDestination_CaravanBabyToCarry.Postfix(new Pawn(), ref baby);
            Assert.That(baby, Is.SameAs(kept), "No held caravan: vanilla's baby stands.");
        }

        [Test]
        public void TheReadingsSearchRegionsAsVanillasSendCheckWouldAndAskTheAreaGate()
        {
            List<string> group = Calls(typeof(ExitReadings), "GroupOf");
            Assert.That(group, Is.SupersetOf(new[]
            {
                "Verse.AnimalPenUtility.NeedsToBeManagedByRope",
                "Verse.TraverseParms.WithFenceblockedOf",
                "Verse.RegionTraverser.BreadthFirstTraverse",
                "RimWorld.ForbidUtility.CaresAboutForbidden",
            }), "A pen animal is searched for as a colonist leading it.");
            Assert.That(ILCalls.CalledAnywhereInTheMod(typeof(Region), nameof(Region.Allows)).Select(ILCalls.Describe).Any(c => c.Contains("ExitReadings")), Is.True,
                "The search enters a region only where vanilla's own reachability would.");

            List<string> allowed = Calls(typeof(ExitReadings), "AllowedFor");
            Assert.That(allowed, Is.SupersetOf(new[]
            {
                "RimWorld.Pawn_PlayerSettings.get_AreaRestrictionInPawnCurrentMap",
                "CaravansCriticallyEnhanced.Core.ExitPolicy.IsRestricted",
                "Verse.Area.get_TrueCount",
            }));
            Assert.That(allowed, Does.Not.Contain("RimWorld.Pawn_PlayerSettings.get_EffectiveAreaRestrictionInPawnCurrentMap"), "Null for every pawn in a lord.");

            Assert.That(Calls(typeof(ExitReadings), "ReadThreats"), Does.Contain("RimWorld.GenHostility.IsActiveThreatToPlayer"));
            Assert.That(Calls(typeof(ExitReadings), nameof(ExitReadings.Read)), Does.Contain("RimWorld.Planet.WorldGrid.GetRotFromTo"));
            Assert.That(ToggleReads.AskedBy(Method(typeof(ExitReadings), nameof(ExitReadings.Read))), Is.EqualTo(new Toggle?[] { Toggle.AreaAwareExit, Toggle.ThreatAwareExit }));
        }

        [Test]
        public void TheAlertReadsTheHeldStateAndPlansNothing()
        {
            List<string> report = Calls(typeof(Alert_CaravanNoPermittedExit), nameof(Alert_CaravanNoPermittedExit.GetReport));
            Assert.That(report, Does.Contain("CaravansCriticallyEnhanced.Core.CaravanExitStates.IsHolding"));
            Assert.That(report.Where(c => c.Contains("ExitPlanner") || c.Contains("ExitReadings")), Is.Empty, "It is asked every few frames.");
            Assert.That(report, Is.SupersetOf(new[] { "Verse.Pawn.get_Dead", "Verse.AI.Group.LordUtility.GetLord" }), "Nobody dead or gone from the caravan is named.");
            Assert.That(report.Count(c => c == "System.Collections.Generic.List`1.Add"), Is.EqualTo(2),
                "Each named pawn goes into both lists: the alert is active only while it has culprits.");
            Assert.That(typeof(Alert_CaravanNoPermittedExit).GetConstructor(Type.EmptyTypes), Is.Not.Null, "The game builds it by reflection.");
            Assert.That(typeof(Alert_CaravanNoPermittedExit).GetMethod(nameof(Alert.GetLabel)).DeclaringType, Is.EqualTo(typeof(Alert_CaravanNoPermittedExit)));
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, type.Name + " has no " + name + ".");
            return method;
        }

        private static List<string> Calls(Type type, string name)
        {
            return ILCalls.CalledBy(Method(type, name)).Select(Generic).ToList();
        }

        private static string Generic(MethodBase method)
        {
            Type type = method.DeclaringType;
            if (type != null && type.IsGenericType)
            {
                type = type.GetGenericTypeDefinition();
            }

            return type == null ? method.Name : type.FullName + "." + method.Name;
        }
    }
}
