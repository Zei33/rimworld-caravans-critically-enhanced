using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Every branch of the completion guard.
    /// </summary>
    [TestFixture]
    public class CompletionGuardTests
    {
        private const int Grace = 2500;
        private const int Watchdog = 5000;
        private const int Now = 100000;

        [Test]
        public void VanillaNotDoneOrTheSettingOffPassesThrough()
        {
            Assert.That(Decide(vanillaDone: false, takeable: 50), Is.EqualTo(CompletionVerdict.PassThrough));
            Assert.That(Decide(enabled: false, takeable: 50), Is.EqualTo(CompletionVerdict.PassThrough));
        }

        [Test]
        public void NothingOutstandingPassesThrough()
        {
            // The all-clear control: vanilla done, enabled, nothing left.
            Assert.That(Decide(), Is.EqualTo(CompletionVerdict.PassThrough));
        }

        [Test]
        public void TakeableUnitsHoldTheCaravan()
        {
            Assert.That(Decide(takeable: 1), Is.EqualTo(CompletionVerdict.Hold));
            Assert.That(Decide(takeable: 1, blocked: 99, blockedTicks: 10 * Grace), Is.EqualTo(CompletionVerdict.Hold),
                "Blocked units past their grace must not release a caravan while something is still takeable.");
        }

        [Test]
        public void BlockedUnitsHoldUntilTheGraceRunsOut()
        {
            Assert.That(Decide(blocked: 5, blockedTicks: 0), Is.EqualTo(CompletionVerdict.Hold), "First sight starts the count.");
            Assert.That(Decide(blocked: 5, blockedTicks: Grace - 1), Is.EqualTo(CompletionVerdict.Hold));
            Assert.That(Decide(blocked: 5, blockedTicks: Grace), Is.EqualTo(CompletionVerdict.ExpireBlocked), "The boundary expires.");
        }

        [Test]
        public void HeldForTheWatchdogPeriodWithNoProgressLetsEverythingGo()
        {
            Assert.That(Decide(takeable: 3, heldTicks: Watchdog), Is.EqualTo(CompletionVerdict.ExpireAll));
            Assert.That(Decide(takeable: 3, heldTicks: Watchdog - 1), Is.EqualTo(CompletionVerdict.Hold));
        }

        [Test]
        public void TheGapSinceAHoldingReadingCountsAndTheGapAfterAPausedOneDoesNot()
        {
            var held = new GuardClocks(Watchdog - 100, 0, Now - 100, Now - 10 * Watchdog, holding: true);
            var paused = new GuardClocks(Watchdog - 100, 0, Now - 100, Now - 10 * Watchdog, holding: false);

            Assert.That(CompletionGuard.Decide(true, true, 3, 0, Now, held, Grace, Watchdog), Is.EqualTo(CompletionVerdict.ExpireAll));
            Assert.That(CompletionGuard.Decide(true, true, 3, 0, Now, paused, Grace, Watchdog), Is.EqualTo(CompletionVerdict.Hold),
                "The members were eating or asleep for that gap, which is not time spent holding.");
        }

        [Test]
        public void ProgressSinceTheLastReadingStartsBothCountsAgain()
        {
            var clocks = new GuardClocks(10 * Watchdog, 10 * Grace, Now - 120, Now - 50, holding: true);

            Assert.That(CompletionGuard.Decide(true, true, 0, 5, Now, clocks, Grace, Watchdog), Is.EqualTo(CompletionVerdict.Hold));
            Assert.That(CompletionGuard.HeldAfter(clocks, Now), Is.EqualTo(0));
            Assert.That(CompletionGuard.BlockedAfter(clocks, 0, Now), Is.EqualTo(0));
        }

        [Test]
        public void ASleepingCaravanIsNotLetGoOnWaking()
        {
            // The first reading after vanilla says done, however long the members slept before it.
            GuardClocks first = GuardClocks.Idle.WithProgress(Now - 100 * Watchdog);

            Assert.That(CompletionGuard.Decide(true, true, 3, 0, Now, first, Grace, Watchdog), Is.EqualTo(CompletionVerdict.Hold));
        }

        [Test]
        public void TheWatchdogAlsoEndsABlockedHoldWhoseGraceIsLonger()
        {
            // Grace is a setting; even set longer than the watchdog the hold cannot outlive it.
            Assert.That(Decide(blocked: 5, heldTicks: Watchdog, blockedTicks: Watchdog / 2, grace: 10 * Watchdog), Is.EqualTo(CompletionVerdict.ExpireAll));
        }

        [Test]
        public void ReleaseIsNeverReachedWhileTakeableUnitsRemainInsideTheWatchdog()
        {
            // Swept rather than sampled: every held time inside the watchdog and every blocked time.
            for (int held = 0; held < Watchdog; held += 97)
            {
                for (int blockedFor = 0; blockedFor < 4 * Grace; blockedFor += 311)
                {
                    Assert.That(Decide(takeable: 1, blocked: 7, heldTicks: held, blockedTicks: blockedFor), Is.EqualTo(CompletionVerdict.Hold),
                        $"held {held}, blocked {blockedFor}");
                }
            }
        }

        [Test]
        public void TheHoldAndBlockedCountsAreReadFromTheirOwnFields()
        {
            // The two are easy to swap. A blocked count past its grace with little held time must
            // expire; the same numbers the other way round must not.
            Assert.That(Decide(blocked: 5, heldTicks: 1, blockedTicks: Grace), Is.EqualTo(CompletionVerdict.ExpireBlocked));
            Assert.That(Decide(blocked: 5, heldTicks: Grace, blockedTicks: 1), Is.EqualTo(CompletionVerdict.Hold));
        }

        [Test]
        public void AHoldingReadingAddsTheGapToBothCounts()
        {
            var before = new GuardClocks(300, 200, Now - 120, Now - 5000, holding: true);
            GuardClocks after = CompletionGuard.Advance(before, true, CompletionVerdict.Hold, 0, Now);

            Assert.That(new[] { after.HeldTicks, after.BlockedTicks, after.LastReadingTick, after.LastProgressTick }, Is.EqualTo(new[] { 420, 320, Now, Now - 5000 }));
            Assert.That(after.Holding, Is.True);
        }

        [Test]
        public void SomethingTakeableClearsTheBlockedCountButNotTheHeldOne()
        {
            var before = new GuardClocks(300, 200, Now - 120, Now - 5000, holding: true);
            GuardClocks after = CompletionGuard.Advance(before, true, CompletionVerdict.Hold, 4, Now);

            Assert.That(new[] { after.HeldTicks, after.BlockedTicks }, Is.EqualTo(new[] { 420, 0 }));
        }

        [Test]
        public void AReadingWhereVanillaSaysNotDonePausesBothCounts()
        {
            // The defect this replaced: such a reading used to clear both, and members eat and sleep
            // often enough that a long grace period never ran out.
            var before = new GuardClocks(300, 200, Now - 120, Now - 5000, holding: true);
            GuardClocks after = CompletionGuard.Advance(before, false, CompletionVerdict.PassThrough, 0, Now);

            Assert.That(new[] { after.HeldTicks, after.BlockedTicks, after.LastReadingTick }, Is.EqualTo(new[] { 300, 200, Now }));
            Assert.That(after.Holding, Is.False, "The time until the next reading is not held time.");
        }

        [Test]
        public void ReleasingTheCaravanClearsBothCounts()
        {
            var before = new GuardClocks(300, 200, Now - 120, Now - 5000, holding: true);
            GuardClocks after = CompletionGuard.Advance(before, true, CompletionVerdict.PassThrough, 0, Now);

            Assert.That(new[] { after.HeldTicks, after.BlockedTicks, after.LastProgressTick }, Is.EqualTo(new[] { 0, 0, Now - 5000 }));
            Assert.That(after.Holding, Is.False);
        }

        [Test]
        public void ProgressDuringAPauseStillStartsTheCountsAgain()
        {
            var before = new GuardClocks(300, 200, Now - 120, Now - 60, holding: false);
            GuardClocks after = CompletionGuard.Advance(before, false, CompletionVerdict.PassThrough, 0, Now);

            Assert.That(new[] { after.HeldTicks, after.BlockedTicks }, Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void ABlockedItemIsDroppedEvenWhenTheMembersKeepStoppingToEat()
        {
            // The scenario that found the defect: one item nobody can take, the longest grace period,
            // and members who break off every few hours. Readings every 120 ticks as vanilla's toil
            // makes them; every 6000 ticks the members spend 1500 eating, when vanilla says not done.
            const int LongGrace = 12 * 2500;
            GuardClocks clocks = GuardClocks.Idle.WithProgress(0);
            CompletionVerdict verdict = CompletionVerdict.Hold;
            int tick = 120;

            for (; tick < 40 * LongGrace && verdict != CompletionVerdict.ExpireBlocked; tick += 120)
            {
                bool vanillaDone = tick % 6000 >= 1500;
                verdict = CompletionGuard.Decide(vanillaDone, true, 0, 1, tick, clocks, LongGrace, 2 * LongGrace);
                clocks = CompletionGuard.Advance(clocks, vanillaDone, verdict, 0, tick);
            }

            Assert.That(verdict, Is.EqualTo(CompletionVerdict.ExpireBlocked));

            // Three quarters of the time is held, so the grace runs out after about four thirds of it.
            Assert.That(tick, Is.InRange(LongGrace, 2 * LongGrace));
        }

        [Test]
        public void ClearedClocksKeepOnlyTheProgressTick()
        {
            GuardClocks cleared = new GuardClocks(300, 200, Now - 120, Now - 5000, holding: true).Cleared();

            Assert.That(new[] { cleared.HeldTicks, cleared.BlockedTicks, cleared.LastReadingTick, cleared.LastProgressTick }, Is.EqualTo(new[] { 0, 0, -1, Now - 5000 }));
            Assert.That(cleared.Holding, Is.False);
            Assert.That(cleared.WithProgress(Now).LastProgressTick, Is.EqualTo(Now));
        }

        [TestCase(10, 4, 3, 4, 3)]
        [TestCase(10, 20, 20, 10, 0)]
        [TestCase(10, 3, 20, 3, 7)]
        [TestCase(10, 0, 0, 0, 0)]
        [TestCase(0, 5, 5, 0, 0)]
        [TestCase(-2, 5, 5, 0, 0)]
        [TestCase(10, -1, -1, 0, 0)]
        public void ARowsOutstandingUnitsSplitTakeableFirstAndGoneUnitsCountAsNeither(
            int remaining, int takeableLive, int blockedLive, int expectedTakeable, int expectedBlocked)
        {
            CompletionGuard.SplitRow(remaining, takeableLive, blockedLive, out int takeable, out int blocked);

            Assert.That(takeable, Is.EqualTo(expectedTakeable));
            Assert.That(blocked, Is.EqualTo(expectedBlocked));
            Assert.That(takeable + blocked, Is.LessThanOrEqualTo(System.Math.Max(remaining, 0)));
        }

        [Test]
        public void TheProgressClockMovesOnlyWhenTheReadingChanges()
        {
            Assert.That(CompletionGuard.NextProgressTick(7, Now - 300, 7, Now), Is.EqualTo(Now - 300));
            Assert.That(CompletionGuard.NextProgressTick(7, Now - 300, 6, Now), Is.EqualTo(Now));
        }

        [Test]
        public void TheGameIsToldDoneOnlyWhenVanillaSaidSoAndTheGuardLetItStand()
        {
            foreach (CompletionVerdict verdict in System.Enum.GetValues(typeof(CompletionVerdict)))
            {
                Assert.That(CompletionGuard.Done(false, verdict), Is.False, verdict.ToString());
                Assert.That(CompletionGuard.Done(true, verdict), Is.EqualTo(verdict == CompletionVerdict.PassThrough), verdict.ToString());
            }
        }

        [Test]
        public void TheGuardAppliesOnlyWhileItemsAreGathered()
        {
            // Not in the pause after it either: nobody loads during a mental break, and clocks left
            // running through one would hand the caravan to the watchdog when gathering resumed.
            var applies = new System.Collections.Generic.List<FormingStage>();
            foreach (FormingStage stage in System.Enum.GetValues(typeof(FormingStage)))
            {
                if (CompletionGuard.Applies(stage))
                {
                    applies.Add(stage);
                }
            }

            Assert.That(applies, Is.EqualTo(new[] { FormingStage.GatherItems }));
        }

        [TestCase(false, false, false, MemberLoadingState.Ignored)]
        [TestCase(false, true, true, MemberLoadingState.Ignored)]
        [TestCase(false, false, true, MemberLoadingState.Ignored)]
        [TestCase(true, true, false, MemberLoadingState.Ignored)]
        [TestCase(true, true, true, MemberLoadingState.Ignored)]
        [TestCase(true, false, true, MemberLoadingState.Waiting)]
        [TestCase(true, false, false, MemberLoadingState.Busy)]
        public void AMemberCountsOnlyAsAnUndraftedColonist(bool colonist, bool drafted, bool waiting, MemberLoadingState expected)
        {
            Assert.That(CompletionGuard.MemberState(colonist, drafted, waiting), Is.EqualTo(expected));
        }

        [TestCase(1, 0, 0, true)]
        [TestCase(3, 0, 0, true)]
        [TestCase(1, 1, 0, false)]
        [TestCase(1, 0, 1, false)]
        [TestCase(0, 0, 0, false)]
        [TestCase(0, 1, 0, false)]
        public void TheDraftedExemptionNeedsSomebodyWaitingAndNobodyBusyOrHauling(int waiting, int busy, int inFlight, bool expected)
        {
            // 0, 0, 0 is every colonist drafted: nobody is loading at all, so it is not done. Vanilla
            // waits there, and an answer of done would hand the caravan to the watchdog.
            Assert.That(CompletionGuard.DoneIgnoringDrafted(waiting, busy, inFlight), Is.EqualTo(expected));
        }

        [Test]
        public void ATakerIsASpawnedUndraftedColonistOnItsFeetWithHands()
        {
            // Drafted members run no duty, so they gather nothing, and the exemption does not wait
            // for them; an item only they could reach is blocked, not held for.
            int yes = 0;
            foreach (bool colonist in new[] { false, true })
            foreach (bool spawned in new[] { false, true })
            foreach (bool downed in new[] { false, true })
            foreach (bool drafted in new[] { false, true })
            foreach (bool hands in new[] { false, true })
            {
                bool taker = CompletionGuard.IsTaker(colonist, spawned, downed, drafted, hands);
                Assert.That(taker, Is.EqualTo(colonist && spawned && !downed && !drafted && hands), $"{colonist} {spawned} {downed} {drafted} {hands}");
                yes += taker ? 1 : 0;
            }

            Assert.That(yes, Is.EqualTo(1));
        }

        [TestCase(false, false, false, LiveUnitKind.Gone)]
        [TestCase(false, false, true, LiveUnitKind.Gone)]
        [TestCase(false, true, false, LiveUnitKind.Blocked)]
        [TestCase(false, true, true, LiveUnitKind.Blocked)]
        [TestCase(true, false, true, LiveUnitKind.Takeable)]
        [TestCase(true, false, false, LiveUnitKind.Blocked)]
        [TestCase(true, true, false, LiveUnitKind.Blocked)]
        [TestCase(true, true, true, LiveUnitKind.Blocked)]
        public void ACarriedThingIsBlockedNeverGone(bool onMap, bool carried, bool canTake, LiveUnitKind expected)
        {
            // A non-member hauling a listed stack still has it, though it is neither on the ground nor
            // on a shelf. Counting it as gone would let the caravan finish the moment it was picked up.
            Assert.That(CompletionGuard.Classify(onMap, carried, canTake), Is.EqualTo(expected));
        }

        [Test]
        public void TheLordTickActsOnlyOnAFreshExpiry()
        {
            const int Interval = 250;

            Assert.That(CompletionGuard.LetGoDue(true, CompletionVerdict.ExpireAll, 0, Interval), Is.True);
            Assert.That(CompletionGuard.LetGoDue(true, CompletionVerdict.ExpireBlocked, Interval, Interval), Is.True);
            Assert.That(CompletionGuard.LetGoDue(true, CompletionVerdict.ExpireBlocked, Interval + 1, Interval), Is.False, "Stale.");
            Assert.That(CompletionGuard.LetGoDue(true, CompletionVerdict.ExpireAll, -1, Interval), Is.False, "No verdict yet.");
            Assert.That(CompletionGuard.LetGoDue(false, CompletionVerdict.ExpireAll, 0, Interval), Is.False, "Setting off.");
            Assert.That(CompletionGuard.LetGoDue(true, CompletionVerdict.Hold, 0, Interval), Is.False);
            Assert.That(CompletionGuard.LetGoDue(true, CompletionVerdict.PassThrough, 0, Interval), Is.False);
        }

        [Test]
        public void AnExpiryIsConfirmedAgainstTheCaravanAsItStandsNow()
        {
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.ExpireAll, 5, 0), Is.True, "The watchdog lets takeable units go too.");
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.ExpireAll, 0, 1), Is.False, "Somebody started hauling.");
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.ExpireBlocked, 0, 0), Is.True);
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.ExpireBlocked, 1, 0), Is.False, "A blocked unit became takeable.");
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.ExpireBlocked, 0, 2), Is.False);
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.Hold, 0, 0), Is.False);
            Assert.That(CompletionGuard.ConfirmLetGo(CompletionVerdict.PassThrough, 0, 0), Is.False);
        }
        /// <summary>
        /// Decides with a reading taken at the same tick as the last one, so the held and blocked
        /// counts are exactly the ones given.
        /// </summary>
        private static CompletionVerdict Decide(
            bool vanillaDone = true,
            bool enabled = true,
            int takeable = 0,
            int blocked = 0,
            int heldTicks = 0,
            int blockedTicks = 0,
            int grace = Grace)
        {
            var clocks = new GuardClocks(heldTicks, blockedTicks, Now, Now - 10 * Watchdog, holding: true);
            return CompletionGuard.Decide(vanillaDone, enabled, takeable, blocked, Now, clocks, grace, Watchdog);
        }
    }
}
