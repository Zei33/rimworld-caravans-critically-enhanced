using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Stops a helper's gather job once the caravan has moved on from loading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's driver fails only when the lord is removed, and helpers are not members, so the
    /// transitions that end members' jobs (<c>TransitionAction_EndAllJobs</c>) never reach them. After
    /// a force depart a helper keeps chasing a carrier toward the exit until the caravan is gone.
    /// </para>
    /// <para>
    /// <c>MakeNewToils</c> is not an iterator in this class: it chooses between two iterator methods
    /// and returns one. So a postfix runs once, before any toil does, from <c>SetupToils</c>, which a
    /// loaded game calls again, and a fail condition added here lives on the driver for the job's
    /// whole life.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(JobDriver_PrepareCaravan_GatherItems), "MakeNewToils")]
    public static class Patch_JobDriver_PrepareCaravan_GatherItems_MakeNewToils
    {
        /// <summary>
        /// Adds the stage check to a helper's job.
        /// </summary>
        /// <param name="__instance">The driver.</param>
        public static void Postfix(JobDriver_PrepareCaravan_GatherItems __instance)
        {
            if (!CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LoadingWorkType))
            {
                return;
            }

            Lord lord = __instance.job?.lord;
            if (!FormingLords.IsVanilla(lord) || __instance.pawn.GetLord() == lord)
            {
                return;
            }

            __instance.AddFailCondition(() => !FormingStages.HelpersMayContinue(FormingLords.StageOf(lord)));
        }
    }
}
