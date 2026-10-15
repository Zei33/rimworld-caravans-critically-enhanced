using System.Collections.Generic;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The scope that keeps a pawn's ropes while it is drafted.
    /// </summary>
    [TestFixture]
    public class LeadKeepScopeTests
    {
        [TearDown]
        public void LeaveNoScopeOpen()
        {
            Assert.That(Empty(), Is.EqualTo(0), "A test left a scope open.");
        }

        /// <summary>
        /// Empties the scope whatever a test left in it, so one failure does not fail every test after it.
        /// </summary>
        /// <returns>How many scopes were open.</returns>
        internal static int Empty()
        {
            var kept = (List<Pawn>)typeof(LeadKeepScope).GetField("Kept", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            int open = kept.Count;
            kept.Clear();
            return open;
        }

        [Test]
        public void APawnIsInsideOnlyBetweenItsPushAndPop()
        {
            var pawn = new Pawn();
            Assert.That(LeadKeepScope.Contains(pawn), Is.False);

            LeadKeepScope.Push(pawn);
            Assert.That(LeadKeepScope.Contains(pawn), Is.True);

            Assert.That(LeadKeepScope.Pop(pawn), Is.True);
            Assert.That(LeadKeepScope.Contains(pawn), Is.False);
        }

        [Test]
        public void ScopesNestAndAnotherPawnsScopeIsNotThisOnes()
        {
            // Another mod drafting a second pawn inside the setter must not end the first's scope.
            var first = new Pawn();
            var second = new Pawn();

            LeadKeepScope.Push(first);
            LeadKeepScope.Push(second);
            Assert.That(LeadKeepScope.Pop(second), Is.True);
            Assert.That(LeadKeepScope.Contains(first), Is.True);
            Assert.That(LeadKeepScope.Contains(second), Is.False);
            Assert.That(LeadKeepScope.Pop(first), Is.True);
        }

        [Test]
        public void TheSamePawnTwiceNeedsTwoPops()
        {
            var pawn = new Pawn();
            LeadKeepScope.Push(pawn);
            LeadKeepScope.Push(pawn);

            Assert.That(LeadKeepScope.Pop(pawn), Is.True);
            Assert.That(LeadKeepScope.Contains(pawn), Is.True);
            Assert.That(LeadKeepScope.Pop(pawn), Is.True);
            Assert.That(LeadKeepScope.Contains(pawn), Is.False);
        }

        [Test]
        public void PoppingAPawnThatWasNeverPushedChangesNothing()
        {
            var pushed = new Pawn();
            LeadKeepScope.Push(pushed);

            Assert.That(LeadKeepScope.Pop(new Pawn()), Is.False);
            Assert.That(LeadKeepScope.Contains(pushed), Is.True);
            LeadKeepScope.Pop(pushed);
        }
    }
}
