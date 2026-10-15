using System;
using System.Reflection;
using CaravansCriticallyEnhanced.Utils;
using NUnit.Framework;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Pins every private field name <see cref="CaravanLordAccess"/> reaches into, on the type it
    /// reaches into, and checks the accessors read the field they name.
    /// </summary>
    /// <remarks>
    /// A renamed field fails <c>FieldRefAccess</c> at type initialisation, which in game is the first
    /// time any caravan code touches the class, with a <c>TypeInitializationException</c> every time
    /// after. These turn that into a failing test on the day the game updates.
    /// </remarks>
    [TestFixture]
    public class CaravanLordAccessTests
    {
        [TestCase(typeof(LordJob_FormAndSendCaravan), CaravanLordAccess.ExitSpotField)]
        [TestCase(typeof(LordJob_FormAndSendCaravan), CaravanLordAccess.MeetingPointField)]
        [TestCase(typeof(LordToil_PrepareCaravan_Leave), CaravanLordAccess.ExitSpotField)]
        [TestCase(typeof(LordToil_PrepareCaravan_GatherDownedPawns), CaravanLordAccess.ExitSpotField)]
        [TestCase(typeof(LordToil_PrepareCaravan_RopeAnimals), CaravanLordAccess.DestinationPointField)]
        public void EachNamedFieldIsAnInstanceCellDeclaredOnThatType(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.That(field, Is.Not.Null, type.Name + " no longer declares " + name + ".");
            Assert.That(field.FieldType, Is.EqualTo(typeof(IntVec3)));
        }

        [Test]
        public void TheLordJobAccessorsReadTheCellsTheConstructorWrote()
        {
            // Proves the accessor delegates work on this runtime and point at the right field, not
            // merely that a field of that name exists. The public ExitSpot getter is the cross-check.
            var meeting = new IntVec3(10, 0, 20);
            var exit = new IntVec3(1, 0, 99);
            var job = new LordJob_FormAndSendCaravan(null, null, meeting, exit, PlanetTile.Invalid, PlanetTile.Invalid);

            Assert.That(CaravanLordAccess.JobMeetingPoint(job), Is.EqualTo(meeting));
            Assert.That(CaravanLordAccess.JobExitSpot(job), Is.EqualTo(exit));
            Assert.That(job.ExitSpot, Is.EqualTo(exit));

            var moved = new IntVec3(5, 0, 0);
            CaravanLordAccess.JobExitSpot(job) = moved;
            Assert.That(job.ExitSpot, Is.EqualTo(moved), "Writing through the accessor does not reach the field the getter reads.");
            Assert.That(CaravanLordAccess.JobMeetingPoint(job), Is.EqualTo(meeting), "Writing the exit touched the meeting point.");
        }

        [Test]
        public void TheToilAccessorsReadTheCellsTheConstructorsWroteAndWriteThemBack()
        {
            var exit = new IntVec3(3, 0, 4);
            var meeting = new IntVec3(7, 0, 8);
            var moved = new IntVec3(9, 0, 1);

            var leave = new LordToil_PrepareCaravan_Leave(exit);
            var downed = new LordToil_PrepareCaravan_GatherDownedPawns(meeting, exit);
            var collect = new LordToil_PrepareCaravan_CollectAnimals(exit);

            Assert.That(CaravanLordAccess.LeaveExitSpot(leave), Is.EqualTo(exit));
            Assert.That(CaravanLordAccess.GatherDownedPawnsExitSpot(downed), Is.EqualTo(exit));
            Assert.That(CaravanLordAccess.RopeDestinationPoint(collect), Is.EqualTo(exit));
            Assert.That(CaravanLordAccess.RopeDestinationPoint(new LordToil_PrepareCaravan_GatherAnimals(meeting)), Is.EqualTo(meeting));

            // Written through the accessor, read back by plain reflection on the named field, so a
            // delegate pointed at the wrong field of the right type cannot pass both halves.
            CaravanLordAccess.LeaveExitSpot(leave) = moved;
            CaravanLordAccess.GatherDownedPawnsExitSpot(downed) = moved;
            CaravanLordAccess.RopeDestinationPoint(collect) = moved;

            Assert.That(Read(leave, typeof(LordToil_PrepareCaravan_Leave), CaravanLordAccess.ExitSpotField), Is.EqualTo(moved));
            Assert.That(Read(downed, typeof(LordToil_PrepareCaravan_GatherDownedPawns), CaravanLordAccess.ExitSpotField), Is.EqualTo(moved));
            Assert.That(Read(downed, typeof(LordToil_PrepareCaravan_GatherDownedPawns), CaravanLordAccess.MeetingPointField), Is.EqualTo(meeting),
                "Writing the downed toil's exit touched its meeting point.");
            Assert.That(Read(collect, typeof(LordToil_PrepareCaravan_RopeAnimals), CaravanLordAccess.DestinationPointField), Is.EqualTo(moved));
        }

        [Test]
        public void EveryExitAccessorResolves()
        {
            Assert.That(CaravanLordAccess.AllExitFieldsAvailable, Is.True);
            Assert.That(CaravanLordAccess.JobMeetingPoint, Is.Not.Null);
        }

        private static object Read(object target, Type declaring, string field)
        {
            return declaring.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
    }
}
