using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds every C# type the mod's XML names to a real, concrete type of the right kind.
    /// </summary>
    /// <remarks>
    /// Adapted from Great Escape's fixture of the same name. A class name in XML is resolved at load,
    /// and a wrong one costs the def it is in, with the reason in the startup log. Bare names resolve
    /// only inside the namespaces <c>GenTypes</c> treats as implicit, so the mod's own classes are
    /// written namespace-qualified.
    /// </remarks>
    [TestFixture]
    public class TypeReferenceTests
    {
        /// <summary>
        /// The elements that name a class, and the base class each must derive from.
        /// </summary>
        private static readonly Dictionary<string, Type> ClassElements = new Dictionary<string, Type>
        {
            { "giverClass", typeof(WorkGiver) },
            { "driverClass", typeof(JobDriver) },
        };

        [Test]
        public void EveryClassNamedInTheDefsResolvesToTheRightKindOfType()
        {
            var references = new List<(string Name, Type Base, string Where)>();

            foreach (XElement def in DefXml.ModDefNodes)
            {
                string where = def.Element("defName")?.Value ?? def.Name.LocalName;
                foreach (XElement element in def.Descendants())
                {
                    if (ClassElements.TryGetValue(element.Name.LocalName, out Type baseType))
                    {
                        references.Add((element.Value.Trim(), baseType, where));
                    }
                }
            }

            // Positive control: two work givers and one job driver at Slice 2.
            Assert.That(references.Count, Is.EqualTo(3));

            List<string> failures = references
                .Select(r => (r, Type: Resolve(r.Name)))
                .Where(x => x.Type == null || !x.r.Base.IsAssignableFrom(x.Type) || x.Type.IsAbstract)
                .Select(x => $"'{x.r.Name}' in {x.r.Where}: " + (x.Type == null ? "does not resolve" : $"is not a concrete {x.r.Base.Name}"))
                .ToList();

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void EveryClassInThePatchesResolvesToo()
        {
            // A Class attribute in a PatchOperation names the operation's type, and a wrong one fails
            // the whole patch file.
            var named = new List<string>();
            foreach (string file in DefXml.ModFiles("Patches"))
            {
                named.AddRange(XDocument.Load(file).Descendants().Select(e => e.Attribute("Class")?.Value).Where(v => v != null));
            }

            Assert.That(named, Is.Not.Empty);
            Assert.That(named.Where(n => Resolve(n) == null || !typeof(PatchOperation).IsAssignableFrom(Resolve(n))), Is.Empty);
        }

        [Test]
        public void TheModsOwnClassesAreWrittenQualified()
        {
            string[] ownTypes = typeof(FormingStages).Assembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("CaravansCriticallyEnhanced", StringComparison.Ordinal)
                            && !t.Namespace.StartsWith("CaravansCriticallyEnhanced.Tests", StringComparison.Ordinal))
                .Select(t => t.Name)
                .ToArray();

            Assert.That(ownTypes, Does.Contain("WorkGiver_CCE_LoadCaravan"));

            List<string> bare = DefXml.ModDefNodes
                .SelectMany(d => d.DescendantsAndSelf())
                .Where(e => !e.HasElements)
                .Select(e => e.Value.Trim())
                .Where(ownTypes.Contains)
                .ToList();

            Assert.That(bare, Is.Empty);
        }

        /// <summary>
        /// Resolves a type name as a stricter subset of <c>GenTypes.GetTypeInAnyAssembly</c>: only the
        /// game and the mod, case-sensitive, implicit namespaces from the game's own list.
        /// </summary>
        internal static Type Resolve(string name)
        {
            Type[] anchors = { typeof(Pawn), typeof(FormingStages) };

            if (name.Contains('.'))
            {
                return anchors.Select(a => a.Assembly.GetType(name, throwOnError: false)).FirstOrDefault(t => t != null);
            }

            return GenTypes.IgnoredNamespaceNames
                .Select(ns => typeof(Pawn).Assembly.GetType(ns + "." + name, throwOnError: false))
                .FirstOrDefault(t => t != null);
        }
    }
}
