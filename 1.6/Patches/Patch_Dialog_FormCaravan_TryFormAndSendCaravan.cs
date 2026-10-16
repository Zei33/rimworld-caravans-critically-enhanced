using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Opens the exit handoff for the length of a send from the formation window.
    /// </summary>
    /// <remarks>
    /// Nothing is planned here: vanilla checks the caravan for errors first, and the exit search it
    /// calls next is where the plan is made (<see cref="ExitHandoff"/>). Reforming a caravan on a
    /// world map site never reaches this method.
    /// </remarks>
    [HarmonyPatch(typeof(Dialog_FormCaravan), "TryFormAndSendCaravan")]
    public static class Patch_Dialog_FormCaravan_TryFormAndSendCaravan
    {
        /// <summary>
        /// Opens the handoff when either exit switch is on.
        /// </summary>
        /// <param name="__instance">The window.</param>
        /// <param name="___map">Its map.</param>
        /// <param name="___startingTile">The first tile of the chosen route.</param>
        public static void Prefix(Dialog_FormCaravan __instance, Map ___map, PlanetTile ___startingTile)
        {
            CaravansCriticallyEnhancedSettings settings = CaravansCriticallyEnhancedMod.Settings;
            if (settings.IsOn(Toggle.AreaAwareExit) || settings.IsOn(Toggle.ThreatAwareExit))
            {
                ExitHandoff.Open(___map, __instance.transferables, ___startingTile);
            }
        }

        /// <summary>
        /// Closes the handoff, whatever happened.
        /// </summary>
        public static void Finalizer()
        {
            ExitHandoff.Close();
        }
    }
}
