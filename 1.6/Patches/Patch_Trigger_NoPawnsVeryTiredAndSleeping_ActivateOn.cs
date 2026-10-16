using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Keeps a held caravan waiting rather than setting off to collect its animals.
    /// </summary>
    /// <remarks>
    /// The trigger is the only way out of the stage where the caravan waits for sleepers, and it is
    /// asked every tick there, so the cheap test of vanilla's answer comes first. Other lords use this
    /// trigger class too, so the caravan's type is checked.
    /// </remarks>
    [HarmonyPatch(typeof(Trigger_NoPawnsVeryTiredAndSleeping), nameof(Trigger_NoPawnsVeryTiredAndSleeping.ActivateOn))]
    public static class Patch_Trigger_NoPawnsVeryTiredAndSleeping_ActivateOn
    {
        /// <summary>
        /// Turns a yes into a no for a held caravan.
        /// </summary>
        /// <param name="lord">The lord.</param>
        /// <param name="__result">Vanilla's answer.</param>
        public static void Postfix(Lord lord, ref bool __result)
        {
            if (__result && CaravanExitStates.IsHolding(FormingLords.VanillaJobOf(lord)))
            {
                __result = false;
            }
        }
    }
}
