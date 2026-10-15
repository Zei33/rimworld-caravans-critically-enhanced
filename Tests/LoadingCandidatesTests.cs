using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// The rules for building the Load giver's search set.
    /// </summary>
    [TestFixture]
    public class LoadingCandidatesTests
    {
        private readonly object steel = new object();
        private readonly object meals = new object();
        private readonly object medicine = new object();

        [Test]
        public void EligibleThingsAreAddedInOrderWithTheirOwner()
        {
            var into = new List<object>();
            var owners = new Dictionary<object, string>();

            LoadingCandidates.Collect("first caravan", new[] { steel, meals }, _ => true, into, owners);

            Assert.That(into, Is.EqualTo(new[] { steel, meals }));
            Assert.That(owners[steel], Is.EqualTo("first caravan"));
            Assert.That(owners[meals], Is.EqualTo("first caravan"));
        }

        [Test]
        public void NullsAndIneligibleThingsAreLeftOut()
        {
            var into = new List<object>();
            var owners = new Dictionary<object, string>();

            LoadingCandidates.Collect("caravan", new[] { null, steel, meals }, t => t != meals, into, owners);

            Assert.That(into, Is.EqualTo(new[] { steel }));
            Assert.That(owners.ContainsKey(meals), Is.False);
        }

        [Test]
        public void AThingListedTwiceBelongsToTheFirstListThatNamedIt()
        {
            // The same stack in two forming caravans, and twice in one row's list: HasJobOnThing and
            // JobOnThing must resolve it to the same caravan, so the first claim stands.
            var into = new List<object>();
            var owners = new Dictionary<object, string>();

            LoadingCandidates.Collect("first", new[] { steel, steel }, _ => true, into, owners);
            LoadingCandidates.Collect("second", new[] { steel, medicine }, _ => true, into, owners);

            Assert.That(into, Is.EqualTo(new[] { steel, medicine }));
            Assert.That(owners[steel], Is.EqualTo("first"));
            Assert.That(owners[medicine], Is.EqualTo("second"));
        }

        [Test]
        public void ANullListIsNothing()
        {
            var into = new List<object>();
            LoadingCandidates.Collect<string, object>("caravan", null, _ => true, into, new Dictionary<object, string>());
            Assert.That(into, Is.Empty);
        }

        [Test]
        public void EligibilityIsNotAskedOfAThingAlreadyClaimed()
        {
            // The eligibility test reads the map, so asking it twice for one thing is wasted work.
            var asked = new List<object>();
            var into = new List<object>();
            var owners = new Dictionary<object, string>();

            LoadingCandidates.Collect("first", new[] { steel }, t => { asked.Add(t); return true; }, into, owners);
            LoadingCandidates.Collect("second", new[] { steel }, t => { asked.Add(t); return true; }, into, owners);

            Assert.That(asked, Is.EqualTo(new[] { steel }));
        }
    }
}
