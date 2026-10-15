using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Runs the game's own dev-mode check for a missing <c>[StaticConstructorOnStartup]</c> over the
    /// mod's types.
    /// </summary>
    /// <remarks>
    /// <c>StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes</c> warns, with dev mode on,
    /// about every type lacking the attribute that has a static field whose type, or array element
    /// type, is a <c>Texture</c>, <c>Material</c>, <c>Shader</c>, <c>Graphic</c>, <c>GameObject</c> or
    /// <c>MaterialPropertyBlock</c>. A modder reads that yellow line as a bug report, so the check is
    /// reproduced here, predicate for predicate, from Simple Improve's fixture.
    /// </remarks>
    [TestFixture]
    public class StartupAttributeTests
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void NoModTypeWouldDrawTheMissingAttributeWarning()
        {
            List<string> offenders = ModTypes()
                .Where(t => !t.IsDefined(typeof(StaticConstructorOnStartup), true))
                .Select(t => new { Type = t, Field = t.GetFields(StaticFields).FirstOrDefault(IsAssetField) })
                .Where(x => x.Field != null)
                .Select(x => x.Type.FullName + "." + x.Field.Name)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "These would log \"probably needs a StaticConstructorOnStartup attribute\" in dev mode.");
        }

        [Test]
        public void ThePredicateRecognisesATextureField()
        {
            // The positive control. The mod holds no texture yet at Slice 0, so the predicate is shown
            // to work on a type of this fixture's own. When the first icon holder lands, point this at
            // it, as Simple Improve's control points at ImproveSelection.
            Assert.That(
                typeof(HoldsATexture).GetFields(StaticFields).Where(IsAssetField).Select(f => f.Name),
                Is.EqualTo(new[] { "icon" }));
            Assert.That(ModTypes(), Has.Count.GreaterThan(5), "The scan found too few of the mod's types to mean anything.");
        }

        /// <summary>
        /// The game's own test, from <c>ReportProbablyMissingAttributes</c>.
        /// </summary>
        private static bool IsAssetField(FieldInfo field)
        {
            Type type = field.FieldType;
            if (type.IsArray)
            {
                type = type.GetElementType();
            }

            return typeof(Texture).IsAssignableFrom(type)
                || typeof(Material).IsAssignableFrom(type)
                || typeof(Shader).IsAssignableFrom(type)
                || typeof(Graphic).IsAssignableFrom(type)
                || typeof(GameObject).IsAssignableFrom(type)
                || typeof(MaterialPropertyBlock).IsAssignableFrom(type);
        }

        private static List<Type> ModTypes()
        {
            return typeof(CaravansCriticallyEnhancedMod).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && (t.Namespace == "CaravansCriticallyEnhanced" || t.Namespace.StartsWith("CaravansCriticallyEnhanced.", StringComparison.Ordinal))
                    && !t.Namespace.StartsWith("CaravansCriticallyEnhanced.Tests", StringComparison.Ordinal))
                .ToList();
        }

        /// <summary>
        /// A type with one texture field, for the positive control.
        /// </summary>
        private static class HoldsATexture
        {
#pragma warning disable CS0169, CS0649
            private static Texture2D icon;
#pragma warning restore CS0169, CS0649
        }
    }
}
