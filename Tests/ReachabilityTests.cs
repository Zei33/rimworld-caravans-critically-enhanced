using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the mod to containing no private or internal code that nothing can reach.
    /// </summary>
    /// <remarks>
    /// Adapted from Simple Improve, where two dead designators kept ten translated strings alive and
    /// documented a tool no player could open.
    /// </remarks>
    [TestFixture]
    public class ReachabilityTests
    {
        [Test]
        public void EveryPrivateOrInternalMethodInTheModIsCalledFromSomewhere()
        {
            // A private or internal method can only be reached from inside the mod, so if no compiled
            // body calls it, loads it as a delegate or constructs it, nothing ever runs it. Static
            // constructors, compiler-generated types and explicit interface implementations are reached
            // implicitly and are excluded by kind. Calls from the tests do not count.
            var called = new HashSet<MethodBase>();

            foreach (MethodBase method in ILCalls.AllModMethods())
            {
                foreach (MethodBase target in ILCalls.CalledBy(method))
                {
                    called.Add(target is MethodInfo info && info.IsGenericMethod ? info.GetGenericMethodDefinition() : target);
                }
            }

            List<MethodBase> examined = ILCalls.AllModMethods()
                .Where(method => method.IsPrivate || method.IsAssembly || method.IsFamilyAndAssembly)
                .Where(method => !(method.IsConstructor && method.IsStatic))
                .Where(method => method.IsConstructor || !method.Name.Contains("."))
                .Where(method => !IsCompilerGenerated(method.DeclaringType))
                .ToList();

            // The positive control: the scan has to hold private methods where they actually are. The
            // settings window's helpers are reached only from the window, and Index only from a static
            // initialiser, which shows a call from a static constructor is counted.
            List<string> examinedNames = examined.Select(ILCalls.Describe).ToList();

            Assert.That(examinedNames, Does.Contain("CaravansCriticallyEnhanced.Core.CaravansCriticallyEnhancedSettings.DrawToggle"));
            Assert.That(examinedNames, Does.Contain("CaravansCriticallyEnhanced.Core.SettingToggles.Index"));

            Assert.That(
                examined.Where(method => !called.Contains(method)).Select(ILCalls.Describe).ToList(),
                Is.Empty,
                "These private or internal methods are never called by anything in the mod.");
        }

        [Test]
        public void EveryClassTheGameOnlyBuildsFromADefIsNamedByAShippedDef()
        {
            // RimWorld never discovers a WorkGiver, JobDriver, ThinkNode or ITab by reflection: each is
            // built only because a def or a patch names its class. One that nothing names is dead however
            // complete it looks. The XML is parsed and only element text and Class attributes are read,
            // so a class named inside a comment does not count.
            Type[] defBuilt = { typeof(WorkGiver), typeof(JobDriver), typeof(ThinkNode), typeof(ITab) };

            List<string> declared = typeof(FormingStages).Assembly.GetTypes()
                .Where(type => type.Namespace != null
                               && type.Namespace.StartsWith("CaravansCriticallyEnhanced", StringComparison.Ordinal)
                               && !type.Namespace.StartsWith("CaravansCriticallyEnhanced.Tests", StringComparison.Ordinal))
                .Where(type => !type.IsAbstract && defBuilt.Any(baseType => baseType.IsAssignableFrom(type)))
                .Select(type => type.FullName)
                .ToList();

            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (string folder in new[] { "Defs", "Patches" })
            {
                foreach (string file in Directory.GetFiles(Repo.PathOf("1.6", folder), "*.xml", SearchOption.AllDirectories))
                {
                    foreach (XElement element in XDocument.Load(file).Descendants())
                    {
                        if (!element.HasElements)
                        {
                            named.Add(element.Value.Trim());
                        }

                        string cls = element.Attribute("Class")?.Value;
                        if (cls != null)
                        {
                            named.Add(cls);
                        }
                    }
                }
            }

            // The positive control: the work giver the mod registers is found on both sides.
            Assert.That(declared, Does.Contain("CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan"));
            Assert.That(named, Does.Contain("CaravansCriticallyEnhanced.Jobs.WorkGiver_CCE_LoadCaravan"));

            Assert.That(declared.Where(name => !named.Contains(name)).ToList(), Is.Empty,
                "These classes can only be built from a def and no shipped def names them.");
        }

        private static bool IsCompilerGenerated(System.Type type)
        {
            for (System.Type current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
