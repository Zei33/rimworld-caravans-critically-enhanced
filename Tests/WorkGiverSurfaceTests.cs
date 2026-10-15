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
    /// Holds the Load work giver to the shape that keeps it cheap and consistent, and its unrunnable
    /// bodies to the decisions they delegate to.
    /// </summary>
    /// <remarks>
    /// Adapted from Simple Improve's fixture of the same name. The giver needs a spawned pawn and a
    /// map, so none of its methods run here; what runs is its declared surface, by reflection, and
    /// its compiled bodies, by IL. The IL pins which calls appear and in what order, never their
    /// arguments.
    /// </remarks>
    [TestFixture]
    public class WorkGiverSurfaceTests
    {
        [Test]
        public void TheWorkGiverDeclaresNoThingRequestSoNoRegionSearchIsBuilt()
        {
            ThingRequest request = new WorkGiver_CCE_LoadCaravan().PotentialWorkThingRequest;

            Assert.That(request.IsUndefined, Is.True,
                "A thing request makes JobGiver_Work run a region search over every matching thing on every job search.");
        }

        [TestCase(nameof(WorkGiver_CCE_LoadCaravan.ShouldSkip), new[] { typeof(Pawn), typeof(bool) })]
        [TestCase(nameof(WorkGiver_CCE_LoadCaravan.PotentialWorkThingsGlobal), new[] { typeof(Pawn) })]
        [TestCase(nameof(WorkGiver_CCE_LoadCaravan.HasJobOnThing), new[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
        [TestCase(nameof(WorkGiver_CCE_LoadCaravan.JobOnThing), new[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
        public void TheWorkGiverOverridesEachScanEntryPointItself(string name, Type[] parameters)
        {
            MethodInfo method = typeof(WorkGiver_CCE_LoadCaravan).GetMethod(name, parameters);

            Assert.That(method.DeclaringType, Is.EqualTo(typeof(WorkGiver_CCE_LoadCaravan)), name + " is inherited.");
        }

        [Test]
        public void TheWorkGiverLeavesTheRegionBudgetAloneAndReachesByTouch()
        {
            var giver = new WorkGiver_CCE_LoadCaravan();

            Assert.That(giver.MaxRegionsToScanBeforeGlobalSearch, Is.EqualTo(-1));
            Assert.That(giver.PathEndMode, Is.EqualTo(PathEndMode.Touch), "The vanilla gather driver walks to its item by touch.");
        }

        [Test]
        public void TheWorkGiverDefPointsAtThisClass()
        {
            List<string> classes = DefXml.ModDefNodes
                .Where(n => n.Name.LocalName == "WorkGiverDef" && n.Element("defName")?.Value == "CCE_LoadCaravan")
                .Select(n => n.Element("giverClass")?.Value)
                .ToList();

            Assert.That(classes, Is.EqualTo(new[] { typeof(WorkGiver_CCE_LoadCaravan).FullName }));
        }

        [Test]
        public void BothScanEntryPointsAnswerFromTheSameDecisionAndNothingElse()
        {
            // JobGiver_Work uses HasJobOnThing as its scan validator and builds the winner with
            // JobOnThing, with nothing between; if they ever disagree the pawn abandons its whole work
            // search on a Log.ErrorOnce key every work giver in the game shares.
            Assert.That(CallNamesIn("HasJobOnThing"), Is.EqualTo(new[] { "CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.JobFor" }));
            Assert.That(CallNamesIn("JobOnThing"), Is.EqualTo(new[] { "CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.JobFor" }));
        }

        [Test]
        public void TheDecisionGoesThroughTheMemo()
        {
            Assert.That(CallsIn("JobFor").Select(Generic), Is.EqualTo(new[]
            {
                "Verse.Find.get_TickManager",
                "Verse.TickManager.get_TicksGame",
                "CaravansCriticallyEnhanced.Core.JobMemo`3.Answer",
            }));

            Assert.That(
                ILCalls.CalledAnywhereInTheMod(typeof(WorkGiver_CCE_LoadCaravan), "BuildJob").Select(ILCalls.Describe),
                Is.EqualTo(new[] { "CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan..ctor" }),
                "BuildJob is reached from somewhere other than the memo, which then builds jobs twice.");
        }

        [Test]
        public void TheMemoIsBuiltOverThisGiversJobBuilderAndItsCarrierCache()
        {
            ConstructorInfo constructor = typeof(WorkGiver_CCE_LoadCaravan).GetConstructor(Type.EmptyTypes);

            Assert.That(ILCalls.CalledBy(constructor).Select(Generic), Is.EqualTo(new[]
            {
                "RimWorld.WorkGiver_Scanner..ctor",
                "CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.BuildJob",
                "System.Func`4..ctor",
                "CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.ForgetCarrierGate",
                "System.Action..ctor",
                "CaravansCriticallyEnhanced.Core.JobMemo`3..ctor",
            }));
        }

        [Test]
        public void TheJobIsVanillasGatherJobPointedAtTheItemsCaravan()
        {
            // Reusing the vanilla def is what keeps vanilla's in-flight count, its completion check and
            // the pack animals' follow behaviour working for helpers, and the driver casts job.lord.
            WalkResult build = ILCalls.Read(Method("BuildJob"));

            Assert.That(build.Fields.Select(f => f.Field.DeclaringType.Name + "." + f.Field.Name + (f.IsStore ? " store" : " load")),
                Does.Contain("JobDefOf.PrepareCaravan_GatherItems load").And.Contain("Job.lord store"));
        }

        [Test]
        public void TheJobDecisionChecksInTheOrderTheReasonsNeed()
        {
            // The owner first, so every later test is about the right caravan. The carrier next: it is
            // memoised per pawn, caravan and tick, so it is free after the first item, and when there
            // is no carrier it turns every item away before the count, which walks every pawn on the
            // map. The job last.
            List<string> calls = CallNamesIn("BuildJob");

            int claim = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingIndex.ClaimOf");
            int carrier = calls.IndexOf("CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.AnyCarrierFor");
            int count = calls.IndexOf("RimWorld.GatherItemsForCaravanUtility.CountLeftToTransfer");
            int reserve = calls.IndexOf("Verse.AI.ReservationUtility.CanReserve");
            int make = calls.IndexOf("Verse.JobMaker.MakeJob");

            Assert.That(new[] { claim, carrier, count, reserve, make }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { claim, carrier, count, reserve, make }, Is.Ordered);
        }

        [Test]
        public void TheCarrierCheckIsVanillasPredicatePlusTheHelpersArea()
        {
            List<string> calls = CallNamesIn("AnyCarrierFor");

            Assert.That(calls, Does.Contain("RimWorld.JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier"));
            Assert.That(calls, Does.Contain("RimWorld.ForbidUtility.IsForbidden"));
        }

        [Test]
        public void SkippingIsDecidedByTheSettingTheMembershipAndTheOpenCaravans()
        {
            List<string> calls = CallNamesIn("ShouldSkip");

            Assert.That(calls, Does.Contain("Verse.AI.Group.LordUtility.GetLord"), "A pawn in any lord has its job's lord overwritten by its duty.");
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.LoadingIndex.AnyOpen"));
            Assert.That(ToggleReads.AskedBy(Method("ShouldSkip")), Is.EqualTo(new Toggle?[] { Toggle.LoadingWorkType, Toggle.EarlyLoading }),
                "Also the positive control for ToggleReads: two switches read by opcode, in IL order.");
        }

        [Test]
        public void WithNothingToLoadTheGiverLetsGoOfWhatItHeld()
        {
            // One instance lives all session; a held pawn, lord or job would keep the last save's map.
            List<string> skip = CallNamesIn("ShouldSkip");
            List<string> forget = CallsIn("ForgetEverything").Select(Generic).ToList();

            Assert.That(skip.IndexOf("CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.ForgetEverything"),
                Is.GreaterThan(skip.IndexOf("CaravansCriticallyEnhanced.Core.LoadingIndex.AnyOpen")));
            Assert.That(forget, Is.EqualTo(new[]
            {
                "CaravansCriticallyEnhanced.Core.JobMemo`3.Rekey",
                "CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan.ForgetCarrierGate",
            }));
        }

        [Test]
        public void TheSearchSetHasItsOwnEarlyExitBeforeTheSetIsBuilt()
        {
            // The float menu asks for the set before it asks ShouldSkip.
            List<string> calls = CallNamesIn("PotentialWorkThingsGlobal");

            int open = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingIndex.AnyOpen");
            int set = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingIndex.ThingsOn");

            Assert.That(open, Is.GreaterThanOrEqualTo(0));
            Assert.That(set, Is.GreaterThan(open));
        }

        private static MethodInfo Method(string name)
        {
            MethodInfo method = typeof(WorkGiver_CCE_LoadCaravan).GetMethod(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, "WorkGiver_CCE_LoadCaravan has no " + name + ".");
            return method;
        }

        private static List<MethodBase> CallsIn(string name)
        {
            return ILCalls.CalledBy(Method(name)).ToList();
        }

        private static List<string> CallNamesIn(string name)
        {
            return CallsIn(name).Select(ILCalls.Describe).ToList();
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
