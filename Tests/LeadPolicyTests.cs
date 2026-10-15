using System;
using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using RimWorld;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The rules for keeping, releasing, dropping and taking a lead while drafted.
    /// </summary>
    [TestFixture]
    public class LeadPolicyTests
    {
        [Test]
        public void OnlyADraftedCaravanColonistLeadingAnimalsIsHeld()
        {
            // The whole truth table has one yes.
            int yes = 0;
            foreach (bool on in new[] { false, true })
            foreach (bool drafted in new[] { false, true })
            foreach (bool caravan in new[] { false, true })
            foreach (bool roping in new[] { false, true })
            foreach (bool roped in new[] { false, true })
            {
                bool keeps = LeadPolicy.KeepsLeads(on, drafted, caravan, roping, roped);
                Assert.That(keeps, Is.EqualTo(on && drafted && caravan && roping && !roped), $"{on} {drafted} {caravan} {roping} {roped}");
                yes += keeps ? 1 : 0;
            }

            Assert.That(yes, Is.EqualTo(1));
        }

        [Test]
        public void EachOfVanillasOtherBreakingConditionsStillBreaks()
        {
            Assert.That(LeadPolicy.IsFatal(false, false, false, false, false), Is.False, "The all-clear control.");
            Assert.That(LeadPolicy.IsFatal(true, false, false, false, false), Is.True, "Dead.");
            Assert.That(LeadPolicy.IsFatal(false, true, false, false, false), Is.True, "Downed.");
            Assert.That(LeadPolicy.IsFatal(false, false, true, false, false), Is.True, "Asleep, which vanilla reaches through its rope-job check.");
            Assert.That(LeadPolicy.IsFatal(false, false, false, true, false), Is.True, "Broken; roaming is an animal's state, so for a colonist any break.");
            Assert.That(LeadPolicy.IsFatal(false, false, false, false, true), Is.True, "Burning.");
        }

        [Test]
        public void AnAnimalIsLetGoOnlyWhenUrgentlyHungryOrExhausted()
        {
            var released = new List<string>();
            foreach (HungerCategory hunger in Enum.GetValues(typeof(HungerCategory)))
            {
                foreach (RestCategory rest in Enum.GetValues(typeof(RestCategory)))
                {
                    LeadRelease release = LeadPolicy.ShouldRelease(hunger, rest);
                    LeadRelease expected = hunger >= HungerCategory.UrgentlyHungry ? LeadRelease.Hungry
                        : rest == RestCategory.Exhausted ? LeadRelease.Exhausted
                        : LeadRelease.None;
                    Assert.That(release, Is.EqualTo(expected), $"{hunger} {rest}");
                }
            }

            Assert.That(LeadPolicy.ShouldRelease(null, null), Is.EqualTo(LeadRelease.None), "No needs at all, a mech or a dryad.");
            Assert.That(LeadPolicy.ShouldRelease(null, RestCategory.Exhausted), Is.EqualTo(LeadRelease.Exhausted));
            Assert.That(LeadPolicy.ShouldRelease(HungerCategory.Starving, null), Is.EqualTo(LeadRelease.Hungry));
        }

        [Test]
        public void TheAutoUndraftIsSuppressedOnlyForALeadHolderInACaravan()
        {
            Assert.That(LeadPolicy.SuppressAutoUndraft(true, 2, true, FormingStage.GatherItems), Is.True);
            Assert.That(LeadPolicy.SuppressAutoUndraft(false, 2, true, FormingStage.GatherItems), Is.False, "Setting off.");
            Assert.That(LeadPolicy.SuppressAutoUndraft(true, 0, true, FormingStage.GatherItems), Is.False, "Holding nothing: the safety net stays.");
            Assert.That(LeadPolicy.SuppressAutoUndraft(true, 2, false, FormingStage.GatherItems), Is.False, "Not in a forming caravan: the safety net stays.");
        }

        [Test]
        public void TheAutoUndraftStillRescuesTheCaravanWhereAHolderWouldStallIt()
        {
            // Gathering and collecting resume leading on undraft, and leaving waits for every member,
            // so in those stages a forgotten holder is undrafted as in vanilla.
            List<FormingStage> suppressed = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(stage => LeadPolicy.SuppressAutoUndraft(true, 1, true, stage))
                .ToList();

            Assert.That(suppressed, Is.EquivalentTo(new[]
            {
                FormingStage.GatherAnimalsPause,
                FormingStage.GatherItems,
                FormingStage.GatherItemsPause,
                FormingStage.GatherDownedPawns,
                FormingStage.GatherDownedPawnsPause,
                FormingStage.Wait,
                FormingStage.WaitPause,
                FormingStage.CollectAnimalsPause,
                FormingStage.End,
                FormingStage.Unknown,
            }));
        }

        [Test]
        public void NeedyAnimalsAreNotLetGoWhileLeaving()
        {
            List<FormingStage> kept = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(stage => !LeadPolicy.ReleasesNeedyAnimals(stage))
                .ToList();

            Assert.That(kept, Is.EquivalentTo(new[] { FormingStage.Leave, FormingStage.LeavePause }));
        }

        [Test]
        public void OnlyARopeAnimalThatCouldKeepTheLeadCanBeTaken()
        {
            // One yes in the whole table: an animal that walks by itself would be tied at the spot
            // for good, and the rest would be let go again at once.
            int yes = 0;
            foreach (bool needsRope in new[] { false, true })
            foreach (bool roped in new[] { false, true })
            foreach (bool downed in new[] { false, true })
            foreach (bool broken in new[] { false, true })
            foreach (bool needy in new[] { false, true })
            {
                bool can = LeadPolicy.CanBeTaken(needsRope, roped, downed, broken, needy, FormingStage.GatherItems);
                Assert.That(can, Is.EqualTo(needsRope && !roped && !downed && !broken && !needy));
                yes += can ? 1 : 0;
            }

            Assert.That(yes, Is.EqualTo(1));
        }

        [Test]
        public void ANeedyAnimalCanBeTakenOnlyWhileLeaving()
        {
            // While leaving nothing is let go, and a shut-in animal is the one likely to be starving.
            List<FormingStage> needyTakeable = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(stage => LeadPolicy.CanBeTaken(true, false, false, false, true, stage))
                .ToList();

            Assert.That(needyTakeable, Is.EquivalentTo(new[] { FormingStage.Leave, FormingStage.LeavePause }));
            Assert.That(LeadPolicy.CanBeTaken(false, false, false, false, true, FormingStage.Leave), Is.False, "Still never one that walks by itself.");
        }

        [Test]
        public void ALeavingCaravanGoesBackOnlyWhenCollectingCouldFinish()
        {
            Assert.That(LeadPolicy.SendsBackToCollect(true, true), Is.True);
            Assert.That(LeadPolicy.SendsBackToCollect(true, false), Is.False, "Another loose animal nobody would fetch: collecting would wait for it for good.");
            Assert.That(LeadPolicy.SendsBackToCollect(false, true), Is.False, "Nothing stranded: the loose ones walk out.");
            Assert.That(LeadPolicy.SendsBackToCollect(false, false), Is.False);
        }

        [Test]
        public void OnlyALeavingCaravanWatchesForStrandedAnimals()
        {
            List<FormingStage> watched = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(stage => LeadPolicy.WatchesForStrandedAnimals(true, stage))
                .ToList();

            Assert.That(watched, Is.EqualTo(new[] { FormingStage.Leave }), "The pause while leaving goes back to collecting by itself.");
            Assert.That(LeadPolicy.WatchesForStrandedAnimals(false, FormingStage.Leave), Is.False, "Setting off.");
        }

        [Test]
        public void APenAnimalIsStrandedWhenNobodyInTheCaravanLeadsItAndItCannotWalkOut()
        {
            Assert.That(LeadPolicy.IsStrandedWhileLeaving(true, true, false, false, false), Is.True, "Shut in, loose.");
            Assert.That(LeadPolicy.IsStrandedWhileLeaving(true, true, false, true, true), Is.True, "Tied at a spot, or held by an outsider.");
            Assert.That(LeadPolicy.IsStrandedWhileLeaving(true, true, false, false, true), Is.False, "Loose in the open: the leaving duty walks it out.");
            Assert.That(LeadPolicy.IsStrandedWhileLeaving(true, true, true, false, false), Is.False, "Led by a member.");
            Assert.That(LeadPolicy.IsStrandedWhileLeaving(false, true, false, false, false), Is.False, "Walks by itself, so vanilla never ropes it.");
            Assert.That(LeadPolicy.IsStrandedWhileLeaving(true, false, false, false, false), Is.False, "Not on the map.");
        }

        [Test]
        public void VanillaResumesLeadingOnlyInTheGatherAndCollectStages()
        {
            List<FormingStage> resumes = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(LeadPolicy.VanillaResumesOnUndraft)
                .ToList();

            Assert.That(resumes, Is.EquivalentTo(new[] { FormingStage.GatherAnimals, FormingStage.CollectAnimals }),
                "Only those duties have a rope giver; their pauses do not.");
        }

        [TestCase(0, DropLeadAction.None)]
        [TestCase(-1, DropLeadAction.None)]
        [TestCase(1, DropLeadAction.DropDirectly)]
        [TestCase(2, DropLeadAction.Menu)]
        [TestCase(12, DropLeadAction.Menu)]
        public void TheDropButtonDropsALoneAnimalAndOtherwiseOffersAMenu(int held, DropLeadAction expected)
        {
            Assert.That(LeadPolicy.DropLead(held), Is.EqualTo(expected));
        }

        [Test]
        public void TakingALeadNeedsTheCaravanThenACellThenTheReservationThenThePath()
        {
            Assert.That(LeadPolicy.TakeLead(true, true, true, true), Is.EqualTo(TakeLeadBlocker.None), "The all-clear control.");
            Assert.That(LeadPolicy.TakeLead(false, false, false, false), Is.EqualTo(TakeLeadBlocker.NotInCaravan));
            Assert.That(LeadPolicy.TakeLead(true, false, false, false), Is.EqualTo(TakeLeadBlocker.NoRopeCell));
            Assert.That(LeadPolicy.TakeLead(true, true, false, false), Is.EqualTo(TakeLeadBlocker.Reserved));
            Assert.That(LeadPolicy.TakeLead(true, true, true, false), Is.EqualTo(TakeLeadBlocker.Unreachable));
        }
    }
}
