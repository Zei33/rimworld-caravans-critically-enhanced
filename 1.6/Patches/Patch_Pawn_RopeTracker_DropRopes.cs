using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Refuses to drop a pawn's ropes while it is being drafted with leads the mod keeps.
    /// </summary>
    /// <remarks>
    /// Only while the pawn is spawned. <c>Pawn.DeSpawn</c> reaches <c>Notify_DeSpawned</c>, which
    /// drops the ropes, with <c>Spawned</c> already false, so no rope ever survives into a world
    /// caravan or a despawn, whatever scope is open.
    /// </remarks>
    [HarmonyPatch(typeof(Pawn_RopeTracker), nameof(Pawn_RopeTracker.DropRopes))]
    public static class Patch_Pawn_RopeTracker_DropRopes
    {
        /// <summary>
        /// Skips vanilla inside a scope.
        /// </summary>
        /// <param name="___pawn">The tracker's pawn.</param>
        /// <returns><c>false</c> to keep the ropes.</returns>
        public static bool Prefix(Pawn ___pawn)
        {
            return !(LeadKeepScope.Contains(___pawn) && ___pawn.Spawned);
        }
    }
}
