using System;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Keeps a caravan colonist's leads through being drafted, and says so when undrafting drops them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drafting ends the current job, and a rope job's finish action drops every rope. The prefix
    /// opens a <see cref="LeadKeepScope"/> for a roper the mod will hold once drafted; the patch on
    /// <c>DropRopes</c> refuses inside it; the finalizer closes it whatever happened. A finalizer
    /// rather than a postfix, so it also runs after other mods' postfixes on this setter (Achtung!
    /// and Allow Tool both have one) and after an exception.
    /// </para>
    /// <para>
    /// Undrafting needs no help where the caravan's duty gives a rope job, which picks the held
    /// animals up again; anywhere else the next rope tick drops them, and the finalizer leaves a
    /// notice for that tick to tell the player (<see cref="LeadUndraftNotices"/>).
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    public static class Patch_Pawn_DraftController_Drafted
    {
        /// <summary>
        /// Opens the scope when a roper the mod will hold is drafted.
        /// </summary>
        /// <param name="__instance">The draft controller.</param>
        /// <param name="value">Whether the pawn is being drafted.</param>
        /// <param name="__state">Whether a scope was opened.</param>
        public static void Prefix(Pawn_DraftController __instance, bool value, out bool __state)
        {
            __state = false;
            if (value && !__instance.Drafted && LeadUtility.KeepsLeadsWhenDrafted(__instance.pawn))
            {
                LeadKeepScope.Push(__instance.pawn);
                __state = true;
            }
        }

        /// <summary>
        /// Closes the scope, and tells the player when undrafting is about to drop the leads.
        /// </summary>
        /// <param name="__instance">The draft controller.</param>
        /// <param name="value">Whether the pawn was being drafted.</param>
        /// <param name="__state">Whether a scope was opened.</param>
        public static void Finalizer(Pawn_DraftController __instance, bool value, bool __state)
        {
            Pawn pawn = __instance.pawn;
            if (__state)
            {
                LeadKeepScope.Pop(pawn);
            }

            if (value || pawn?.roping == null || !pawn.Spawned || !pawn.roping.IsRopingOthers || pawn.jobs?.curDriver is JobDriver_RopeToDestination)
            {
                return;
            }

            // A mental break undrafts before it is recorded, so whether this is one is not known yet;
            // the rope tick that drops the leads tells the player, or not (LeadUndraftNotices).
            if (CaravansCriticallyEnhancedMod.Settings.IsOn(Toggle.LeadsSurviveDrafting) && FormingLords.VanillaLordOf(pawn) != null)
            {
                LeadUndraftNotices.Expect(pawn, Find.TickManager.TicksGame);
            }
        }
    }
}
