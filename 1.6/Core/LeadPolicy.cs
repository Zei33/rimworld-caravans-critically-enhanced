using RimWorld;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Why a held animal is let go.
    /// </summary>
    public enum LeadRelease
    {
        /// <summary>It keeps following.</summary>
        None,

        /// <summary>It is too hungry to keep following.</summary>
        Hungry,

        /// <summary>It is too tired to keep following.</summary>
        Exhausted,
    }

    /// <summary>
    /// How the drop-lead button acts on what the selected colonists hold.
    /// </summary>
    public enum DropLeadAction
    {
        /// <summary>Nothing is held: the button is not shown.</summary>
        None,

        /// <summary>One animal is held: it is let go at once.</summary>
        DropDirectly,

        /// <summary>Several are held: a menu lists each, and all of them.</summary>
        Menu,
    }

    /// <summary>
    /// Why a selected colonist cannot take the lead of a caravan animal.
    /// </summary>
    public enum TakeLeadBlocker
    {
        /// <summary>Nothing stops it.</summary>
        None,

        /// <summary>The colonist is not going with the animal's caravan.</summary>
        NotInCaravan,

        /// <summary>There is no cell next to the animal to tie a rope from.</summary>
        NoRopeCell,

        /// <summary>Somebody else has the animal reserved.</summary>
        Reserved,

        /// <summary>The colonist cannot reach the animal.</summary>
        Unreachable,
    }

    /// <summary>
    /// The rules for keeping, releasing, dropping and taking a lead while drafted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla cuts a lead in four places when its holder is drafted: the draft ends the rope job,
    /// whose finish action drops every rope; <c>Pawn_RopeTracker.RopingTick</c> breaks the ropes of a
    /// drafted roper; it breaks them again for a roper no longer in a rope job; and
    /// <c>JobGiver_FollowRoper</c> gives an animal nothing to do unless its roper is in a rope job.
    /// The patches that close all four ask these rules.
    /// </para>
    /// <para>
    /// Only a colonist going with a vanilla forming caravan keeps its leads, as the setting says. A
    /// handler drafted while leading animals to a pen, or a Load helper, drops them as in vanilla.
    /// </para>
    /// </remarks>
    public static class LeadPolicy
    {
        /// <summary>
        /// Determines whether the mod holds a roper's leads in place of vanilla.
        /// </summary>
        /// <param name="featureOn">Whether "Keep leads when drafted" is on.</param>
        /// <param name="drafted">Whether the roper is drafted.</param>
        /// <param name="inVanillaFormingCaravan">Whether it belongs to a vanilla forming caravan.</param>
        /// <param name="ropingOthers">Whether it is leading any animal.</param>
        /// <param name="ropedItself">Whether it is itself on a rope, which vanilla's tick also handles.</param>
        /// <returns><c>true</c> when the mod's rules replace vanilla's for this roper.</returns>
        public static bool KeepsLeads(bool featureOn, bool drafted, bool inVanillaFormingCaravan, bool ropingOthers, bool ropedItself)
        {
            return featureOn && drafted && inVanillaFormingCaravan && ropingOthers && !ropedItself;
        }

        /// <summary>
        /// Determines whether a held roper has to let go of everything, as vanilla's tick would.
        /// </summary>
        /// <param name="dead">Whether it is dead.</param>
        /// <param name="downed">Whether it is downed.</param>
        /// <param name="asleep">Whether it is asleep.</param>
        /// <param name="mentalStateNotRoaming">Whether it is in a mental state other than roaming.</param>
        /// <param name="burning">Whether it is on fire.</param>
        /// <returns><c>true</c> when every rope breaks.</returns>
        /// <remarks>
        /// <para>
        /// Vanilla's breaking checks with being drafted taken out, which is the point, and the
        /// not-doing-a-rope-job check taken out, because a drafted colonist never is.
        /// </para>
        /// <para>
        /// Two of them are not quite vanilla's. Vanilla's sleep check is about a pawn that is itself on a
        /// rope, the animal; a roper that sleeps loses its leads to the rope-job check instead. With that
        /// check gone, being asleep breaks the ropes here directly, so a colonist collapsing from
        /// exhaustion still lets go. And roaming is an animal's mental state, so for a colonist every
        /// break counts; a break also undrafts it, which hands the ropes back to vanilla anyway.
        /// </para>
        /// </remarks>
        public static bool IsFatal(bool dead, bool downed, bool asleep, bool mentalStateNotRoaming, bool burning)
        {
            return dead || downed || asleep || mentalStateNotRoaming || burning;
        }

        /// <summary>
        /// Decides whether a held animal has to be let go for its own needs.
        /// </summary>
        /// <param name="hunger">Its hunger, or <c>null</c> if it has no food need.</param>
        /// <param name="rest">Its rest, or <c>null</c> if it has no rest need.</param>
        /// <returns>Why it is let go, or <see cref="LeadRelease.None"/>.</returns>
        /// <remarks>
        /// <c>JobGiver_FollowRoper</c> sits above eating and resting in the animal's think tree, so an
        /// animal on a rope never eats or sleeps. A drafted colonist can hold one indefinitely, which
        /// vanilla never allowed, so a starving or exhausted animal is let go. A sleeping animal breaks
        /// its own rope in vanilla's tick, and that stays.
        /// </remarks>
        public static LeadRelease ShouldRelease(HungerCategory? hunger, RestCategory? rest)
        {
            if (hunger != null && hunger.Value >= HungerCategory.UrgentlyHungry)
            {
                return LeadRelease.Hungry;
            }

            if (rest != null && rest.Value >= RestCategory.Exhausted)
            {
                return LeadRelease.Exhausted;
            }

            return LeadRelease.None;
        }

        /// <summary>
        /// Determines whether a drafted colonist is spared vanilla's automatic undraft.
        /// </summary>
        /// <param name="featureOn">Whether "Keep leads when drafted" is on.</param>
        /// <param name="ropeeCount">How many animals it is leading.</param>
        /// <param name="inVanillaFormingCaravan">Whether it belongs to a vanilla forming caravan.</param>
        /// <param name="stage">Its caravan's stage.</param>
        /// <returns><c>true</c> when it stays drafted.</returns>
        /// <remarks>
        /// <para>
        /// <c>AutoUndrafter</c> undrafts a colonist left standing for 10000 ticks with no enemy about.
        /// Where undrafting would drop the leads, while items, downed pawns or sleepers are waited for
        /// and in every pause before leaving, that undoes the player's staging, so a lead holder there
        /// is spared.
        /// </para>
        /// <para>
        /// Not while animals are gathered or collected: there undrafting carries on leading them as the
        /// caravan's own job, and a forgotten holder would otherwise hold the caravan up for good. Not
        /// while leaving or in the pause while leaving, where the caravan waits for every member at the
        /// exit; animals let go there that cannot reach the exit by themselves send the caravan back to
        /// collect them (<see cref="IsStrandedWhileLeaving"/>). Everywhere outside a forming caravan it
        /// is the only thing that stops a forgotten drafted colonist starving, so it stays.
        /// </para>
        /// </remarks>
        public static bool SuppressAutoUndraft(bool featureOn, int ropeeCount, bool inVanillaFormingCaravan, FormingStage stage)
        {
            return featureOn
                && ropeeCount > 0
                && inVanillaFormingCaravan
                && !VanillaResumesOnUndraft(stage)
                && FormingStages.WorkingStageOf(stage) != FormingStage.Leave;
        }

        /// <summary>
        /// Determines whether hungry or tired animals are let go at a stage.
        /// </summary>
        /// <param name="stage">The caravan's stage.</param>
        /// <returns><c>false</c> while the caravan is leaving.</returns>
        /// <remarks>
        /// A pen animal let go while leaving can end up shut in behind a door it cannot open. At best
        /// the caravan then goes back to collecting its animals, and every roper lets go and ropes its
        /// train again (<see cref="SendsBackToCollect"/>); at worst nobody can fetch it and the caravan
        /// waits at the exit. The player is walking the animal out by then, and can drop it by hand.
        /// </remarks>
        public static bool ReleasesNeedyAnimals(FormingStage stage)
        {
            return FormingStages.WorkingStageOf(stage) != FormingStage.Leave;
        }

        /// <summary>
        /// Determines whether an animal of a forming caravan is one a drafted colonist may take the lead of.
        /// </summary>
        /// <param name="needsRope">Whether it has to be led on a rope, <c>AnimalPenUtility.NeedsToBeManagedByRope</c>.</param>
        /// <param name="ropedByPawn">Whether somebody is already leading it.</param>
        /// <param name="downed">Whether it is downed.</param>
        /// <param name="brokenNotRoaming">Whether it is in a mental state other than roaming, which breaks its own rope.</param>
        /// <param name="needy">Whether it is urgently hungry or exhausted.</param>
        /// <param name="stage">Its caravan's stage.</param>
        /// <returns><c>true</c> when the option is offered.</returns>
        /// <remarks>
        /// <para>
        /// An animal that walks by itself must never be led: undrafted while animals gather, the
        /// colonist would tie it at the spot, and vanilla never unties an animal that does not need a
        /// rope, so the caravan would wait for it at the exit for good.
        /// </para>
        /// <para>
        /// A needy animal would be let go again at once, except while leaving and in the pause while
        /// leaving, where held animals are not let go for their needs (<see cref="ReleasesNeedyAnimals"/>).
        /// There it can be taken: an animal shut in somewhere is the one most likely to be starving, and
        /// leading it out is how the player gets it back. If the caravan then goes back to collecting,
        /// the pause's end or <see cref="SendsBackToCollect"/>, it is let go there like any other, and
        /// the caravan's own colonists rope it again.
        /// </para>
        /// </remarks>
        public static bool CanBeTaken(bool needsRope, bool ropedByPawn, bool downed, bool brokenNotRoaming, bool needy, FormingStage stage)
        {
            return needsRope && !ropedByPawn && !downed && !brokenNotRoaming && !(needy && ReleasesNeedyAnimals(stage));
        }

        /// <summary>
        /// Determines whether a leaving caravan watches for animals that cannot get to the exit.
        /// </summary>
        /// <param name="featureOn">Whether "Keep leads when drafted" is on.</param>
        /// <param name="stage">The caravan's stage.</param>
        /// <returns><c>true</c> while it is leaving, not in the pause.</returns>
        /// <remarks>
        /// Vanilla leaves the leaving stage only when every member is at the exit, an animal is added,
        /// a member is removed or somebody breaks, with no timeout, and letting go of an animal sends no
        /// signal. Once the caravan is leaving, a lead dropped by hand or by the automatic undraft can
        /// leave a pen animal shut in behind a door it cannot open, and the caravan waits for it at the
        /// exit for good. Vanilla's own way back is the signal it sends when a pen animal joins: the
        /// caravan goes back to collecting its animals, and the colonists fetch it. The pause while
        /// leaving already goes back to collecting when it ends.
        /// </remarks>
        public static bool WatchesForStrandedAnimals(bool featureOn, FormingStage stage)
        {
            return featureOn && stage == FormingStage.Leave;
        }

        /// <summary>
        /// Determines whether a member of a leaving caravan is a pen animal that will never reach the
        /// exit unless somebody fetches it.
        /// </summary>
        /// <param name="needsRope">Whether it has to be led on a rope, <c>AnimalPenUtility.NeedsToBeManagedByRope</c>.</param>
        /// <param name="spawned">Whether it is on the map.</param>
        /// <param name="ledByMember">Whether a member of its caravan is leading it, vanilla's <c>IsRopedByCaravanPawn</c>.</param>
        /// <param name="tiedElsewhere">Whether it is on some other rope: tied at a spot or held by an outsider.</param>
        /// <param name="canWalkToExit">Whether it can reach the exit by itself, as the leaving duty's own goto asks.</param>
        /// <returns><c>true</c> when it is stranded.</returns>
        /// <remarks>
        /// One that can walk there needs nothing: the leaving duty sends every member, animals too, to
        /// the exit. Going back to collect it would make every roper let go and rope its animals again.
        /// </remarks>
        public static bool IsStrandedWhileLeaving(bool needsRope, bool spawned, bool ledByMember, bool tiedElsewhere, bool canWalkToExit)
        {
            return needsRope && spawned && !ledByMember && (tiedElsewhere || !canWalkToExit);
        }

        /// <summary>
        /// Decides whether a leaving caravan goes back to collecting its animals.
        /// </summary>
        /// <param name="anyStranded">Whether any of its pen animals is stranded (<see cref="IsStrandedWhileLeaving"/>).</param>
        /// <param name="everyLooseOneFetchable">Whether every pen animal no member leads is one some colonist member would fetch.</param>
        /// <returns><c>true</c> when it goes back.</returns>
        /// <remarks>
        /// Collecting ends only once every pen animal is led by a member, so going back for one animal
        /// while another can never be fetched would only move the wait from the exit to the collecting
        /// stage. Then the caravan is left waiting where vanilla would have left it.
        /// </remarks>
        public static bool SendsBackToCollect(bool anyStranded, bool everyLooseOneFetchable)
        {
            return anyStranded && everyLooseOneFetchable;
        }

        /// <summary>
        /// Determines whether vanilla takes a colonist's leads back on when it is undrafted.
        /// </summary>
        /// <param name="stage">Its caravan's stage.</param>
        /// <returns><c>true</c> in the two stages whose duty gives a rope job that reuses held animals.</returns>
        /// <remarks>
        /// <c>JobGiver_PrepareCaravan_RopePawns</c> gives a roper already leading animals a rope job for
        /// the first of them, and the rope driver skips roping an animal already held. Only the gather
        /// and collect duties have that giver; anywhere else, pauses included, the leads drop on the
        /// next tick and the player is told.
        /// </remarks>
        public static bool VanillaResumesOnUndraft(FormingStage stage)
        {
            return stage == FormingStage.GatherAnimals || stage == FormingStage.CollectAnimals;
        }

        /// <summary>
        /// Decides what the drop-lead button does.
        /// </summary>
        /// <param name="heldAcrossSelection">How many animals the selected colonists hold between them.</param>
        /// <returns>The action.</returns>
        public static DropLeadAction DropLead(int heldAcrossSelection)
        {
            if (heldAcrossSelection <= 0)
            {
                return DropLeadAction.None;
            }

            return heldAcrossSelection == 1 ? DropLeadAction.DropDirectly : DropLeadAction.Menu;
        }

        /// <summary>
        /// Decides whether one selected colonist can take the lead of a caravan animal.
        /// </summary>
        /// <param name="sameCaravan">Whether the colonist is going with the animal's caravan.</param>
        /// <param name="hasRopeCell">Whether there is a cell next to the animal to tie a rope from.</param>
        /// <param name="canReserve">Whether the colonist can reserve the animal.</param>
        /// <param name="canReach">Whether the colonist can reach the animal.</param>
        /// <returns>The first thing in the way, or <see cref="TakeLeadBlocker.None"/>.</returns>
        /// <remarks>
        /// Only a member may hold a caravan animal: vanilla's collect stage counts an animal as led only
        /// when its roper is in the same caravan.
        /// </remarks>
        public static TakeLeadBlocker TakeLead(bool sameCaravan, bool hasRopeCell, bool canReserve, bool canReach)
        {
            if (!sameCaravan)
            {
                return TakeLeadBlocker.NotInCaravan;
            }

            if (!hasRopeCell)
            {
                return TakeLeadBlocker.NoRopeCell;
            }

            if (!canReserve)
            {
                return TakeLeadBlocker.Reserved;
            }

            return canReach ? TakeLeadBlocker.None : TakeLeadBlocker.Unreachable;
        }
    }
}
