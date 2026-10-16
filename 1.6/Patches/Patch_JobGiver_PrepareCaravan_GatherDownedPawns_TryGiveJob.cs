using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Stops a held caravan's colonists carrying downed pawns to the edge.
    /// </summary>
    /// <remarks>
    /// The giver's drop cell comes from <c>FindRandomDropCell</c>, which reads the lord job's exit, the
    /// fallback while the caravan is held. So a held caravan with downed pawns waits in this stage, its
    /// colonists at the meeting point, and carries them to the real exit once released.
    /// </remarks>
    [HarmonyPatch(typeof(JobGiver_PrepareCaravan_GatherDownedPawns), "TryGiveJob")]
    public static class Patch_JobGiver_PrepareCaravan_GatherDownedPawns_TryGiveJob
    {
        /// <summary>
        /// Withdraws the job for a member of a held caravan.
        /// </summary>
        /// <param name="pawn">The colonist.</param>
        /// <param name="__result">Vanilla's job.</param>
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && CaravanExitStates.IsHolding(FormingLords.VanillaJobOf(pawn.GetLord())))
            {
                __result = null;
            }
        }
    }
}
