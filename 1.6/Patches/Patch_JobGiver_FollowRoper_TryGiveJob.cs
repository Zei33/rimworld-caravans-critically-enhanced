using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Lets an animal follow a drafted caravan colonist that holds its lead.
    /// </summary>
    /// <remarks>
    /// Vanilla's giver follows only a roper in a rope job. For a roper
    /// <see cref="LeadUtility.KeepsLeads"/> accepts it hands out the same job, with the same expiry,
    /// that vanilla would; <c>JobDriver_FollowRoper</c> already follows a roper outside a rope job by
    /// keeping in touch with it.
    /// </remarks>
    [HarmonyPatch(typeof(JobGiver_FollowRoper), "TryGiveJob")]
    public static class Patch_JobGiver_FollowRoper_TryGiveJob
    {
        /// <summary>
        /// How long the follow job lasts before the animal thinks again, as in vanilla.
        /// </summary>
        public const int FollowJobExpireInterval = 140;

        /// <summary>
        /// Gives the follow job vanilla refused.
        /// </summary>
        /// <param name="pawn">The animal.</param>
        /// <param name="__result">Vanilla's job, or <c>null</c>.</param>
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null)
            {
                return;
            }

            Pawn roper = pawn?.roping?.RopedByPawn;
            if (roper == null || !roper.Spawned || !LeadUtility.KeepsLeads(roper) || !pawn.CanReach(roper, PathEndMode.Touch, Danger.Deadly))
            {
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.FollowRoper, roper);
            job.expiryInterval = FollowJobExpireInterval;
            job.checkOverrideOnExpire = true;
            __result = job;
        }
    }
}
