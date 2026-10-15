using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Patches;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds each Load patch's body, which cannot run here, to the tested decision it hands over to.
    /// </summary>
    /// <remarks>
    /// IL order is not execution order and arguments are invisible, so these say "this body still
    /// asks that decision", and the in-game checks in <c>Tests/README.md</c> say the rest.
    /// </remarks>
    [TestFixture]
    public class LoadingPatchBodyTests
    {
        [Test]
        public void TheCarrierPostfixAppliesTheCarrierRule()
        {
            List<string> calls = Calls(typeof(Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier), "Postfix");

            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.CarrierRule.Allows"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla"),
                "The carrier rule must stay off subclass lords.");
            Assert.That(calls.IndexOf("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla"),
                Is.LessThan(calls.IndexOf("CaravansCriticallyEnhanced.Core.CarrierRule.Allows")));
        }

        [Test]
        public void TheCarrierPostfixNarrowsHelpersOnlyAndOnlyWithLoadOn()
        {
            // Two lord reads, the carrier's and the asking pawn's: the second is what keeps a member's
            // own carrier choice vanilla's. And the drafted and mental-state readings feed the rule.
            List<string> calls = Calls(typeof(Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier), "Postfix");

            Assert.That(calls.Count(c => c == "Verse.AI.Group.LordUtility.GetLord"), Is.EqualTo(2), string.Join(", ", calls));
            Assert.That(calls, Does.Contain("Verse.Pawn.get_Drafted"));
            Assert.That(calls, Does.Contain("Verse.Pawn.get_InMentalState"));
            Assert.That(calls, Does.Contain("RimWorld.ForbidUtility.IsForbidden"), "Vanilla's carrier choice never asks about the helper's area.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier), "Postfix")),
                Is.EqualTo(new Toggle?[] { Toggle.LoadingWorkType, Toggle.ColonistCarriers }));
        }

        [Test]
        public void ARopeAnimalCountsAsTiedOnlyAtItsOwnCaravansSpot()
        {
            // IsRopedToSpot alone is true at a hitching post anywhere on the map.
            Assert.That(Calls(typeof(Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier), "Postfix"),
                Does.Contain("CaravansCriticallyEnhanced.Patches.Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier.IsTiedAtItsSpot"));
            List<string> tied = Calls(typeof(Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier), "IsTiedAtItsSpot");

            Assert.That(tied, Does.Contain("RimWorld.Pawn_RopeTracker.get_RopedToSpot"));
            Assert.That(tied, Does.Contain("CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_JobMeetingPoint"),
                "The pause duty has no spot, so the meeting point comes from the lord job.");
            Assert.That(
                tied.Count(c => c.StartsWith("HarmonyLib.AccessTools+FieldRef`2", System.StringComparison.Ordinal) && c.EndsWith(".Invoke", System.StringComparison.Ordinal)),
                Is.EqualTo(1),
                "The accessor is fetched but the field is never read. " + string.Join(", ", tied));
        }

        [Test]
        public void HelpersLeaveClosedHaulSourcesAlone()
        {
            // An outfit stand does not allow removing by default, and members' search respects that.
            Assert.That(Calls(typeof(LoadingIndex), "CanBeFetchedOn"), Does.Contain("RimWorld.IHaulSource.get_HaulSourceEnabled"));
        }

        [Test]
        public void TheHelperStopsWhenTheCaravanLeavesLoading()
        {
            // The check lives in a lambda the compiler hoists out of the postfix, so the whole mod is
            // searched, and the caller is shown to be that postfix's closure.
            List<string> callers = ILCalls.CalledAnywhereInTheMod(typeof(FormingStages), nameof(FormingStages.HelpersMayContinue))
                .Select(ILCalls.Describe)
                .ToList();

            Assert.That(callers.Any(c => c.Contains("Patch_JobDriver_PrepareCaravan_GatherItems_MakeNewToils")), Is.True, string.Join(", ", callers));
            Assert.That(Calls(typeof(Patch_JobDriver_PrepareCaravan_GatherItems_MakeNewToils), "Postfix"),
                Does.Contain("Verse.AI.JobDriver.AddFailCondition"));
        }

        [Test]
        public void TheCompletionPostfixHandsOverToTheGuardAfterTheDraftedExemption()
        {
            List<string> calls = Calls(typeof(Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan), "Postfix");

            int exemption = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.DoneIgnoringDrafted");
            int guard = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.Answer");

            Assert.That(exemption, Is.GreaterThanOrEqualTo(0));
            Assert.That(guard, Is.GreaterThan(exemption));
        }

        [Test]
        public void TheCompletionPostfixStandsAsideOutsideItemGatheringAndGatesTheExemption()
        {
            MethodInfo postfix = Method(typeof(Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan), "Postfix");
            List<string> calls = Calls(typeof(Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan), "Postfix");

            int applies = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Applies");
            int rest = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.Rest");
            int exemption = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.DoneIgnoringDrafted");

            Assert.That(applies, Is.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(rest, Is.GreaterThan(applies));
            Assert.That(exemption, Is.GreaterThan(rest));
            Assert.That(ToggleReads.AskedBy(postfix), Is.EqualTo(new Toggle?[] { Toggle.LeadsSurviveDrafting }),
                "The exemption is Slice 3's, and must stay switchable with it.");
        }

        [Test]
        public void TheExemptionsReaderCountsMembersThroughThePureRules()
        {
            List<string> calls = Calls(typeof(LoadingCompletion), nameof(LoadingCompletion.DoneIgnoringDrafted));

            int member = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.MemberState");
            int done = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.DoneIgnoringDrafted");

            Assert.That(member, Is.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(done, Is.GreaterThan(member));
            Assert.That(calls, Does.Contain("Verse.Pawn.get_Drafted"));
        }

        [Test]
        public void TheCountsGoThroughTheTakerAndUnitRules()
        {
            Assert.That(Calls(typeof(LoadingCompletion), "TakersOf"), Does.Contain("CaravansCriticallyEnhanced.Core.CompletionGuard.IsTaker"));
            Assert.That(Calls(typeof(LoadingCompletion), "TakersOf"), Does.Contain("Verse.PawnCapacitiesHandler.CapableOf"));

            List<string> count = Calls(typeof(LoadingCompletion), "CountOutstanding");
            int takers = count.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.TakersOf");
            int wouldHaul = count.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.AnyMemberWouldHaul");
            int carried = count.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.IsCarried");
            int onMap = count.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.OnMap");
            int classify = count.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Classify");
            int split = count.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.SplitRow");

            int[] order = { takers, wouldHaul, carried, onMap, classify, split };
            Assert.That(order, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", count));
            Assert.That(order, Is.Ordered);

            // The member test is vanilla's own search, so the guard holds exactly while a member would act.
            Assert.That(
                Calls(typeof(LoadingCompletion), "AnyMemberWouldHaul").Where(c => !c.StartsWith("System.Collections.Generic.List`1", System.StringComparison.Ordinal)),
                Is.EqualTo(new[] { "RimWorld.GatherItemsForCaravanUtility.FindThingToHaul" }));
            Assert.That(Calls(typeof(LoadingCompletion), "TakersOf"), Does.Contain("Verse.Pawn.get_Drafted"));
        }

        [Test]
        public void RowsWithNothingLeftAreClearedSoVanillasListCannotThrow()
        {
            // On every completion test, just before vanilla's Force-depart list reads the rows, and on
            // every lord tick pass in every stage, ahead of the stage check.
            List<string> answer = Calls(typeof(LoadingCompletion), nameof(LoadingCompletion.Answer));
            List<string> tick = Calls(typeof(LoadingCompletion), nameof(LoadingCompletion.Tick));
            List<string> clear = Calls(typeof(LoadingCompletion), "ClearEmptyRows");

            Assert.That(answer.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.ClearEmptyRows"),
                Is.GreaterThan(answer.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Decide")));
            Assert.That(tick.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.ClearEmptyRows"), Is.GreaterThanOrEqualTo(0));
            Assert.That(tick.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.ClearEmptyRows"),
                Is.LessThan(tick.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Applies")));
            Assert.That(clear, Does.Contain("RimWorld.Transferable.get_HasAnyThing"));
            Assert.That(clear, Does.Contain("RimWorld.Transferable.ForceTo"));
        }

        [Test]
        public void LettingGoTakesARowOffTheListBeforeLabellingItAndNeverReadsAnEmptyRowsLabel()
        {
            // TransferableOneWay.Label reads the row's first thing, so it throws on an emptied row.
            List<string> calls = Calls(typeof(LoadingCompletion), "LetGo");

            int force = calls.IndexOf("RimWorld.Transferable.ForceTo");
            int label = calls.IndexOf("RimWorld.GenLabel.ThingLabel");

            Assert.That(calls, Does.Not.Contain("RimWorld.TransferableOneWay.get_Label"));
            Assert.That(force, Is.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(label, Is.GreaterThan(force));
        }

        [Test]
        public void TheLordTickConfirmsAnExpiryBeforeLettingGo()
        {
            List<string> calls = Calls(typeof(LoadingCompletion), nameof(LoadingCompletion.Tick));

            int applies = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Applies");
            int due = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.LetGoDue");
            int recount = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.CountOutstanding");
            int confirm = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.ConfirmLetGo");
            int letGo = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingCompletion.LetGo");

            Assert.That(new[] { applies, due, recount, confirm, letGo }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { applies, due, recount, confirm, letGo }, Is.Ordered);
            Assert.That(calls.Count(c => c == "CaravansCriticallyEnhanced.Core.LoadingCompletion.InFlightFor"), Is.EqualTo(1),
                "The confirmation reads the hauls in progress now.");
        }

        [Test]
        public void TheGuardsReaderAsksThePureDecision()
        {
            // Decide, then the clocks moved on, then the answer the game gets: the last is what a
            // reader returning vanilla's answer straight back would drop.
            List<string> calls = Calls(typeof(LoadingCompletion), nameof(LoadingCompletion.Answer));

            int decide = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Decide");
            int advance = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Advance");
            int done = calls.IndexOf("CaravansCriticallyEnhanced.Core.CompletionGuard.Done");

            Assert.That(new[] { decide, advance, done }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { decide, advance, done }, Is.Ordered);
        }

        [Test]
        public void ExpiredItemsAreLetGoTheWayTheTabsRemoveButtonDoesAndSaid()
        {
            List<string> calls = Calls(typeof(LoadingCompletion), "LetGo");

            Assert.That(calls, Does.Contain("RimWorld.Transferable.ForceTo"));
            Assert.That(calls, Does.Contain("CaravansCriticallyEnhanced.Utils.CaravanMessages.Post"));
        }

        [Test]
        public void TheLordTickRunsTheLoadingPass()
        {
            Assert.That(Calls(typeof(Patch_LordJob_FormAndSendCaravan_LordJobTick), "Postfix"),
                Does.Contain("CaravansCriticallyEnhanced.Core.LoadingCompletion.Tick"));
        }

        [Test]
        public void TheMigrationAsksTheDecisionBeforeSettingAPriority()
        {
            List<string> calls = Calls(typeof(Patch_Pawn_WorkSettings_ExposeData), "Apply");

            int decide = calls.IndexOf("CaravansCriticallyEnhanced.Core.LoadingMigration.Decide");
            int set = calls.IndexOf("RimWorld.Pawn_WorkSettings.SetPriority");

            Assert.That(decide, Is.GreaterThanOrEqualTo(0));
            Assert.That(set, Is.GreaterThan(decide));
            Assert.That(calls.Count(c => c == "RimWorld.Pawn_WorkSettings.SetPriority"), Is.EqualTo(1));
        }

        [Test]
        public void TheMigrationReadsWhetherThePawnWasMigratedBefore()
        {
            // Replacing the reading with a literal false copies Haul over every pawn's Load priority on
            // every load, and nothing else in the suite would notice.
            List<string> calls = Calls(typeof(Patch_Pawn_WorkSettings_ExposeData), "Postfix");

            int read = calls.FindIndex(c => c.StartsWith("System.Runtime.CompilerServices.ConditionalWeakTable`2", System.StringComparison.Ordinal)
                                            && c.EndsWith(".TryGetValue", System.StringComparison.Ordinal));
            int apply = calls.IndexOf("CaravansCriticallyEnhanced.Patches.Patch_Pawn_WorkSettings_ExposeData.Apply");

            Assert.That(read, Is.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(apply, Is.GreaterThan(read));
        }

        [Test]
        public void AFirstLoadRealignsTheShiftedPrioritiesBeforeAnythingReadsThem()
        {
            List<string> postfix = Calls(typeof(Patch_Pawn_WorkSettings_ExposeData), "Postfix");
            List<string> realign = Calls(typeof(Patch_Pawn_WorkSettings_ExposeData), "Realign");

            Assert.That(postfix.IndexOf("CaravansCriticallyEnhanced.Patches.Patch_Pawn_WorkSettings_ExposeData.Realign"),
                Is.LessThan(postfix.IndexOf("CaravansCriticallyEnhanced.Patches.Patch_Pawn_WorkSettings_ExposeData.Apply")),
                "LoadingVars comes first in the switch, as it does in a load.");
            Assert.That(postfix, Does.Contain("CaravansCriticallyEnhanced.Patches.Patch_Pawn_WorkSettings_ExposeData.Realign"));
            Assert.That(realign, Does.Contain("CaravansCriticallyEnhanced.Core.LoadingMigration.MakeRoom"));
        }

        [Test]
        public void TheMigrationFlagNodeIsPinnedAsALiteral()
        {
            // The node name is what a save is read by. Renamed, it reads as absent, and every pawn's
            // Load priority is overwritten with its Haul priority again on the next load.
            Assert.That(Patch_Pawn_WorkSettings_ExposeData.FlagNode, Is.EqualTo("cceLoadingInit"));
        }

        [Test]
        public void ThePatchesAreAppliedClassByClassAndNeverAllAtOnce()
        {
            List<string> apply = Calls(typeof(CaravansCriticallyEnhancedMod), "ApplyPatches");

            Assert.That(apply, Does.Contain("HarmonyLib.Harmony.CreateClassProcessor"));
            Assert.That(apply.IndexOf("System.Attribute.IsDefined"), Is.GreaterThanOrEqualTo(0), "Only classes with a Harmony attribute, as PatchAll in Harmony 2.4.");
            Assert.That(apply.IndexOf("System.Attribute.IsDefined"), Is.LessThan(apply.IndexOf("HarmonyLib.Harmony.CreateClassProcessor")));
            Assert.That(apply, Does.Contain("HarmonyLib.PatchClassProcessor.Patch"));
            Assert.That(
                ILCalls.CalledBy(typeof(CaravansCriticallyEnhancedMod).GetConstructors()[0]).Select(ILCalls.Describe),
                Does.Contain("CaravansCriticallyEnhanced.CaravansCriticallyEnhancedMod.ApplyPatches"));
            Assert.That(
                ILCalls.AllModMethods().SelectMany(ILCalls.CalledBy).Select(ILCalls.Describe).Where(c => c.StartsWith("HarmonyLib.Harmony.PatchAll", System.StringComparison.Ordinal)),
                Is.Empty,
                "PatchAll stops at the first class that throws, leaving an arbitrary half of the mod applied.");
        }

        [Test]
        public void TheVanillaHelperStandsDownOnlyForTheExactLord()
        {
            Assert.That(Calls(typeof(Patch_WorkGiver_HelpGatheringItemsForCaravan_NonScanJob), "AnySubclassFormingLord"),
                Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.IsVanillaJob"));
        }

        private static MethodInfo Method(System.Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, type.Name + " has no " + name + ".");
            return method;
        }

        private static List<string> Calls(System.Type type, string name)
        {
            return ILCalls.CalledBy(Method(type, name)).Select(ILCalls.Describe).ToList();
        }
    }
}
