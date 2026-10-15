using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Jobs;
using CaravansCriticallyEnhanced.Patches;
using CaravansCriticallyEnhanced.Utils;
using NUnit.Framework;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds each lead patch, the drop-lead button, the take-lead option and its driver to the rule
    /// they hand over to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Harmony cannot patch on this runtime, but a patch method is an ordinary static method, and a few
    /// of these run here when called directly with a bare <c>new Pawn()</c>: the draft setter's prefix
    /// and finalizer, the <c>DropRopes</c> prefix, the rope tick prefix's ways back to vanilla, and the
    /// first exit of <see cref="LeadUtility.KeepsLeads"/>. Those are run. A bare pawn is never spawned
    /// and the settings are null outside the game, so the paths that keep leads cannot run. Nor can a
    /// pawn be given a <c>Pawn_RopeTracker</c>: its static initialiser loads a rope material, and an
    /// uninitialised instance runs it as soon as a field is set, so the exits after the first are
    /// pinned only by the IL.
    /// </para>
    /// <para>
    /// For everything else the IL pins which calls appear and in what order, never their arguments or
    /// the branches between them; <c>Tests/README.md</c> lists the in-game checks for the rest.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class LeadPatchBodyTests
    {
        [TearDown]
        public void LeaveNoScopeOpen()
        {
            Assert.That(LeadKeepScopeTests.Empty(), Is.EqualTo(0), "A test left a scope open.");
            Assert.That(LeadUndraftNoticesTests.Empty(), Is.EqualTo(0), "A test left a notice waiting.");
        }

        [Test]
        public void DraftingAPawnTheModDoesNotHoldOpensNoScope()
        {
            var controller = new Pawn_DraftController(new Pawn());

            Patch_Pawn_DraftController_Drafted.Prefix(controller, true, out bool drafting);
            Patch_Pawn_DraftController_Drafted.Prefix(controller, false, out bool undrafting);

            Assert.That(new[] { drafting, undrafting }, Is.EqualTo(new[] { false, false }));
            Assert.That(LeadKeepScope.Depth, Is.EqualTo(0));
        }

        [Test]
        public void TheFinalizerClosesItsOwnScopeWhicheverWayThePawnWent()
        {
            foreach (bool value in new[] { true, false })
            {
                var pawn = new Pawn();
                LeadKeepScope.Push(pawn);

                Patch_Pawn_DraftController_Drafted.Finalizer(new Pawn_DraftController(pawn), value, true);

                Assert.That(LeadKeepScope.Contains(pawn), Is.False, value ? "Drafting." : "Undrafting.");
                Assert.That(LeadKeepScope.Depth, Is.EqualTo(0), value ? "Drafting." : "Undrafting.");
            }
        }

        [Test]
        public void TheFinalizerLeavesAScopeItDidNotOpen()
        {
            // An outer draft of the same pawn, by another mod from inside the setter, owns this one.
            var pawn = new Pawn();
            LeadKeepScope.Push(pawn);

            Patch_Pawn_DraftController_Drafted.Finalizer(new Pawn_DraftController(pawn), true, false);
            Patch_Pawn_DraftController_Drafted.Finalizer(new Pawn_DraftController(pawn), false, false);

            Assert.That(LeadKeepScope.Contains(pawn), Is.True);
            Assert.That(LeadKeepScope.Pop(pawn), Is.True);
        }

        [Test]
        public void DroppingRopesRunsVanillaOutsideTheScopeAndForAnUnspawnedPawnInsideIt()
        {
            var pawn = new Pawn();
            Assert.That(Patch_Pawn_RopeTracker_DropRopes.Prefix(pawn), Is.True, "Outside the scope.");

            LeadKeepScope.Push(pawn);
            Assert.That(Patch_Pawn_RopeTracker_DropRopes.Prefix(pawn), Is.True, "Despawned: a rope must never reach a world caravan.");
            Assert.That(Patch_Pawn_RopeTracker_DropRopes.Prefix(new Pawn()), Is.True, "Another pawn's scope.");
            LeadKeepScope.Pop(pawn);
        }

        [Test]
        public void TheRopeTickHandsEveryRoperTheModDoesNotHoldToVanilla()
        {
            var pawn = new Pawn();

            Assert.That(Patch_Pawn_RopeTracker_RopingTick.Prefix(null, pawn, null), Is.True, "No list.");
            Assert.That(Patch_Pawn_RopeTracker_RopingTick.Prefix(null, pawn, new List<Pawn>()), Is.True, "Leading nothing.");
            Assert.That(Patch_Pawn_RopeTracker_RopingTick.Prefix(null, pawn, new List<Pawn> { new Pawn() }), Is.True, "Leading, but not held.");
        }

        [Test]
        public void KeepingLeadsAnswersNoForAPawnWithNoRopeTracker()
        {
            // The settings are null here, so reaching them would throw.
            Assert.That(LeadUtility.KeepsLeads(null), Is.False, "No pawn.");
            Assert.That(LeadUtility.KeepsLeads(new Pawn()), Is.False, "No rope tracker.");
            Assert.That(LeadUtility.KeepsLeadsWhenDrafted(new Pawn()), Is.False, "No rope tracker.");
        }

        [Test]
        public void TheLetGoPostfixIgnoresANullAnimal()
        {
            Assert.DoesNotThrow(() => Patch_Pawn_RopeTracker_DropRope.Postfix(new Pawn(), null));
        }

        [Test]
        public void TheRopeTickLeavesAtOnceForAPawnLeadingNothing()
        {
            // Every tick, every spawned pawn: the count is read before anything else.
            List<string> calls = Calls(typeof(Patch_Pawn_RopeTracker_RopingTick), "Prefix");

            Assert.That(calls.First(), Is.EqualTo("System.Collections.Generic.List`1.get_Count"), string.Join(", ", calls));
            Assert.That(calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.KeepsLeads"), Is.GreaterThan(0));
        }

        [Test]
        public void TheRopeTickStillBreaksOnVanillasOtherConditionsAndLooksAfterTheAnimals()
        {
            List<string> calls = Calls(typeof(Patch_Pawn_RopeTracker_RopingTick), "Prefix");

            int keeps = calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.KeepsLeads");
            int fatal = calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.IsFatal");
            int breakAll = calls.IndexOf("RimWorld.Pawn_RopeTracker.BreakAllRopes");
            int interval = calls.IndexOf("Verse.Gen.IsHashIntervalTick");
            int needy = calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.ReleaseNeedyRopees");
            int speed = calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.MatchSpeed");

            int[] order = { keeps, fatal, breakAll, interval, needy, speed };
            Assert.That(order, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(order, Is.Ordered);

            WalkResult walk = ILCalls.Read(Method(typeof(Patch_Pawn_RopeTracker_RopingTick), "Prefix"));
            int hash = walk.Calls.Select(ILCalls.Describe).ToList().IndexOf("Verse.Gen.IsHashIntervalTick");
            Assert.That(walk.IntBeforeCall[hash], Is.EqualTo(Patch_Pawn_RopeTracker_RopingTick.NeedsInterval), "Every 250 ticks, not every tick.");
            Assert.That(walk.Calls[hash].GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(Thing), typeof(int) }),
                "The overload whose last argument is the interval.");
            Assert.That(Patch_Pawn_RopeTracker_RopingTick.NeedsInterval, Is.EqualTo(250));
        }

        [Test]
        public void TheRopersReadingsGoThroughThePolicy()
        {
            List<string> keeps = Calls(typeof(LeadUtility), nameof(LeadUtility.KeepsLeads));
            Assert.That(keeps, Does.Contain("CaravansCriticallyEnhanced.Core.LeadPolicy.KeepsLeads"));
            Assert.That(keeps, Does.Contain("Verse.Pawn.get_Drafted"));
            Assert.That(keeps, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.VanillaLordOf"));
            Assert.That(keeps, Does.Contain("RimWorld.Pawn_RopeTracker.get_IsRoped"));
            Assert.That(keeps.First(), Is.EqualTo("Verse.Pawn.get_Drafted"), "The cheapest exit first: " + string.Join(", ", keeps));
            Assert.That(keeps.IndexOf("System.Collections.Generic.List`1.get_Count"),
                Is.InRange(0, keeps.IndexOf("CaravansCriticallyEnhanced.CaravansCriticallyEnhancedMod.get_Settings")), "Then leading nothing, before the settings.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(LeadUtility), nameof(LeadUtility.KeepsLeads))), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));

            List<string> whenDrafted = Calls(typeof(LeadUtility), nameof(LeadUtility.KeepsLeadsWhenDrafted));
            Assert.That(whenDrafted, Does.Contain("CaravansCriticallyEnhanced.Core.LeadPolicy.KeepsLeads"));
            Assert.That(whenDrafted, Does.Contain("Verse.Thing.get_Spawned"));
            Assert.That(whenDrafted, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.VanillaLordOf"), "Only a caravan's colonist keeps its leads.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(LeadUtility), nameof(LeadUtility.KeepsLeadsWhenDrafted))), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));

            List<string> fatal = Calls(typeof(LeadUtility), nameof(LeadUtility.IsFatal));
            Assert.That(fatal, Does.Contain("CaravansCriticallyEnhanced.Core.LeadPolicy.IsFatal"));
            Assert.That(fatal, Does.Contain("Verse.Pawn.get_Downed"));
            Assert.That(fatal, Does.Contain("RimWorld.RestUtility.Awake"));
            Assert.That(fatal, Does.Contain("Verse.Pawn.get_InMentalState"));
            Assert.That(fatal, Does.Contain("Verse.Pawn.get_Dead"));
            Assert.That(fatal, Does.Contain("RimWorld.FireUtility.IsBurning"));
            Assert.That(fatal, Does.Not.Contain("Verse.Pawn.get_Drafted"), "Being drafted is the one condition dropped on purpose.");
        }

        [Test]
        public void NeedyAnimalsAreLetGoWithAMessageAndTheRoperMatchesTheSlowest()
        {
            List<string> needy = Calls(typeof(LeadUtility), nameof(LeadUtility.ReleaseNeedyRopees));
            int stage = needy.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.StageOf");
            int leaving = needy.IndexOf("CaravansCriticallyEnhanced.Core.LeadPolicy.ReleasesNeedyAnimals");
            int rule = needy.IndexOf("CaravansCriticallyEnhanced.Core.LeadPolicy.ShouldRelease");
            int drop = needy.IndexOf("RimWorld.Pawn_RopeTracker.DropRope");
            int message = needy.IndexOf("CaravansCriticallyEnhanced.Utils.CaravanMessages.Post");
            Assert.That(new[] { stage, leaving, rule, drop, message }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", needy));
            Assert.That(new[] { stage, leaving, rule, drop, message }, Is.Ordered, "Nothing is let go while the caravan leaves.");

            WalkResult speed = ILCalls.Read(Method(typeof(LeadUtility), nameof(LeadUtility.MatchSpeed)));
            Assert.That(speed.Fields.Where(f => f.IsStore).Select(f => f.Field.Name), Does.Contain("locomotionUrgencySameAs"));
            Assert.That(speed.Calls.Select(ILCalls.Describe), Does.Contain("Verse.Pawn.get_TicksPerMoveCardinal"));
        }

        [Test]
        public void DraftingOpensTheScopeAndTheFinalizerAlwaysClosesIt()
        {
            List<string> prefix = Calls(typeof(Patch_Pawn_DraftController_Drafted), "Prefix");
            List<string> finalizer = Calls(typeof(Patch_Pawn_DraftController_Drafted), "Finalizer");

            Assert.That(prefix.IndexOf("CaravansCriticallyEnhanced.Core.LeadKeepScope.Push"),
                Is.GreaterThan(prefix.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.KeepsLeadsWhenDrafted")));
            Assert.That(prefix.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.KeepsLeadsWhenDrafted"), Is.GreaterThanOrEqualTo(0));
            Assert.That(finalizer.First(), Is.EqualTo("CaravansCriticallyEnhanced.Core.LeadKeepScope.Pop"), "Closing comes before every call that could throw: " + string.Join(", ", finalizer));
            Assert.That(finalizer, Does.Contain("CaravansCriticallyEnhanced.Core.LeadUndraftNotices.Expect"));
            Assert.That(finalizer, Does.Not.Contain("CaravansCriticallyEnhanced.Utils.CaravanMessages.Post"), "A break is not recorded yet here, so the rope tick tells.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_Pawn_DraftController_Drafted), "Finalizer")), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));
        }

        [Test]
        public void TheUndraftMessageWaitsForTheRopeTickThatDropsTheLeadsAndSkipsABreak()
        {
            // TryStartMentalState undrafts before it records the break; by the next rope tick it has.
            List<string> tick = Calls(typeof(Patch_Pawn_RopeTracker_RopingTick), "Prefix");
            int count = tick.IndexOf("CaravansCriticallyEnhanced.Core.LeadUndraftNotices.get_Count");
            int tell = tick.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.TellUndraftDroppedLeads");
            int fatal = tick.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.IsFatal");
            Assert.That(new[] { tick.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.KeepsLeads"), count, tell, fatal }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", tick));
            Assert.That(new[] { count, tell, fatal }, Is.Ordered, "Only for a roper the mod is not holding, and only when a notice waits.");

            List<string> told = Calls(typeof(LeadUtility), nameof(LeadUtility.TellUndraftDroppedLeads));
            int take = told.IndexOf("CaravansCriticallyEnhanced.Core.LeadUndraftNotices.Take");
            int broken = told.IndexOf("Verse.Pawn.get_InMentalState");
            int post = told.IndexOf("CaravansCriticallyEnhanced.Utils.CaravanMessages.Post");
            Assert.That(new[] { take, broken, post }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", told));
            Assert.That(new[] { take, broken, post }, Is.Ordered);
        }

        [Test]
        public void ALeavingCaravanGoesBackForAStrandedAnimalOnlyWhenAColonistWouldFetchIt()
        {
            List<string> pass = Calls(typeof(Patch_LordJob_FormAndSendCaravan_LordJobTick), "Postfix");
            Assert.That(pass, Does.Contain("CaravansCriticallyEnhanced.Utils.LeadUtility.RecollectStrandedAnimals"));

            MethodInfo recollect = Method(typeof(LeadUtility), nameof(LeadUtility.RecollectStrandedAnimals));
            List<string> calls = Calls(typeof(LeadUtility), nameof(LeadUtility.RecollectStrandedAnimals));
            int watch = calls.IndexOf("CaravansCriticallyEnhanced.Core.LeadPolicy.WatchesForStrandedAnimals");
            int fetch = calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeadUtility.AnyColonistWouldFetch");
            int stranded = calls.IndexOf("CaravansCriticallyEnhanced.Core.LeadPolicy.IsStrandedWhileLeaving");
            int decide = calls.IndexOf("CaravansCriticallyEnhanced.Core.LeadPolicy.SendsBackToCollect");
            int memo = calls.IndexOf("Verse.AI.Group.Lord.ReceiveMemo");
            Assert.That(new[] { watch, fetch, stranded, decide, memo }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { watch, fetch, stranded, decide, memo }, Is.Ordered);
            Assert.That(calls, Does.Contain("Verse.ReachabilityUtility.CanReach"), "Whether it can walk out by itself.");
            Assert.That(calls, Does.Contain("RimWorld.PawnUtility.ResolveMaxDanger"), "As the leaving duty's own goto asks.");
            Assert.That(ToggleReads.AskedBy(recollect), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));

            List<string> fetcher = Calls(typeof(LeadUtility), "AnyColonistWouldFetch");
            Assert.That(fetcher, Does.Contain("RimWorld.JobGiver_PrepareCaravan_CollectPawns.DoesAnimalNeedGathering"),
                "Vanilla's collecting giver's own test, asked of every loose pen animal, since collecting waits for all of them.");
            Assert.That(fetcher, Does.Contain("Verse.Pawn.get_Drafted"), "A drafted colonist runs no duty, so it fetches nothing.");

            // The memo is vanilla's, and the leaving stage really does go back to collecting on it.
            Assert.That(ILCalls.Read(recollect).Strings, Does.Contain(LeadUtility.RecollectMemo));
            MethodInfo createGraph = typeof(LordJob_FormAndSendCaravan).GetMethod(nameof(LordJob_FormAndSendCaravan.CreateGraph));
            Assert.That(ILCalls.Read(createGraph).Strings, Does.Contain(LeadUtility.RecollectMemo));
        }

        [Test]
        public void DroppingRopesIsRefusedOnlyInsideTheScopeAndWhileSpawned()
        {
            List<string> calls = Calls(typeof(Patch_Pawn_RopeTracker_DropRopes), "Prefix");

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.LeadKeepScope.Contains"));
            Assert.That(calls, Does.Contain("Verse.Thing.get_Spawned"), "Without it a rope would survive a despawn into a world caravan.");
        }

        [Test]
        public void ALetGoAnimalStopsFollowingAndNoLongerSetsThePace()
        {
            List<string> calls = Calls(typeof(Patch_Pawn_RopeTracker_DropRope), "Postfix");

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Utils.LeadUtility.ForgetSpeedOf"));
            Assert.That(calls, Does.Contain("Verse.AI.Pawn_JobTracker.EndCurrentJob"));
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_Pawn_RopeTracker_DropRope), "Postfix")), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));

            WalkResult forget = ILCalls.Read(Method(typeof(LeadUtility), nameof(LeadUtility.ForgetSpeedOf)));
            Assert.That(forget.Fields.Where(f => f.IsStore).Select(f => f.Field.Name), Is.EqualTo(new[] { "locomotionUrgencySameAs" }));
        }

        [Test]
        public void RopingAnAnimalLetsItsPreviousRoperGoFirst()
        {
            List<string> calls = Calls(typeof(Patch_Pawn_RopeTracker_CreateRope), "Prefix");

            Assert.That(calls, Does.Contain("RimWorld.Pawn_RopeTracker.get_RopedByPawn"));
            Assert.That(calls, Does.Contain("RimWorld.Pawn_RopeTracker.DropRope"));
            Assert.That(calls, Does.Contain("System.Collections.Generic.List`1.Contains"), "The same roper twice would add the animal twice.");
        }

        [Test]
        public void AnAnimalFollowsADraftedRoperWithVanillasOwnJob()
        {
            WalkResult walk = ILCalls.Read(Method(typeof(Patch_JobGiver_FollowRoper_TryGiveJob), "Postfix"));
            List<string> calls = walk.Calls.Select(ILCalls.Describe).ToList();

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Utils.LeadUtility.KeepsLeads"));
            Assert.That(calls, Does.Contain("Verse.ReachabilityUtility.CanReach"));
            Assert.That(walk.Fields.Select(f => f.Field.DeclaringType.Name + "." + f.Field.Name + (f.IsStore ? " store" : " load")),
                Is.SupersetOf(new[] { "JobDefOf.FollowRoper load", "Job.expiryInterval store", "Job.checkOverrideOnExpire store" }));
            Assert.That(walk.Fields.Where(f => f.IsStore && f.Field.Name == "expiryInterval").Select(f => f.IntBefore), Is.EqualTo(new int?[] { 140 }));
            Assert.That(walk.Fields.Where(f => f.IsStore && f.Field.Name == "checkOverrideOnExpire").Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }));
            Assert.That(Patch_JobGiver_FollowRoper_TryGiveJob.FollowJobExpireInterval, Is.EqualTo(140), "Vanilla's own expiry.");
        }

        [Test]
        public void TheAutoUndraftIsSuppressedThroughThePolicy()
        {
            List<string> calls = Calls(typeof(Patch_AutoUndrafter_ShouldAutoUndraft), "Postfix");
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.LeadPolicy.SuppressAutoUndraft"));
            Assert.That(calls.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.StageOf"),
                Is.InRange(0, calls.IndexOf("CaravansCriticallyEnhanced.Core.LeadPolicy.SuppressAutoUndraft")), "The rule depends on the stage.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_AutoUndrafter_ShouldAutoUndraft), "Postfix")), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));
        }

        [Test]
        public void CancellingACaravanHasItsDraftedMembersLetGoBeforeVanillaLeadsTheAnimalsHome()
        {
            // Vanilla leads home only animals nobody holds, and never with a drafted roper.
            List<string> prefix = Calls(typeof(Patch_CaravanFormingUtility_StopFormingCaravan), "Prefix");
            Assert.That(prefix, Does.Contain("CaravansCriticallyEnhanced.Utils.LeadUtility.LetGoForCancelled"));

            List<string> letGo = Calls(typeof(LeadUtility), nameof(LeadUtility.LetGoForCancelled));
            Assert.That(letGo, Does.Contain("Verse.Pawn.get_Drafted"));
            Assert.That(letGo, Does.Contain("RimWorld.Pawn_RopeTracker.DropRopes"));
        }

        [Test]
        public void AStageChangeEndsTheJobsOfUndraftedMembersOfVanillaCaravansOnly()
        {
            List<string> calls = Calls(typeof(Patch_TransitionAction_EndAllJobs_DoAction), "Prefix");

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla"));
            Assert.That(calls.IndexOf("Verse.Pawn.get_Drafted"), Is.LessThan(calls.IndexOf("Verse.AI.Pawn_JobTracker.EndCurrentJob")));
            Assert.That(calls.IndexOf("Verse.Pawn.get_Drafted"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_TransitionAction_EndAllJobs_DoAction), "Prefix")), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));
        }

        [Test]
        public void TheGizmoPostfixAddsTheDropButtonWhereTheRuleSays()
        {
            // A pass-through postfix is an iterator, so its body is a hoisted MoveNext.
            List<string> callers = ILCalls.CalledAnywhereInTheMod(typeof(LeadUtility), nameof(LeadUtility.ShowsDropLead)).Select(ILCalls.Describe).ToList();
            List<string> builders = ILCalls.CalledAnywhereInTheMod(typeof(Command_DropLead), ".ctor").Select(ILCalls.Describe).ToList();

            Assert.That(callers.Any(c => c.Contains("Patch_Pawn_GetGizmos")), Is.True, string.Join(", ", callers));
            Assert.That(builders.Any(c => c.Contains("Patch_Pawn_GetGizmos")), Is.True, string.Join(", ", builders));
            Assert.That(Calls(typeof(LeadUtility), nameof(LeadUtility.ShowsDropLead)), Does.Contain("Verse.Pawn.get_IsColonistPlayerControlled"));
        }

        [Test]
        public void TheDropButtonDecidesThroughThePolicyAndSoundsOnce()
        {
            List<string> calls = Calls(typeof(Command_DropLead), nameof(Command_DropLead.ProcessGroupInput));

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.LeadPolicy.DropLead"));
            Assert.That(calls, Does.Contain("RimWorld.Pawn_RopeTracker.DropRope"));

            WalkResult ctor = ILCalls.Read(typeof(Command_DropLead).GetConstructors()[0]);
            Assert.That(ctor.Fields.Where(f => f.IsStore).Select(f => f.Field.Name), Is.SupersetOf(new[] { "groupKey", "alsoClickIfOtherInGroupClicked", "icon" }));
            Assert.That(ctor.Fields.Where(f => f.IsStore && f.Field.Name == "alsoClickIfOtherInGroupClicked").Select(f => f.IntBefore), Is.EqualTo(new int?[] { 0 }),
                "Off: the default is on, which plays the click once per holder.");
        }

        [Test]
        public void TheTakeLeadOptionDecidesThroughThePolicyAndOrdersTheModsJob()
        {
            List<string> calls = Calls(typeof(FloatMenuOptionProvider_TakeLead), "GetSingleOptionFor");

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.VanillaLordOf"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.LeadPolicy.TakeLead"));
            Assert.That(calls, Does.Contain("Verse.AnimalPenUtility.RopeAttachmentInteractionCell"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Utils.LeadUtility.CanBeTaken"), "A dog tied at the spot is never untied.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(FloatMenuOptionProvider_TakeLead), "AppliesInt")), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }));
            Assert.That(Calls(typeof(LeadUtility), nameof(LeadUtility.CanBeTaken)), Is.SupersetOf(new[]
            {
                "Verse.AnimalPenUtility.NeedsToBeManagedByRope",
                "CaravansCriticallyEnhanced.Core.LeadPolicy.ShouldRelease",
                "CaravansCriticallyEnhanced.Core.FormingLords.StageOf",
                "CaravansCriticallyEnhanced.Core.LeadPolicy.CanBeTaken",
            }), "A needy animal can be taken while leaving, so the stage is read.");
            Assert.That(
                ILCalls.CalledAnywhereInTheMod(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob)).Select(ILCalls.Describe).Any(c => c.Contains("FloatMenuOptionProvider_TakeLead")),
                Is.True, "The option's action orders the job.");
        }

        [Test]
        public void TheTakeLeadJobRopesWithVanillasToilsAndFailsWhenItNoLongerApplies()
        {
            List<string> ropers = ILCalls.CalledAnywhereInTheMod(typeof(Toils_Rope), nameof(Toils_Rope.RopePawn)).Select(ILCalls.Describe).ToList();
            Assert.That(ropers.Any(c => c.Contains("JobDriver_CCE_TakeLead")), Is.True, string.Join(", ", ropers));

            List<string> undraft = ILCalls.CalledAnywhereInTheMod(typeof(Pawn), "get_Drafted").Select(ILCalls.Describe).ToList();
            Assert.That(undraft.Any(c => c.Contains("JobDriver_CCE_TakeLead")), Is.True, "The job fails once its colonist is undrafted: " + string.Join(", ", undraft));

            List<string> check = Calls(typeof(JobDriver_CCE_TakeLead), "AnimalNoLongerTakeable");
            Assert.That(check, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.VanillaLordOf"));
            Assert.That(check, Does.Contain("Verse.AI.Group.LordUtility.GetLord"));
            Assert.That(check, Does.Contain("RimWorld.Pawn_RopeTracker.get_RopedByPawn"));
            Assert.That(check, Does.Contain("CaravansCriticallyEnhanced.Utils.LeadUtility.CanBeTaken"));

            Assert.That(
                DefXml.ModDefNodes.Where(n => n.Name.LocalName == "JobDef" && n.Element("defName")?.Value == "CCE_TakeLead").Select(n => n.Element("driverClass")?.Value),
                Is.EqualTo(new[] { typeof(JobDriver_CCE_TakeLead).FullName }));
        }

        private static readonly FieldInfo DraftedField = typeof(Pawn_DraftController).GetField("draftedInt", BindingFlags.NonPublic | BindingFlags.Instance);

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
