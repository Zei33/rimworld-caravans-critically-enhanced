using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Decides which forming caravans this mod may touch, and reads where one has got to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one rule is an exact type test, never <c>is</c>. Vehicle Framework forms vehicle caravans
    /// through <c>LordJob_FormAndSendVehicles</c> and Vanilla Trading Expanded sends traders off
    /// through <c>LordJob_GrabItemsAndLeave</c>, and both subclass
    /// <c>LordJob_FormAndSendCaravan</c>. Their graphs, toils and fields are their own, so a patch
    /// written against the vanilla graph would act on the wrong toil there. Every place this mod acts
    /// on a forming lord asks <see cref="IsVanilla(Lord)"/> first.
    /// </para>
    /// <para>
    /// Vanilla itself mostly tests <c>is LordJob_FormAndSendCaravan</c>, through the extension
    /// <c>CaravanFormingUtility.IsFormingCaravan</c> and <c>CaravanFormingUtility.GetFormAndSendCaravanLord</c>. Where
    /// this mod needs to know whether a pawn is in any forming caravan at all, which is the question
    /// those answer, it keeps asking them.
    /// </para>
    /// </remarks>
    public static class FormingLords
    {
        /// <summary>
        /// Determines whether a lord job is vanilla's own caravan forming job and not a subclass.
        /// </summary>
        /// <param name="job">The lord job, which may be null.</param>
        /// <returns><c>true</c> only for an exact <c>LordJob_FormAndSendCaravan</c>.</returns>
        public static bool IsVanillaJob(LordJob job)
        {
            return job != null && job.GetType() == typeof(LordJob_FormAndSendCaravan);
        }

        /// <summary>
        /// Determines whether a lord is the player's own vanilla forming caravan.
        /// </summary>
        /// <param name="lord">The lord, which may be null.</param>
        /// <returns><c>true</c> when this mod may act on the lord.</returns>
        public static bool IsVanilla(Lord lord)
        {
            return lord != null && IsVanilla(lord.faction, lord.LordJob);
        }

        /// <summary>
        /// Determines whether a faction and a lord job together make a lord this mod may act on.
        /// </summary>
        /// <param name="faction">The lord's faction, which may be null.</param>
        /// <param name="job">The lord's job, which may be null.</param>
        /// <returns><c>true</c> for the player's faction running an exact <c>LordJob_FormAndSendCaravan</c>.</returns>
        /// <remarks>
        /// Split out of <see cref="IsVanilla(Lord)"/> so that it can be tested: a <c>Lord</c> cannot be
        /// built outside the game, because its static initialiser loads a material.
        /// </remarks>
        public static bool IsVanilla(Faction faction, LordJob job)
        {
            return faction != null && faction.IsPlayer && IsVanillaJob(job);
        }

        /// <summary>
        /// Gets the lord job of a lord this mod may act on.
        /// </summary>
        /// <param name="lord">The lord, which may be null.</param>
        /// <returns>The job, or <c>null</c> when <see cref="IsVanilla(Lord)"/> is false.</returns>
        public static LordJob_FormAndSendCaravan VanillaJobOf(Lord lord)
        {
            return IsVanilla(lord) ? (LordJob_FormAndSendCaravan)lord.LordJob : null;
        }

        /// <summary>
        /// Gets the vanilla forming caravan a pawn is a member of.
        /// </summary>
        /// <param name="pawn">The pawn, which may be null.</param>
        /// <returns>The lord, or <c>null</c> when the pawn is in none or in a subclass's.</returns>
        /// <remarks>
        /// Membership only. A member that goes down leaves the lord and waits in the lord job's
        /// <c>downedPawns</c> to be carried, and this returns null for it, where vanilla's
        /// <c>CaravanFormingUtility.GetFormAndSendCaravanLord</c> still finds the caravan. Ask that for
        /// "which caravan is this pawn going with".
        /// </remarks>
        public static Lord VanillaLordOf(Pawn pawn)
        {
            Lord lord = pawn?.GetLord();
            return IsVanilla(lord) ? lord : null;
        }

        /// <summary>
        /// Reads where a forming caravan has got to.
        /// </summary>
        /// <param name="lord">A lord for which <see cref="IsVanilla(Lord)"/> holds.</param>
        /// <returns>The stage, or <see cref="FormingStage.Unknown"/> for anything else.</returns>
        /// <remarks>
        /// Read from the toil's position in the graph rather than by comparing it with the lord job's
        /// private toil fields. The position is what the save format restores the toil by, so it is
        /// the more stable of the two, and it needs no reflection.
        /// </remarks>
        public static FormingStage StageOf(Lord lord)
        {
            if (!IsVanilla(lord) || lord.Graph == null)
            {
                return FormingStage.Unknown;
            }

            return StageOf(lord.Graph.lordToils, lord.CurLordToil);
        }

        /// <summary>
        /// Names the stage of a toil from its position in a vanilla forming graph.
        /// </summary>
        /// <param name="toils">The graph's toils, <c>lord.Graph.lordToils</c>.</param>
        /// <param name="current">The lord's current toil.</param>
        /// <returns>The stage, or <see cref="FormingStage.Unknown"/> when the toil is not in the list.</returns>
        public static FormingStage StageOf(List<LordToil> toils, LordToil current)
        {
            return current == null ? FormingStage.Unknown : FormingStages.FromToilIndex(toils.IndexOf(current));
        }
    }
}
