using System;
using System.Collections.Generic;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// Builds the Load work giver's search set from the caravans' item lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The set has three rules, and each has a reason in vanilla's code.
    /// </para>
    /// <para>
    /// <b>No nulls.</b> <c>GenClosest.ClosestThing_Global</c> dereferences each entry, and
    /// <c>JobGiver_Work</c> catches the exception with <c>Log.Error</c>, not <c>Log.ErrorOnce</c>, so a
    /// null costs a red line on every job search of every pawn and the giver yields nothing.
    /// </para>
    /// <para>
    /// <b>Each thing once, owned by the first caravan that lists it.</b> Nothing stops two forming
    /// caravans listing the same stack: <c>CaravanFormingUtility.AllReachableColonyItems</c> filters
    /// only on reservations. <c>HasJobOnThing</c> and <c>JobOnThing</c> must agree about which caravan a
    /// thing is for, or <c>JobGiver_Work</c> abandons the pawn's whole work search on the shared
    /// <c>Log.ErrorOnce</c> key. Resolving owners here, once, in the lord manager's order, makes the
    /// answer the same for both calls.
    /// </para>
    /// <para>
    /// <b>A <c>List&lt;Thing&gt;</c>.</b> <c>ClosestThing_Global</c> takes an <c>IList&lt;Thing&gt;</c>
    /// fast path for a count and an indexed loop, which a lazy sequence or a <c>HashSet</c> misses.
    /// </para>
    /// <para>
    /// Generic so a test can run it over plain objects; the game runs it over <c>Thing</c>.
    /// </para>
    /// </remarks>
    public static class LoadingCandidates
    {
        /// <summary>
        /// Adds one list row's things to the search set.
        /// </summary>
        /// <typeparam name="TOwner">What a thing is claimed by: a caravan and its row in the game.</typeparam>
        /// <typeparam name="TThing">The things.</typeparam>
        /// <param name="owner">The caravan and row these things are listed in.</param>
        /// <param name="things">The row's things, which may contain nulls.</param>
        /// <param name="eligible">Whether a thing can be fetched at all: on this map, not destroyed.</param>
        /// <param name="into">The search set being built.</param>
        /// <param name="owners">Which caravan each thing in the set belongs to.</param>
        public static void Collect<TOwner, TThing>(
            TOwner owner,
            IList<TThing> things,
            Func<TThing, bool> eligible,
            List<TThing> into,
            Dictionary<TThing, TOwner> owners)
            where TThing : class
        {
            if (things == null)
            {
                return;
            }

            for (int i = 0; i < things.Count; i++)
            {
                TThing thing = things[i];
                if (thing == null || owners.ContainsKey(thing) || !eligible(thing))
                {
                    continue;
                }

                owners.Add(thing, owner);
                into.Add(thing);
            }
        }
    }
}
