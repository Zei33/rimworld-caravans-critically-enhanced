using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using RimWorld;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds every def name this mod writes, in XML and in C#, to a def of the right type.
    /// </summary>
    /// <remarks>
    /// Adapted from Great Escape's fixture of the same name. A mistyped defName is not caught by the
    /// build: the game logs it at startup and leaves a field null. Typed, because several names exist
    /// as more than one def type and an untyped check reports a broken reference as healthy.
    /// </remarks>
    [TestFixture]
    public class DefReferenceTests
    {
        /// <summary>
        /// Elements in the mod's defs whose text is a defName, and the def type it must be.
        /// </summary>
        private static readonly Dictionary<string, string> ReferenceElements = new Dictionary<string, string>
        {
            { "workType", "WorkTypeDef" },
        };

        /// <summary>
        /// Lists of defs, by list element, and the def type of each item.
        /// </summary>
        private static readonly Dictionary<string, string> ReferenceLists = new Dictionary<string, string>
        {
            { "requiredCapacities", "PawnCapacityDef" },
        };

        [Test]
        public void EveryDefReferenceResolvesToADefOfTheRightType()
        {
            List<(string Type, string Name, string Where)> references = CollectReferences().ToList();

            // The count is the positive control. Update it when a reference is added on purpose.
            Assert.That(references.Count, Is.EqualTo(4), "Def references found in 1.6/Defs.");

            List<string> unresolved = references
                .Where(r => !DefXml.DefNamesOfType(r.Type).Contains(r.Name))
                .Select(r => $"{r.Type} '{r.Name}' in {r.Where}")
                .ToList();

            Assert.That(unresolved, Is.Empty);
        }

        [Test]
        public void EveryDefOfFieldNamesADefOfItsType()
        {
            // Reading FieldType and Name does not run the DefOf static constructor, which the harness
            // could not survive.
            List<FieldInfo> fields = typeof(FormingStages).Assembly.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(DefOf), inherit: false).Any())
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
                .ToList();

            Assert.That(fields.Count, Is.EqualTo(3), "Fields on the mod's [DefOf] classes.");

            List<string> unbound = fields
                .Where(f => !DefXml.DefNamesOfType(f.FieldType.Name).Contains(f.Name))
                .Select(f => $"{f.DeclaringType.Name}.{f.Name} ({f.FieldType.Name})")
                .ToList();

            Assert.That(unbound, Is.Empty);
        }

        [Test]
        public void EveryDefTheModAddsCarriesThePrefix()
        {
            // Defs share one namespace per type across every mod a player runs.
            List<string> names = DefXml.ModDefNodes
                .Select(n => n.Element("defName")?.Value)
                .Where(n => n != null)
                .ToList();

            Assert.That(names, Is.Not.Empty);
            Assert.That(names.Where(n => !n.StartsWith("CCE_", StringComparison.Ordinal)), Is.Empty);
        }

        private static IEnumerable<(string Type, string Name, string Where)> CollectReferences()
        {
            foreach (XElement def in DefXml.ModDefNodes)
            {
                string where = def.Element("defName")?.Value ?? def.Name.LocalName;

                foreach (XElement element in def.Descendants())
                {
                    if (ReferenceElements.TryGetValue(element.Name.LocalName, out string type) && !element.HasElements)
                    {
                        yield return (type, element.Value.Trim(), where);
                    }

                    if (ReferenceLists.TryGetValue(element.Name.LocalName, out string itemType))
                    {
                        foreach (XElement item in element.Elements("li"))
                        {
                            yield return (itemType, item.Value.Trim(), where);
                        }
                    }
                }
            }
        }
    }
}
