using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Keeps a held caravan in the stage that gathers its downed pawns.
    /// </summary>
    /// <remarks>
    /// The stage ends once every listed downed pawn lies within 7 cells of the toil's copy of the exit,
    /// which a held caravan points at its meeting point. A downed pawn in a bed near the spot would
    /// then count as gathered, and the stages after this one never carry anybody but babies, so it
    /// would be left when the caravan went. Held, the test is skipped; after the release the next job
    /// ending runs it against the real exit.
    /// </remarks>
    [HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "CheckAllPawnsArrived")]
    public static class Patch_LordToil_PrepareCaravan_GatherDownedPawns_CheckAllPawnsArrived
    {
        /// <summary>
        /// Skips vanilla's test for a held caravan.
        /// </summary>
        /// <param name="__instance">The toil.</param>
        /// <returns><c>false</c> while the caravan is held.</returns>
        public static bool Prefix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
        {
            return !CaravanExitStates.IsHolding(FormingLords.VanillaJobOf(__instance.lord));
        }
    }
}
