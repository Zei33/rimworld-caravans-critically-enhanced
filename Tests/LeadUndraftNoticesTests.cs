using System.Collections;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The notices the draft setter leaves for the rope tick that drops an undrafted colonist's leads.
    /// </summary>
    [TestFixture]
    public class LeadUndraftNoticesTests
    {
        [TearDown]
        public void LeaveNoNoticeWaiting()
        {
            Assert.That(Empty(), Is.EqualTo(0), "A test left a notice waiting.");
        }

        /// <summary>
        /// Empties the notices whatever a test left, so one failure does not fail every test after it.
        /// </summary>
        /// <returns>How many were waiting.</returns>
        internal static int Empty()
        {
            int waiting = LeadUndraftNotices.Count;
            foreach (string name in new[] { "Pawns", "Ticks" })
            {
                ((IList)typeof(LeadUndraftNotices).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();
            }

            return waiting;
        }

        [Test]
        public void ANoticeIsTakenOnceByItsOwnPawn()
        {
            var pawn = new Pawn();
            LeadUndraftNotices.Expect(pawn, 100);

            Assert.That(LeadUndraftNotices.Take(new Pawn(), 101), Is.False, "Another pawn's tick.");
            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(1));
            Assert.That(LeadUndraftNotices.Take(pawn, 101), Is.True);
            Assert.That(LeadUndraftNotices.Take(pawn, 101), Is.False, "Told once.");
            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(0));
        }

        [Test]
        public void ANoticeExpiresAfterItsWindowAndOnATickFromBeforeIt()
        {
            var pawn = new Pawn();

            LeadUndraftNotices.Expect(pawn, 100);
            Assert.That(LeadUndraftNotices.Take(pawn, 100 + LeadUndraftNotices.ExpiryTicks), Is.True, "The last tick of the window.");

            LeadUndraftNotices.Expect(pawn, 100);
            Assert.That(LeadUndraftNotices.Take(pawn, 101 + LeadUndraftNotices.ExpiryTicks), Is.False, "One tick late.");
            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(0), "And it is gone.");

            LeadUndraftNotices.Expect(pawn, 100);
            Assert.That(LeadUndraftNotices.Take(pawn, 99), Is.False, "An older save loaded since.");
            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(0));
        }

        [Test]
        public void TakingAnyNoticeClearsEveryExpiredOne()
        {
            // Otherwise a pawn whose rope tick never came would hold its notice for good.
            var stale = new Pawn();
            var fresh = new Pawn();
            LeadUndraftNotices.Expect(stale, 100);
            LeadUndraftNotices.Expect(fresh, 105);
            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(2), "Both still within the window.");

            Assert.That(LeadUndraftNotices.Take(new Pawn(), 101 + LeadUndraftNotices.ExpiryTicks), Is.False);
            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(1), "The stale one is gone and the fresh one stays.");
            Assert.That(LeadUndraftNotices.Take(stale, 101 + LeadUndraftNotices.ExpiryTicks), Is.False);
            Assert.That(LeadUndraftNotices.Take(fresh, 101 + LeadUndraftNotices.ExpiryTicks), Is.True);
        }

        [Test]
        public void UndraftingTheSamePawnAgainLeavesOneNotice()
        {
            var pawn = new Pawn();
            LeadUndraftNotices.Expect(pawn, 100);
            LeadUndraftNotices.Expect(pawn, 102);

            Assert.That(LeadUndraftNotices.Count, Is.EqualTo(1));
            Assert.That(LeadUndraftNotices.Take(pawn, 102 + LeadUndraftNotices.ExpiryTicks), Is.True, "The newer one, timed from its own tick.");
        }
    }
}
