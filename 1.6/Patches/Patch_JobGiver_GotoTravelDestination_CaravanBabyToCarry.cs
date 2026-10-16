using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Stops a held caravan's colonists picking up its babies.
    /// </summary>
    /// <remarks>
    /// Vanilla has a caravan's colonists pick up any of its babies they can reach and hold them while
    /// they wait, which is a moment in vanilla and the whole hold here, out of reach of feeding and
    /// care. Held, the baby stays where it is; once released, the colonists pick it up as before.
    /// </remarks>
    [HarmonyPatch(typeof(JobGiver_GotoTravelDestination), "CaravanBabyToCarry")]
    public static class Patch_JobGiver_GotoTravelDestination_CaravanBabyToCarry
    {
        /// <summary>
        /// Withdraws the baby for a member of a held caravan.
        /// </summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="__result">The baby vanilla chose, or <c>null</c>.</param>
        public static void Postfix(Pawn pawn, ref Pawn __result)
        {
            if (__result != null && CaravanExitStates.IsHolding(FormingLords.VanillaJobOf(pawn.GetLord())))
            {
                __result = null;
            }
        }
    }
}
