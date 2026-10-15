using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Patches
{
    /// <summary>
    /// Narrows which members a helper may hand an item to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One predicate covers every place the choice is made: the Load giver's carrier check, the
    /// driver's <c>FindBestCarrier</c> and its random fallback, and the re-checks while the helper
    /// walks to the carrier and waits beside it. <see cref="CarrierRule"/> holds the rule.
    /// </para>
    /// <para>
    /// A hot path: the driver re-checks its carrier on every tick of the walk. The early exits come
    /// first, and a refusal from vanilla or a member asking for itself costs one test. Pack Animal
    /// Training patches this method too; a postfix that only narrows the answer stacks with it.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(JobDriver_PrepareCaravan_GatherItems), nameof(JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier))]
    public static class Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier
    {
        /// <summary>
        /// Applies <see cref="CarrierRule.Allows"/> to a helper's carrier.
        /// </summary>
        /// <param name="p">The candidate carrier, a member of a forming caravan when vanilla said yes.</param>
        /// <param name="forPawn">The pawn holding the item.</param>
        /// <param name="__result">Vanilla's answer, narrowed.</param>
        public static void Postfix(Pawn p, Pawn forPawn, ref bool __result)
        {
            if (!__result || p == forPawn)
            {
                return;
            }

            CaravansCriticallyEnhancedSettings settings = CaravansCriticallyEnhancedMod.Settings;
            if (!settings.IsOn(Toggle.LoadingWorkType))
            {
                return;
            }

            Lord lord = p.GetLord();
            if (!FormingLords.IsVanilla(lord) || forPawn.GetLord() == lord)
            {
                return;
            }

            bool isColonist = p.IsColonist;
            bool ropeAnimal = AnimalPenUtility.NeedsToBeManagedByRope(p);

            __result = CarrierRule.Allows(
                FormingLords.StageOf(lord),
                isColonist,
                p.Drafted || p.InMentalState || p.IsForbidden(forPawn),
                isColonist && MassUtility.IsOverEncumbered(p),
                ropeAnimal,
                ropeAnimal && IsTiedAtItsSpot(p, lord),
                settings.IsOn(Toggle.ColonistCarriers));
        }

        /// <summary>
        /// Determines whether a rope animal is tied at the spot its caravan is gathering at.
        /// </summary>
        /// <param name="animal">The animal.</param>
        /// <param name="lord">Its caravan, a lord <see cref="FormingLords.IsVanilla(Lord)"/> accepts.</param>
        /// <remarks>
        /// <para>
        /// <c>IsRopedToSpot</c> alone is true for any spot, a hitching post across the map included.
        /// Vanilla counts an animal as gathered only when it is tied at the meeting point
        /// (<c>LordToil_PrepareCaravan_GatherAnimals.LordToilTick</c>), and so does this.
        /// </para>
        /// <para>
        /// The meeting point is read from the lord job, not the animal's duty. In the pause after a
        /// mental break every member's duty is the pause duty, which has no spot, so reading the duty
        /// would refuse every animal and make a helper already on its way drop what it carries.
        /// The duty is the fallback only if the field has gone in a game update.
        /// </para>
        /// </remarks>
        private static bool IsTiedAtItsSpot(Pawn animal, Lord lord)
        {
            if (animal.roping == null || !animal.roping.IsRopedToSpot)
            {
                return false;
            }

            AccessTools.FieldRef<LordJob_FormAndSendCaravan, IntVec3> meetingPoint = CaravanLordAccess.JobMeetingPoint;
            IntVec3 spot = meetingPoint != null
                ? meetingPoint((LordJob_FormAndSendCaravan)lord.LordJob)
                : animal.mindState?.duty?.focus.Cell ?? IntVec3.Invalid;

            return spot.IsValid && animal.roping.RopedToSpot == spot;
        }
    }
}
