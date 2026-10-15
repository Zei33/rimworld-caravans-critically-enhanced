using System;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// What the completion guard does with vanilla's "all items loaded" answer.
    /// </summary>
    public enum CompletionVerdict
    {
        /// <summary>Vanilla's answer stands.</summary>
        PassThrough,

        /// <summary>Vanilla said done, but items are still waiting; report not done.</summary>
        Hold,

        /// <summary>Only unreachable or reserved items are left and the grace period is over; drop them from the list.</summary>
        ExpireBlocked,

        /// <summary>Loading has made no progress for the whole watchdog period; drop what is left from the list.</summary>
        ExpireAll,
    }

    /// <summary>
    /// How one caravan member counts in the completion test.
    /// </summary>
    public enum MemberLoadingState
    {
        /// <summary>Not a colonist, or drafted: not asked.</summary>
        Ignored,

        /// <summary>Waiting for the others to finish gathering items.</summary>
        Waiting,

        /// <summary>Doing anything else, which keeps loading open.</summary>
        Busy,
    }

    /// <summary>
    /// How one of a list row's things counts.
    /// </summary>
    public enum LiveUnitKind
    {
        /// <summary>No longer on the map: destroyed, eaten, merged away, or carried off.</summary>
        Gone,

        /// <summary>A member can reach and reserve it.</summary>
        Takeable,

        /// <summary>It exists, but nobody in the caravan can take it now.</summary>
        Blocked,
    }

    /// <summary>
    /// What the completion guard keeps between readings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grace period and the watchdog count time spent holding, not time on the clock. Between two
    /// readings that both held the caravan, the gap is added to each; a reading where vanilla says not
    /// done, because a member went to eat or sleep, pauses both where they are rather than restarting
    /// them. Restarting there was a defect: members eat and sleep often enough that, with a long grace
    /// period, a blocked item could hold a caravan for good. Counting wall time from the start of the
    /// hold would be the opposite defect: a caravan whose members slept through the watchdog period
    /// would be let go on waking, with items they could fetch at once.
    /// </para>
    /// <para>
    /// One value rather than loose arguments, so a call site cannot hand the guard one clock in
    /// another's place. A negative tick means none yet.
    /// </para>
    /// </remarks>
    public readonly struct GuardClocks
    {
        /// <summary>Nothing held, no reading and no progress seen.</summary>
        public static readonly GuardClocks Idle = new GuardClocks(0, 0, -1, -1, false);

        /// <summary>
        /// Initializes a new instance of the <see cref="GuardClocks"/> struct.
        /// </summary>
        /// <param name="heldTicks">Time spent holding the caravan since the last progress.</param>
        /// <param name="blockedTicks">Time spent holding it with nothing takeable since the last progress.</param>
        /// <param name="lastReadingTick">The tick of the last reading.</param>
        /// <param name="lastProgressTick">The last tick anything about the loading changed.</param>
        /// <param name="holding">Whether the last reading held the caravan.</param>
        public GuardClocks(int heldTicks, int blockedTicks, int lastReadingTick, int lastProgressTick, bool holding)
        {
            HeldTicks = heldTicks;
            BlockedTicks = blockedTicks;
            LastReadingTick = lastReadingTick;
            LastProgressTick = lastProgressTick;
            Holding = holding;
        }

        /// <summary>Gets the time spent holding the caravan since the last progress, in ticks.</summary>
        public int HeldTicks { get; }

        /// <summary>Gets the time spent holding it with nothing takeable since the last progress, in ticks.</summary>
        public int BlockedTicks { get; }

        /// <summary>Gets the tick of the last reading, or a negative number before the first.</summary>
        public int LastReadingTick { get; }

        /// <summary>Gets the last tick anything about the loading changed, or a negative number before the first reading.</summary>
        public int LastProgressTick { get; }

        /// <summary>Gets a value indicating whether the last reading held the caravan.</summary>
        public bool Holding { get; }

        /// <summary>
        /// Returns these clocks with the progress clock moved.
        /// </summary>
        /// <param name="lastProgressTick">The new progress tick.</param>
        /// <returns>The updated clocks.</returns>
        public GuardClocks WithProgress(int lastProgressTick)
        {
            return new GuardClocks(HeldTicks, BlockedTicks, LastReadingTick, lastProgressTick, Holding);
        }

        /// <summary>
        /// Returns idle clocks that keep the progress clock.
        /// </summary>
        /// <returns>The cleared clocks.</returns>
        public GuardClocks Cleared()
        {
            return new GuardClocks(0, 0, -1, LastProgressTick, false);
        }
    }

    /// <summary>
    /// Decides whether a forming caravan has really finished loading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's <c>CaravanFormingUtility.AllItemsLoadedOntoCaravan</c> never looks at the item
    /// list. It answers done when every colonist member is wandering with the "waiting for others"
    /// tag and no pawn on the map has a gather job for the lord. With members alone that is close
    /// enough, because a member re-thinks through its duty straight after each haul. With helpers it
    /// is not: a helper holds the whole stack it reserved, the members find nothing left and start
    /// wandering, and if the helper's job ends on anything other than a delivery the next 120-tick
    /// check sees nobody hauling and ends loading with items still on the ground. The lord never goes
    /// back to gathering items after that.
    /// </para>
    /// <para>
    /// So when vanilla says done, this checks the list. It can only turn done into not done, never
    /// the reverse, and it cannot hold forever: items nobody can take are given a grace period, and
    /// if the caravan is held for the watchdog period with no progress at all, everything left is
    /// let go. Both are measured in time spent holding; <see cref="GuardClocks"/> says why.
    /// </para>
    /// </remarks>
    public static class CompletionGuard
    {
        /// <summary>
        /// Decides what to do with vanilla's answer.
        /// </summary>
        /// <param name="vanillaDone">What <c>AllItemsLoadedOntoCaravan</c> returned.</param>
        /// <param name="enabled">Whether the "Wait for every item" setting is on.</param>
        /// <param name="takeableUnits">Units still to load that some colonist member can reach and reserve.</param>
        /// <param name="blockedUnits">Units still to load that exist on the map but nobody in the caravan can take.</param>
        /// <param name="now">The current tick.</param>
        /// <param name="clocks">The time held so far, the time held by blocked units alone, and the last reading and progress.</param>
        /// <param name="graceTicks">How long blocked units may hold the caravan.</param>
        /// <param name="watchdogTicks">How long the caravan may be held with no progress at all.</param>
        /// <returns>The verdict.</returns>
        /// <remarks>
        /// Both limits are measured in time spent holding, which <see cref="GuardClocks"/> explains.
        /// </remarks>
        public static CompletionVerdict Decide(
            bool vanillaDone,
            bool enabled,
            int takeableUnits,
            int blockedUnits,
            int now,
            GuardClocks clocks,
            int graceTicks,
            int watchdogTicks)
        {
            if (!enabled || !vanillaDone)
            {
                return CompletionVerdict.PassThrough;
            }

            if (takeableUnits <= 0 && blockedUnits <= 0)
            {
                return CompletionVerdict.PassThrough;
            }

            if (HeldAfter(clocks, now) >= watchdogTicks)
            {
                return CompletionVerdict.ExpireAll;
            }

            if (takeableUnits > 0)
            {
                return CompletionVerdict.Hold;
            }

            return BlockedAfter(clocks, takeableUnits, now) >= graceTicks
                ? CompletionVerdict.ExpireBlocked
                : CompletionVerdict.Hold;
        }

        /// <summary>
        /// Determines whether the exemption and the guard apply at a stage.
        /// </summary>
        /// <param name="stage">The caravan's stage.</param>
        /// <returns><c>true</c> only while items are being gathered.</returns>
        /// <remarks>
        /// <para>
        /// Vanilla asks the completion test from the item-gathering toil, and from the Force depart
        /// confirmation in any stage. Anywhere but item gathering, vanilla's answer stands and the
        /// guard's clocks are cleared.
        /// </para>
        /// <para>
        /// That includes the pause after item gathering. Nobody loads during a mental break, so the
        /// clocks would otherwise run on through it, and a long break would hand the caravan to the
        /// watchdog the moment gathering resumed.
        /// </para>
        /// </remarks>
        public static bool Applies(FormingStage stage)
        {
            return stage == FormingStage.GatherItems;
        }

        /// <summary>
        /// Turns vanilla's answer and the verdict into the answer the game gets.
        /// </summary>
        /// <param name="vanillaDone">Vanilla's answer, with the drafted-member exemption applied.</param>
        /// <param name="verdict">The guard's verdict.</param>
        /// <returns><c>true</c> only when vanilla said done and the guard let it stand.</returns>
        /// <remarks>
        /// An expiry verdict reports not done as well: the items are taken off the list on the lord's
        /// next tick, and the answer after that passes through.
        /// </remarks>
        public static bool Done(bool vanillaDone, CompletionVerdict verdict)
        {
            return vanillaDone && verdict == CompletionVerdict.PassThrough;
        }

        /// <summary>
        /// Moves every clock on after a reading.
        /// </summary>
        /// <param name="clocks">The clocks before the reading.</param>
        /// <param name="vanillaDone">Vanilla's answer, with the drafted-member exemption applied.</param>
        /// <param name="verdict">The verdict just reached.</param>
        /// <param name="takeableUnits">Units some member can take.</param>
        /// <param name="now">The current tick.</param>
        /// <returns>The clocks to keep for the next reading.</returns>
        /// <remarks>
        /// A pass-through with vanilla done ends the hold and clears both counts. A pass-through with
        /// vanilla not done pauses them: a member is busy, eating or asleep, and the hold resumes where
        /// it stopped. Any other verdict holds, and the time since the last reading is added.
        /// </remarks>
        public static GuardClocks Advance(GuardClocks clocks, bool vanillaDone, CompletionVerdict verdict, int takeableUnits, int now)
        {
            if (verdict == CompletionVerdict.PassThrough)
            {
                if (vanillaDone)
                {
                    return new GuardClocks(0, 0, now, clocks.LastProgressTick, false);
                }

                bool progressed = Progressed(clocks);
                return new GuardClocks(
                    progressed ? 0 : clocks.HeldTicks,
                    progressed ? 0 : clocks.BlockedTicks,
                    now,
                    clocks.LastProgressTick,
                    false);
            }

            return new GuardClocks(HeldAfter(clocks, now), BlockedAfter(clocks, takeableUnits, now), now, clocks.LastProgressTick, true);
        }

        /// <summary>
        /// Works out the time held since the last progress, as of a reading now.
        /// </summary>
        /// <param name="clocks">The clocks from the previous reading.</param>
        /// <param name="now">The current tick.</param>
        /// <returns>The held time, 0 when loading has moved since the previous reading.</returns>
        public static int HeldAfter(GuardClocks clocks, int now)
        {
            return Progressed(clocks) ? 0 : clocks.HeldTicks + Elapsed(clocks, now);
        }

        /// <summary>
        /// Works out the time held by blocked units alone, as of a reading now.
        /// </summary>
        /// <param name="clocks">The clocks from the previous reading.</param>
        /// <param name="takeableUnits">Units some member can take now.</param>
        /// <param name="now">The current tick.</param>
        /// <returns>The blocked time, 0 while anything is takeable or when loading has moved since the previous reading.</returns>
        /// <remarks>
        /// Starting again when something becomes takeable is deliberate: a blocked item that becomes
        /// takeable, a wall deconstructed or a reservation released, gets a fresh grace period if it
        /// is blocked again later. Progress starts it again too, so items a helper is still fetching
        /// from where no member would look are not dropped while the helper works.
        /// </remarks>
        public static int BlockedAfter(GuardClocks clocks, int takeableUnits, int now)
        {
            return takeableUnits > 0 || Progressed(clocks) ? 0 : clocks.BlockedTicks + Elapsed(clocks, now);
        }

        /// <summary>
        /// Places one caravan member in vanilla's completion test, with drafted colonists left out.
        /// </summary>
        /// <param name="isColonist">Whether the member is a colonist; vanilla asks only colonists.</param>
        /// <param name="drafted">Whether it is drafted.</param>
        /// <param name="waitingForOthers">Whether its last job tag is vanilla's "waiting for others to finish gathering items".</param>
        /// <returns>How the member counts.</returns>
        public static MemberLoadingState MemberState(bool isColonist, bool drafted, bool waitingForOthers)
        {
            if (!isColonist || drafted)
            {
                return MemberLoadingState.Ignored;
            }

            return waitingForOthers ? MemberLoadingState.Waiting : MemberLoadingState.Busy;
        }

        /// <summary>
        /// Vanilla's completion test with drafted colonist members left out.
        /// </summary>
        /// <param name="waiting">Undrafted colonist members waiting for the others.</param>
        /// <param name="busy">Undrafted colonist members doing anything else.</param>
        /// <param name="inFlight">Pawns on the map running a gather job for the caravan.</param>
        /// <returns><c>true</c> when the undrafted members are all waiting and nobody is hauling.</returns>
        /// <remarks>
        /// <para>
        /// A drafted member's last job tag is never vanilla's waiting tag, so in vanilla one drafted
        /// member holds loading open until it is undrafted. With leads kept while drafted, that is how
        /// a pawn holds an animal, so the drafted are not waited on.
        /// </para>
        /// <para>
        /// At least one member has to be waiting. With every colonist drafted nobody is loading at
        /// all, and an answer of done would hand the caravan to the guard's watchdog, which would take
        /// every item off the list while the player was busy with a raid. Vanilla waits instead, and
        /// so does this.
        /// </para>
        /// </remarks>
        public static bool DoneIgnoringDrafted(int waiting, int busy, int inFlight)
        {
            return waiting > 0 && busy <= 0 && inFlight <= 0;
        }

        /// <summary>
        /// Determines whether a caravan member is one whose item search the guard asks.
        /// </summary>
        /// <param name="isColonist">Whether the member is a colonist; only colonists gather items.</param>
        /// <param name="spawned">Whether it is on the map.</param>
        /// <param name="downed">Whether it is downed.</param>
        /// <param name="drafted">Whether it is drafted.</param>
        /// <param name="canManipulate">Whether it is capable of Manipulation, which vanilla's member gather giver requires.</param>
        /// <returns><c>true</c> when the member counts.</returns>
        /// <remarks>
        /// A drafted member does not count. It runs no duty, so it gathers nothing however near it
        /// stands, and the drafted-member exemption has already decided the caravan does not wait for
        /// it. An item only it could reach counts as blocked, which ends in the grace period and a
        /// message naming the item, rather than in a hold that lasts until the watchdog.
        /// </remarks>
        public static bool IsTaker(bool isColonist, bool spawned, bool downed, bool drafted, bool canManipulate)
        {
            return isColonist && spawned && !downed && !drafted && canManipulate;
        }

        /// <summary>
        /// Classifies one of a list row's things.
        /// </summary>
        /// <param name="whereAMemberCouldTakeIt">Whether the thing is on the caravan's map on the ground or in a haul source such as a shelf.</param>
        /// <param name="inSomebodysHands">Whether a pawn on the caravan's map is carrying it.</param>
        /// <param name="anyTakerCanTake">Whether some member's own item search finds something to haul now.</param>
        /// <returns>How its units count.</returns>
        /// <remarks>
        /// Carried counts as blocked, never as gone. A pawn hauling a listed stack for any other job
        /// still has it, and it may be put down within reach; treating it as gone would let the
        /// caravan finish the moment the stack was picked up. A carrier hauling for the caravan itself
        /// runs a gather job, which keeps vanilla's answer at not done before the guard is asked.
        /// </remarks>
        public static LiveUnitKind Classify(bool whereAMemberCouldTakeIt, bool inSomebodysHands, bool anyTakerCanTake)
        {
            if (inSomebodysHands)
            {
                return LiveUnitKind.Blocked;
            }

            if (!whereAMemberCouldTakeIt)
            {
                return LiveUnitKind.Gone;
            }

            return anyTakerCanTake ? LiveUnitKind.Takeable : LiveUnitKind.Blocked;
        }

        /// <summary>
        /// Determines whether the lord's tick should act on the last verdict.
        /// </summary>
        /// <param name="enabled">Whether the "Wait for every item" setting is on.</param>
        /// <param name="lastVerdict">The verdict the completion test reached last.</param>
        /// <param name="ticksSinceVerdict">How long ago that was.</param>
        /// <param name="interval">How often the lord's tick runs.</param>
        /// <returns><c>true</c> for a fresh expiry verdict.</returns>
        public static bool LetGoDue(bool enabled, CompletionVerdict lastVerdict, int ticksSinceVerdict, int interval)
        {
            return enabled
                && ticksSinceVerdict >= 0
                && ticksSinceVerdict <= interval
                && (lastVerdict == CompletionVerdict.ExpireBlocked || lastVerdict == CompletionVerdict.ExpireAll);
        }

        /// <summary>
        /// Confirms an expiry against the caravan as it stands now.
        /// </summary>
        /// <param name="verdict">The expiry verdict.</param>
        /// <param name="takeableNow">Units some member can take now, counted only for <see cref="CompletionVerdict.ExpireBlocked"/>.</param>
        /// <param name="inFlightNow">Pawns running a gather job for the caravan now.</param>
        /// <returns><c>true</c> when the items may come off the list.</returns>
        /// <remarks>
        /// Up to a lord tick passes between the verdict and acting on it. If somebody has started
        /// hauling since, or a blocked item has become takeable, the verdict is stale and the next
        /// completion test decides again.
        /// </remarks>
        public static bool ConfirmLetGo(CompletionVerdict verdict, int takeableNow, int inFlightNow)
        {
            if (inFlightNow > 0)
            {
                return false;
            }

            if (verdict == CompletionVerdict.ExpireBlocked)
            {
                return takeableNow <= 0;
            }

            return verdict == CompletionVerdict.ExpireAll;
        }

        /// <summary>
        /// Splits one list row's outstanding units into takeable and blocked.
        /// </summary>
        /// <param name="remaining">The row's count still to load.</param>
        /// <param name="takeableLive">Units of the row's things that some member can take.</param>
        /// <param name="blockedLive">Units of the row's things that exist but nobody can take.</param>
        /// <param name="takeable">Outstanding units that are takeable.</param>
        /// <param name="blocked">Outstanding units that are blocked.</param>
        /// <remarks>
        /// Takeable units are counted first, because a member takes those before anything else, and
        /// neither total can exceed what the row still needs. Whatever the row needs beyond the live
        /// units is gone, eaten, destroyed or merged away, and counts as neither: holding a caravan
        /// for an item that no longer exists would hold it forever.
        /// </remarks>
        public static void SplitRow(int remaining, int takeableLive, int blockedLive, out int takeable, out int blocked)
        {
            int need = Math.Max(remaining, 0);
            takeable = Math.Min(need, Math.Max(takeableLive, 0));
            blocked = Math.Min(need - takeable, Math.Max(blockedLive, 0));
        }

        /// <summary>
        /// Moves the progress clock on when the loading progress reading has changed.
        /// </summary>
        /// <param name="lastMetric">The previous reading.</param>
        /// <param name="lastProgressTick">The tick of the previous change.</param>
        /// <param name="metric">The current reading.</param>
        /// <param name="now">The current tick.</param>
        /// <returns>The tick of the latest change.</returns>
        public static int NextProgressTick(int lastMetric, int lastProgressTick, int metric, int now)
        {
            return metric != lastMetric ? now : lastProgressTick;
        }

        /// <summary>
        /// Determines whether loading moved after the previous reading, or there was none.
        /// </summary>
        private static bool Progressed(GuardClocks clocks)
        {
            return clocks.LastProgressTick > clocks.LastReadingTick;
        }

        /// <summary>
        /// The time since the previous reading, counted only when that reading held the caravan.
        /// </summary>
        private static int Elapsed(GuardClocks clocks, int now)
        {
            return clocks.Holding && clocks.LastReadingTick >= 0 ? Math.Max(0, now - clocks.LastReadingTick) : 0;
        }
    }
}
