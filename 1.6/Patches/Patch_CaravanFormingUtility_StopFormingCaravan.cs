using System;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Jobs;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Cancelling a forming caravan: everything the mod has to undo before vanilla takes it apart.
    /// </summary>
    /// <remarks>
    /// One prefix for every feature with something to undo here. So far that is the animal helpers,
    /// who let go so that vanilla can lead every pen animal home with the caravan's colonists; see
    /// <see cref="JobDriver_CCE_RopeAnimalToCaravan.LetGoFor"/>.
    /// </remarks>
    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.StopFormingCaravan))]
    public static class Patch_CaravanFormingUtility_StopFormingCaravan
    {
        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch.
        /// </summary>
        public const int ErrorKey = 0x43434503;

        /// <summary>
        /// Undoes the mod's part in a caravan that is being cancelled.
        /// </summary>
        /// <param name="lord">The caravan.</param>
        public static void Prefix(Lord lord)
        {
            if (!FormingLords.IsVanilla(lord) || lord.Map == null)
            {
                return;
            }

            try
            {
                JobDriver_CCE_RopeAnimalToCaravan.LetGoFor(lord);
            }
            catch (Exception exception)
            {
                // Vanilla still cancels; the helpers' own fail condition lets go a tick later.
                Log.ErrorOnce("[CaravansCriticallyEnhanced] Letting go of a cancelled caravan's animals failed: " + exception, ErrorKey);
            }
        }
    }
}
