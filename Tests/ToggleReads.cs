using System.Collections.Generic;
using System.Reflection;
using CaravansCriticallyEnhanced.Core;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Reads which settings switches a method body asks for, from its IL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>settings.IsOn(Toggle.X)</c> compiles to a constant load straight before the call, and for
    /// the first nine switches that load is the one-byte <c>ldc.i4.0</c> to <c>ldc.i4.8</c>, whose
    /// opcode alone names the value. Those nine are the Loading and Leads switches and the first four
    /// Exits ones, which is every switch a patch or work giver asks for so far.
    /// </para>
    /// <para>
    /// A switch past the ninth, or one passed in a variable, is read as <c>null</c> rather than
    /// guessed, so an assertion that a switch is asked cannot pass on a misreading.
    /// </para>
    /// </remarks>
    public static class ToggleReads
    {
        private const ushort LoadConstantZero = 0x16;
        private const ushort LoadConstantEight = 0x1E;

        /// <summary>
        /// Lists the switch each <c>IsOn</c> call in a body asks for, in IL order.
        /// </summary>
        /// <param name="method">The body to read.</param>
        /// <returns>One entry per call; <c>null</c> where the switch cannot be read from the opcode.</returns>
        public static List<Toggle?> AskedBy(MethodBase method)
        {
            WalkResult walk = ILCalls.Read(method);
            var asked = new List<Toggle?>();

            for (int i = 0; i < walk.Calls.Count; i++)
            {
                MethodBase call = walk.Calls[i];
                if (call.DeclaringType != typeof(CaravansCriticallyEnhancedSettings) || call.Name != nameof(CaravansCriticallyEnhancedSettings.IsOn))
                {
                    continue;
                }

                ushort before = walk.OpcodeBeforeCall[i];
                asked.Add(before >= LoadConstantZero && before <= LoadConstantEight ? (Toggle?)(before - LoadConstantZero) : null);
            }

            return asked;
        }
    }
}
