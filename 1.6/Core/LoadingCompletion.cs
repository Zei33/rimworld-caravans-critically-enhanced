using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CaravansCriticallyEnhanced.Utils;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Reads a forming caravan's item list for the completion guard, and lets go of what the guard expires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game half of <see cref="CompletionGuard"/>. It turns the lord into counts, hands them to
    /// the pure decision, and keeps the few ticks the decision needs between calls. It cannot be run
    /// outside the game and decides nothing itself.
    /// </para>
    /// <para>
    /// Nothing here is saved. After a load the hold, grace period and progress clock start again from
    /// that moment, which can only lengthen a wait, never end one early.
    /// </para>
    /// </remarks>
    public static class LoadingCompletion
    {
        /// <summary>How often the lord tick looks at the caravan, in ticks.</summary>
        public const int TickInterval = 250;

        private static readonly ConditionalWeakTable<LordJob_FormAndSendCaravan, Watch> Watches =
            new ConditionalWeakTable<LordJob_FormAndSendCaravan, Watch>();

        /// <summary>
        /// The completion guard's answer for <c>CaravanFormingUtility.AllItemsLoadedOntoCaravan</c>.
        /// </summary>
        /// <param name="lord">A lord <see cref="FormingLords.IsVanilla(Lord)"/> accepts.</param>
        /// <param name="map">The lord's map.</param>
        /// <param name="vanillaDone">Vanilla's answer, with the drafted-member exemption already applied.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>Whether loading is finished.</returns>
        public static bool Answer(Lord lord, Map map, bool vanillaDone, CaravansCriticallyEnhancedSettings settings)
        {
            var job = (LordJob_FormAndSendCaravan)lord.LordJob;
            Watch watch = WatchOf(job);
            int now = Find.TickManager.TicksGame;

            int takeable = 0;
            int blocked = 0;
            if (vanillaDone && settings.IsOn(Toggle.CompletionGuard))
            {
                CountOutstanding(lord, map, out takeable, out blocked);
            }

            watch.ClockFrom(ProgressOf(lord, map), now);

            CompletionVerdict verdict = CompletionGuard.Decide(
                vanillaDone,
                settings.IsOn(Toggle.CompletionGuard),
                takeable,
                blocked,
                now,
                watch.Clocks,
                settings.CompletionGraceTicks,
                settings.CompletionWatchdogTicks);

            watch.Clocks = CompletionGuard.Advance(watch.Clocks, vanillaDone, verdict, takeable, now);
            watch.LastVerdict = verdict;
            watch.LastVerdictTick = now;

            if (settings.IsOn(Toggle.CompletionGuard))
            {
                ClearEmptyRows(job);
            }

            return CompletionGuard.Done(vanillaDone, verdict);
        }

        /// <summary>
        /// Takes rows whose things have all gone off the list.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A row's things are dropped from it as they are destroyed, on every save
        /// (<c>TransferableOneWay.ExposeData</c>), so a row whose stacks were all eaten or merged away
        /// can be left with a count to load and nothing to load it from. Nothing can ever fill it, and
        /// its label reads its first thing: vanilla's Force depart confirmation lists every row with a
        /// count whenever this test answers not done, and throws on that one.
        /// </para>
        /// <para>
        /// Vanilla has the same trap; a held caravan answers not done for longer and springs it more
        /// often. So with "Wait for every item" on, these rows are cleared on every completion test,
        /// which runs just before vanilla's confirmation lists the rows, and on every lord tick pass in
        /// every stage, which catches a row emptied by a save or load within 250 ticks. Silently:
        /// there is nothing left to name.
        /// </para>
        /// </remarks>
        private static void ClearEmptyRows(LordJob_FormAndSendCaravan job)
        {
            List<TransferableOneWay> rows = job.transferables;
            for (int r = 0; r < rows.Count; r++)
            {
                if (rows[r].CountToTransfer > 0 && !rows[r].HasAnyThing)
                {
                    rows[r].ForceTo(0);
                }
            }
        }

        /// <summary>
        /// Vanilla's completion test with drafted colonist members left out.
        /// </summary>
        /// <param name="lord">The lord.</param>
        /// <param name="map">Its map.</param>
        /// <returns>
        /// <c>true</c> when at least one undrafted colonist member is waiting, every other undrafted one
        /// is too, and no pawn on the map is running a gather job for the lord.
        /// </returns>
        /// <remarks>
        /// The rules are <see cref="CompletionGuard.MemberState"/> and
        /// <see cref="CompletionGuard.DoneIgnoringDrafted"/>, which say why. The completion guard still
        /// holds the caravan while anything takeable is left.
        /// </remarks>
        public static bool DoneIgnoringDrafted(Lord lord, Map map)
        {
            int waiting = 0;
            int busy = 0;
            List<Pawn> members = lord.ownedPawns;
            for (int i = 0; i < members.Count; i++)
            {
                Pawn member = members[i];
                MemberLoadingState state = CompletionGuard.MemberState(
                    member.IsColonist,
                    member.Drafted,
                    member.mindState.lastJobTag == JobTag.WaitingForOthersToFinishGatheringItems);

                if (state == MemberLoadingState.Busy)
                {
                    busy++;
                }
                else if (state == MemberLoadingState.Waiting)
                {
                    waiting++;
                }
            }

            // The map-wide pawn scan only when it can change the answer.
            int inFlight = waiting > 0 && busy == 0 ? InFlightFor(lord, map) : 0;
            return CompletionGuard.DoneIgnoringDrafted(waiting, busy, inFlight);
        }

        /// <summary>
        /// Clears the guard's hold for a caravan outside the item-gathering stage.
        /// </summary>
        /// <param name="job">A vanilla lord job.</param>
        public static void Rest(LordJob_FormAndSendCaravan job)
        {
            if (Watches.TryGetValue(job, out Watch watch))
            {
                watch.Reset();
            }
        }

        /// <summary>
        /// The every-250-ticks pass from the lord job's tick: keeps the progress clock and lets go of
        /// what the guard has expired.
        /// </summary>
        /// <param name="job">A vanilla lord job.</param>
        /// <param name="settings">The settings.</param>
        public static void Tick(LordJob_FormAndSendCaravan job, CaravansCriticallyEnhancedSettings settings)
        {
            Lord lord = job.lord;
            Map map = lord?.Map;
            if (map == null)
            {
                return;
            }

            if (settings.IsOn(Toggle.CompletionGuard))
            {
                ClearEmptyRows(job);
            }

            Watch watch = WatchOf(job);
            if (!CompletionGuard.Applies(FormingLords.StageOf(lord)))
            {
                watch.Reset();
                return;
            }

            int now = Find.TickManager.TicksGame;
            watch.ClockFrom(ProgressOf(lord, map), now);

            if (!CompletionGuard.LetGoDue(settings.IsOn(Toggle.CompletionGuard), watch.LastVerdict, now - watch.LastVerdictTick, TickInterval))
            {
                return;
            }

            // Confirmed again rather than trusted: somebody may have started hauling since the verdict,
            // or a blocked item become takeable.
            int takeable = 0;
            if (watch.LastVerdict == CompletionVerdict.ExpireBlocked)
            {
                CountOutstanding(lord, map, out takeable, out _);
            }

            try
            {
                if (CompletionGuard.ConfirmLetGo(watch.LastVerdict, takeable, InFlightFor(lord, map)))
                {
                    LetGo(job, watch.LastVerdict, settings);
                }
            }
            finally
            {
                // Even when letting go fails, so one bad row cannot make the same verdict fire again
                // every pass.
                watch.Reset();
            }
        }

        /// <summary>
        /// Takes everything left on the item list off it, as the forming caravan tab's remove button
        /// does, and says so.
        /// </summary>
        private static void LetGo(LordJob_FormAndSendCaravan job, CompletionVerdict verdict, CaravansCriticallyEnhancedSettings settings)
        {
            var dropped = new List<string>();
            var targets = new List<Thing>();

            foreach (TransferableOneWay row in job.transferables)
            {
                int count = row.CountToTransfer;
                if (count <= 0)
                {
                    continue;
                }

                // Off the list first, so a label that fails cannot leave the row on it.
                row.ForceTo(0);

                // A row with no things left has no label: TransferableOneWay.Label reads its first thing.
                Thing any = row.AnyThing;
                if (any == null)
                {
                    continue;
                }

                dropped.Add(GenLabel.ThingLabel(any, count));
                if (any.SpawnedOrAnyParentSpawned)
                {
                    targets.Add(any);
                }
            }

            if (dropped.Count == 0)
            {
                return;
            }

            string text = verdict == CompletionVerdict.ExpireAll
                ? "CaravansCriticallyEnhanced_LoadingGaveUp".Translate(settings.CompletionWatchdogTicks.ToStringTicksToPeriod(), dropped.ToCommaList(useAnd: true))
                : "CaravansCriticallyEnhanced_LoadingSkippedBlocked".Translate(settings.CompletionGraceTicks.ToStringTicksToPeriod(), dropped.ToCommaList(useAnd: true));

            CaravanMessages.Post(text, new LookTargets(targets), MessageTypeDefOf.CautionInput);
        }

        /// <summary>
        /// Counts the units still to load while a member would go and fetch something, and those that
        /// exist on the map when no member would.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The guard needs only to know whether anything is still takeable, not which units, so the
        /// question is put once, as <see cref="AnyMemberWouldHaul"/>. While the answer is yes every
        /// live unit counts as takeable and the caravan is held; once it is no, every live unit is
        /// blocked and the grace period runs. A unit in somebody's hands is blocked either way.
        /// </para>
        /// <para>
        /// A row with no things left is skipped: it is gone, and <see cref="ClearEmptyRows"/> takes it
        /// off the list.
        /// </para>
        /// </remarks>
        private static void CountOutstanding(Lord lord, Map map, out int takeable, out int blocked)
        {
            takeable = 0;
            blocked = 0;

            List<Pawn> takers = TakersOf(lord);
            bool memberWouldHaul = AnyMemberWouldHaul(takers, lord);
            List<TransferableOneWay> rows = ((LordJob_FormAndSendCaravan)lord.LordJob).transferables;

            for (int r = 0; r < rows.Count; r++)
            {
                TransferableOneWay row = rows[r];
                if (row.CountToTransfer <= 0 || !row.HasAnyThing)
                {
                    continue;
                }

                int takeableLive = 0;
                int blockedLive = 0;
                for (int i = 0; i < row.things.Count; i++)
                {
                    Thing thing = row.things[i];
                    bool carried = IsCarried(thing, map);
                    bool onMap = !carried && OnMap(thing, map);

                    switch (CompletionGuard.Classify(onMap, carried, memberWouldHaul))
                    {
                        case LiveUnitKind.Takeable:
                            takeableLive += thing.stackCount;
                            break;
                        case LiveUnitKind.Blocked:
                            blockedLive += thing.stackCount;
                            break;
                    }
                }

                CompletionGuard.SplitRow(row.CountToTransfer, takeableLive, blockedLive, out int rowTakeable, out int rowBlocked);
                takeable += rowTakeable;
                blocked += rowBlocked;
            }
        }

        private static List<Pawn> TakersOf(Lord lord)
        {
            var takers = new List<Pawn>();
            foreach (Pawn member in lord.ownedPawns)
            {
                if (CompletionGuard.IsTaker(
                        member.IsColonist,
                        member.Spawned,
                        member.Downed,
                        member.Drafted,
                        member.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)))
                {
                    takers.Add(member);
                }
            }

            return takers;
        }

        /// <summary>
        /// Determines whether any member's own item search would find something to haul now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The search is vanilla's, <c>GatherItemsForCaravanUtility.FindThingToHaul</c>, the one
        /// <c>JobGiver_PrepareCaravan_GatherItems</c> runs for every member, so the guard holds exactly
        /// while a member would act. Any test of its own would disagree with it somewhere: the
        /// search skips haul sources the player has closed, such as an outfit stand that does not
        /// allow removing, and does not look inside haul sources at all once it falls back to its
        /// map-wide pass.
        /// </para>
        /// <para>
        /// Run only while vanilla says done, once per member, every 120 ticks at most.
        /// </para>
        /// </remarks>
        private static bool AnyMemberWouldHaul(List<Pawn> takers, Lord lord)
        {
            for (int i = 0; i < takers.Count; i++)
            {
                if (GatherItemsForCaravanUtility.FindThingToHaul(takers[i], lord) != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a pawn on the caravan's map is carrying the thing in its hands.
        /// </summary>
        private static bool IsCarried(Thing thing, Map map)
        {
            return thing != null
                && !thing.Destroyed
                && thing.ParentHolder is Pawn_CarryTracker carrier
                && carrier.pawn != null
                && carrier.pawn.Spawned
                && carrier.pawn.Map == map;
        }

        /// <summary>
        /// Determines whether the thing is on the caravan's map where a member could pick it up: on
        /// the ground, or in a haul source such as a shelf.
        /// </summary>
        /// <remarks>
        /// A pawn's inventory does not count. A stack a carrier already took whole sits in the
        /// carrier's inventory under its original reference, and its row has already been counted
        /// down for it.
        /// </remarks>
        private static bool OnMap(Thing thing, Map map)
        {
            if (thing == null || thing.Destroyed)
            {
                return false;
            }

            return thing.Spawned ? thing.Map == map : HaulAIUtility.IsInHaulableInventory(thing) && thing.MapHeld == map;
        }

        /// <summary>
        /// One number that changes whenever loading moves: the units still to load plus the gather
        /// jobs in progress for the lord.
        /// </summary>
        private static int ProgressOf(Lord lord, Map map)
        {
            int remaining = 0;
            List<TransferableOneWay> rows = ((LordJob_FormAndSendCaravan)lord.LordJob).transferables;
            for (int r = 0; r < rows.Count; r++)
            {
                remaining += System.Math.Max(rows[r].CountToTransfer, 0);
            }

            return unchecked((remaining * 397) ^ InFlightFor(lord, map));
        }

        /// <summary>
        /// Counts the pawns on the map running a gather job for the lord, by the same test
        /// <c>AllItemsLoadedOntoCaravan</c> makes.
        /// </summary>
        private static int InFlightFor(Lord lord, Map map)
        {
            int count = 0;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.CurJob != null && pawn.jobs.curDriver is JobDriver_PrepareCaravan_GatherItems && pawn.CurJob.lord == lord)
                {
                    count++;
                }
            }

            return count;
        }

        private static Watch WatchOf(LordJob_FormAndSendCaravan job)
        {
            return Watches.GetValue(job, _ => new Watch());
        }

        private sealed class Watch
        {
            public GuardClocks Clocks = GuardClocks.Idle;
            public int LastMetric = int.MinValue;
            public CompletionVerdict LastVerdict = CompletionVerdict.PassThrough;
            public int LastVerdictTick = -1;

            public void ClockFrom(int metric, int now)
            {
                int last = Clocks.LastProgressTick;
                Clocks = Clocks.WithProgress(last < 0 ? now : CompletionGuard.NextProgressTick(LastMetric, last, metric, now));
                LastMetric = metric;
            }

            public void Reset()
            {
                Clocks = Clocks.Cleared();
                LastVerdict = CompletionVerdict.PassThrough;
                LastVerdictTick = -1;
            }
        }
    }
}
