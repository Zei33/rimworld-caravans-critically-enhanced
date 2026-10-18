namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// What the exit pass does to a forming caravan.
    /// </summary>
    public enum ExitAction
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>Hold the caravan at its spot, with the fallback exit written for vanilla.</summary>
        Park,

        /// <summary>Stop holding and leave by the exit the plan chose.</summary>
        ReleaseToPlan,

        /// <summary>Stop holding and leave by the exit already written, the fallback.</summary>
        ReleaseInPlace,

        /// <summary>Move to the exit the plan chose, because an allowed area no longer holds the one it has.</summary>
        Retarget,

        /// <summary>Move to the exit the plan chose, quietly, because nobody can reach the one it has any more.</summary>
        Reroute,

        /// <summary>Stay held, with the plan's newer fallback written for vanilla, quietly.</summary>
        MoveFallback,
    }

    /// <summary>
    /// What a waiting caravan does about downed pawns the mod's exit write left away from its exit.
    /// </summary>
    public enum RegatherStep
    {
        /// <summary>Nothing is pending.</summary>
        Idle,

        /// <summary>Stop asking: nobody is left away from the exit, or the caravan has moved on.</summary>
        Forget,

        /// <summary>Go back to gathering downed pawns.</summary>
        GoBack,

        /// <summary>Some cannot be fetched yet; ask again on the next pass.</summary>
        Retry,
    }

    /// <summary>
    /// Which pawns an allowed area restricts, and what the exit pass does about them.
    /// </summary>
    public static class ExitPolicy
    {
        /// <summary>
        /// Determines whether a pawn's allowed area restricts where its caravan may leave.
        /// </summary>
        /// <param name="hasSettings">Whether it has player settings at all.</param>
        /// <param name="supportsAreas">Whether it supports allowed areas: not a roamer, and its race does not disable them.</param>
        /// <param name="playerFaction">Whether it belongs to the player.</param>
        /// <param name="hostFactionNull">Whether it has no host faction, so it is no guest or prisoner.</param>
        /// <param name="mutantIgnoresAreas">Whether it is a mutant whose kind does not respect allowed areas.</param>
        /// <param name="isMech">Whether it is a mechanoid.</param>
        /// <param name="hasOverseer">Whether it has an overseer, for a mechanoid.</param>
        /// <param name="hasArea">Whether an area is set for it on this map.</param>
        /// <param name="areaCells">How many cells that area holds.</param>
        /// <returns><c>true</c> when the exit has to be inside its area.</returns>
        /// <remarks>
        /// <para>
        /// A pawn counts only where the player can see and change its area: vanilla's inspect pane
        /// (<c>InspectPaneFiller.DrawAreaAllowed</c>) and the Assign tab
        /// (<c>PawnColumnWorker_AllowedArea</c>) disagree about guests and overseen mechanoids, so a
        /// pawn either of them hides is left out. An area with no cells restricts nothing, as in
        /// <c>ForbidUtility.InAllowedArea</c>.
        /// </para>
        /// <para>
        /// Read from <c>AreaRestrictionInPawnCurrentMap</c>, not the effective area, which is null for
        /// every pawn in a lord.
        /// </para>
        /// </remarks>
        public static bool IsRestricted(bool hasSettings, bool supportsAreas, bool playerFaction, bool hostFactionNull, bool mutantIgnoresAreas, bool isMech, bool hasOverseer, bool hasArea, int areaCells)
        {
            bool paneShows = hasSettings && supportsAreas && playerFaction && hostFactionNull && !mutantIgnoresAreas;
            bool columnShows = playerFaction && supportsAreas && !mutantIgnoresAreas && (!isMech || hasOverseer);
            return paneShows && columnShows && hasArea && areaCells > 0;
        }

        /// <summary>
        /// Determines whether the area rule still applies at a stage.
        /// </summary>
        /// <param name="stage">The caravan's stage.</param>
        /// <returns><c>true</c> before animal collection starts.</returns>
        /// <remarks>
        /// Matthew's decision of 2026-10-16: once the caravan starts collecting its animals the exit it
        /// has stands, even if an area changes, so no caravan is ever held mid-departure, where vanilla
        /// offers no Force depart.
        /// </remarks>
        public static bool AreaRuleApplies(FormingStage stage)
        {
            switch (FormingStages.WorkingStageOf(stage))
            {
                case FormingStage.GatherAnimals:
                case FormingStage.GatherItems:
                case FormingStage.GatherDownedPawns:
                case FormingStage.Wait:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Decides what the exit pass does.
        /// </summary>
        /// <param name="featureOn">Whether "Leave inside allowed areas" is on.</param>
        /// <param name="stage">The caravan's stage.</param>
        /// <param name="holding">Whether it is being held.</param>
        /// <param name="foreign">Whether another mod has written its exit, so the mod keeps out.</param>
        /// <param name="playerOverride">Whether the player sent it off regardless (Force depart).</param>
        /// <param name="blocker">Why the plan found no exit, or <see cref="ExitBlocker.None"/>.</param>
        /// <param name="currentValid">Whether the exit it has is still valid.</param>
        /// <param name="currentReachable">Whether every mover that reaches an edge can still reach the exit it has, areas aside.</param>
        /// <param name="fallbackMoved">Whether the plan's best exit ignoring areas differs from the one written.</param>
        /// <returns>The action.</returns>
        /// <remarks>
        /// A held caravan keeps its fallback current, so that Force depart, the switch going off or the
        /// mod being removed send it by an exit it can still reach. A move away from an exit nobody can
        /// reach any more (a wall, a tree, fog) is the area rule's business only in that it has to land
        /// inside the areas, so it is made without the message that blames the areas.
        /// </remarks>
        public static ExitAction Decide(bool featureOn, FormingStage stage, bool holding, bool foreign, bool playerOverride, ExitBlocker blocker, bool currentValid, bool currentReachable, bool fallbackMoved)
        {
            bool applies = featureOn && !foreign && !playerOverride && AreaRuleApplies(stage);
            if (holding)
            {
                if (!applies)
                {
                    return ExitAction.ReleaseInPlace;
                }

                if (blocker == ExitBlocker.None)
                {
                    return ExitAction.ReleaseToPlan;
                }

                bool areas = blocker == ExitBlocker.AreaExcludesReachableEdges || blocker == ExitBlocker.AreasDisjoint;
                return fallbackMoved && areas ? ExitAction.MoveFallback : ExitAction.None;
            }

            if (!applies || currentValid)
            {
                return ExitAction.None;
            }

            switch (blocker)
            {
                case ExitBlocker.None:
                    return currentReachable ? ExitAction.Retarget : ExitAction.Reroute;
                case ExitBlocker.AreaExcludesReachableEdges:
                case ExitBlocker.AreasDisjoint:
                    return ExitAction.Park;
                default:
                    // Nobody can reach any edge: vanilla would be stuck too, and holding helps nobody.
                    return ExitAction.None;
            }
        }

        /// <summary>
        /// Determines whether a pass's action wrote the exit.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns><c>true</c> for every action but <see cref="ExitAction.None"/>.</returns>
        public static bool WritesExit(ExitAction action)
        {
            return action != ExitAction.None;
        }

        /// <summary>
        /// Decides what a waiting caravan does about downed pawns away from its exit.
        /// </summary>
        /// <param name="pending">Whether the mod has written the exit while the caravan waited and not yet settled the downed pawns since.</param>
        /// <param name="waiting">Whether the caravan is in the waiting stage or its pause.</param>
        /// <param name="strays">How many listed downed pawns, babies included, lie more than vanilla's 7 cells from the exit it will leave by and are not held by a member who works no duties and is not in a mental state, a drafted one say.</param>
        /// <param name="unfetchableStrays">How many of those no member can reach now.</param>
        /// <returns>The step.</returns>
        /// <remarks>
        /// Vanilla carries downed pawns only in the stage before waiting, and sends only those near the
        /// exit, so once the mod writes the exit while the caravan waits they could be left. Only then:
        /// going back ends in vanilla's step forward again, whose job ending can wake the sleeper the
        /// caravan waits for, so a caravan whose exit the mod left alone is never sent back. Babies
        /// count, since one member picks up only one baby while collecting and leaving. One that cannot
        /// be reached now (a baby being fed by a colonist not going, a pawn behind a door being built)
        /// is asked about again on later passes, for as long as the caravan waits; one never reachable
        /// would keep the gathering stage open for good, so it is never gone back for.
        /// </remarks>
        public static RegatherStep Regather(bool pending, bool waiting, int strays, int unfetchableStrays)
        {
            if (!pending)
            {
                return RegatherStep.Idle;
            }

            if (!waiting || strays == 0)
            {
                return RegatherStep.Forget;
            }

            return unfetchableStrays == 0 ? RegatherStep.GoBack : RegatherStep.Retry;
        }
    }
}
