using System;
using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The whole truth table of <see cref="CarrierRule.Allows"/>.
    /// </summary>
    [TestFixture]
    public class CarrierRuleTests
    {
        [Test]
        public void TheWholeTableAgreesWithTheStatedRule()
        {
            // Every combination of the six flags at every stage, against the rule written out longhand
            // here. 13 stages plus Unknown, times 64, is 896 rows; the count is asserted so a stage
            // added to the enum cannot quietly fall outside the walk.
            int rows = 0;
            var disagreements = new List<string>();

            foreach (FormingStage stage in Enum.GetValues(typeof(FormingStage)))
            {
                foreach (bool colonist in new[] { false, true })
                foreach (bool unavailable in new[] { false, true })
                foreach (bool overEncumbered in new[] { false, true })
                foreach (bool ropeAnimal in new[] { false, true })
                foreach (bool tied in new[] { false, true })
                foreach (bool colonistCarriers in new[] { false, true })
                {
                    rows++;
                    bool gatheringAnimals = stage == FormingStage.GatherAnimals || stage == FormingStage.GatherAnimalsPause;
                    bool expected = !unavailable && (colonist
                        ? colonistCarriers && !overEncumbered && !gatheringAnimals
                        : !(ropeAnimal && gatheringAnimals) || tied);

                    bool actual = CarrierRule.Allows(stage, colonist, unavailable, overEncumbered, ropeAnimal, tied, colonistCarriers);
                    if (actual != expected)
                    {
                        disagreements.Add($"{stage} colonist={colonist} unavailable={unavailable} over={overEncumbered} rope={ropeAnimal} tied={tied} setting={colonistCarriers}: {actual}");
                    }
                }
            }

            Assert.That(rows, Is.EqualTo(14 * 64));
            Assert.That(disagreements, Is.Empty);
        }

        [Test]
        public void AColonistWithRoomIsAllowedOnlyWithTheSetting()
        {
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, true, false, false, false, false, colonistCarriers: true), Is.True);
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, true, false, false, false, false, colonistCarriers: false), Is.False);
        }

        [Test]
        public void AnOverloadedColonistIsRefusedAlthoughVanillaWouldAcceptThem()
        {
            // Vanilla's allowColonists branch returns true for any colonist with no weight check.
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, true, false, true, false, false, true), Is.False);
        }

        [Test]
        public void WhileAnimalsGatherColonistsAreBusyAndRopeAnimalsMustBeTiedFirst()
        {
            Assert.That(CarrierRule.Allows(FormingStage.GatherAnimals, true, false, false, false, false, true), Is.False);
            Assert.That(CarrierRule.Allows(FormingStage.GatherAnimals, false, false, false, true, false, true), Is.False);
            Assert.That(CarrierRule.Allows(FormingStage.GatherAnimals, false, false, false, true, true, true), Is.True);
            Assert.That(CarrierRule.Allows(FormingStage.GatherAnimalsPause, false, false, false, true, false, true), Is.False,
                "A pause is still the animal gathering stage.");

            // The control: once items are gathering the same untied rope animal is fine.
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, false, false, false, true, false, true), Is.True);
        }

        [Test]
        public void ADraftedOrBrokenMemberIsNeverACarrierForAHelper()
        {
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, true, true, false, false, false, true), Is.False, "A drafted colonist with room.");
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, false, true, false, false, false, true), Is.False, "A pack animal in a mental state.");

            // The control: the same colonist undrafted.
            Assert.That(CarrierRule.Allows(FormingStage.GatherItems, true, false, false, false, false, true), Is.True);
        }

        [Test]
        public void APackAnimalOrPrisonerIsNeverNarrowed()
        {
            // Vanilla already checks their weight. Not a colonist, available, and not a rope animal: always yes.
            IEnumerable<bool> answers = Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Select(stage => CarrierRule.Allows(stage, false, false, false, false, false, false));

            Assert.That(answers, Is.All.True);
        }
    }
}
