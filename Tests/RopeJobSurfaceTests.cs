using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Jobs;
using NUnit.Framework;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the rope work giver and its driver to their shape, and their unrunnable bodies to the
    /// decisions they delegate to.
    /// </summary>
    /// <remarks>
    /// The same approach as <see cref="WorkGiverSurfaceTests"/>: nothing here needs a map, so the
    /// declared surface is read by reflection and the bodies by IL. The IL pins which calls appear
    /// and in what order, never their arguments or the branches between them.
    /// </remarks>
    [TestFixture]
    public class RopeJobSurfaceTests
    {
        private static readonly Type Giver = typeof(WorkGiver_CCE_RopeCaravanAnimals);
        private static readonly Type Driver = typeof(JobDriver_CCE_RopeAnimalToCaravan);

        [Test]
        public void TheGiverDeclaresNoThingRequestSoNoRegionSearchIsBuilt()
        {
            Assert.That(new WorkGiver_CCE_RopeCaravanAnimals().PotentialWorkThingRequest.IsUndefined, Is.True);
        }

        [TestCase(nameof(WorkGiver_Scanner.ShouldSkip), new[] { typeof(Pawn), typeof(bool) })]
        [TestCase(nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal), new[] { typeof(Pawn) })]
        [TestCase(nameof(WorkGiver_Scanner.HasJobOnThing), new[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
        [TestCase(nameof(WorkGiver_Scanner.JobOnThing), new[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
        public void TheGiverOverridesEachScanEntryPointItself(string name, Type[] parameters)
        {
            Assert.That(Giver.GetMethod(name, parameters).DeclaringType, Is.EqualTo(Giver), name + " is inherited.");
        }

        [Test]
        public void TheGiverReachesItsAnimalByTouch()
        {
            var giver = new WorkGiver_CCE_RopeCaravanAnimals();

            Assert.That(giver.MaxRegionsToScanBeforeGlobalSearch, Is.EqualTo(-1));
            Assert.That(giver.PathEndMode, Is.EqualTo(PathEndMode.Touch));
        }

        [Test]
        public void TheDefsPointAtTheseClasses()
        {
            Assert.That(
                DefXml.ModDefNodes
                    .Where(n => n.Name.LocalName == "WorkGiverDef" && n.Element("defName")?.Value == "CCE_RopeAnimalsToCaravan")
                    .Select(n => n.Element("giverClass")?.Value),
                Is.EqualTo(new[] { Giver.FullName }));

            Assert.That(
                DefXml.ModDefNodes
                    .Where(n => n.Name.LocalName == "JobDef" && n.Element("defName")?.Value == "CCE_RopeAnimalToCaravan")
                    .Select(n => n.Element("driverClass")?.Value),
                Is.EqualTo(new[] { Driver.FullName }));
        }

        [Test]
        public void BothScanEntryPointsAnswerFromTheSameDecisionAndNothingElse()
        {
            string jobFor = Giver.FullName + ".JobFor";

            Assert.That(CallNamesIn(Giver, "HasJobOnThing"), Is.EqualTo(new[] { jobFor }));
            Assert.That(CallNamesIn(Giver, "JobOnThing"), Is.EqualTo(new[] { jobFor }));
            Assert.That(
                ILCalls.CalledAnywhereInTheMod(Giver, "BuildJob").Select(ILCalls.Describe),
                Is.EqualTo(new[] { Giver.FullName + "..ctor" }),
                "BuildJob is reached from somewhere other than the memo.");
        }

        [Test]
        public void SkippingIsDecidedByTheSettingTheColonistTheMembershipAndTheGathering()
        {
            List<string> calls = CallNamesIn(Giver, "ShouldSkip");

            Assert.That(calls, Does.Contain("Verse.Pawn.get_IsColonist"));
            Assert.That(calls, Does.Contain("Verse.AI.Group.LordUtility.GetLord"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.AnyGathering"));
            Assert.That(calls.IndexOf("CaravansCriticallyEnhanced.Core.JobMemo`3.Rekey"),
                Is.GreaterThan(calls.IndexOf("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.AnyGathering")),
                "With nothing gathering, the memo lets go of the last save's pawns and jobs.");
            Assert.That(ToggleReads.AskedBy(Method(Giver, "ShouldSkip")), Is.EqualTo(new Toggle?[] { Toggle.HelpersRopeAnimals }));
        }

        [Test]
        public void TheSearchSetHasItsOwnEarlyExitBeforeTheSetIsBuilt()
        {
            List<string> calls = CallNamesIn(Giver, "PotentialWorkThingsGlobal");

            int any = calls.IndexOf("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.AnyGathering");
            int set = calls.IndexOf("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.AnimalsOn");

            Assert.That(any, Is.GreaterThanOrEqualTo(0));
            Assert.That(set, Is.GreaterThan(any));
        }

        [Test]
        public void TheJobDecisionChecksInTheOrderTheReasonsNeed()
        {
            // The caravan first, so every later test is about the right spot; the area and the
            // reservation before the path, which is the expensive one; the job last.
            List<string> calls = CallNamesIn(Giver, "BuildJob");

            int destination = calls.IndexOf("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.TryGetDestination");
            int area = calls.IndexOf("RimWorld.ForbidUtility.IsForbidden");
            int reserve = calls.IndexOf("Verse.AI.ReservationUtility.CanReserve");
            int path = calls.IndexOf("RimWorld.GatherAnimalsAndSlavesForCaravanUtility.CanRoperTakeAnimalToDest");
            int make = calls.IndexOf("Verse.JobMaker.MakeJob");

            int[] order = { destination, area, reserve, path, make };
            Assert.That(order, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(order, Is.Ordered);
        }

        [Test]
        public void ARightClickNeverTakesAnAnimalOverFromAnotherPawn()
        {
            // CanReserve's last argument is ignoreOtherReservations. A literal false there, rather than
            // the forced flag, is what stops a forced ask ending a member's rope job and scattering
            // every animal it leads.
            WalkResult walk = ILCalls.Read(Method(Giver, "BuildJob"));
            int index = walk.Calls.ToList().FindIndex(m => m.Name == "CanReserve");

            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(walk.Calls[index].GetParameters().Last().Name, Is.EqualTo("ignoreOtherReservations"));
            Assert.That(walk.OpcodeBeforeCall[index], Is.EqualTo(ILCalls.LoadZero));
        }

        [Test]
        public void TheJobIsTheModsRopeJobPointedAtTheAnimalsCaravan()
        {
            WalkResult build = ILCalls.Read(Method(Giver, "BuildJob"));

            Assert.That(
                build.Fields.Select(f => f.Field.DeclaringType.Name + "." + f.Field.Name + (f.IsStore ? " store" : " load")),
                Does.Contain("CaravansCriticallyEnhancedDefOf.CCE_RopeAnimalToCaravan load").And.Contain("Job.lord store"));
        }

        [Test]
        public void TheDriverIsARopeDriverSoTheAnimalsFollowIt()
        {
            // JobGiver_FollowRoper and JobDriver_FollowRoper both test the roper's driver with
            // "is JobDriver_RopeToDestination"; any other base and the animal stands still.
            Assert.That(typeof(JobDriver_RopeToDestination).IsAssignableFrom(Driver), Is.True);
        }

        [TestCase("HasRopeeArrived")]
        [TestCase("ProcessArrivedRopee")]
        [TestCase("ShouldOpportunisticallyRopeAnimal")]
        [TestCase("MakeNewToils")]
        public void TheDriverOverridesEachHookItself(string name)
        {
            Assert.That(Method(Driver, name).DeclaringType, Is.EqualTo(Driver));
        }

        [Test]
        public void ArrivalIsJudgedAgainstTargetB()
        {
            // A helper has no duty, so vanilla's reading of the roper's duty would never arrive.
            List<string> calls = CallNamesIn(Driver, "HasRopeeArrived");

            Assert.That(calls, Does.Contain("Verse.AI.Job.GetTarget"));
            Assert.That(calls, Does.Contain("Verse.GenAdj.InHorDistOf").Or.Contain("Verse.IntVec3.InHorDistOf"));
            Assert.That(calls.Count(c => c.EndsWith(".GetDistrict", StringComparison.Ordinal)), Is.EqualTo(3), string.Join(", ", calls));
        }

        [Test]
        public void AnArrivedAnimalIsTiedAtTheSpot()
        {
            // At target B, never at the animal's duty: in the pause for a mental break, which the job
            // carries on through, every duty is the pause duty and has no spot.
            List<string> calls = CallNamesIn(Driver, "ProcessArrivedRopee");
            WalkResult walk = ILCalls.Read(Method(Driver, "ProcessArrivedRopee"));

            Assert.That(calls, Does.Contain("RimWorld.Pawn_RopeTracker.RopeToSpot"));
            Assert.That(calls, Does.Contain("Verse.AI.Group.LordUtility.GetLord"), "An animal taken out of the caravan on the way is let go, not tied.");
            Assert.That(calls.IndexOf("Verse.AI.Job.GetTarget"), Is.GreaterThanOrEqualTo(0));
            Assert.That(calls.IndexOf("Verse.AI.Job.GetTarget"), Is.LessThan(calls.IndexOf("RimWorld.Pawn_RopeTracker.RopeToSpot")));
            Assert.That(walk.Fields.Select(f => f.Field.Name), Does.Not.Contain("duty"));
        }

        [Test]
        public void PickingUpOnTheWayObeysTheSearchRule()
        {
            // Same caravan, same rule, the helper's area (the base search never asks it), then the
            // reservation and the path.
            List<string> calls = CallNamesIn(Driver, "ShouldOpportunisticallyRopeAnimal");
            WalkResult walk = ILCalls.Read(Method(Driver, "ShouldOpportunisticallyRopeAnimal"));

            Assert.That(calls, Does.Contain("Verse.AI.Group.LordUtility.GetLord"));
            Assert.That(walk.Fields.Select(f => f.Field.DeclaringType.Name + "." + f.Field.Name), Does.Contain("Job.lord"));
            Assert.That(calls, Does.Contain("RimWorld.ForbidUtility.IsForbidden"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.NeedsLeading"));
            Assert.That(calls, Does.Contain("Verse.AI.ReservationUtility.CanReserve"));
            Assert.That(calls, Does.Contain("RimWorld.GatherAnimalsAndSlavesForCaravanUtility.CanRoperTakeAnimalToDest"));
        }

        [Test]
        public void TheToilsAreTheBaseToilsBehindOneFailCondition()
        {
            // Not an iterator: the condition is attached when the toils are set up, and the base
            // iterator, with its finish action that drops every rope, is returned as it is.
            Assert.That(Method(Driver, "MakeNewToils").IsDefined(typeof(System.Runtime.CompilerServices.IteratorStateMachineAttribute), false), Is.False);

            List<string> calls = CallNamesIn(Driver, "MakeNewToils");
            int fail = calls.IndexOf("Verse.AI.ToilFailConditions.FailOn");
            int toils = calls.IndexOf("RimWorld.JobDriver_RopeToDestination.MakeNewToils");

            Assert.That(fail, Is.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(toils, Is.GreaterThan(fail));
        }

        [Test]
        public void TheFailConditionAsksWhetherTheCaravanIsStillGathering()
        {
            List<string> calls = CallNamesIn(Driver, "CaravanMovedOn");

            Assert.That(calls, Does.Contain("System.Collections.Generic.List`1.Contains"), "A removed lord keeps its toil, so only the map's list says it is gone.");
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.StageOf"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.FormingStages.WorkingStageOf"));
        }

        [Test]
        public void TheIndexAppliesTheCandidateRuleToEachAnimal()
        {
            // The pure rule is tested on its own; these hold the reader to asking it, with the real
            // stage, about animals only.
            Type index = typeof(CaravansCriticallyEnhanced.Core.AnimalGatherIndex);

            List<string> fresh = CallNamesIn(index, "Fresh");
            Assert.That(fresh, Does.Contain("Verse.RaceProperties.get_Animal"));
            Assert.That(fresh, Does.Contain("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.NeedsLeading"));
            Assert.That(fresh, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.StageOf"));

            List<string> needs = CallNamesIn(index, "NeedsLeading");
            int stage = needs.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.StageOf");
            int rule = needs.IndexOf("CaravansCriticallyEnhanced.Core.RopeCandidates.NeedsLeading");

            Assert.That(needs, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla"));
            Assert.That(needs, Does.Contain("Verse.AnimalPenUtility.NeedsToBeManagedByRope"));
            Assert.That(needs, Does.Contain("RimWorld.Pawn_RopeTracker.get_IsRopedByPawn"));
            Assert.That(needs, Does.Contain("RimWorld.Pawn_RopeTracker.get_RopedToSpot"), "Tied counts only at the duty's own spot.");
            Assert.That(stage, Is.GreaterThanOrEqualTo(0), string.Join(", ", needs));
            Assert.That(rule, Is.GreaterThan(stage));
        }

        [Test]
        public void TheDestinationIsTheAnimalsOwnDutySpot()
        {
            // Asked again live: the set may be from before a cancel, a downing or a removal this tick.
            WalkResult walk = ILCalls.Read(Method(typeof(CaravansCriticallyEnhanced.Core.AnimalGatherIndex), "TryGetDestination"));

            Assert.That(CallNamesIn(typeof(CaravansCriticallyEnhanced.Core.AnimalGatherIndex), "TryGetDestination"),
                Does.Contain("CaravansCriticallyEnhanced.Core.AnimalGatherIndex.NeedsLeading"));

            Assert.That(walk.Fields.Select(f => f.Field.Name), Does.Contain("duty"));
            Assert.That(walk.Fields.Select(f => f.Field.Name), Does.Contain("focus"));
        }

        [Test]
        public void TheRopeGiverComesBeforeLoadingAndNeverToMechs()
        {
            // Animals are gathered before items, so the rope giver sits higher in the Load column;
            // and only colonists rope animals in vanilla.
            System.Xml.Linq.XElement rope = DefXml.ModDefNodes.Single(n => n.Name.LocalName == "WorkGiverDef" && n.Element("defName")?.Value == "CCE_RopeAnimalsToCaravan");
            System.Xml.Linq.XElement load = DefXml.ModDefNodes.Single(n => n.Name.LocalName == "WorkGiverDef" && n.Element("defName")?.Value == "CCE_LoadCaravan");

            Assert.That(int.Parse(rope.Element("priorityInType").Value), Is.GreaterThan(int.Parse(load.Element("priorityInType").Value)));
            Assert.That(rope.Element("workType")?.Value, Is.EqualTo("CCE_Loading"));
            Assert.That(rope.Element("canBeDoneByMechs")?.Value, Is.EqualTo("false"));
        }

        [Test]
        public void CancellingACaravanEndsItsHelpersRopeJobsBeforeVanillaRuns()
        {
            // Vanilla leads home only animals nobody holds, and helpers are not members.
            List<string> prefix = ILCalls.CalledBy(typeof(CaravansCriticallyEnhanced.Patches.Patch_CaravanFormingUtility_StopFormingCaravan).GetMethod("Prefix"))
                .Select(ILCalls.Describe).ToList();
            List<string> letGo = CallNamesIn(Driver, "LetGoFor");

            Assert.That(prefix.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla"), Is.GreaterThanOrEqualTo(0));
            Assert.That(prefix.IndexOf("CaravansCriticallyEnhanced.Jobs.JobDriver_CCE_RopeAnimalToCaravan.LetGoFor"),
                Is.GreaterThan(prefix.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla")));
            // Queued rope jobs too: they keep their reservations, which make the pen lord's rope job
            // fail for the member who takes the animal.
            Assert.That(letGo, Does.Contain("Verse.AI.Pawn_JobTracker.EndCurrentOrQueuedJob"));
            Assert.That(letGo, Does.Contain("Verse.AI.JobQueue.get_Item"));
            Assert.That(CallNamesIn(Driver, "IsRopeJobFor"), Is.Empty, "A pure field test: def and lord.");
            Assert.That(
                ILCalls.Read(Method(Driver, "IsRopeJobFor")).Fields.Select(f => f.Field.DeclaringType.Name + "." + f.Field.Name),
                Is.SupersetOf(new[] { "Job.def", "Job.lord", "CaravansCriticallyEnhancedDefOf.CCE_RopeAnimalToCaravan" }));
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, type.Name + " has no " + name + ".");
            return method;
        }

        private static List<string> CallNamesIn(Type type, string name)
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
