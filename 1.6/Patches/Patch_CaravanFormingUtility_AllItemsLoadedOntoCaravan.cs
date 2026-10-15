using System;
using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// The single owner of the "all items loaded" answer: the drafted-member exemption, then the
    /// completion guard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One postfix for every feature that has an opinion here, run last so it sees every other mod's
    /// answer. It can turn vanilla's not done into done only through the drafted-member exemption,
    /// which is vanilla's own test with drafted colonists left out, and it can turn done into not done
    /// only through <see cref="CompletionGuard"/>, which cannot hold forever.
    /// </para>
    /// <para>
    /// Called every 120 ticks per forming lord by <c>LordToil_PrepareCaravan_GatherItems.LordToilTick</c>
    /// and from the Force depart confirmation, which lists the items still on the list whenever this
    /// answers false. That listing is what a held caravan shows the player. Outside the item-gathering
    /// stage vanilla's answer stands untouched; <see cref="CompletionGuard.Applies"/> says why.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.AllItemsLoadedOntoCaravan))]
    public static class Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan
    {
        /// <summary>
        /// The <c>Log.ErrorOnce</c> key for an exception from this patch.
        /// </summary>
        public const int ErrorKey = 0x43434502;

        /// <summary>
        /// Applies the exemption and the guard to vanilla's answer.
        /// </summary>
        /// <param name="lord">The forming lord.</param>
        /// <param name="map">Its map.</param>
        /// <param name="__result">Vanilla's answer, replaced.</param>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Lord lord, Map map, ref bool __result)
        {
            if (!FormingLords.IsVanilla(lord) || map == null)
            {
                return;
            }

            try
            {
                if (!CompletionGuard.Applies(FormingLords.StageOf(lord)))
                {
                    LoadingCompletion.Rest((LordJob_FormAndSendCaravan)lord.LordJob);
                    return;
                }

                CaravansCriticallyEnhancedSettings settings = CaravansCriticallyEnhancedMod.Settings;
                bool done = __result || (settings.IsOn(Toggle.LeadsSurviveDrafting) && LoadingCompletion.DoneIgnoringDrafted(lord, map));

                __result = LoadingCompletion.Answer(lord, map, done, settings);
            }
            catch (Exception exception)
            {
                // Vanilla's answer stands. The caller runs inside LordManager's per-lord try, which
                // would log every 120 ticks for as long as the caravan forms.
                Log.ErrorOnce("[CaravansCriticallyEnhanced] The completion guard failed, so vanilla decides: " + exception, ErrorKey);
            }
        }
    }
}
