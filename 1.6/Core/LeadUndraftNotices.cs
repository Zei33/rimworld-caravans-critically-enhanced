using System;
using System.Collections.Generic;
using Verse;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The colonists just undrafted whose leads are about to drop, waiting to be told about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The draft setter's finalizer cannot tell an undraft by a mental break from any other:
    /// <c>MentalStateHandler.TryStartMentalState</c> undrafts the pawn before it records the break,
    /// so <c>InMentalState</c> is still false inside the setter. The leads drop on the pawn's next rope
    /// tick, by which time the break is recorded, so the message waits for that tick and is skipped
    /// for a break, whose own letter says what happened.
    /// </para>
    /// <para>
    /// A notice is good for <see cref="ExpiryTicks"/> game ticks, so one whose rope tick never comes
    /// (the leads were dropped some other way first) can never be shown later for something else. A
    /// tick earlier than the notice, after loading an older save, expires it too.
    /// </para>
    /// <para>
    /// The pawns are held weakly. Nothing clears a static on load, so a notice left when the player
    /// loads another save would otherwise keep that game's pawn, and everything it references, alive
    /// until the next undraft or rope tick that happens to prune it.
    /// </para>
    /// </remarks>
    public static class LeadUndraftNotices
    {
        /// <summary>
        /// How many game ticks a notice waits for its rope tick.
        /// </summary>
        public const int ExpiryTicks = 10;

        private static readonly List<WeakReference> Pawns = new List<WeakReference>();

        private static readonly List<int> Ticks = new List<int>();

        /// <summary>
        /// Gets the number of notices waiting, which the rope tick reads before anything else.
        /// </summary>
        public static int Count => Pawns.Count;

        /// <summary>
        /// Records that a pawn's leads will drop on its next rope tick.
        /// </summary>
        /// <param name="pawn">The pawn just undrafted.</param>
        /// <param name="now">The current game tick.</param>
        public static void Expect(Pawn pawn, int now)
        {
            Take(pawn, now);
            Pawns.Add(new WeakReference(pawn));
            Ticks.Add(now);
        }

        /// <summary>
        /// Takes the notice waiting for a pawn, and drops every notice that has expired.
        /// </summary>
        /// <param name="pawn">The pawn whose rope tick this is.</param>
        /// <param name="now">The current game tick.</param>
        /// <returns><c>true</c> when a fresh notice was waiting for it.</returns>
        public static bool Take(Pawn pawn, int now)
        {
            bool found = false;
            for (int i = Pawns.Count - 1; i >= 0; i--)
            {
                int age = now - Ticks[i];
                object held = Pawns[i].Target;
                bool expired = held == null || age < 0 || age > ExpiryTicks;
                bool mine = ReferenceEquals(held, pawn);
                if (expired || mine)
                {
                    found |= mine && !expired;
                    Pawns.RemoveAt(i);
                    Ticks.RemoveAt(i);
                }
            }

            return found;
        }
    }
}
