using System;
using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// Keeps a forming caravan's exit inside every restricted pawn's allowed area, holding the caravan
    /// at its spot while there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Run from the lord tick postfix every 250 ticks per caravan, and once on the first tick after a
    /// load for a held one. Areas change without any hook to catch (vanilla's Invert, Clear and Delete
    /// never mark anything dirty), so this polls. <see cref="ExitPolicy.Decide"/> says what to do;
    /// this reads and writes.
    /// </para>
    /// <para>
    /// Moving the exit is several writes, not one. The lord job's <c>exitSpot</c> is saved and is what
    /// the toils are rebuilt from on load; three toils hold their own copies (gathering downed pawns,
    /// collecting animals, leaving), which are not saved; and the members' duties, which are saved
    /// with the pawns, are rebuilt from the current toil with <c>UpdateAllDuties</c>. Gathering
    /// animals also has a destination field, which holds the meeting point and is never touched.
    /// </para>
    /// <para>
    /// Held, the lord job and the collecting and leaving copies keep the best exit ignoring areas, the
    /// fallback, so a save loaded without the mod, or anything that sends the caravan on regardless,
    /// leaves by it. Only the downed-pawn stage's copy points at the meeting point, because its duty
    /// sends idle colonists to that copy. The gates in the trigger, downed-pawn and baby patches keep a
    /// held caravan from moving on or carrying anybody off.
    /// </para>
    /// </remarks>
    public static class ExitDirector
    {
        /// <summary>
        /// How close to its exit a downed pawn being carried there has to be dropped, as vanilla's
        /// <c>JobGiver_PrepareCaravan_GatherDownedPawns.IsDownedPawnNearExitPoint</c> measures.
        /// </summary>
        public const float DownedNearExit = 7f;

        /// <summary>
        /// Runs the pass on the first tick after a load for a held caravan, so its toils point at the
        /// meeting point again before anything can send the members to the fallback, and the alert
        /// has its names back.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="settings">The mod's settings.</param>
        public static void FirstTickAfterLoad(LordJob_FormAndSendCaravan job, CaravansCriticallyEnhancedSettings settings)
        {
            if (CaravanExitStates.NeedsFirstPass(job))
            {
                Tick(job, settings);
            }
        }

        /// <summary>
        /// Runs one caravan's exit pass.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="settings">The mod's settings.</param>
        public static void Tick(LordJob_FormAndSendCaravan job, CaravansCriticallyEnhancedSettings settings)
        {
            Lord lord = job.lord;
            if (lord?.Map == null)
            {
                return;
            }

            CaravanExitState state = CaravanExitStates.For(job);
            state.Passed = true;
            if (!CaravanLordAccess.AllExitFieldsAvailable)
            {
                // Nothing can be moved, so nothing may stay held: the gates read only the flag.
                state.Holding = false;
                state.BlockingPawns.Clear();
                return;
            }

            if (!state.Initialised)
            {
                state.LastWrittenSpot = job.ExitSpot;
                state.Initialised = true;
            }

            if (job.ExitSpot != state.LastWrittenSpot)
            {
                // Another mod moved the exit: it owns it from now on.
                state.Foreign = true;
            }

            FormingStage stage = FormingLords.StageOf(lord);
            bool featureOn = settings.IsOn(Toggle.AreaAwareExit);
            bool ruleApplies = featureOn && !state.Foreign && !state.PlayerOverride && ExitPolicy.AreaRuleApplies(stage);
            ExitReading reading = null;
            ExitPlan plan = null;
            if (ruleApplies)
            {
                reading = ExitReadings.Read(lord.Map, lord.ownedPawns, CaravanLordAccess.JobMeetingPoint(job), StartingTile(job), job.ExitSpot, settings);
                plan = ExitPlanner.Plan(reading.Input);
            }

            bool fallbackMoved = plan != null && plan.FallbackIndex >= 0 && reading.Cells[plan.FallbackIndex] != job.ExitSpot;
            ExitAction action = ExitPolicy.Decide(featureOn, stage, state.Holding, state.Foreign, state.PlayerOverride, plan?.Blocker ?? ExitBlocker.None, plan?.CurrentValid ?? true, plan?.CurrentReachable ?? true, fallbackMoved);
            switch (action)
            {
                case ExitAction.Park when plan.FallbackIndex >= 0:
                    Park(job, state, reading.Cells[plan.FallbackIndex]);
                    Tell(job, "CaravansCriticallyEnhanced_ExitHeld", CaravanLordAccess.JobMeetingPoint(job), MessageTypeDefOf.CautionInput);
                    break;

                case ExitAction.MoveFallback:
                    Park(job, state, reading.Cells[plan.FallbackIndex]);
                    break;

                case ExitAction.ReleaseToPlan:
                    Release(job, state, reading.Cells[plan.CandidateIndex]);
                    Tell(job, "CaravansCriticallyEnhanced_ExitReleased", job.ExitSpot, MessageTypeDefOf.NeutralEvent);
                    break;

                case ExitAction.ReleaseInPlace:
                    Release(job, state, job.ExitSpot);
                    break;

                case ExitAction.Retarget:
                    Release(job, state, reading.Cells[plan.CandidateIndex]);
                    Tell(job, "CaravansCriticallyEnhanced_ExitMoved", job.ExitSpot, MessageTypeDefOf.NeutralEvent);
                    break;

                case ExitAction.Reroute:
                    Release(job, state, reading.Cells[plan.CandidateIndex]);
                    break;
            }

            if (state.Holding)
            {
                // A pass that cannot say whose areas are to blame (nobody reaches any edge) keeps the
                // names it had.
                if (plan != null && plan.BlockingMoverIds.Count > 0)
                {
                    reading.PawnsOf(plan.BlockingMoverIds, state.BlockingPawns);
                }

                // After a load the toils are rebuilt from the lord job's exit, the fallback.
                KeepParked(job, state);
            }

            FormingStage now = FormingLords.StageOf(lord);
            bool waiting = now == FormingStage.Wait || now == FormingStage.WaitPause;
            // Not while held: the lord job's exit is then only the fallback, and the release's own write
            // arms it against the exit the caravan will really leave by.
            if (ExitPolicy.WritesExit(action) && waiting && !state.Holding)
            {
                state.RegatherPending = true;
            }

            RegatherDowned(job, state, now, waiting);
        }

        /// <summary>
        /// Sets up a caravan's exit state as it starts forming, and holds it at once when the send found
        /// no exit inside every area.
        /// </summary>
        /// <param name="job">The new caravan's lord job.</param>
        /// <param name="exitSpot">The exit the send passed in.</param>
        /// <remarks>
        /// A lord job whose exit already differs from the one passed in had it rewritten while its
        /// graph was built, by a mod such as Enter Here, which then owns it.
        /// </remarks>
        public static void Begin(LordJob_FormAndSendCaravan job, IntVec3 exitSpot)
        {
            CaravanExitState state = CaravanExitStates.For(job);
            state.Initialised = true;
            state.Passed = true;
            state.LastWrittenSpot = job.ExitSpot;
            state.Foreign = job.ExitSpot != exitSpot;

            if (ExitHandoff.HoldOnStart && !state.Foreign && CaravanLordAccess.AllExitFieldsAvailable)
            {
                Park(job, state, job.ExitSpot);

                // Vanilla rethought every member's job inside the send, before this hold: a baby pick-up
                // beside the crib can already have landed.
                StopCarrying(job, anywhere: true);
                state.BlockingPawns.AddRange(ExitHandoff.Blocking);
                Tell(job, "CaravansCriticallyEnhanced_ExitHeld", CaravanLordAccess.JobMeetingPoint(job), MessageTypeDefOf.CautionInput);
            }
        }

        /// <summary>
        /// Lets a held caravan go by its fallback because the player forced its departure.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        public static void Override(LordJob_FormAndSendCaravan job)
        {
            CaravanExitState state = CaravanExitStates.For(job);
            state.PlayerOverride = true;
            if (state.Holding && CaravanLordAccess.AllExitFieldsAvailable)
            {
                Release(job, state, job.ExitSpot);
            }
        }

        /// <summary>
        /// Holds a caravan at its spot.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="state">Its exit state.</param>
        /// <param name="fallback">The best exit ignoring areas, written for vanilla.</param>
        public static void Park(LordJob_FormAndSendCaravan job, CaravanExitState state, IntVec3 fallback)
        {
            state.Holding = true;
            Write(job, state, fallback, CaravanLordAccess.JobMeetingPoint(job));
        }

        /// <summary>
        /// Lets a caravan leave by an exit, held or not.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="state">Its exit state.</param>
        /// <param name="exit">The exit.</param>
        public static void Release(LordJob_FormAndSendCaravan job, CaravanExitState state, IntVec3 exit)
        {
            state.Holding = false;
            state.BlockingPawns.Clear();
            Write(job, state, exit, exit);
        }

        /// <summary>
        /// Gets the first tile of a caravan's world route.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <returns>The tile, or invalid when the field has gone.</returns>
        public static PlanetTile StartingTile(LordJob_FormAndSendCaravan job)
        {
            return CaravanLordAccess.JobStartingTile != null ? CaravanLordAccess.JobStartingTile(job) : PlanetTile.Invalid;
        }

        private static void KeepParked(LordJob_FormAndSendCaravan job, CaravanExitState state)
        {
            IntVec3 meetingPoint = CaravanLordAccess.JobMeetingPoint(job);
            if (!CopiesAt(job.lord.Graph.lordToils, job.ExitSpot, meetingPoint))
            {
                Write(job, state, job.ExitSpot, meetingPoint);
            }

            StopCarrying(job, anywhere: false);
        }

        private static void Write(LordJob_FormAndSendCaravan job, CaravanExitState state, IntVec3 exit, IntVec3 downedSpot)
        {
            CaravanLordAccess.JobExitSpot(job) = exit;
            state.LastWrittenSpot = exit;
            WriteCopies(job.lord.Graph.lordToils, exit, downedSpot);
            job.lord.CurLordToil?.UpdateAllDuties();
            EndStaleWalks(job.lord, downedSpot);
        }

        /// <summary>
        /// Writes every toil's copy of the exit.
        /// </summary>
        /// <param name="toils">The caravan's toils.</param>
        /// <param name="exit">The cell for the collecting and leaving copies.</param>
        /// <param name="downedSpot">The cell for the downed-pawn stage's copy, which its idle colonists walk to.</param>
        /// <remarks>
        /// Exact types: gathering animals is a rope toil too, and its destination is the meeting
        /// point, which nothing may move.
        /// </remarks>
        internal static void WriteCopies(List<LordToil> toils, IntVec3 exit, IntVec3 downedSpot)
        {
            foreach (LordToil toil in toils)
            {
                Type type = toil.GetType();
                if (type == typeof(LordToil_PrepareCaravan_Leave))
                {
                    CaravanLordAccess.LeaveExitSpot((LordToil_PrepareCaravan_Leave)toil) = exit;
                }
                else if (type == typeof(LordToil_PrepareCaravan_GatherDownedPawns))
                {
                    CaravanLordAccess.GatherDownedPawnsExitSpot((LordToil_PrepareCaravan_GatherDownedPawns)toil) = downedSpot;
                }
                else if (type == typeof(LordToil_PrepareCaravan_CollectAnimals))
                {
                    CaravanLordAccess.RopeDestinationPoint((LordToil_PrepareCaravan_CollectAnimals)toil) = exit;
                }
            }
        }

        /// <summary>
        /// Determines whether every toil's copy of the exit is where <see cref="WriteCopies"/> would put it.
        /// </summary>
        /// <param name="toils">The caravan's toils.</param>
        /// <param name="exit">The cell for the collecting and leaving copies.</param>
        /// <param name="downedSpot">The cell for the downed-pawn stage's copy.</param>
        /// <returns><c>true</c> when all of them are.</returns>
        internal static bool CopiesAt(List<LordToil> toils, IntVec3 exit, IntVec3 downedSpot)
        {
            foreach (LordToil toil in toils)
            {
                Type type = toil.GetType();
                bool elsewhere =
                    (type == typeof(LordToil_PrepareCaravan_Leave) && CaravanLordAccess.LeaveExitSpot((LordToil_PrepareCaravan_Leave)toil) != exit)
                    || (type == typeof(LordToil_PrepareCaravan_GatherDownedPawns) && CaravanLordAccess.GatherDownedPawnsExitSpot((LordToil_PrepareCaravan_GatherDownedPawns)toil) != downedSpot)
                    || (type == typeof(LordToil_PrepareCaravan_CollectAnimals) && CaravanLordAccess.RopeDestinationPoint((LordToil_PrepareCaravan_CollectAnimals)toil) != exit);
                if (elsewhere)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ends the jobs still heading for the old spot while downed pawns are gathered.
        /// </summary>
        /// <remarks>
        /// Only that stage's duty names the exit. A carry ending keeps the carried pawn, since its job
        /// def has <c>carryThingAfterJob</c>; released, the giver hands the carrier its pawn again with
        /// the new drop cell, and held, <see cref="StopCarrying"/> has it put down at the spot. The walk to the
        /// spot is a goto with an expiry of 999999 ticks, so nothing else would stop it. Drafted pawns
        /// are the player's and are never touched.
        /// </remarks>
        private static void EndStaleWalks(Lord lord, IntVec3 downedSpot)
        {
            if (lord.CurLordToil?.GetType() != typeof(LordToil_PrepareCaravan_GatherDownedPawns))
            {
                return;
            }

            List<Pawn> owned = lord.ownedPawns;
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                Pawn pawn = owned[i];
                Job current = pawn.CurJob;
                if (current == null || pawn.Drafted || !pawn.IsColonist)
                {
                    continue;
                }

                bool staleCarry = current.def == JobDefOf.PrepareCaravan_GatherDownedPawns && !current.targetB.Cell.InHorDistOf(downedSpot, DownedNearExit);
                bool staleWalk = current.def == JobDefOf.Goto && !current.targetA.Cell.InHorDistOf(downedSpot, DownedNearExit);
                if (staleCarry || staleWalk)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        /// <summary>
        /// Keeps a held caravan's undrafted members from carrying its downed pawns and babies about:
        /// a pick-up still on its way is stopped, and a pawn already carried is put down once its
        /// carrier is only wandering at the meeting point, or at once when the hold starts with the send.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="anywhere">Whether to put carried pawns down wherever their carriers stand.</param>
        /// <remarks>
        /// <para>
        /// Run when a hold starts at the send and on every pass while it lasts. Vanilla rethinks every
        /// member's job inside the send, before the mod has held the caravan, so a baby pick-up can
        /// already be on its way, or done when the colonist stood by the crib; the gates stop only new
        /// ones.
        /// </para>
        /// <para>
        /// Wandering and walking keep a carried thing (<c>Goto</c>, <c>GotoWander</c> and
        /// <c>Wait_Wander</c> have <c>dropThingBeforeJob</c> false), so a carrier would otherwise hold
        /// the pawn, unspawned, for the whole hold. Put down where the carrier happened to be, an adult
        /// could lie at a far edge for as long, since nothing rescues or feeds a downed pawn its
        /// caravan is to take; the duties of gathering animals, of the held downed stage and of
        /// waiting walk the carrier to the meeting point first. Any other job vanilla gives a carrier
        /// (a haul, a meal, sleep) drops the pawn where it stands, as in vanilla; one that uses the
        /// carry, such as feeding a baby, is left alone.
        /// </para>
        /// </remarks>
        private static void StopCarrying(LordJob_FormAndSendCaravan job, bool anywhere)
        {
            List<Pawn> downed = job.downedPawns;
            if (downed == null || downed.Count == 0)
            {
                return;
            }

            IntVec3 meetingPoint = CaravanLordAccess.JobMeetingPoint(job);
            List<Pawn> owned = job.lord.ownedPawns;
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                Pawn pawn = owned[i];
                if (pawn.Drafted || !pawn.Spawned)
                {
                    continue;
                }

                Job current = pawn.CurJob;
                Pawn carried = pawn.carryTracker?.CarriedThing as Pawn;
                bool listed = carried != null && ExitReadings.IsAmong(carried, downed);
                bool fetching = current != null
                    && (current.def == JobDefOf.PickupToHold || current.def == JobDefOf.PrepareCaravan_GatherDownedPawns)
                    && current.targetA.Thing is Pawn target && ExitReadings.IsAmong(target, downed)
                    && carried != target;
                if (fetching)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    continue;
                }

                bool idleAtSpot = listed && current != null
                    && (current.def == JobDefOf.Goto || current.def == JobDefOf.GotoWander || current.def == JobDefOf.Wait_Wander)
                    && pawn.Position.InHorDistOf(meetingPoint, DownedNearExit);
                if ((anywhere && listed) || idleAtSpot)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _);
                }
            }
        }

        /// <summary>
        /// Sends a waiting caravan back to gathering its downed pawns when the mod's exit write left
        /// some away from its exit, as soon as every one of them can be reached.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="state">Its exit state, which holds whether a write is pending.</param>
        /// <param name="stage">Its stage.</param>
        /// <param name="waiting">Whether that is the waiting stage or its pause.</param>
        /// <remarks>
        /// From the waiting stage's pause it goes to the gathering stage's own pause, which unpauses
        /// into the gathering stage once the break is over, as vanilla's pauses do.
        /// </remarks>
        private static void RegatherDowned(LordJob_FormAndSendCaravan job, CaravanExitState state, FormingStage stage, bool waiting)
        {
            if (!state.RegatherPending)
            {
                return;
            }

            Lord lord = job.lord;
            CountStrays(job, job.ExitSpot, out int strays, out int unreachable, null, workersOnly: false);
            RegatherStep step = ExitPolicy.Regather(state.RegatherPending, waiting, strays, unreachable);
            if (step == RegatherStep.Forget || step == RegatherStep.GoBack)
            {
                state.RegatherPending = false;
            }

            if (step != RegatherStep.GoBack)
            {
                return;
            }

            // The graph's order is vanilla's and pinned by test: the gathering stage, then its pause.
            List<LordToil> toils = lord.Graph.lordToils;
            int gathering = (int)FormingStage.GatherDownedPawns;
            if (toils.Count > gathering + 1 && toils[gathering].GetType() == typeof(LordToil_PrepareCaravan_GatherDownedPawns))
            {
                lord.GotoToil(toils[stage == FormingStage.WaitPause ? gathering + 1 : gathering]);
            }
        }

        /// <summary>
        /// Counts a caravan's listed downed pawns that lie away from an exit, and those of them nobody
        /// can fetch now.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="exit">The exit.</param>
        /// <param name="strays">How many lie, or are held, more than vanilla's 7 cells from it, babies included.</param>
        /// <param name="unfetchable">How many of those nobody can fetch.</param>
        /// <param name="unfetchablePawns">A list to fill with the unfetchable ones, or <c>null</c>; cleared first.</param>
        /// <param name="workersOnly">Whether only an undrafted member not in a mental state may fetch, as for leaving now; otherwise a drafted one counts too, for whom the gathering stage waits.</param>
        /// <remarks>
        /// <para>
        /// As vanilla's carry giver: only a pawn still downed, which vanilla's lord tick otherwise takes
        /// off the list.
        /// </para>
        /// <para>
        /// One held by a member that does not work the duties (drafted, or unable to) goes with it:
        /// a carried pawn joins the caravan with its carrier, whatever the distance. One held by a
        /// worker far from the exit is a stray its carrier fetches (the giver hands a carrier its own
        /// pawn first), and must be: the gathering stage ends while its last pawn is still carried,
        /// and a collecting rope job, which drops whatever its roper holds, would leave it wherever
        /// that was. One held by a member in a mental state cannot be fetched.
        /// </para>
        /// </remarks>
        internal static void CountStrays(LordJob_FormAndSendCaravan job, IntVec3 exit, out int strays, out int unfetchable, List<Pawn> unfetchablePawns, bool workersOnly)
        {
            strays = 0;
            unfetchable = 0;
            unfetchablePawns?.Clear();
            List<Pawn> downed = job.downedPawns;
            if (downed == null)
            {
                return;
            }

            for (int i = 0; i < downed.Count; i++)
            {
                Pawn pawn = downed[i];
                if (pawn == null || pawn.Destroyed || !pawn.Downed)
                {
                    continue;
                }

                Pawn carrier = CarrierOf(pawn, job.lord);
                if ((carrier != null && !carrier.InMentalState && !IsWorker(carrier))
                    || JobGiver_PrepareCaravan_GatherDownedPawns.IsDownedPawnNearExitPoint(pawn, exit))
                {
                    continue;
                }

                strays++;
                bool fetchable = carrier != null ? IsWorker(carrier) : AnyMemberReaches(job.lord, pawn, workersOnly);
                if (!fetchable)
                {
                    unfetchable++;
                    unfetchablePawns?.Add(pawn);
                }
            }
        }

        /// <summary>
        /// Sends a caravan off from a cell the player chose: Leave from here.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="spot">The edge cell.</param>
        /// <remarks>
        /// An override, as Force depart is: the area rule leaves the caravan alone from now on, and a
        /// hold ends. The stage it goes to is the caller's (<see cref="LeaveFromHerePolicy.RouteFor"/>).
        /// </remarks>
        public static void LeaveFrom(LordJob_FormAndSendCaravan job, IntVec3 spot)
        {
            CaravanExitState state = CaravanExitStates.For(job);
            state.Initialised = true;
            state.PlayerOverride = true;
            state.RegatherPending = false;
            Release(job, state, spot);
        }

        /// <summary>
        /// Gets the member of a lord carrying a downed pawn.
        /// </summary>
        /// <param name="downed">The downed pawn.</param>
        /// <param name="lord">The lord.</param>
        /// <returns>The spawned member holding it, or <c>null</c>.</returns>
        internal static Pawn CarrierOf(Pawn downed, Lord lord)
        {
            return downed.ParentHolder is Pawn_CarryTracker tracker && tracker.pawn != null && tracker.pawn.Spawned && tracker.pawn.GetLord() == lord
                ? tracker.pawn
                : null;
        }

        /// <summary>
        /// Determines whether a member works its stage's duties: an undrafted colonist on its feet
        /// with hands, not in a mental state. A drafted pawn takes orders, which come above the duty.
        /// </summary>
        /// <param name="pawn">The member.</param>
        /// <returns><c>true</c> when it does.</returns>
        internal static bool IsWorker(Pawn pawn)
        {
            return pawn.IsColonist && !pawn.Drafted && !pawn.InMentalState && !pawn.Downed && pawn.Spawned
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        /// <summary>
        /// Determines whether some colonist member with hands can reach the pawn where it lies now.
        /// </summary>
        /// <remarks>
        /// Reach only, not the downed giver's full test: a reservation held elsewhere passes, and the
        /// giver waits for it in the gathering stage. A pawn somebody else is carrying, or one off the
        /// map, cannot be reached now.
        /// </remarks>
        /// <param name="lord">The caravan's lord.</param>
        /// <param name="downed">The downed pawn.</param>
        /// <param name="workersOnly">Whether only a member that works the duties counts; otherwise a drafted one does too.</param>
        /// <returns><c>true</c> when some member can.</returns>
        internal static bool AnyMemberReaches(Lord lord, Pawn downed, bool workersOnly)
        {
            if (!downed.Spawned)
            {
                return false;
            }

            List<Pawn> owned = lord.ownedPawns;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i];
                bool able = workersOnly
                    ? IsWorker(pawn)
                    : pawn.IsColonist && !pawn.Downed && pawn.Spawned && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
                if (able && pawn.CanReach(downed, PathEndMode.Touch, Danger.Deadly))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Tell(LordJob_FormAndSendCaravan job, string key, IntVec3 cell, MessageTypeDef type)
        {
            CaravanMessages.Post(key.Translate(), new LookTargets(new GlobalTargetInfo(cell, job.lord.Map)), type);
        }
    }
}
