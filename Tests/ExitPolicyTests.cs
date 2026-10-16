using System;
using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Which pawns restrict the exit, and what the exit pass does to a forming caravan.
    /// </summary>
    [TestFixture]
    public class ExitPolicyTests
    {
        [Test]
        public void AColonistWithAnAreaRestrictsTheExit()
        {
            Assert.That(ExitPolicy.IsRestricted(true, true, true, true, false, false, false, true, 40), Is.True, "The all-restricted control.");
        }

        [Test]
        public void APawnEitherAreaControlHidesRestrictsNothing()
        {
            Assert.That(ExitPolicy.IsRestricted(false, true, true, true, false, false, false, true, 40), Is.False, "No player settings.");
            Assert.That(ExitPolicy.IsRestricted(true, false, true, true, false, false, false, true, 40), Is.False, "A roamer: pen animals have no area.");
            Assert.That(ExitPolicy.IsRestricted(true, true, false, true, false, false, false, true, 40), Is.False, "Not the player's.");
            Assert.That(ExitPolicy.IsRestricted(true, true, true, false, false, false, false, true, 40), Is.False, "A guest or prisoner: the pane hides it.");
            Assert.That(ExitPolicy.IsRestricted(true, true, true, true, true, false, false, true, 40), Is.False, "A mutant that ignores areas.");
            Assert.That(ExitPolicy.IsRestricted(true, true, true, true, false, true, false, true, 40), Is.False, "A mech with no overseer: the Assign tab hides it.");
            Assert.That(ExitPolicy.IsRestricted(true, true, true, true, false, true, true, true, 40), Is.True, "A mech with an overseer.");
        }

        [Test]
        public void NoAreaOrAnEmptyOneRestrictsNothing()
        {
            Assert.That(ExitPolicy.IsRestricted(true, true, true, true, false, false, false, false, 0), Is.False, "Unrestricted.");
            Assert.That(ExitPolicy.IsRestricted(true, true, true, true, false, false, false, true, 0), Is.False, "An area with no cells, as vanilla's own test.");
        }

        [Test]
        public void TheAreaRuleStopsOnceAnimalCollectionStarts()
        {
            List<FormingStage> applies = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(ExitPolicy.AreaRuleApplies)
                .ToList();

            Assert.That(applies, Is.EquivalentTo(new[]
            {
                FormingStage.GatherAnimals,
                FormingStage.GatherAnimalsPause,
                FormingStage.GatherItems,
                FormingStage.GatherItemsPause,
                FormingStage.GatherDownedPawns,
                FormingStage.GatherDownedPawnsPause,
                FormingStage.Wait,
                FormingStage.WaitPause,
            }), "Matthew's decision of 2026-10-16: never held mid-departure.");
        }

        [Test]
        public void ACaravanNotHeldParksOnlyWhenAreasBlockAndItsExitIsNoLongerValid()
        {
            Assert.That(Decide(holding: false, blocker: ExitBlocker.AreaExcludesReachableEdges), Is.EqualTo(ExitAction.Park));
            Assert.That(Decide(holding: false, blocker: ExitBlocker.AreasDisjoint), Is.EqualTo(ExitAction.Park));
            Assert.That(Decide(holding: false, blocker: ExitBlocker.None), Is.EqualTo(ExitAction.Retarget), "Another exit is valid.");
            Assert.That(Decide(holding: false, blocker: ExitBlocker.None, currentReachable: false), Is.EqualTo(ExitAction.Reroute), "Nobody reaches its exit any more: moved, without blaming the areas.");
            Assert.That(Decide(holding: false, blocker: ExitBlocker.None, currentValid: true), Is.EqualTo(ExitAction.None), "Its exit is still fine.");
            Assert.That(Decide(holding: false, blocker: ExitBlocker.Unreachable), Is.EqualTo(ExitAction.None), "Nobody reaches an edge: holding helps nobody.");
            Assert.That(Decide(holding: false, blocker: ExitBlocker.NoEdgeCells), Is.EqualTo(ExitAction.None));
        }

        [Test]
        public void AHeldCaravanIsReleasedOnceAnExitFitsAndStaysHeldOtherwise()
        {
            Assert.That(Decide(holding: true, blocker: ExitBlocker.None), Is.EqualTo(ExitAction.ReleaseToPlan));
            Assert.That(Decide(holding: true, blocker: ExitBlocker.None, fallbackMoved: true), Is.EqualTo(ExitAction.ReleaseToPlan));
            Assert.That(Decide(holding: true, blocker: ExitBlocker.AreaExcludesReachableEdges), Is.EqualTo(ExitAction.None));
            Assert.That(Decide(holding: true, blocker: ExitBlocker.Unreachable), Is.EqualTo(ExitAction.None));
            Assert.That(Decide(holding: true, blocker: ExitBlocker.AreasDisjoint, fallbackMoved: true), Is.EqualTo(ExitAction.MoveFallback), "Still held, with the fallback kept current.");
            Assert.That(Decide(holding: true, blocker: ExitBlocker.AreaExcludesReachableEdges, fallbackMoved: true), Is.EqualTo(ExitAction.MoveFallback));
            Assert.That(Decide(holding: true, blocker: ExitBlocker.Unreachable, fallbackMoved: true), Is.EqualTo(ExitAction.None), "Only an area block has a fallback to keep.");
            Assert.That(Decide(holding: true, blocker: ExitBlocker.NoEdgeCells, fallbackMoved: true), Is.EqualTo(ExitAction.None));
            Assert.That(Decide(holding: false, blocker: ExitBlocker.AreasDisjoint, fallbackMoved: true, currentValid: true), Is.EqualTo(ExitAction.None), "Only a held caravan's fallback is kept.");
        }

        [Test]
        public void TheSwitchAForeignWriterOrAPlayerOverrideLetsAHeldCaravanGoByItsFallback()
        {
            Assert.That(Decide(holding: true, featureOn: false), Is.EqualTo(ExitAction.ReleaseInPlace), "Switched off while holding.");
            Assert.That(Decide(holding: true, foreign: true), Is.EqualTo(ExitAction.ReleaseInPlace), "Another mod owns the exit.");
            Assert.That(Decide(holding: true, playerOverride: true), Is.EqualTo(ExitAction.ReleaseInPlace), "Force depart.");
            Assert.That(Decide(holding: true, stage: FormingStage.CollectAnimals), Is.EqualTo(ExitAction.ReleaseInPlace), "Past the rule's stages, however it got there.");
            Assert.That(Decide(holding: true, featureOn: false, fallbackMoved: true), Is.EqualTo(ExitAction.ReleaseInPlace));

            Assert.That(Decide(holding: false, featureOn: false), Is.EqualTo(ExitAction.None));
            Assert.That(Decide(holding: false, foreign: true), Is.EqualTo(ExitAction.None));
            Assert.That(Decide(holding: false, playerOverride: true), Is.EqualTo(ExitAction.None));
            Assert.That(Decide(holding: false, stage: FormingStage.Leave), Is.EqualTo(ExitAction.None), "An area changed after collecting began: the exit stands.");
        }

        [Test]
        public void AWaitingCaravanGoesBackForDownedPawnsAwayFromItsExitWhenItCanReachThemAll()
        {
            Assert.That(ExitPolicy.Regather(pending: false, waiting: true, strays: 3, unfetchableStrays: 0), Is.EqualTo(RegatherStep.Idle),
                "Nothing pending: a caravan whose exit the mod left alone is never sent back, since going back can wake its sleeper.");
            Assert.That(ExitPolicy.Regather(pending: true, waiting: true, strays: 1, unfetchableStrays: 0), Is.EqualTo(RegatherStep.GoBack));
            Assert.That(ExitPolicy.Regather(pending: true, waiting: true, strays: 2, unfetchableStrays: 1), Is.EqualTo(RegatherStep.Retry),
                "One that cannot be reached yet keeps it pending; going back now could keep the stage open for good.");
            Assert.That(ExitPolicy.Regather(pending: true, waiting: true, strays: 0, unfetchableStrays: 0), Is.EqualTo(RegatherStep.Forget), "Every downed pawn is at the exit.");
            Assert.That(ExitPolicy.Regather(pending: true, waiting: false, strays: 1, unfetchableStrays: 0), Is.EqualTo(RegatherStep.Forget), "The caravan has moved on.");
        }

        [Test]
        public void EveryActionButNothingWritesTheExit()
        {
            foreach (ExitAction action in Enum.GetValues(typeof(ExitAction)))
            {
                Assert.That(ExitPolicy.WritesExit(action), Is.EqualTo(action != ExitAction.None), action.ToString());
            }
        }

        private static ExitAction Decide(bool holding, ExitBlocker blocker = ExitBlocker.AreasDisjoint, bool featureOn = true, FormingStage stage = FormingStage.GatherItems, bool foreign = false, bool playerOverride = false, bool currentValid = false, bool currentReachable = true, bool fallbackMoved = false)
        {
            return ExitPolicy.Decide(featureOn, stage, holding, foreign, playerOverride, blocker, currentValid, currentReachable, fallbackMoved);
        }
    }
}
