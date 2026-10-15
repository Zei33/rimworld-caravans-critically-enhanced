using System;
using System.Runtime.Serialization;
using NUnit.Framework;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Pins the boundary of what this harness can reach, so a test that quietly stops testing shows up.
    /// </summary>
    [TestFixture]
    public class HarnessTests
    {
        [Test]
        public void RunsAgainstTheRealGameAssembly()
        {
            // If this ever resolves to a stub, every other test in the suite is measuring nothing.
            Assert.That(typeof(ModSettings).Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp"));
        }

        [Test]
        public void TheModsSettingsStaticIsNullOutsideTheGame()
        {
            // The reason decisions take their settings as parameters. Only the game constructs the Mod.
            Assert.That(CaravansCriticallyEnhancedMod.Settings, Is.Null);
        }

        [Test]
        public void TheVanillaLordJobConstructs()
        {
            Assert.That(new LordJob_FormAndSendCaravan(), Is.Not.Null);
        }

        [Test]
        public void NoLordCanBeConstructedHere()
        {
            // Lord's static FlagTex initialiser calls MaterialPool.MatFrom, which reaches
            // UnityData.IsInMainThread and fails with a MissingMethodException inside two
            // TypeInitializationExceptions. FormatterServices.GetUninitializedObject does allocate one,
            // but setting its private fields by reflection and then reading a member ran that same
            // initialiser and failed, when that test ran before anything else had touched the type
            // (measured 2026-10-15, and confirmed by the review the same day). So a usable Lord cannot
            // be had, FormingLords takes a lord's parts for every decision, and its Lord overloads are
            // pinned by IL. Only the constructor is asserted, because it fails whatever ran first.
            Assert.That(() => new Lord(), Throws.InstanceOf<TypeInitializationException>());
        }

        [Test]
        public void ToilsAndFactionsCanBeBuilt()
        {
            // The parts FormingLords decides over. A faction is built uninitialised with its def set,
            // because Faction.IsPlayer reads def.isPlayer and nothing else.
            Assert.That(new LordToil_PrepareCaravan_Pause(), Is.Not.Null);
            Assert.That(new LordToil_PrepareCaravan_Leave(IntVec3.Zero), Is.Not.Null);
            Assert.That(FormatterServices.GetUninitializedObject(typeof(Faction)), Is.InstanceOf<Faction>());
        }
    }
}
