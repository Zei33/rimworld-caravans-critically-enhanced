using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// Reads a roper for <see cref="LeadPolicy"/> and acts on its answers.
    /// </summary>
    /// <remarks>
    /// The game half of the lead rules. It decides nothing itself; the patches on the rope tracker,
    /// the draft setter, the follow giver and the auto-undrafter all come through here, so that each
    /// asks the same question the same way.
    /// </remarks>
    public static class LeadUtility
    {
        /// <summary>
        /// Determines whether the mod holds this roper's leads in place of vanilla.
        /// </summary>
        /// <param name="roper">The roper.</param>
        /// <returns><c>true</c> for a drafted colonist of a vanilla forming caravan leading animals.</returns>
        /// <remarks>
        /// Asked every tick for every roper and every frame for every selected colonist, so the two
        /// cheapest readings that already answer no for nearly everyone come first. The policy still
        /// gets every reading and decides.
        /// </remarks>
        public static bool KeepsLeads(Pawn roper)
        {
            Pawn_RopeTracker roping = roper?.roping;
            if (roping == null || !roper.Drafted || roping.Ropees.Count == 0)
            {
                return false;
            }

            return LeadPolicy.KeepsLeads(
                CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting),
                roper.Drafted,
                FormingLords.VanillaLordOf(roper) != null,
                roping.Ropees.Count > 0,
                roping.IsRoped);
        }

        /// <summary>
        /// Determines whether drafting this pawn now should keep its leads.
        /// </summary>
        /// <param name="pawn">The pawn about to be drafted.</param>
        /// <returns><c>true</c> when it would be a roper the mod holds once drafted.</returns>
        public static bool KeepsLeadsWhenDrafted(Pawn pawn)
        {
            Pawn_RopeTracker roping = pawn?.roping;
            if (roping == null || !pawn.Spawned)
            {
                return false;
            }

            return LeadPolicy.KeepsLeads(
                CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting),
                true,
                FormingLords.VanillaLordOf(pawn) != null,
                roping.Ropees.Count > 0,
                roping.IsRoped);
        }

        /// <summary>
        /// Determines whether a held roper has to let go of everything.
        /// </summary>
        /// <param name="roper">The roper.</param>
        /// <returns><c>true</c> when it is dead, downed, asleep, burning or broken.</returns>
        public static bool IsFatal(Pawn roper)
        {
            return LeadPolicy.IsFatal(
                roper.Dead,
                roper.Downed,
                !roper.Awake(),
                roper.InMentalState && roper.MentalStateDef != MentalStateDefOf.Roaming,
                roper.IsBurning());
        }

        /// <summary>
        /// Lets go of every held animal too hungry or tired to keep following, and says so.
        /// </summary>
        /// <param name="roper">A roper the mod holds.</param>
        public static void ReleaseNeedyRopees(Pawn roper)
        {
            if (!LeadPolicy.ReleasesNeedyAnimals(FormingLords.StageOf(roper.GetLord())))
            {
                return;
            }

            List<Pawn> ropees = roper.roping.Ropees;
            for (int i = ropees.Count - 1; i >= 0; i--)
            {
                Pawn animal = ropees[i];
                LeadRelease release = LeadPolicy.ShouldRelease(animal.needs?.food?.CurCategory, animal.needs?.rest?.CurCategory);
                if (release == LeadRelease.None)
                {
                    continue;
                }

                roper.roping.DropRope(animal);
                string key = release == LeadRelease.Hungry
                    ? "CaravansCriticallyEnhanced_LeadReleasedHungry"
                    : "CaravansCriticallyEnhanced_LeadReleasedTired";
                CaravanMessages.Post(key.Translate(roper.LabelShort, animal.LabelShort), new LookTargets(animal), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// Makes a held roper walk no faster than its slowest animal.
        /// </summary>
        /// <param name="roper">A roper the mod holds.</param>
        /// <param name="force">Whether to set it again when the current job already has one.</param>
        /// <remarks>
        /// <c>Pawn_PathFollower</c> moves a pawn at the speed of its driver's
        /// <c>locomotionUrgencySameAs</c>, which vanilla's rope driver sets the same way. Without it a
        /// drafted roper outpaces its animals, and they stop and start every second behind it. Each
        /// new job starts without it, so it is set as soon as a job is seen without it, and refreshed
        /// now and then in case an animal joined or left.
        /// </remarks>
        public static void MatchSpeed(Pawn roper, bool force)
        {
            JobDriver driver = roper.jobs?.curDriver;
            if (driver == null || driver is JobDriver_RopeToDestination || (!force && driver.locomotionUrgencySameAs != null))
            {
                return;
            }

            Pawn slowest = null;
            List<Pawn> ropees = roper.roping.Ropees;
            for (int i = 0; i < ropees.Count; i++)
            {
                Pawn animal = ropees[i];
                if (animal.Spawned && (slowest == null || animal.TicksPerMoveCardinal > slowest.TicksPerMoveCardinal))
                {
                    slowest = animal;
                }
            }

            driver.locomotionUrgencySameAs = slowest;
        }

        /// <summary>
        /// Determines whether a caravan animal is one a drafted colonist may take the lead of.
        /// </summary>
        /// <param name="animal">The animal.</param>
        /// <returns><c>true</c> when the option is offered.</returns>
        public static bool CanBeTaken(Pawn animal)
        {
            return LeadPolicy.CanBeTaken(
                AnimalPenUtility.NeedsToBeManagedByRope(animal),
                animal.roping.IsRopedByPawn,
                animal.Downed,
                animal.InMentalState && animal.MentalStateDef != MentalStateDefOf.Roaming,
                LeadPolicy.ShouldRelease(animal.needs?.food?.CurCategory, animal.needs?.rest?.CurCategory) != LeadRelease.None,
                FormingLords.StageOf(animal.GetLord()));
        }

        /// <summary>
        /// The memo that sends a leaving caravan back to collecting its animals.
        /// </summary>
        /// <remarks>
        /// Vanilla's own, which <c>LordJob_FormAndSendCaravan.Notify_PawnAdded</c> sends when a pen
        /// animal joins; <c>CreateGraph</c> moves the leaving stage back to collecting on it.
        /// </remarks>
        public const string RecollectMemo = "AddedRopeAnimal";

        /// <summary>
        /// Sends a leaving caravan back to collect a pen animal that cannot get to the exit, when one of
        /// its colonists would fetch it.
        /// </summary>
        /// <param name="job">The caravan's lord job.</param>
        /// <param name="settings">The mod's settings.</param>
        /// <remarks>
        /// <para>
        /// Asked every 250 ticks per caravan, and does nothing outside the leaving stage
        /// (<see cref="LeadPolicy.WatchesForStrandedAnimals"/>). An animal let go while leaving, by the
        /// drop-lead button or when its holder is undrafted, walks to the exit by itself if it can;
        /// one behind a door it cannot open never would, and vanilla's caravan would wait at the exit
        /// for good.
        /// </para>
        /// <para>
        /// Whether a colonist would fetch it is vanilla's collecting giver's own test,
        /// <c>JobGiver_PrepareCaravan_CollectPawns.DoesAnimalNeedGathering</c>, which reads the
        /// colonist's duty spot; while leaving every member's duty is the exit, which is where
        /// collecting takes the animals too. Collecting waits for every pen animal nobody in the
        /// caravan leads, so the caravan goes back only when each of them would be fetched; otherwise
        /// going back would only move the wait (<see cref="LeadPolicy.SendsBackToCollect"/>).
        /// </para>
        /// </remarks>
        public static void RecollectStrandedAnimals(LordJob_FormAndSendCaravan job, CaravansCriticallyEnhancedSettings settings)
        {
            Lord lord = job.lord;
            if (!LeadPolicy.WatchesForStrandedAnimals(settings.IsOn(Toggle.LeadsSurviveDrafting), FormingLords.StageOf(lord)))
            {
                return;
            }

            List<Pawn> owned = lord.ownedPawns;
            Pawn stranded = null;
            bool everyLooseOneFetchable = true;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn animal = owned[i];

                // Led animals are what collecting would wait for least, and nearly every member is one.
                if (!AnimalPenUtility.NeedsToBeManagedByRope(animal) || !animal.Spawned || GatherAnimalsAndSlavesForCaravanUtility.IsRopedByCaravanPawn(animal))
                {
                    continue;
                }

                if (!AnyColonistWouldFetch(owned, animal))
                {
                    everyLooseOneFetchable = false;
                    break;
                }

                if (stranded == null && LeadPolicy.IsStrandedWhileLeaving(
                        AnimalPenUtility.NeedsToBeManagedByRope(animal),
                        animal.Spawned,
                        GatherAnimalsAndSlavesForCaravanUtility.IsRopedByCaravanPawn(animal),
                        animal.roping.IsRoped,
                        animal.CanReach(job.ExitSpot, PathEndMode.OnCell, PawnUtility.ResolveMaxDanger(animal, Danger.Some))))
                {
                    stranded = animal;
                }
            }

            if (LeadPolicy.SendsBackToCollect(stranded != null, everyLooseOneFetchable))
            {
                CaravanMessages.Post(
                    "CaravansCriticallyEnhanced_LeadRecollecting".Translate(stranded.LabelShort),
                    new LookTargets(stranded),
                    MessageTypeDefOf.NeutralEvent);
                lord.ReceiveMemo(RecollectMemo);
            }
        }

        /// <summary>
        /// Tells the player that undrafting a colonist has dropped its leads, on the rope tick that
        /// drops them.
        /// </summary>
        /// <param name="roper">A roper whose leads the mod is not holding.</param>
        /// <remarks>
        /// The draft setter's finalizer leaves a notice (<see cref="LeadUndraftNotices"/>), because it
        /// cannot tell whether a mental break is undrafting the pawn. Here the break is recorded, and
        /// it gets no message: its own letter says what happened.
        /// </remarks>
        public static void TellUndraftDroppedLeads(Pawn roper)
        {
            if (!LeadUndraftNotices.Take(roper, Find.TickManager.TicksGame)
                || roper.InMentalState
                || roper.Drafted
                || roper.jobs?.curDriver is JobDriver_RopeToDestination)
            {
                return;
            }

            CaravanMessages.Post(
                "CaravansCriticallyEnhanced_LeadsDroppedOnUndraft".Translate(roper.LabelShort),
                new LookTargets(roper),
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// Clears the speed match a roper's current job got from an animal it has just let go.
        /// </summary>
        /// <param name="roper">The roper.</param>
        /// <param name="ropee">The animal let go.</param>
        /// <remarks>
        /// Nothing else would: the rope tick patch leaves at once for a roper holding nothing, and the
        /// field outlives the lead until the job ends, so the colonist would walk on at the animal's
        /// pace. Vanilla's rope driver manages its own; this touches only another driver's.
        /// </remarks>
        public static void ForgetSpeedOf(Pawn roper, Pawn ropee)
        {
            JobDriver driver = roper?.jobs?.curDriver;
            if (driver != null && !(driver is JobDriver_RopeToDestination) && driver.locomotionUrgencySameAs == ropee)
            {
                driver.locomotionUrgencySameAs = null;
            }
        }

        /// <summary>
        /// Lets every drafted member of a caravan being cancelled go of its animals.
        /// </summary>
        /// <param name="lord">The caravan.</param>
        /// <remarks>
        /// Vanilla then has the caravan's undrafted colonists lead its pen animals home, but only animals
        /// nobody holds, and never with a drafted colonist as the roper; held ones would be dropped a
        /// tick later, once the holder has no caravan, wherever they stood.
        /// </remarks>
        public static void LetGoForCancelled(Lord lord)
        {
            List<Pawn> owned = lord.ownedPawns;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i];
                if (pawn.Drafted && pawn.roping != null && pawn.roping.Ropees.Count > 0)
                {
                    pawn.roping.DropRopes();
                }
            }
        }

        private static bool AnyColonistWouldFetch(List<Pawn> owned, Pawn animal)
        {
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn colonist = owned[i];
                if (colonist.IsColonist && colonist.Spawned && !colonist.Drafted && !colonist.Downed
                    && colonist.mindState?.duty != null
                    && JobGiver_PrepareCaravan_CollectPawns.DoesAnimalNeedGathering(colonist, animal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a pawn shows the drop-lead button.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <returns><c>true</c> for a player-controlled colonist whose leads the mod holds.</returns>
        public static bool ShowsDropLead(Pawn pawn)
        {
            return pawn.IsColonistPlayerControlled && KeepsLeads(pawn);
        }
    }
}
