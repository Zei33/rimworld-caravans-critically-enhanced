using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Stops an animal following a roper that has just let go of it.
    /// </summary>
    /// <remarks>
    /// Vanilla's rope driver ends the follow job itself when an animal arrives, but nothing does when
    /// a rope is dropped any other way, and the animal trails its old roper until the follow job
    /// expires. With the drop-lead button that is the common case. The roper's speed match on that
    /// animal is cleared too.
    /// </remarks>
    [HarmonyPatch(typeof(Pawn_RopeTracker), nameof(Pawn_RopeTracker.DropRope))]
    public static class Patch_Pawn_RopeTracker_DropRope
    {
        /// <summary>
        /// Ends the let-go animal's follow job.
        /// </summary>
        /// <param name="___pawn">The roper.</param>
        /// <param name="ropee">The animal let go.</param>
        public static void Postfix(Pawn ___pawn, Pawn ropee)
        {
            if (ropee == null || !CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting))
            {
                return;
            }

            LeadUtility.ForgetSpeedOf(___pawn, ropee);

            if (ropee.Spawned && ropee.jobs?.curDriver is JobDriver_FollowRoper && ropee.CurJob.GetTarget(TargetIndex.A).Thing == ___pawn)
            {
                ropee.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }
}
