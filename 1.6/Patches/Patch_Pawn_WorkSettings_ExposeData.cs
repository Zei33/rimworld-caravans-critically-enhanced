using System.Runtime.CompilerServices;
using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Gives every pawn in an existing save a Load priority, once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Saving writes a flag, <c>cceLoadingInit</c>, inside each pawn's <c>workSettings</c> node. A load
    /// that does not find it is the first load with this mod, and copies the priority of the work type
    /// vanilla's caravan helper sits in, which is Hauling in vanilla and Transport under FSF Complex
    /// Jobs. <see cref="LoadingMigration"/> decides; see there for why the copy is unconditional.
    /// </para>
    /// <para>
    /// The copy happens in <c>PostLoadInit</c>, after vanilla's own body has switched off every work
    /// type the pawn cannot do, so a pawn incapable of Load is never handed a priority, which would log
    /// a red error from <c>SetPriority</c>. The raw stored value is read rather than <c>GetPriority</c>,
    /// which answers 3 for any non-zero value while manual priorities are off and would lose the
    /// player's numbers for when they turn them back on.
    /// </para>
    /// <para>
    /// The same first load also realigns the saved priorities of work types from mods loaded after
    /// this one, which adding a work type shifts by one place; <see cref="LoadingMigration"/> says how.
    /// </para>
    /// <para>
    /// The flag is remembered between the two passes in a <see cref="ConditionalWeakTable{TKey, TValue}"/>
    /// keyed on the work settings object. Vanilla ignores the unknown node if the mod is removed.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.ExposeData))]
    public static class Patch_Pawn_WorkSettings_ExposeData
    {
        /// <summary>
        /// The node name of the flag. Never rename it: a renamed flag reads as absent, and every pawn's
        /// Load priority would be copied from Hauling again on the next load.
        /// </summary>
        public const string FlagNode = "cceLoadingInit";

        /// <summary>
        /// The defName of vanilla's caravan helper, whose work type is the one copied.
        /// </summary>
        public const string VanillaHelperGiver = "HelpGatheringItemsForCaravan";

        private static readonly ConditionalWeakTable<Pawn_WorkSettings, object> MigratedBefore =
            new ConditionalWeakTable<Pawn_WorkSettings, object>();

        /// <summary>
        /// Writes the flag, reads it, and applies the migration.
        /// </summary>
        /// <param name="__instance">The work settings.</param>
        /// <param name="___pawn">Their pawn.</param>
        /// <param name="___priorities">The raw priorities, positional by work type.</param>
        public static void Postfix(Pawn_WorkSettings __instance, Pawn ___pawn, DefMap<WorkTypeDef, int> ___priorities)
        {
            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:
                    if (___priorities != null)
                    {
                        bool migrated = true;
                        Scribe_Values.Look(ref migrated, FlagNode);
                    }

                    break;

                case LoadSaveMode.LoadingVars:
                    bool seen = false;
                    Scribe_Values.Look(ref seen, FlagNode);
                    MigratedBefore.Remove(__instance);
                    if (seen)
                    {
                        MigratedBefore.Add(__instance, null);
                    }
                    else if (___priorities != null)
                    {
                        Realign(___priorities);
                    }

                    break;

                case LoadSaveMode.PostLoadInit:
                    if (___priorities != null && ___pawn != null)
                    {
                        Apply(__instance, ___pawn, ___priorities, MigratedBefore.TryGetValue(__instance, out _));
                    }

                    MigratedBefore.Remove(__instance);
                    break;
            }
        }

        /// <summary>
        /// Moves the loaded priorities of work types after this mod's back to their own work types.
        /// </summary>
        /// <param name="priorities">The priorities as loaded and padded.</param>
        /// <remarks>
        /// In <c>LoadingVars</c>, straight after vanilla read them, so vanilla's own pass in
        /// <c>PostLoadInit</c>, which switches off what the pawn cannot do, and the copy after it both
        /// work on realigned values. <see cref="LoadingMigration.MakeRoom"/> says when this is right.
        /// </remarks>
        private static void Realign(DefMap<WorkTypeDef, int> priorities)
        {
            var values = new int[priorities.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = priorities[i];
            }

            int[] realigned = LoadingMigration.MakeRoom(values, CaravansCriticallyEnhancedDefOf.CCE_Loading.index);
            for (int i = 0; i < realigned.Length; i++)
            {
                priorities[i] = realigned[i];
            }
        }

        private static void Apply(Pawn_WorkSettings settings, Pawn pawn, DefMap<WorkTypeDef, int> priorities, bool migratedBefore)
        {
            WorkTypeDef loading = CaravansCriticallyEnhancedDefOf.CCE_Loading;
            WorkTypeDef source = DefDatabase<WorkGiverDef>.GetNamedSilentFail(VanillaHelperGiver)?.workType ?? WorkTypeDefOf.Hauling;

            int? priority = LoadingMigration.Decide(
                migratedBefore,
                pawn.WorkTypeIsDisabled(loading),
                pawn.IsColonyMech,
                priorities[loading],
                priorities[source]);

            if (priority != null)
            {
                settings.SetPriority(loading, priority.Value);
            }
        }
    }
}
