using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// When a forming caravan moves its exit away from a threat, as whole tables.
    /// </summary>
    [TestFixture]
    public class ThreatPolicyTests
    {
        [Test]
        public void TheExitMovesOnlyInAWorkingStageNeverInAPause()
        {
            var movable = Enumerable.Range(-1, 14).Select(i => (FormingStage)i).Where(ThreatPolicy.Movable).ToList();
            Assert.That(movable, Is.EqualTo(new[]
            {
                FormingStage.GatherAnimals,
                FormingStage.GatherItems,
                FormingStage.GatherDownedPawns,
                FormingStage.Wait,
                FormingStage.CollectAnimals,
                FormingStage.Leave,
            }));
        }

        [Test]
        public void HalfTheColonistsAtTheExitOrDownedAlreadyAtItCommitsIt()
        {
            Assert.That(ThreatPolicy.Committed(2, 4, FormingStage.GatherItems, 0, 0), Is.True, "Half.");
            Assert.That(ThreatPolicy.Committed(1, 4, FormingStage.GatherItems, 0, 0), Is.False);
            Assert.That(ThreatPolicy.Committed(3, 5, FormingStage.GatherItems, 0, 0), Is.True);
            Assert.That(ThreatPolicy.Committed(2, 5, FormingStage.GatherItems, 0, 0), Is.False);
            Assert.That(ThreatPolicy.Committed(0, 0, FormingStage.GatherItems, 0, 0), Is.False, "Nobody to commit.");
            Assert.That(ThreatPolicy.CommitRadius, Is.EqualTo(15f), "The plan's 15 cells.");

            // Each count over every stage, with nothing else to commit.
            List<FormingStage> stages = Enumerable.Range(-1, 14).Select(i => (FormingStage)i).ToList();
            Assert.That(stages.Where(s => ThreatPolicy.Committed(4, 4, s, 0, 0)), Is.EqualTo(stages.Where(s => s != FormingStage.GatherDownedPawns)),
                "The colonists at the exit, in every stage but gathering downed pawns, whose duty walks the idle there to wait.");
            Assert.That(stages.Where(s => ThreatPolicy.Committed(0, 4, s, 0, 0)), Is.Empty, "Nobody at the exit and nobody downed: never.");
            Assert.That(stages.Where(s => ThreatPolicy.Committed(0, 4, s, 1, 1)), Is.EqualTo(new[] { FormingStage.Wait, FormingStage.CollectAnimals, FormingStage.Leave }),
                "One already at the exit, from waiting on: while waiting a move would send carriers back into the threat for it, and from collecting on it is one to take.");
            Assert.That(stages.Where(s => ThreatPolicy.Committed(0, 4, s, 0, 1)), Is.EqualTo(new[] { FormingStage.Wait }),
                "The count at the exit alone, which the game never gives (a pawn at the exit is one to take): waiting reads it. Before waiting, the downed stage fetches every listed pawn to wherever the exit is, so a move spares the rest the carry into the threat.");
            Assert.That(stages.Where(s => ThreatPolicy.Committed(0, 4, s, 1, 0)), Is.EqualTo(new[] { FormingStage.CollectAnimals, FormingStage.Leave }),
                "None at the exit yet: only once collecting, where going back would send the animals back too. Before, the downed stage, or waiting's go-back, carries them to the new exit.");
        }

        [Test]
        public void TheCooldownRunsAnHourFromTheLastMove()
        {
            Assert.That(ThreatPolicy.Cooling(now: 5000, last: -1), Is.False, "Never moved.");
            Assert.That(ThreatPolicy.Cooling(now: 5000, last: 5000 - ThreatPolicy.CooldownTicks + 1), Is.True);
            Assert.That(ThreatPolicy.Cooling(now: 5000, last: 5000 - ThreatPolicy.CooldownTicks), Is.False, "Exactly an hour on.");
            Assert.That(ThreatPolicy.CooldownTicks, Is.EqualTo(2500));
        }

        [Test]
        public void TheExitMovesOnlyForACurrentThreatToASafeExitWithNothingElseInTheWay()
        {
            var yes = new List<string>();
            foreach (bool featureOn in new[] { false, true })
            {
                foreach (bool foreign in new[] { false, true })
                {
                    foreach (bool playerOverride in new[] { false, true })
                    {
                        foreach (bool holding in new[] { false, true })
                        {
                            foreach (bool safeExitOpen in new[] { false, true })
                            {
                                foreach (bool committed in new[] { false, true })
                                {
                                    foreach (bool cooling in new[] { false, true })
                                    {
                                        if (ThreatPolicy.Moves(featureOn, foreign, playerOverride, holding, FormingStage.GatherItems, 5f, safeExitOpen, committed, cooling))
                                        {
                                            yes.Add($"{featureOn} {foreign} {playerOverride} {holding} {safeExitOpen} {committed} {cooling}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Assert.That(yes, Is.EqualTo(new[] { "True False False False True False False" }), "One row in 128.");

            Assert.That(Move(current: 0f, safeExitOpen: true), Is.False, "Not threatened: the exit stands.");
            Assert.That(Move(current: 5f, safeExitOpen: false), Is.False, "Only to a safe exit: a small gain never moves it.");
            Assert.That(Move(current: 0.1f, safeExitOpen: true), Is.True, "Any threat, if a safe exit is open.");
            Assert.That(Move(current: 5f, safeExitOpen: true), Is.True);
            Assert.That(ThreatPolicy.Moves(true, false, false, false, FormingStage.LeavePause, 5f, true, false, false), Is.False, "Not in a pause.");
        }

        [Test]
        public void TheDistancesAMoveIsRankedByAreHalfAsFarAgain()
        {
            Assert.That(ThreatPolicy.ClearanceFactor, Is.EqualTo(1.5f));
            Assert.That(ThreatPolicy.Clearance(25), Is.EqualTo(38), "Rounded up.");
            Assert.That(ThreatPolicy.Clearance(3), Is.EqualTo(5), "Up, not to the nearest even number.");
            Assert.That(ThreatPolicy.Clearance(12), Is.EqualTo(18));
            Assert.That(ThreatPolicy.Clearance(0), Is.EqualTo(0));
        }

        [Test]
        public void OnlyCollectingAndLeavingNeedTheCaravanRoutedAgain()
        {
            var reroute = Enumerable.Range(-1, 14).Select(i => (FormingStage)i).Where(ThreatPolicy.ReroutesAfterMove).ToList();
            Assert.That(reroute, Is.EqualTo(new[] { FormingStage.CollectAnimals, FormingStage.Leave }));
        }

        private static bool Move(float current, bool safeExitOpen)
        {
            return ThreatPolicy.Moves(true, false, false, false, FormingStage.GatherItems, current, safeExitOpen, false, false);
        }
    }
}
