using System.Collections.Generic;
using System.Linq;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Lists, in vanilla's Force depart confirmation, the downed pawns and babies it would leave behind.
    /// </summary>
    /// <remarks>
    /// Vanilla's Force depart clears the caravan's list of downed pawns, so every one not in the arms of
    /// a member it keeps undrafted stays on the map (<see cref="LeaveFromHerePolicy.LeftByForceDepart"/>),
    /// and its confirmation never says so: it lists only members that are downed or in a mental state,
    /// and a member that goes down leaves the members for that list. The lines
    /// take vanilla's own shape. Leave from here builds its confirmation from the same method and keeps
    /// the list, so this stays out of it.
    /// </remarks>
    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.GetForceDepartWarningMessage))]
    public static class Patch_CaravanFormingUtility_GetForceDepartWarningMessage
    {
        /// <summary>
        /// Appends the pawns left behind.
        /// </summary>
        /// <param name="lordJob">The caravan's lord job.</param>
        /// <param name="__result">Vanilla's text.</param>
        public static void Postfix(LordJob_FormAndSendCaravan lordJob, ref string __result)
        {
            if (LeaveFromHere.Confirming || !FormingLords.IsVanillaJob(lordJob) || lordJob.downedPawns == null || lordJob.downedPawns.Count == 0
                || !CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.DepartureWarnings))
            {
                return;
            }

            var left = new List<string>();
            foreach (Pawn pawn in lordJob.downedPawns)
            {
                bool gone = pawn == null || pawn.Destroyed || !pawn.Downed;
                Pawn carrier = gone ? null : ExitDirector.CarrierOf(pawn, lordJob.lord);
                if (LeaveFromHerePolicy.LeftByForceDepart(gone, carrier != null && ExitDirector.IsWorker(carrier)))
                {
                    string label = pawn.LabelNoCountColored.ToString();
                    left.Add(pawn.DevelopmentalStage.Baby() ? label + " (" + pawn.ageTracker.CurLifeStage.label + ")" : label);
                }
            }

            if (left.Count > 0)
            {
                __result += "\n\n" + "CaravansCriticallyEnhanced_ForceDepartLeftBehind".Translate() + "\n\n" + left.ToLineList("- ");
            }
        }
    }
}
