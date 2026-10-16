using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Saves and loads the mod's state of a forming caravan with its lord job.
    /// </summary>
    /// <remarks>
    /// One class for every feature that keeps state per caravan; so far the exit. Everything goes in
    /// <c>cce_</c> nodes, which vanilla ignores if the mod is removed. Only for vanilla's own lord job
    /// type, never a subclass another mod saves its own way.
    /// </remarks>
    [HarmonyPatch(typeof(LordJob_FormAndSendCaravan), nameof(LordJob_FormAndSendCaravan.ExposeData))]
    public static class Patch_LordJob_FormAndSendCaravan_ExposeData
    {
        /// <summary>
        /// Exposes each feature's state.
        /// </summary>
        /// <param name="__instance">The lord job.</param>
        public static void Postfix(LordJob_FormAndSendCaravan __instance)
        {
            if (FormingLords.IsVanillaJob(__instance))
            {
                CaravanExitStates.Expose(__instance);
            }
        }
    }
}
