using System.Collections.Generic;
using Verse;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The pawns whose ropes must not be dropped while something vanilla does to them runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drafting ends the current job, and a rope job's finish action, added by
    /// <c>JobDriver_RopeToDestination.MakeNewToils</c>, drops every rope the pawn holds. The draft
    /// setter's patch puts the pawn in this scope for the length of the setter, and the patch on
    /// <c>Pawn_RopeTracker.DropRopes</c> refuses while the pawn is in it and spawned.
    /// </para>
    /// <para>
    /// A list rather than a set, because scopes nest: another mod drafting a second pawn from inside
    /// the setter must not end the first pawn's scope. A pawn is pushed once per entry and popped
    /// once per exit, in a <c>finally</c>-style finalizer, so an exception cannot leave it in.
    /// </para>
    /// </remarks>
    public static class LeadKeepScope
    {
        private static readonly List<Pawn> Kept = new List<Pawn>();

        /// <summary>
        /// Gets the number of open scopes.
        /// </summary>
        public static int Depth => Kept.Count;

        /// <summary>
        /// Opens a scope for a pawn.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        public static void Push(Pawn pawn)
        {
            Kept.Add(pawn);
        }

        /// <summary>
        /// Closes the most recent scope opened for a pawn.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <returns><c>true</c> when a scope for it was open.</returns>
        public static bool Pop(Pawn pawn)
        {
            for (int i = Kept.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Kept[i], pawn))
                {
                    Kept.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a pawn is in an open scope.
        /// </summary>
        /// <param name="pawn">The pawn.</param>
        /// <returns><c>true</c> when it is.</returns>
        /// <remarks>
        /// Called on every <c>DropRopes</c>, which almost always finds the list empty. Compared by
        /// reference: <c>Pawn</c> implements <c>IEquatable</c> by comparing def names as strings and
        /// then thing ids, which is slower and not the question asked here.
        /// </remarks>
        public static bool Contains(Pawn pawn)
        {
            for (int i = 0; i < Kept.Count; i++)
            {
                if (ReferenceEquals(Kept[i], pawn))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
