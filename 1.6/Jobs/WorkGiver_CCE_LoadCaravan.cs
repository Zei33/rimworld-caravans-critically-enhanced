using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// Lets a pawn who is not going with a caravan carry its listed items to its pack animals,
    /// prisoners or colonists. The Load work type's giver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla already has this mechanism, gated almost shut. <c>WorkGiver_HelpGatheringItemsForCaravan</c>
    /// sits in Hauling, helps only while items are being gathered, and starts only if some member is a
    /// pack animal or prisoner with room, so a caravan of colonists alone never gets help. Its job,
    /// <c>PrepareCaravan_GatherItems</c>, was written for outside helpers: the driver's
    /// <c>FindCarrier</c> has an explicit branch for a hauler who is not a member. So this giver reuses
    /// that job and driver exactly, and only replaces how a helper is found and admitted. A patch on
    /// <c>NonScanJob</c> switches the vanilla giver off while this one is on.
    /// </para>
    /// <para>
    /// Reusing the vanilla job def is load-bearing. <c>GatherItemsForCaravanUtility.TransferableCountHauledByOthers</c>
    /// counts hauls in progress by job def equality and casts the driver, and
    /// <c>CaravanFormingUtility.AllItemsLoadedOntoCaravan</c> holds loading open while any pawn runs
    /// that driver for the lord. Both keep working for helpers only because the def is the same.
    /// </para>
    /// <para>
    /// The shape follows the workspace rule for a scanner whose set is small and exact: no thing
    /// request, so no region search is built; <see cref="ShouldSkip"/> free when nothing is forming;
    /// the set a <c>List&lt;Thing&gt;</c>; and one decision behind both <see cref="HasJobOnThing"/> and
    /// <see cref="JobOnThing"/>, because <c>JobGiver_Work</c> abandons a pawn's whole work search when
    /// the two disagree.
    /// </para>
    /// </remarks>
    public class WorkGiver_CCE_LoadCaravan : WorkGiver_Scanner
    {
        private readonly JobMemo<Pawn, Thing, Job> memo;

        private Pawn gatePawn;
        private Lord gateLord;
        private bool gateForced;
        private int gateTick = -1;
        private int gateFrame = -1;
        private bool gateAnswer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkGiver_CCE_LoadCaravan"/> class.
        /// </summary>
        /// <remarks>
        /// <c>WorkGiverDef.Worker</c> builds one instance per def and every pawn shares it, which is why
        /// the memo is keyed on the pawn as well as the tick.
        /// </remarks>
        public WorkGiver_CCE_LoadCaravan()
        {
            memo = new JobMemo<Pawn, Thing, Job>(BuildJob, ForgetCarrierGate);
        }

        /// <inheritdoc />
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// A pawn in any lord is skipped, which covers every member of a forming caravan, paused ones
        /// included. <c>ThinkNode_Duty</c> overwrites a job's lord with the pawn's own for every job
        /// its duty gives, and a forming caravan's pause duty, like Defend, Follow and Build, runs
        /// <c>JobGiver_Work</c>. The job would then point at the wrong lord: for a member, the driver
        /// goes into the member-only Haul mode against the wrong caravan, and for a pawn in any other
        /// lord, it throws casting that lord's job to the caravan's.
        /// </para>
        /// <para>
        /// With nothing to load, the giver also lets go of what it remembered from the last search.
        /// One instance lives for the whole session, so a pawn, lord or job held here would otherwise
        /// keep a previous save's map in memory after another is loaded.
        /// </para>
        /// </remarks>
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn.GetLord() != null)
            {
                return true;
            }

            CaravansCriticallyEnhancedSettings settings = CaravansCriticallyEnhancedMod.Settings;
            if (!settings.IsOn(Toggle.LoadingWorkType) || !LoadingIndex.AnyOpen(pawn.Map, settings.IsOn(Toggle.EarlyLoading)))
            {
                ForgetEverything();
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Has its own early exit. <c>FloatMenuOptionProvider_WorkGivers.ScannerShouldSkip</c> asks for
        /// this set twice per right-clicked thing, for the first selected pawn, before it asks
        /// <see cref="ShouldSkip"/>, so <c>ShouldSkip</c> does not protect it there.
        /// </remarks>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            CaravansCriticallyEnhancedSettings settings = CaravansCriticallyEnhancedMod.Settings;
            bool earlyLoading = settings.IsOn(Toggle.EarlyLoading);

            if (!settings.IsOn(Toggle.LoadingWorkType) || !LoadingIndex.AnyOpen(pawn.Map, earlyLoading))
            {
                return LoadingIndex.None;
            }

            return LoadingIndex.ThingsOn(pawn.Map, earlyLoading);
        }

        /// <inheritdoc />
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobFor(pawn, t, forced) != null;
        }

        /// <inheritdoc />
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobFor(pawn, t, forced);
        }

        /// <summary>
        /// The one decision behind both scan entry points.
        /// </summary>
        private Job JobFor(Pawn pawn, Thing t, bool forced)
        {
            return memo.Answer(pawn, forced, Find.TickManager.TicksGame, t);
        }

        /// <summary>
        /// Builds the helper's job for one listed item, or refuses.
        /// </summary>
        /// <param name="pawn">The helper.</param>
        /// <param name="t">The item.</param>
        /// <param name="forced">Whether the player ordered it from the right-click menu.</param>
        /// <returns>The vanilla gather job pointed at the item's caravan, or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// A refusal's reason is built only when <paramref name="forced"/> is set. The right-click menu,
        /// the one place that shows it, always asks with <c>forced: true</c>, and the memo is keyed on
        /// it, so the menu never sees an answer built without one; the work scan is spared a
        /// translation per refused item.
        /// </para>
        /// <para>
        /// The carrier comes straight after the claim. It is asked once per pawn, caravan and tick, so
        /// from the second item on it costs nothing, and when there is no carrier, which is the steady
        /// state while animals gather or with "Load into colonists" off on a caravan of colonists, it
        /// turns every item away before the per-item work: the count, which walks every pawn on the
        /// map, and the reservation.
        /// </para>
        /// </remarks>
        private Job BuildJob(Pawn pawn, Thing t, bool forced)
        {
            CaravansCriticallyEnhancedSettings settings = CaravansCriticallyEnhancedMod.Settings;
            LoadingClaim claim = LoadingIndex.ClaimOf(pawn.Map, t, settings.IsOn(Toggle.EarlyLoading));
            if (claim == null)
            {
                return null;
            }

            if (!AnyCarrierFor(pawn, claim.Lord, forced, settings.IsOn(Toggle.ColonistCarriers)))
            {
                if (forced)
                {
                    JobFailReason.Is("CaravansCriticallyEnhanced_NoCarrier".Translate());
                }

                return null;
            }

            if (GatherItemsForCaravanUtility.CountLeftToTransfer(pawn, claim.Row, claim.Lord) <= 0)
            {
                return null;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                Pawn reserver = forced ? pawn.Map.reservationManager.FirstRespectedReserver(t, pawn) : null;
                if (reserver != null)
                {
                    JobFailReason.Is("ReservedBy".Translate(reserver.LabelShort, reserver));
                }

                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.PrepareCaravan_GatherItems, t);
            job.lord = claim.Lord;
            return job;
        }

        /// <summary>
        /// Determines whether any member of the caravan could take an item from this helper.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The same test vanilla's helper makes before it starts, with the colonist half switched by
        /// the setting: <c>IsUsableCarrier</c>, which this mod's postfix narrows for helpers, and the
        /// member not forbidden to the helper, which for a pawn is a test of the helper's allowed area
        /// at the member's cell.
        /// </para>
        /// <para>
        /// Asked once per pawn, caravan, tick and frame rather than once per item. <c>IsUsableCarrier</c>
        /// runs a reachability test for every member, and inside the scan validator that would be items
        /// times members of them on every job search.
        /// </para>
        /// </remarks>
        private bool AnyCarrierFor(Pawn pawn, Lord lord, bool forced, bool colonistCarriers)
        {
            int tick = Find.TickManager.TicksGame;
            int frame = Time.frameCount;

            if (pawn == gatePawn && lord == gateLord && forced == gateForced && tick == gateTick && frame == gateFrame)
            {
                return gateAnswer;
            }

            gatePawn = pawn;
            gateLord = lord;
            gateForced = forced;
            gateTick = tick;
            gateFrame = frame;
            gateAnswer = false;

            List<Pawn> members = lord.ownedPawns;
            for (int i = 0; i < members.Count; i++)
            {
                Pawn member = members[i];
                if (member != pawn
                    && JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier(member, pawn, colonistCarriers)
                    && !member.IsForbidden(pawn))
                {
                    gateAnswer = true;
                    break;
                }
            }

            return gateAnswer;
        }

        /// <summary>
        /// Drops every held job and the carrier answer, once; later calls cost one comparison.
        /// </summary>
        private void ForgetEverything()
        {
            if (memo.Rekey(null, false, -1))
            {
                ForgetCarrierGate();
            }
        }

        /// <summary>
        /// Drops the carrier answer, which the memo calls whenever its own key changes.
        /// </summary>
        private void ForgetCarrierGate()
        {
            gatePawn = null;
            gateLord = null;
            gateTick = -1;
            gateFrame = -1;
        }
    }
}
