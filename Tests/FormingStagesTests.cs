using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using RimWorld;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Covers the mapping from a toil index to a stage, and pins the vanilla graph it depends on.
    /// </summary>
    [TestFixture]
    public class FormingStagesTests
    {
        /// <summary>
        /// The toil types <c>CreateGraph</c> adds, in the order it adds them, which is the order
        /// <see cref="FormingStage"/> numbers them in.
        /// </summary>
        private static readonly Type[] VanillaToilOrder =
        {
            typeof(LordToil_PrepareCaravan_GatherAnimals),
            typeof(LordToil_PrepareCaravan_Pause),
            typeof(LordToil_PrepareCaravan_GatherItems),
            typeof(LordToil_PrepareCaravan_Pause),
            typeof(LordToil_PrepareCaravan_GatherDownedPawns),
            typeof(LordToil_PrepareCaravan_Pause),
            typeof(LordToil_PrepareCaravan_Wait),
            typeof(LordToil_PrepareCaravan_Pause),
            typeof(LordToil_PrepareCaravan_CollectAnimals),
            typeof(LordToil_PrepareCaravan_Pause),
            typeof(LordToil_PrepareCaravan_Leave),
            typeof(LordToil_PrepareCaravan_Pause),
            typeof(LordToil_End),
        };

        [Test]
        public void TheVanillaGraphAddsItsToilsInTheOrderTheStagesAreNumbered()
        {
            // The whole stage mapping rests on this. Lord.ExposeData saves the current toil as its
            // index in graph.lordToils and restores it by index, so the order is part of the save
            // format, but a reshuffle on a game update would still silently relabel every stage.
            //
            // Reads the real Assembly-CSharp. Each toil is constructed and then handed to AddToil, so
            // the calls that matter alternate: new toil, AddToil, new toil, AddToil. Anything else in
            // the body (the StateGraph, the transitions and their triggers) is filtered out, and the
            // alternation is asserted along with the order, so a toil constructed but never added, or
            // added out of order, fails here rather than slipping past a list of constructors.
            MethodInfo createGraph = typeof(LordJob_FormAndSendCaravan).GetMethod(
                "CreateGraph", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.That(createGraph, Is.Not.Null, "LordJob_FormAndSendCaravan no longer declares CreateGraph.");

            List<MethodBase> relevant = ILCalls.CalledBy(createGraph)
                .Where(m => (m.IsConstructor && typeof(LordToil).IsAssignableFrom(m.DeclaringType))
                            || (m.DeclaringType == typeof(StateGraph) && m.Name == "AddToil"))
                .ToList();

            var constructed = new List<Type>();
            for (int i = 0; i < relevant.Count; i += 2)
            {
                Assert.That(relevant[i].IsConstructor, Is.True,
                    "Call " + i + " should construct a toil, and is " + ILCalls.Describe(relevant[i]) + ".");
                Assert.That(i + 1, Is.LessThan(relevant.Count), "The last toil constructed is never added.");
                Assert.That(relevant[i + 1].Name, Is.EqualTo("AddToil"),
                    "A toil is constructed without being added straight after it.");
                constructed.Add(relevant[i].DeclaringType);
            }

            Assert.That(constructed, Is.EqualTo(VanillaToilOrder));
            Assert.That(constructed.Count, Is.EqualTo(FormingStages.ToilCount));
        }

        [Test]
        public void TheVanillaPlayerLordAddsNoFleeToil()
        {
            // Lord.SetJob appends a panic flee toil after CreateGraph, which would sit at index 13, but
            // only for a non-player faction and only when AddFleeToil is true. This lord job is always
            // the player's, and the override is pinned here as the second guard.
            Assert.That(new LordJob_FormAndSendCaravan().AddFleeToil, Is.False);
        }

        [Test]
        public void EachIndexInTheGraphNamesItsStage()
        {
            var expected = new[]
            {
                FormingStage.GatherAnimals, FormingStage.GatherAnimalsPause,
                FormingStage.GatherItems, FormingStage.GatherItemsPause,
                FormingStage.GatherDownedPawns, FormingStage.GatherDownedPawnsPause,
                FormingStage.Wait, FormingStage.WaitPause,
                FormingStage.CollectAnimals, FormingStage.CollectAnimalsPause,
                FormingStage.Leave, FormingStage.LeavePause,
                FormingStage.End,
            };

            Assert.That(Enumerable.Range(0, FormingStages.ToilCount).Select(FormingStages.FromToilIndex), Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(13)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void AnIndexOutsideTheGraphIsUnknown(int index)
        {
            Assert.That(FormingStages.FromToilIndex(index), Is.EqualTo(FormingStage.Unknown));
        }

        [Test]
        public void EveryPauseFollowsItsWorkingToilAndIsTheOnlyKindCalledAPause()
        {
            // The pause types in the vanilla order are exactly the stages IsPause names, and each
            // pause's working stage is the one before it.
            var pauses = new List<FormingStage>();
            for (int i = 0; i < VanillaToilOrder.Length; i++)
            {
                FormingStage stage = FormingStages.FromToilIndex(i);
                bool isPauseToil = VanillaToilOrder[i] == typeof(LordToil_PrepareCaravan_Pause);

                Assert.That(FormingStages.IsPause(stage), Is.EqualTo(isPauseToil), stage.ToString());

                if (isPauseToil)
                {
                    pauses.Add(stage);
                    Assert.That(FormingStages.WorkingStageOf(stage), Is.EqualTo(FormingStages.FromToilIndex(i - 1)));
                }
                else
                {
                    Assert.That(FormingStages.WorkingStageOf(stage), Is.EqualTo(stage));
                }
            }

            Assert.That(pauses.Count, Is.EqualTo(6));
            Assert.That(FormingStages.IsPause(FormingStage.Unknown), Is.False);
            Assert.That(FormingStages.WorkingStageOf(FormingStage.Unknown), Is.EqualTo(FormingStage.Unknown));
        }

        [Test]
        public void LoadingIsOpenWhileGatheringItemsAndOptionallyWhileGatheringAnimals()
        {
            var openWithoutEarly = new List<FormingStage>();
            var openWithEarly = new List<FormingStage>();

            foreach (FormingStage stage in Enum.GetValues(typeof(FormingStage)))
            {
                if (FormingStages.OpenForLoading(stage, earlyLoading: false))
                {
                    openWithoutEarly.Add(stage);
                }

                if (FormingStages.OpenForLoading(stage, earlyLoading: true))
                {
                    openWithEarly.Add(stage);
                }
            }

            Assert.That(openWithoutEarly, Is.EqualTo(new[] { FormingStage.GatherItems }));
            Assert.That(openWithEarly, Is.EquivalentTo(new[] { FormingStage.GatherAnimals, FormingStage.GatherItems }));
        }

        [Test]
        public void HelpersCarryOnThroughBothGatheringStagesAndTheirPausesOnly()
        {
            // A helper already walking with a stack finishes it through a mental-break pause, and
            // stops the moment the caravan moves on to its downed pawns or anything after.
            var may = new List<FormingStage>();
            foreach (FormingStage stage in Enum.GetValues(typeof(FormingStage)))
            {
                if (FormingStages.HelpersMayContinue(stage))
                {
                    may.Add(stage);
                }
            }

            Assert.That(may, Is.EquivalentTo(new[]
            {
                FormingStage.GatherAnimals,
                FormingStage.GatherAnimalsPause,
                FormingStage.GatherItems,
                FormingStage.GatherItemsPause,
            }));
        }
    }
}
