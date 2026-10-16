using System;
using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Jobs;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// Reads the game for Leave from here and acts on it: whether a pawn gets the button, the edge cell
    /// to leave by, the confirmation, and the departure.
    /// </summary>
    /// <remarks>
    /// <see cref="LeaveFromHerePolicy"/> decides; this reads and writes. The cell comes from the exit
    /// planner over the same readings as the area rule, so every pawn going, pen animals as led, can
    /// reach it; the player's choice overrides areas and threats, which the confirmation lists.
    /// </remarks>
    public static class LeaveFromHere
    {
        /// <summary>
        /// Gets a value indicating whether the Force depart confirmation is being built for Leave from
        /// here, so that the Force depart patch leaves it alone.
        /// </summary>
        public static bool Confirming { get; private set; }

        /// <summary>
        /// Decides whether a pawn gets the button.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <returns>The button's state.</returns>
        public static LeaveButton ButtonFor(Pawn pawn)
        {
            // Asked for every selected pawn every frame: the lord is looked up only for a pawn that could
            // have the button.
            bool drafted = pawn.Drafted;
            bool controlled = pawn.IsColonistPlayerControlled && pawn.Spawned;
            Lord lord = drafted && controlled ? pawn.GetLord() : null;
            bool member = FormingLords.VanillaJobOf(lord) != null && CaravanLordAccess.AllExitFieldsAvailable;
            return LeaveFromHerePolicy.Button(
                CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeaveFromHere),
                member,
                drafted,
                controlled,
                member ? FormingLords.StageOf(lord) : FormingStage.Unknown,
                member ? pawn.Position.DistanceToEdge(pawn.Map) : int.MaxValue);
        }

        /// <summary>
        /// Finds the edge cell to leave by and asks the player to confirm.
        /// </summary>
        /// <param name="lord">The caravan's lord.</param>
        /// <param name="anchor">The member whose button the press went to; the edge cell is near it.</param>
        public static void Offer(Lord lord, Pawn anchor)
        {
            LordJob_FormAndSendCaravan job = FormingLords.VanillaJobOf(lord);
            if (job == null || lord.Map == null)
            {
                return;
            }

            if (!FindSpot(job, anchor, out IntVec3 spot, out ExitReading reading, out int index))
            {
                Reject(anchor);
                return;
            }

            string text = Confirmation(job, spot, reading, index);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Depart(lord, anchor)));
        }

        /// <summary>
        /// Sends the caravan off, once the player confirmed.
        /// </summary>
        /// <param name="lord">The caravan's lord.</param>
        /// <param name="anchor">The member whose button the press went to.</param>
        /// <remarks>
        /// Everything is looked at again: the dialog pauses the game, but other mods may not.
        /// </remarks>
        public static void Depart(Lord lord, Pawn anchor)
        {
            LordJob_FormAndSendCaravan job = FormingLords.VanillaJobOf(lord);
            if (job == null || lord.Map == null || !lord.Map.lordManager.lords.Contains(lord)
                || LeaveFromHerePolicy.RouteFor(FormingLords.StageOf(lord), false, false, false, false) == LeaveRoute.Refuse)
            {
                return;
            }

            if (!FindSpot(job, anchor, out IntVec3 spot, out _, out _))
            {
                Reject(anchor);
                return;
            }

            // As vanilla's Force depart: a member in a mental state is left out, and unloads.
            List<Pawn> owned = lord.ownedPawns;
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i].InMentalState)
                {
                    Pawn broken = owned[i];
                    lord.RemovePawn(broken);
                    broken.inventory.UnloadEverything = true;
                }
            }

            if (owned.Count == 0)
            {
                Map map = lord.Map;
                map.lordManager.RemoveLord(lord);
                CaravanMessages.Post("MessageForceSendingCaravan_AllLost".Translate(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }

            ExitDirector.LeaveFrom(job, spot);
            var unfetchablePawns = new List<Pawn>();
            ExitDirector.CountStrays(job, spot, out int strays, out int unfetchable, unfetchablePawns, workersOnly: true);

            // Left behind, as the question said: the gathering stage waits for every listed downed pawn,
            // so one nobody can fetch would keep it open for good. Off the list, it is rescued as usual.
            foreach (Pawn left in unfetchablePawns)
            {
                job.downedPawns.RemoveAll(p => ReferenceEquals(p, left));
            }

            // A helper's lead ends with the stage change below; let go now, so that an animal it holds is
            // judged by whether it can walk out by itself.
            JobDriver_CCE_RopeAnimalToCaravan.LetGoFor(lord);
            LeaveRoute route = LeaveFromHerePolicy.RouteFor(FormingLords.StageOf(lord), strays > unfetchable, NeedsRoping(lord, spot, out bool penned), AnyWorker(lord), penned);
            if (route == LeaveRoute.LeaveStage)
            {
                // As vanilla's Force depart: an animal tied at the spot walks to the exit by itself.
                UntieFromSpots(lord);
            }

            GoTo(lord, route);
            OrderDraftedOver(lord, spot);
        }

        /// <summary>
        /// Finds the edge cell to leave by.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="anchor">The member the cell has to be near.</param>
        /// <param name="spot">The cell.</param>
        /// <param name="reading">The readings it was chosen from.</param>
        /// <param name="index">Its index in them.</param>
        /// <returns><c>true</c> when there is one.</returns>
        /// <remarks>
        /// Planned over the members who will go: one in a mental state is left out, as the departure
        /// leaves it.
        /// </remarks>
        internal static bool FindSpot(LordJob_FormAndSendCaravan job, Pawn anchor, out IntVec3 spot, out ExitReading reading, out int index)
        {
            Lord lord = job.lord;
            IntVec3 centre = anchor.Position;
            reading = ExitReadings.Read(lord.Map, Going(lord), centre, ExitDirector.StartingTile(job), IntVec3.Invalid, CaravansCriticallyEnhancedMod.Settings);
            ExitPlanInput input = reading.Input;
            for (int i = 0; i < input.Candidates.Count; i++)
            {
                EdgeCandidate candidate = input.Candidates[i];
                if (candidate.Usable && !LeaveFromHerePolicy.Near(candidate.X, candidate.Z, centre.x, centre.z))
                {
                    input.Candidates[i] = new EdgeCandidate(candidate.X, candidate.Z, candidate.RegionId, false, candidate.Side);
                }
            }

            // The player's choice overrides both; the masks stay read for the confirmation.
            input.ApplyAreas = false;
            input.ApplyThreats = false;
            ExitPlan plan = ExitPlanner.Plan(input);
            index = plan.CandidateIndex;
            spot = plan.Blocker == ExitBlocker.None ? reading.Cells[index] : IntVec3.Invalid;
            return plan.Blocker == ExitBlocker.None;
        }

        private static string Confirmation(LordJob_FormAndSendCaravan job, IntVec3 spot, ExitReading reading, int index)
        {
            string text;
            Confirming = true;
            try
            {
                // Vanilla's own: the intro, items still to load, and members in a mental state.
                text = CaravanFormingUtility.GetForceDepartWarningMessage(job);
            }
            finally
            {
                Confirming = false;
            }

            var unfetchable = new List<Pawn>();
            ExitDirector.CountStrays(job, spot, out _, out _, unfetchable, workersOnly: true);
            if (unfetchable.Count > 0)
            {
                text += "\n\n" + "CaravansCriticallyEnhanced_LeaveFromHereUnreachable".Translate() + "\n\n"
                    + unfetchable.Select(p => p.LabelNoCountColored.ToString()).ToLineList("- ");
            }

            var outside = new List<string>();
            foreach (MoverReading mover in reading.Input.Movers)
            {
                if (mover.Allowed != null && !mover.Allowed[index])
                {
                    outside.Add(reading.Movers[mover.Id].LabelNoCountColored.ToString());
                }
            }

            if (outside.Count > 0)
            {
                text += "\n\n" + "CaravansCriticallyEnhanced_LeaveFromHereOutsideAreas".Translate() + "\n\n" + outside.ToLineList("- ");
            }

            return text;
        }

        private static void GoTo(Lord lord, LeaveRoute route)
        {
            // The graph's order is vanilla's and pinned by test.
            List<LordToil> toils = lord.Graph.lordToils;
            int target;
            Type expected;
            switch (route)
            {
                case LeaveRoute.DownedStage:
                    target = (int)FormingStage.GatherDownedPawns;
                    expected = typeof(LordToil_PrepareCaravan_GatherDownedPawns);
                    break;
                case LeaveRoute.CollectStage:
                    target = (int)FormingStage.CollectAnimals;
                    expected = typeof(LordToil_PrepareCaravan_CollectAnimals);
                    break;
                case LeaveRoute.LeaveStage:
                    target = (int)FormingStage.Leave;
                    expected = typeof(LordToil_PrepareCaravan_Leave);
                    break;
                default:
                    return;
            }

            if (toils.Count <= target || toils[target].GetType() != expected)
            {
                return;
            }

            lord.GotoToil(toils[target]);

            // What a stage change does, sparing the drafted, as the mod's transition patch does: collecting
            // jobs under way keep their old destination and never end by themselves.
            List<Pawn> owned = lord.ownedPawns;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i];
                if (pawn.jobs?.curJob != null && !pawn.Drafted)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        private static void OrderDraftedOver(Lord lord, IntVec3 spot)
        {
            // Vanilla's leaving stage waits for every member within 10 cells of the exit, drafted ones
            // too, and the downed stage counts a carried pawn by its carrier's cell, within 7.
            List<Pawn> owned = lord.ownedPawns;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i];
                if (!pawn.Drafted || !pawn.Spawned || pawn.Position.InHorDistOf(spot, LeaveFromHerePolicy.OrderOverBeyond))
                {
                    continue;
                }

                // As vanilla's drafted move: a cell nobody else is ordered to (the search gives back the
                // pawn's own cell when it finds none), and a mech only within its mechanitor's range.
                IntVec3 cell = RCellFinder.BestOrderedGotoDestNear(spot, pawn);
                if (pawn.IsColonyMech && !MechanitorUtility.InMechanitorCommandRange(pawn, cell))
                {
                    // Beyond command, and a mech is never undrafted by itself: undrafted, its leaving
                    // duty walks it over.
                    pawn.drafter.Drafted = false;
                }
                else if (cell != pawn.Position)
                {
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobTag.Misc);
                }
            }
        }

        private static List<Pawn> Going(Lord lord)
        {
            var going = new List<Pawn>();
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (!pawn.InMentalState)
                {
                    going.Add(pawn);
                }
            }

            return going;
        }

        private static bool AnyWorker(Lord lord)
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (ExitDirector.IsWorker(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsRoping(Lord lord, IntVec3 spot, out bool penned)
        {
            // As vanilla's collecting stage finishes, every pen animal on a member's lead; but only a
            // drafted member's counts here, since the undrafted members' jobs are about to end, and
            // their leads with them.
            bool needs = false;
            penned = false;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (!AnimalPenUtility.NeedsToBeManagedByRope(pawn))
                {
                    continue;
                }

                Pawn roper = pawn.roping?.RopedByPawn;
                if (roper != null && roper.Drafted && roper.GetLord() == lord)
                {
                    continue;
                }

                needs = true;
                penned |= pawn.Spawned && !pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly);
            }

            return needs;
        }

        private static void UntieFromSpots(Lord lord)
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn.roping != null && pawn.roping.IsRopedToSpot)
                {
                    pawn.roping.UnropeFromSpot();
                }
            }
        }

        private static void Reject(Pawn anchor)
        {
            CaravanMessages.Post("CaravansCriticallyEnhanced_LeaveFromHereNoSpot".Translate(), new LookTargets(anchor), MessageTypeDefOf.RejectInput);
        }
    }
}
