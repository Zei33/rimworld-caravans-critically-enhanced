using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Covers which lords the mod may touch and how it reads where one has got to.
    /// </summary>
    /// <remarks>
    /// No <c>Lord</c> can be built reliably in this harness, because its static initialiser loads a
    /// material and Mono runs it on allocation; <see cref="HarnessTests"/> says what was measured. So the decisions take the lord's parts, and the IL tests at
    /// the end hold the one-line <c>Lord</c> overloads to delegating to them.
    /// </remarks>
    [TestFixture]
    public class FormingLordsTests
    {
        [Test]
        public void OnlyTheExactVanillaLordJobCounts()
        {
            Assert.That(FormingLords.IsVanillaJob(new LordJob_FormAndSendCaravan()), Is.True);
            Assert.That(FormingLords.IsVanillaJob(new SubclassedFormingJob()), Is.False,
                "A subclass, which is what Vehicle Framework and Vanilla Trading Expanded use, must be left alone.");
            Assert.That(FormingLords.IsVanillaJob(new UnrelatedJob()), Is.False);
            Assert.That(FormingLords.IsVanillaJob(null), Is.False);
        }

        [Test]
        public void ALordMustBeThePlayersAndRunTheExactJob()
        {
            Assert.That(FormingLords.IsVanilla(Faction(player: true), new LordJob_FormAndSendCaravan()), Is.True);
            Assert.That(FormingLords.IsVanilla(Faction(player: false), new LordJob_FormAndSendCaravan()), Is.False);
            Assert.That(FormingLords.IsVanilla(Faction(player: true), new SubclassedFormingJob()), Is.False);
            Assert.That(FormingLords.IsVanilla(null, new LordJob_FormAndSendCaravan()), Is.False);
            Assert.That(FormingLords.IsVanilla(Faction(player: true), null), Is.False);
        }

        [Test]
        public void TheStageIsTheCurrentToilsPositionInTheGraph()
        {
            List<LordToil> toils = Enumerable.Range(0, FormingStages.ToilCount)
                .Select(_ => (LordToil)new LordToil_PrepareCaravan_Pause())
                .ToList();

            Assert.That(FormingLords.StageOf(toils, toils[0]), Is.EqualTo(FormingStage.GatherAnimals));
            Assert.That(FormingLords.StageOf(toils, toils[2]), Is.EqualTo(FormingStage.GatherItems));
            Assert.That(FormingLords.StageOf(toils, toils[11]), Is.EqualTo(FormingStage.LeavePause));
            Assert.That(FormingLords.StageOf(toils, toils[12]), Is.EqualTo(FormingStage.End));
        }

        [Test]
        public void AToilTheGraphDoesNotHoldIsUnknownRatherThanStageZero()
        {
            List<LordToil> toils = new List<LordToil> { new LordToil_PrepareCaravan_Pause() };

            Assert.That(FormingLords.StageOf(toils, new LordToil_PrepareCaravan_Pause()), Is.EqualTo(FormingStage.Unknown));
            Assert.That(FormingLords.StageOf(toils, null), Is.EqualTo(FormingStage.Unknown));
        }

        [Test]
        public void TheLordOverloadsDelegateToTheTestedDecisions()
        {
            // The Lord overloads cannot run here, so the IL holds them to the parts that can.
            Assert.That(CallsIn(nameof(FormingLords.IsVanilla), typeof(Lord)),
                Is.EqualTo(new[]
                {
                    "Verse.AI.Group.Lord.get_LordJob",
                    "CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla",
                }));

            Assert.That(CallsIn(nameof(FormingLords.StageOf), typeof(Lord)),
                Is.EqualTo(new[]
                {
                    "CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla",
                    "Verse.AI.Group.Lord.get_Graph",
                    "Verse.AI.Group.Lord.get_Graph",
                    "Verse.AI.Group.Lord.get_CurLordToil",
                    "CaravansCriticallyEnhanced.Core.FormingLords.StageOf",
                }));

            Assert.That(CallsIn(nameof(FormingLords.VanillaJobOf), typeof(Lord)),
                Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla"));

            Assert.That(CallsIn(nameof(FormingLords.VanillaLordOf), typeof(Pawn)),
                Is.EqualTo(new[]
                {
                    "Verse.AI.Group.LordUtility.GetLord",
                    "CaravansCriticallyEnhanced.Core.FormingLords.IsVanilla",
                }));
        }

        [Test]
        public void TheLordOverloadOfIsVanillaCallsThePartsOverload()
        {
            // The name check above cannot tell the two overloads apart, so this checks the signature.
            MethodBase called = ILCalls.CalledBy(typeof(FormingLords).GetMethod(nameof(FormingLords.IsVanilla), new[] { typeof(Lord) }))
                .Single(m => m.Name == nameof(FormingLords.IsVanilla));

            Assert.That(called.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(Faction), typeof(LordJob) }));
        }

        private static List<string> CallsIn(string name, System.Type parameter)
        {
            MethodInfo method = typeof(FormingLords).GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new[] { parameter }, null);
            Assert.That(method, Is.Not.Null, name + "(" + parameter.Name + ") is gone.");
            return ILCalls.CalledBy(method).Select(ILCalls.Describe).ToList();
        }

        private static Faction Faction(bool player)
        {
            var def = (FactionDef)FormatterServices.GetUninitializedObject(typeof(FactionDef));
            def.isPlayer = player;
            var faction = (Faction)FormatterServices.GetUninitializedObject(typeof(Faction));
            faction.def = def;
            return faction;
        }

        private sealed class SubclassedFormingJob : LordJob_FormAndSendCaravan
        {
        }

        private sealed class UnrelatedJob : LordJob
        {
            public override StateGraph CreateGraph()
            {
                return new StateGraph();
            }
        }
    }
}
