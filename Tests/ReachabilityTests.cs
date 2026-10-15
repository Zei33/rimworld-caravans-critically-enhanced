using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the mod to containing no private or internal code that nothing can reach.
    /// </summary>
    /// <remarks>
    /// Adapted from Simple Improve, where two dead designators kept ten translated strings alive and
    /// documented a tool no player could open. The second half of that fixture, every work giver and
    /// job driver named by a shipped def, arrives with the first def.
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
