namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Decides what a pawn's Load priority should be when a save is loaded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A work type added to an existing save arrives switched off. <c>Pawn_WorkSettings</c> keeps
    /// priorities in a <c>DefMap</c>, which is positional by <c>def.index</c>: on load it pads a def it
    /// has not seen with 0 and trims extras from the end, and nothing re-runs
    /// <c>EnableAndInitialize</c> for a pawn that already has settings. Without this, adding the mod to a
    /// running colony would leave the Load column empty and stop all outside help, which vanilla
    /// Hauling used to give.
    /// </para>
    /// <para>
    /// Being positional also means adding a work type moves every later one. A def's index is its
    /// place in load order, so when this mod's work type arrives in a save made without it, each work
    /// type from a mod loaded after this one reads the value its predecessor saved, and the last is
    /// padded with 0. <see cref="MakeRoom"/> moves those values back to their own work types on the
    /// first load, before anything reads them, which also means the hauling priority copied below is
    /// the right one when the helper's work type is FSF Complex Jobs' Transport and that mod loads
    /// later. Removing this mod shifts them back the other way; vanilla offers no hook for that.
    /// </para>
    /// <para>
    /// The copy is unconditional on the first load, not "only when Load is 0", so whatever the slot
    /// held, it ends up with the hauling priority.
    /// </para>
    /// <para>
    /// Colony mechs are raised again on every load while their Load priority is 0, as Simple Improve
    /// does for its own work type. The Work tab lists colonists only, so a player has no way to set a
    /// mech's priority, and a 0 there is never a choice somebody made.
    /// </para>
    /// </remarks>
    public static class LoadingMigration
    {
        /// <summary>
        /// Moves a save's priorities back to the work types they belong to, after this mod's work
        /// type has been inserted among them.
        /// </summary>
        /// <param name="values">The priorities as loaded, positional by work type index and already padded to the current count.</param>
        /// <param name="slot">The index of this mod's work type.</param>
        /// <returns>
        /// A new array: unchanged before <paramref name="slot"/>, 0 at it, and every value after it
        /// moved one place on, dropping the padding at the end. A slot outside the array changes nothing.
        /// </returns>
        /// <remarks>
        /// Right only for a save made without this mod and with every other work type unchanged, which
        /// is the first load after adding it. A work type mod added or removed in the same session
        /// stays shifted, as it would in vanilla.
        /// </remarks>
        public static int[] MakeRoom(int[] values, int slot)
        {
            var result = new int[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (slot < 0 || slot >= values.Length || i < slot)
                {
                    result[i] = values[i];
                }
                else
                {
                    result[i] = i == slot ? 0 : values[i - 1];
                }
            }

            return result;
        }

        /// <summary>
        /// Decides the Load priority to set, if any.
        /// </summary>
        /// <param name="migratedBefore">Whether this pawn's work settings were saved by this mod before.</param>
        /// <param name="loadingDisabled">Whether the pawn is incapable of Load, by backstory, trait, health or mech whitelist.</param>
        /// <param name="isColonyMech">Whether the pawn is a player mechanoid.</param>
        /// <param name="currentLoading">The pawn's stored Load priority.</param>
        /// <param name="sourcePriority">The stored priority of the work type vanilla's caravan helper sits in.</param>
        /// <returns>The priority to set, or <c>null</c> to leave the pawn alone.</returns>
        public static int? Decide(bool migratedBefore, bool loadingDisabled, bool isColonyMech, int currentLoading, int sourcePriority)
        {
            if (loadingDisabled)
            {
                return null;
            }

            if (!migratedBefore)
            {
                return currentLoading == sourcePriority ? (int?)null : sourcePriority;
            }

            if (isColonyMech && currentLoading == 0 && sourcePriority > 0)
            {
                return sourcePriority;
            }

            return null;
        }
    }
}
