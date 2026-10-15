using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Which caravan animals a helper may lead to the gathering spot.
    /// </summary>
    [TestFixture]
    public class RopeCandidatesTests
    {
        [Test]
        public void AnUnledPenAnimalInTheGatheringStageIsACandidate()
        {
            Assert.That(RopeCandidates.NeedsLeading(FormingStage.GatherAnimals, true, false, false, true), Is.True);
        }

        [Test]
        public void AnAnimalThatWalksByItselfIsNot()
        {
            // Elephants and dogs follow the caravan without a rope, as in vanilla.
            Assert.That(RopeCandidates.NeedsLeading(FormingStage.GatherAnimals, false, false, false, true), Is.False);
        }

        [Test]
        public void AnAnimalAlreadyOnSomebodysRopeIsNot()
        {
            Assert.That(RopeCandidates.NeedsLeading(FormingStage.GatherAnimals, true, true, false, true), Is.False);
        }

        [Test]
        public void AnAnimalAlreadyTiedAtTheSpotIsNot()
        {
            Assert.That(RopeCandidates.NeedsLeading(FormingStage.GatherAnimals, true, false, true, true), Is.False);
        }

        [Test]
        public void AnAnimalWithoutTheGatherDutyIsNot()
        {
            // The duty names the spot. Without it there is nowhere to lead the animal.
            Assert.That(RopeCandidates.NeedsLeading(FormingStage.GatherAnimals, true, false, false, false), Is.False);
        }

        [Test]
        public void OnlyTheGatheringStageItselfCounts()
        {
            // Every stage, including the gathering pause and Unknown, is asked, and only one says yes.
            List<FormingStage> yes = System.Enum.GetValues(typeof(FormingStage)).Cast<FormingStage>()
                .Where(stage => RopeCandidates.NeedsLeading(stage, true, false, false, true))
                .ToList();

            Assert.That(yes, Is.EqualTo(new[] { FormingStage.GatherAnimals }));
        }

        [Test]
        public void TheWholeTruthTableHasExactlyOneYes()
        {
            int yes = 0;
            foreach (bool needsRope in new[] { false, true })
            {
                foreach (bool onRope in new[] { false, true })
                {
                    foreach (bool tied in new[] { false, true })
                    {
                        foreach (bool duty in new[] { false, true })
                        {
                            if (RopeCandidates.NeedsLeading(FormingStage.GatherAnimals, needsRope, onRope, tied, duty))
                            {
                                yes++;
                                Assert.That(new[] { needsRope, !onRope, !tied, duty }, Is.All.True);
                            }
                        }
                    }
                }
            }

            Assert.That(yes, Is.EqualTo(1));
        }
    }
}
