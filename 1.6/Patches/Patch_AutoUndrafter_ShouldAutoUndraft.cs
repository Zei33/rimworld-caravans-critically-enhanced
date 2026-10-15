using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Keeps a caravan colonist holding leads drafted.
    /// </summary>
    /// <remarks>
    /// <see cref="LeadPolicy.SuppressAutoUndraft"/> says when, and why only then.
    /// </remarks>
    [HarmonyPatch(typeof(AutoUndrafter), "ShouldAutoUndraft")]
    public static class Patch_AutoUndrafter_ShouldAutoUndraft
    {
        /// <summary>
        /// Turns a yes into a no for a lead holder in a forming caravan.
        /// </summary>
        /// <param name="___pawn">The drafted pawn.</param>
        /// <param name="__result">Vanilla's answer.</param>
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (!__result || ___pawn?.roping == null)
            {
                return;
            }

            Verse.AI.Group.Lord lord = FormingLords.VanillaLordOf(___pawn);
            if (LeadPolicy.SuppressAutoUndraft(
                    CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting),
                    ___pawn.roping.Ropees.Count,
                    lord != null,
                    FormingLords.StageOf(lord)))
            {
                __result = false;
            }
        }
    }
}
