namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Whether a pawn gets the Leave from here button, and in what state.
    /// </summary>
    public enum LeaveButton
    {
        /// <summary>No button.</summary>
        Hidden,

        /// <summary>The button, greyed out: the pawn stands too far from a map edge.</summary>
        TooFar,

        /// <summary>The button, ready.</summary>
        Enabled,
    }

    /// <summary>
    /// Which stage a caravan sent off from here goes to.
    /// </summary>
    public enum LeaveRoute
    {
        /// <summary>None: the caravan is not forming any more.</summary>
        Refuse,

        /// <summary>Gathering downed pawns, which carries them to the new exit, then on as vanilla goes.</summary>
        DownedStage,

        /// <summary>Collecting animals, which ropes them to the new exit, then leaving.</summary>
        CollectStage,

        /// <summary>Leaving, where every member walks to the new exit: nothing is left to fetch or rope, or nobody undrafted is left to do it.</summary>
        LeaveStage,
    }

    /// <summary>
    /// The rules of Leave from here: a drafted colonist near a map edge sends its forming caravan off
    /// from that edge now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's Force depart sends the caravan to its leaving stage with a memo, which skips the
    /// stage that gathers downed pawns, clears the list of them, and undrafts everyone. Leave from here
    /// keeps the list and the drafts and goes, by <c>Lord.GotoToil</c>, to whichever of vanilla's own
    /// stages does what is still needed: gathering downed pawns when some lie away from the new exit
    /// and can be fetched, collecting animals when a pen animal is not yet on a member's lead, and
    /// otherwise leaving. Only an undrafted colonist with hands works a stage's duties (a drafted one
    /// takes orders, above the duty), so with none left the caravan goes straight to leaving, as
    /// vanilla's Force depart does, and what nobody can fetch is left. Collecting jobs already under
    /// way keep their old destination and never end by themselves, so the undrafted members' jobs are
    /// ended, as a stage change does.
    /// </para>
    /// <para>
    /// Pure, over plain readings.
    /// </para>
    /// </remarks>
    public static class LeaveFromHerePolicy
    {
        /// <summary>
        /// How close to a map edge, in cells, a pawn has to stand for the button to work.
        /// </summary>
        public const int EdgeBand = 10;

        /// <summary>
        /// How far from the colonist whose button was used, in cells, an edge cell may be to leave by.
        /// </summary>
        public const int SpotRadius = 12;

        /// <summary>
        /// How far from the exit, in cells, a drafted member is ordered over: vanilla's leaving stage
        /// waits for every member within 10 cells, and the downed stage counts a carried pawn by its
        /// carrier's cell, within 7.
        /// </summary>
        public const float OrderOverBeyond = 7f;

        /// <summary>
        /// How close to the exit drafted members are sent, in cells.
        /// </summary>
        public const int GatherRadius = 4;

        /// <summary>
        /// Decides whether a pawn gets the button.
        /// </summary>
        /// <param name="featureOn">Whether "Leave from here" is on.</param>
        /// <param name="member">Whether the pawn is a member of a vanilla forming caravan whose exit the mod can write.</param>
        /// <param name="drafted">Whether it is drafted.</param>
        /// <param name="controlled">Whether the player controls it as a colonist, and it is on the map.</param>
        /// <param name="stage">Its caravan's stage.</param>
        /// <param name="edgeDistance">How many cells it stands from the nearest map edge.</param>
        /// <returns>The button's state.</returns>
        public static LeaveButton Button(bool featureOn, bool member, bool drafted, bool controlled, FormingStage stage, int edgeDistance)
        {
            if (!featureOn || !member || !drafted || !controlled || RouteFor(stage, false, false, false, false) == LeaveRoute.Refuse)
            {
                return LeaveButton.Hidden;
            }

            return edgeDistance <= EdgeBand ? LeaveButton.Enabled : LeaveButton.TooFar;
        }

        /// <summary>
        /// Decides which stage the caravan goes to.
        /// </summary>
        /// <param name="stage">Its stage now.</param>
        /// <param name="fetchableStrays">Whether some listed downed pawn lies away from the new exit and an undrafted member holds it or can fetch it.</param>
        /// <param name="needsRoping">Whether some pen animal going is not on a drafted member's lead (an undrafted roper's lead drops when its job is ended).</param>
        /// <param name="anyWorker">Whether some undrafted colonist member with hands is left to do the work.</param>
        /// <param name="penned">Whether some of those pen animals cannot walk to the exit by themselves, fenced in.</param>
        /// <returns>The route.</returns>
        /// <remarks>
        /// With nobody to rope a fenced-in animal, collecting waits until somebody is undrafted, which
        /// is still better than leaving, where it would wait at the exit with nothing to call the
        /// caravan back unless "Keep leads when drafted" is on.
        /// </remarks>
        public static LeaveRoute RouteFor(FormingStage stage, bool fetchableStrays, bool needsRoping, bool anyWorker, bool penned)
        {
            if (stage < FormingStage.GatherAnimals || stage > FormingStage.LeavePause)
            {
                return LeaveRoute.Refuse;
            }

            if (anyWorker && fetchableStrays)
            {
                return LeaveRoute.DownedStage;
            }

            return needsRoping && (anyWorker || penned) ? LeaveRoute.CollectStage : LeaveRoute.LeaveStage;
        }

        /// <summary>
        /// Determines whether an edge cell is near enough to the colonist whose button was used.
        /// </summary>
        /// <param name="x">The cell's x.</param>
        /// <param name="z">The cell's z.</param>
        /// <param name="centreX">The colonist's x.</param>
        /// <param name="centreZ">The colonist's z.</param>
        /// <returns><c>true</c> within <see cref="SpotRadius"/>.</returns>
        public static bool Near(int x, int z, int centreX, int centreZ)
        {
            long dx = x - centreX;
            long dz = z - centreZ;
            return (dx * dx) + (dz * dz) <= (long)SpotRadius * SpotRadius;
        }

        /// <summary>
        /// Determines whether vanilla's Force depart leaves a listed downed pawn behind.
        /// </summary>
        /// <param name="gone">Whether it is destroyed or no longer downed, so vanilla's lord tick takes it off the list anyway.</param>
        /// <param name="carriedByWorker">Whether an undrafted member not in a mental state carries it, so it goes with its carrier.</param>
        /// <returns><c>true</c> when it is left.</returns>
        /// <remarks>
        /// Force depart clears the caravan's list of downed pawns, so every one not in a member's arms
        /// stays, babies included. It also removes members in a mental state, and undrafts the drafted:
        /// undrafting drops what a colonist carries, or starts carrying a baby to safety, which the
        /// departure's job ending then drops. So only an undrafted, unbroken carrier takes its pawn along.
        /// </remarks>
        public static bool LeftByForceDepart(bool gone, bool carriedByWorker)
        {
            return !gone && !carriedByWorker;
        }
    }
}
