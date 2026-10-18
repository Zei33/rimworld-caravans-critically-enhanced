using System;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// When a forming caravan moves its exit away from hostiles that turned up after it was chosen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exit is ranked by threat whenever one is chosen (<see cref="ExitPlanner"/>); this decides
    /// when an exit already chosen is given up for another. Only for a safe one: an exit with no
    /// hostile near it or its way, inside the areas when the area switch is on, so a small gain never
    /// moves the caravan and a move never fights the area rule. A caravan half of whose colonists are
    /// already at the exit is committed to it, except while downed pawns are gathered, when vanilla
    /// sends every colonist with nothing to carry to wait there. So is one waiting with a downed pawn
    /// already brought there, which a move would send carriers back into the threat to fetch, and one
    /// collecting or leaving with downed pawns or babies still to take along, since fetching them would
    /// send the animals back too. An hour's cooldown slows a raid that moves about.
    /// </para>
    /// <para>
    /// Pure, over plain readings.
    /// </para>
    /// </remarks>
    public static class ThreatPolicy
    {
        /// <summary>
        /// How long after one threat move the next may come, in ticks: an in-game hour.
        /// </summary>
        public const int CooldownTicks = 2500;

        /// <summary>
        /// How near its exit, in cells, a member counts as there for the commit rule.
        /// </summary>
        public const float CommitRadius = 15f;

        /// <summary>
        /// How much farther than the threat distances the safe exits are ranked by for a move, so that a
        /// move goes to an exit clear of the wider distances where there is one, and otherwise to the
        /// one least inside them, rather than to the nearest cell just outside the danger, which the
        /// next steps of a raid would reach.
        /// </summary>
        public const float ClearanceFactor = 1.5f;

        /// <summary>
        /// Widens a threat distance for ranking where to move to.
        /// </summary>
        /// <param name="radius">The distance the switch sets, in cells.</param>
        /// <returns>The distance with room to spare.</returns>
        public static int Clearance(int radius)
        {
            return (int)Math.Ceiling(radius * ClearanceFactor);
        }

        /// <summary>
        /// Determines whether a stage is one the exit may be moved in: every working stage, never a
        /// pause, which a mental break holds.
        /// </summary>
        /// <param name="stage">The stage.</param>
        /// <returns><c>true</c> for gathering animals, items and downed pawns, waiting, collecting and leaving.</returns>
        public static bool Movable(FormingStage stage)
        {
            switch (stage)
            {
                case FormingStage.GatherAnimals:
                case FormingStage.GatherItems:
                case FormingStage.GatherDownedPawns:
                case FormingStage.Wait:
                case FormingStage.CollectAnimals:
                case FormingStage.Leave:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether a caravan is committed to its exit.
        /// </summary>
        /// <param name="nearColonists">How many colonist members are within <see cref="CommitRadius"/> of the exit; ignored while downed pawns are gathered.</param>
        /// <param name="colonists">How many colonist members there are, drafted ones included.</param>
        /// <param name="stage">Its stage.</param>
        /// <param name="downedToTake">How many listed downed pawns or babies are not in a drafted member's arms.</param>
        /// <param name="downedAtExit">How many listed downed pawns are within vanilla's 7 cells of the exit and not in a drafted member's arms.</param>
        /// <returns><c>true</c> when committed.</returns>
        /// <remarks>
        /// Colonists only: pets and prisoners walk to the exit long before the caravan is ready. The
        /// colonists are ignored while downed pawns are gathered: that stage's duty walks every colonist
        /// with nothing to carry to the exit to wait, so standing there says nothing of how far along
        /// the caravan is. A move then has the pawns already brought to the old exit carried again,
        /// mostly by those same colonists, who stand beside them and take the nearest first; the
        /// caravan accepts that, and in the one case where it costs most, a caravan with no pen animals
        /// whose last carry is nearly done, a move still sends it the long way round. While waiting,
        /// everybody is back at the meeting point, so a downed pawn already at the exit would be
        /// fetched back out of it by somebody walking into the threat; the caravan accepts walking out
        /// past the raid later instead, by when raiders have usually moved on toward the colony. And
        /// from collecting on any downed pawn to take along commits the caravan: the route back to
        /// gather it would send the animals back to the colony too, and a rope job drops whatever its
        /// roper carries. A pawn at the exit is one to take, so there the last count covers the one
        /// before.
        /// </remarks>
        public static bool Committed(int nearColonists, int colonists, FormingStage stage, int downedToTake, int downedAtExit)
        {
            return (stage != FormingStage.GatherDownedPawns && colonists > 0 && nearColonists * 2 >= colonists)
                || (stage == FormingStage.Wait && downedAtExit > 0)
                || (ReroutesAfterMove(stage) && downedToTake > 0);
        }

        /// <summary>
        /// Determines whether the last threat move was too recent for another.
        /// </summary>
        /// <param name="now">The tick now.</param>
        /// <param name="last">The tick of the last threat move, or -1 for none.</param>
        /// <returns><c>true</c> while cooling down.</returns>
        public static bool Cooling(int now, int last)
        {
            return last >= 0 && now - last < CooldownTicks;
        }

        /// <summary>
        /// Decides whether the caravan moves its exit away from a threat.
        /// </summary>
        /// <param name="featureOn">Whether "Avoid threats when leaving" is on.</param>
        /// <param name="foreign">Whether another mod owns the exit.</param>
        /// <param name="playerOverride">Whether the player chose the departure (Force depart, Leave from here).</param>
        /// <param name="holding">Whether the caravan is held for want of an exit, where the area rule decides.</param>
        /// <param name="stage">Its stage.</param>
        /// <param name="currentScore">The threat score of the exit it has.</param>
        /// <param name="safeExitOpen">Whether a valid exit with no threat is open to it (<see cref="ExitPlan.HasSafeExit"/>); with the current exit threatened, that is another exit.</param>
        /// <param name="committed">Whether it is committed to its exit (<see cref="Committed"/>).</param>
        /// <param name="cooling">Whether its last threat move was too recent.</param>
        /// <returns><c>true</c> to move.</returns>
        public static bool Moves(bool featureOn, bool foreign, bool playerOverride, bool holding, FormingStage stage, float currentScore, bool safeExitOpen, bool committed, bool cooling)
        {
            return featureOn && !foreign && !playerOverride && !holding && Movable(stage)
                && currentScore > 0f && safeExitOpen
                && !committed && !cooling;
        }

        /// <summary>
        /// Determines whether a move in a stage has to send the caravan by another route as well, and
        /// whether the stage measures the way out from where the colonists are rather than from the
        /// meeting point.
        /// </summary>
        /// <param name="stage">The stage.</param>
        /// <returns><c>true</c> for collecting and leaving, whose rope jobs keep the old destination and whose members are on their way.</returns>
        public static bool ReroutesAfterMove(FormingStage stage)
        {
            return stage == FormingStage.CollectAnimals || stage == FormingStage.Leave;
        }
    }
}
