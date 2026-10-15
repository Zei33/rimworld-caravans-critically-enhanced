using HarmonyLib;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Fixes vanilla leaving an animal on its old roper's list when somebody else ropes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Pawn_RopeTracker.CreateRope</c> overwrites the animal's <c>ropedTo</c> without telling its
    /// previous roper, which keeps the animal in its <c>ropees</c>. That roper's next
    /// <c>DropRope</c> or <c>DropRopes</c> then cuts the animal loose from its new roper. Vanilla's
    /// drivers keep this rare: they reserve an animal before roping it and let go of it before tying
    /// it at a spot. But the roping toil never asks whether somebody else holds the animal, and a
    /// drafted colonist holding animals has no reservation on them, since drafting ended the job that
    /// made it. So the prefix lets the old roper go first. Calling it twice for the same roper and
    /// animal would add the animal to the list twice, so that is skipped.
    /// </para>
    /// <para>
    /// An animal tied at a spot has no pawn roper, so there is nothing to do for it here; vanilla
    /// unties it itself.
    /// </para>
    /// <para>
    /// Always on, whatever the settings: it fixes vanilla, and the mod's other rope patches depend on
    /// the lists being right.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(Pawn_RopeTracker), "CreateRope")]
    public static class Patch_Pawn_RopeTracker_CreateRope
    {
        /// <summary>
        /// Lets the previous roper go of the animal first.
        /// </summary>
        /// <param name="roperTarget">The new roper, or the spot.</param>
        /// <param name="ropee">The animal.</param>
        /// <returns><c>false</c> when the same roper already holds the animal.</returns>
        public static bool Prefix(LocalTargetInfo roperTarget, Pawn ropee)
        {
            Pawn previous = ropee?.roping?.RopedByPawn;
            if (previous == null)
            {
                return true;
            }

            if (previous == roperTarget.Thing)
            {
                return !previous.roping.Ropees.Contains(ropee);
            }

            previous.roping.DropRope(ropee);
            return true;
        }
    }
}
