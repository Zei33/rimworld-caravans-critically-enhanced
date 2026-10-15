using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The once-per-save copy of the hauling priority into Load.
    /// </summary>
    [TestFixture]
    public class LoadingMigrationTests
    {
        [Test]
        public void MakingRoomMovesEveryLaterValueOnAndZeroesTheSlot()
        {
            // Saved without the mod: Hauling 2, Transport 4, Fishing 1, then 0 of padding. The mod's
            // work type is inserted at index 1, so Transport's 4 was read into it.
            Assert.That(LoadingMigration.MakeRoom(new[] { 2, 4, 1, 0 }, 1), Is.EqualTo(new[] { 2, 0, 4, 1 }));
        }

        [Test]
        public void MakingRoomAtTheLastSlotOnlyZeroesIt()
        {
            // No work type mod after this one: the slot held the padding.
            Assert.That(LoadingMigration.MakeRoom(new[] { 3, 1, 0 }, 2), Is.EqualTo(new[] { 3, 1, 0 }));
            Assert.That(LoadingMigration.MakeRoom(new[] { 3, 1, 7 }, 2), Is.EqualTo(new[] { 3, 1, 0 }));
        }

        [Test]
        public void MakingRoomAtTheFirstSlotMovesEverything()
        {
            Assert.That(LoadingMigration.MakeRoom(new[] { 5, 6, 0 }, 0), Is.EqualTo(new[] { 0, 5, 6 }));
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(99)]
        public void MakingRoomAtASlotOutsideTheValuesChangesNothing(int slot)
        {
            var values = new[] { 1, 2, 3 };
            int[] result = LoadingMigration.MakeRoom(values, slot);

            Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(result, Is.Not.SameAs(values));
        }

        [Test]
        public void MakingRoomNeverChangesItsInput()
        {
            var values = new[] { 2, 4, 1, 0 };
            LoadingMigration.MakeRoom(values, 1);
            Assert.That(values, Is.EqualTo(new[] { 2, 4, 1, 0 }));
        }

        [Test]
        public void TheFirstLoadCopiesTheSourcePriority()
        {
            Assert.That(LoadingMigration.Decide(false, false, false, currentLoading: 0, sourcePriority: 2), Is.EqualTo(2));
        }

        [Test]
        public void TheFirstLoadOverwritesAStaleValueInTheSlot()
        {
            // DefMap is positional, so on the first load the slot can hold another mod's old value.
            Assert.That(LoadingMigration.Decide(false, false, false, currentLoading: 4, sourcePriority: 1), Is.EqualTo(1));
            Assert.That(LoadingMigration.Decide(false, false, false, currentLoading: 4, sourcePriority: 0), Is.EqualTo(0),
                "A pawn who does not haul does not load either, even over a stale non-zero value.");
        }

        [Test]
        public void TheFirstLoadSetsNothingWhenTheValuesAlreadyMatch()
        {
            Assert.That(LoadingMigration.Decide(false, false, false, currentLoading: 3, sourcePriority: 3), Is.Null);
        }

        [Test]
        public void APawnIncapableOfLoadIsNeverGivenAPriority()
        {
            // SetPriority logs a red error for a non-zero priority on a disabled work type.
            Assert.That(LoadingMigration.Decide(false, true, false, 0, 3), Is.Null);
            Assert.That(LoadingMigration.Decide(true, true, true, 0, 3), Is.Null);
        }

        [Test]
        public void AfterTheFirstLoadAColonistsChoiceIsLeftAlone()
        {
            Assert.That(LoadingMigration.Decide(true, false, false, currentLoading: 0, sourcePriority: 3), Is.Null);
            Assert.That(LoadingMigration.Decide(true, false, false, currentLoading: 2, sourcePriority: 3), Is.Null);
        }

        [Test]
        public void AColonyMechAtZeroIsRaisedOnEveryLoad()
        {
            // No screen lets the player set a mech's priority, so a 0 there was never a choice.
            Assert.That(LoadingMigration.Decide(true, false, true, currentLoading: 0, sourcePriority: 3), Is.EqualTo(3));
            Assert.That(LoadingMigration.Decide(true, false, true, currentLoading: 2, sourcePriority: 3), Is.Null, "Only from zero.");
            Assert.That(LoadingMigration.Decide(true, false, true, currentLoading: 0, sourcePriority: 0), Is.Null, "Not when it does not haul.");
        }
    }
}
