using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaravansCriticallyEnhanced.Utils;
using NUnit.Framework;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds every message the mod posts to the one helper that keeps it out of the History tab.
    /// </summary>
    /// <remarks>
    /// What this pins is which methods call <c>Messages.Message</c>, and that the one that does hands
    /// it a literal <c>false</c> last. The IL walk sees the instruction before each call, and for a
    /// constant last argument that instruction is the constant, so <c>historical: true</c> fails here.
    /// </remarks>
    [TestFixture]
    public class CaravanMessagesTests
    {
        [Test]
        public void OnlyTheMessageHelperPostsMessages()
        {
            List<string> callers = ILCalls.CalledAnywhereInTheMod(typeof(Messages), "Message")
                .Select(ILCalls.Describe)
                .ToList();

            // The positive control is the helper itself, which the same scan has to find.
            Assert.That(callers, Is.EqualTo(new[] { "CaravansCriticallyEnhanced.Utils.CaravanMessages.Post" }));
        }

        [Test]
        public void TheHelperPassesALiteralFalseForHistorical()
        {
            WalkResult walk = ILCalls.Read(typeof(CaravanMessages).GetMethod(nameof(CaravanMessages.Post)));
            int index = walk.Calls.ToList().FindIndex(m => m.DeclaringType == typeof(Messages));

            Assert.That(index, Is.GreaterThanOrEqualTo(0));

            ParameterInfo[] parameters = walk.Calls[index].GetParameters();
            Assert.That(parameters.Last().Name, Is.EqualTo("historical"), "The overload's last parameter is not historical.");
            Assert.That(walk.OpcodeBeforeCall[index], Is.EqualTo(ILCalls.LoadZero),
                "Messages.Message is not handed a literal false for historical, so messages fill the History tab.");
        }

        [Test]
        public void TheThrottledFormGoesThroughTheRepeatAvoiderAndThenThePlainOne()
        {
            List<string> calls = ILCalls.CalledBy(typeof(CaravanMessages).GetMethod(nameof(CaravanMessages.PostThrottled)))
                .Select(ILCalls.Describe)
                .ToList();

            Assert.That(calls, Is.EqualTo(new[]
            {
                "Verse.MessagesRepeatAvoider.MessageShowAllowed",
                "CaravansCriticallyEnhanced.Utils.CaravanMessages.Post",
            }));
        }
    }
}
