using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Hides "Carry to exit" for a member of a forming caravan.
    /// </summary>
    /// <remarks>
    /// On a map that is not a home, with enemies about, vanilla offers to carry a downed pawn off the
    /// map. Its job leaves the map by itself rather than through the walk the mod stops, and makes a
    /// caravan of two, abandoning the one being formed (Matthew's decision of 2026-10-16). Hidden only
    /// for a member of a vanilla forming caravan, with "Leave from here" on.
    /// </remarks>
    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryPawnToExit), "AppliesInt")]
    public static class Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt
    {
        /// <summary>
        /// Turns a yes into a no for a caravan member.
        /// </summary>
        /// <param name="context">The menu's context.</param>
        /// <param name="__result">Vanilla's answer.</param>
        public static void Postfix(FloatMenuContext context, ref bool __result)
        {
            if (__result && FormingLords.VanillaJobOf(context?.FirstSelectedPawn?.GetLord()) != null
                && CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeaveFromHere))
            {
                __result = false;
            }
        }
    }
}
