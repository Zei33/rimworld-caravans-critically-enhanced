using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Resolves every Harmony patch the way Harmony will, and binds every parameter by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Harmony cannot patch on this runtime, so nothing here can show a patch works. What it can
    /// show is everything Harmony decides by name before patching: which method each
    /// <c>[HarmonyPatch]</c> resolves to, among that type's declared members only, and what each
    /// <c>__instance</c>, <c>__result</c>, <c>___field</c> and argument parameter binds to. All of
    /// those compile when wrong and fail only in game, where <c>PatchAll</c> throws and every patch
    /// class after the broken one goes unapplied, or, for a class that lost its attribute, nothing
    /// happens at all.
    /// </para>
    /// <para>
    /// Adapted from Biosculpter Detox's fixture of the same name. This mod has far more patches than
    /// that one, so the set is found by scanning the namespace and then pinned by name, rather than
    /// listed by hand.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class PatchTargetTests
    {
        private static readonly string[] PatchKinds = { "Prefix", "Postfix", "Transpiler", "Finalizer" };

        private static readonly string[] AuxiliaryNames = { "Prepare", "Cleanup", "TargetMethod", "TargetMethods" };

        /// <summary>
        /// Every patch class the mod has, by name. Add a new one here so the tests below check it.
        /// </summary>
        private static readonly string[] ExpectedPatchClasses =
        {
            "Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan",
            "Patch_CaravanFormingUtility_StopFormingCaravan",
            "Patch_AutoUndrafter_ShouldAutoUndraft",
            "Patch_JobGiver_FollowRoper_TryGiveJob",
            "Patch_Pawn_DraftController_Drafted",
            "Patch_Pawn_GetGizmos",
            "Patch_Pawn_RopeTracker_CreateRope",
            "Patch_Pawn_RopeTracker_DropRope",
            "Patch_Pawn_RopeTracker_DropRopes",
            "Patch_Pawn_RopeTracker_RopingTick",
            "Patch_TransitionAction_EndAllJobs_DoAction",
            "Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier",
            "Patch_JobDriver_PrepareCaravan_GatherItems_MakeNewToils",
            "Patch_LordJob_FormAndSendCaravan_LordJobTick",
            "Patch_Pawn_WorkSettings_ExposeData",
            "Patch_WorkGiver_HelpGatheringItemsForCaravan_NonScanJob",
            "Patch_CaravanFormingUtility_ForceCaravanDepart",
            "Patch_CaravanFormingUtility_StartFormingCaravan",
            "Patch_Dialog_FormCaravan_TryFormAndSendCaravan",
            "Patch_JobGiver_PrepareCaravan_GatherDownedPawns_TryGiveJob",
            "Patch_LordJob_FormAndSendCaravan_ExposeData",
            "Patch_RCellFinder_TryFindClosestEdgeCellTo",
            "Patch_Trigger_NoPawnsVeryTiredAndSleeping_ActivateOn",
            "Patch_LordToil_PrepareCaravan_GatherDownedPawns_CheckAllPawnsArrived",
            "Patch_JobGiver_GotoTravelDestination_CaravanBabyToCarry",
            "Patch_CaravanFormingUtility_GetForceDepartWarningMessage",
            "Patch_JobDriver_Goto_TryExitMap",
            "Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt",
        };

        [Test]
        public void OnlyThePatchClassesCarryAHarmonyAttribute()
        {
            // ApplyPatches hands every class with a Harmony attribute to Harmony, wherever it lives,
            // so one outside the Patches namespace would be applied without the checks below.
            List<string> attributed = typeof(CaravansCriticallyEnhancedMod).Assembly.GetTypes()
                .Where(t => Attribute.IsDefined(t, typeof(HarmonyAttribute), true))
                .Select(t => t.Name)
                .ToList();

            Assert.That(attributed, Is.EquivalentTo(ExpectedPatchClasses));
        }

        [Test]
        public void EveryPatchClassIsOneHarmonyWillApply()
        {
            Type[] classes = PatchClasses();

            Assert.That(classes.Select(t => t.Name), Is.EquivalentTo(ExpectedPatchClasses),
                "The set of patch classes changed. Update the list so the tests below check the new one.");

            foreach (Type patchClass in classes)
            {
                Assert.That(HarmonyMethodExtensions.GetFromType(patchClass), Is.Not.Empty,
                    patchClass.Name + " has no [HarmonyPatch], so PatchAll skips it without a word.");
                Assert.That(PatchMethods(patchClass), Is.Not.Empty,
                    patchClass.Name + " declares no Prefix, Postfix, Transpiler or Finalizer.");

                foreach (MethodInfo method in DeclaredStaticMethods(patchClass).Where(m => m.IsPublic))
                {
                    Assert.That(PatchKinds.Concat(AuxiliaryNames), Does.Contain(method.Name),
                        patchClass.Name + "." + method.Name + " is public but not a name Harmony recognises. "
                        + "Make helpers private so a misspelt patch method cannot hide among them.");
                }
            }
        }

        [Test]
        public void EveryPatchClassTargetsADifferentMethod()
        {
            // The house rule: one patch class per vanilla target. Two classes on one method is how two
            // features end up ordering against each other by accident.
            var targets = PatchClasses().Select(c => Describe(Resolve(c))).ToList();

            Assert.That(targets.Distinct().Count(), Is.EqualTo(targets.Count),
                "Two patch classes resolve to the same method: " + string.Join(", ", targets.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key)));
        }

        [Test]
        public void EveryPatchBindsTheWayHarmonyWill()
        {
            var kinds = new HashSet<string>();

            foreach (Type patchClass in PatchClasses())
            {
                HarmonyMethod container = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
                var stateTypes = new HashSet<Type>();

                foreach (MethodInfo patch in PatchMethods(patchClass))
                {
                    HarmonyMethod info = container.Merge(HarmonyMethodExtensions.GetMergedFromMethod(patch));
                    string where = patchClass.Name + "." + patch.Name;
                    MethodType methodType = info.methodType ?? MethodType.Normal;

                    Assert.That(methodType, Is.EqualTo(MethodType.Normal).Or.EqualTo(MethodType.Setter),
                        where + " targets a getter, constructor or enumerator, which this test does not resolve yet.");

                    // A setter is resolved as Harmony resolves it: the declared property's set method,
                    // whose one argument is named value.
                    MethodInfo target = methodType == MethodType.Setter
                        ? AccessTools.DeclaredProperty(info.declaringType, info.methodName)?.GetSetMethod(true)
                        : AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);

                    Assert.That(target, Is.Not.Null,
                        where + " names " + info.declaringType?.Name + "." + info.methodName
                        + ", which that type does not declare with those argument types, so PatchAll throws.");

                    foreach (ParameterInfo parameter in patch.GetParameters())
                    {
                        if (parameter.Name == "__state")
                        {
                            // Shared between the class's patch methods: one type, written by the prefix.
                            Type stateType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
                            stateTypes.Add(stateType);
                            Assert.That(patch.Name != "Prefix" || parameter.IsOut, Is.True, where + ": a prefix must write __state with out.");
                            kinds.Add("__state");
                            continue;
                        }

                        kinds.Add(AssertBinds(where, target, parameter));
                    }
                }

                Assert.That(stateTypes.Count, Is.LessThanOrEqualTo(1), patchClass.Name + " uses __state with different types.");
            }

            Assert.That(kinds, Is.EquivalentTo(new[] { "__instance", "__result", "___field", "argument", "__state" }),
                "The patches no longer use every kind of parameter this test checks, or one kind was never reached.");
        }

        [Test]
        public void TheCompletionGuardRunsAfterEveryOtherPostfix()
        {
            // It owns the answer, so it has to see every other mod's version of it.
            MethodInfo postfix = typeof(Patches.Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan).GetMethod("Postfix");
            HarmonyMethod info = HarmonyMethodExtensions.GetMergedFromMethod(postfix);

            Assert.That(info.priority, Is.EqualTo(Priority.Last));
        }

        [Test]
        public void ThePrivateFieldsTheMigrationInjectsStillExist()
        {
            FieldInfo pawn = typeof(Pawn_WorkSettings).GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FieldInfo priorities = typeof(Pawn_WorkSettings).GetField("priorities", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(pawn, Is.Not.Null, "Pawn_WorkSettings.pawn was renamed, so ___pawn will not bind.");
            Assert.That(pawn.FieldType, Is.EqualTo(typeof(Pawn)));
            Assert.That(priorities, Is.Not.Null, "Pawn_WorkSettings.priorities was renamed, so ___priorities will not bind.");
            Assert.That(priorities.FieldType, Is.EqualTo(typeof(DefMap<WorkTypeDef, int>)));
        }

        [Test]
        public void TheMakeNewToilsTargetIsNotAnIterator()
        {
            // The postfix adds a fail condition and relies on running once, before the toils. On an
            // iterator method a postfix would run when the enumerable is created, which is the same
            // moment here only because the method returns another method's iterator.
            MethodInfo target = typeof(JobDriver_PrepareCaravan_GatherItems).GetMethod(
                "MakeNewToils", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            Assert.That(target, Is.Not.Null);
            Assert.That(target.IsDefined(typeof(System.Runtime.CompilerServices.IteratorStateMachineAttribute), false), Is.False);
        }

        private static MethodInfo Resolve(Type patchClass)
        {
            HarmonyMethod container = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
            return AccessTools.DeclaredMethod(container.declaringType, container.methodName, container.argumentTypes);
        }

        private static string Describe(MethodInfo method)
        {
            return method == null ? "(unresolved)" : method.DeclaringType.FullName + "." + method.Name;
        }

        private static Type[] PatchClasses()
        {
            return typeof(CaravansCriticallyEnhancedMod).Assembly.GetTypes()
                .Where(t => t.Namespace == "CaravansCriticallyEnhanced.Patches" && t.IsClass && !t.Name.StartsWith("<", StringComparison.Ordinal)
                            && t.DeclaringType == null)
                .ToArray();
        }

        private static IEnumerable<MethodInfo> DeclaredStaticMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.Name.StartsWith("<", StringComparison.Ordinal));
        }

        private static List<MethodInfo> PatchMethods(Type type)
        {
            return DeclaredStaticMethods(type).Where(m => PatchKinds.Contains(m.Name)).ToList();
        }

        private static string AssertBinds(string where, MethodInfo target, ParameterInfo parameter)
        {
            string name = parameter.Name;
            string what = where + " parameter " + name;

            if (name == "__instance")
            {
                Assert.That(target.IsStatic, Is.False, what + ": the target is static and has no instance.");
                Assert.That(parameter.ParameterType.IsAssignableFrom(target.DeclaringType), Is.True,
                    what + " cannot hold the " + target.DeclaringType.Name + " Harmony passes.");
                return "__instance";
            }

            if (name == "__result")
            {
                Assert.That(target.ReturnType, Is.Not.EqualTo(typeof(void)), what + ": the target returns nothing.");
                AssertHolds(what, parameter.ParameterType, target.ReturnType);
                return "__result";
            }

            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                FieldInfo field = AccessTools.Field(target.DeclaringType, name.Substring(3));
                Assert.That(field, Is.Not.Null,
                    what + " injects a field " + target.DeclaringType.Name + " does not have, so PatchAll throws.");
                AssertHolds(what, parameter.ParameterType, field.FieldType);
                return "___field";
            }

            Assert.That(name.StartsWith("__", StringComparison.Ordinal), Is.False,
                what + " is a Harmony special name this test does not check. Extend it first.");

            ParameterInfo argument = target.GetParameters().FirstOrDefault(p => p.Name == name);
            Assert.That(argument, Is.Not.Null,
                what + " names no parameter of " + target.Name + ", so PatchAll throws \"Parameter not found\".");
            AssertHolds(what, parameter.ParameterType, argument.ParameterType);
            return "argument";
        }

        private static void AssertHolds(string what, Type parameterType, Type valueType)
        {
            Type value = valueType.IsByRef ? valueType.GetElementType() : valueType;

            if (parameterType.IsByRef)
            {
                Assert.That(parameterType.GetElementType(), Is.EqualTo(value),
                    what + " is a ref to a different type from the " + value.Name + " it is bound to.");
            }
            else
            {
                Assert.That(parameterType.IsAssignableFrom(value), Is.True,
                    what + " cannot hold the " + value.Name + " it is bound to.");
            }
        }
    }
}
