using RimWorld;
using Verse;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The mod's own defs that code refers to by name.
    /// </summary>
    /// <remarks>
    /// <c>DefOfHelper</c> binds each field by its own name once the defs have loaded, and naming a
    /// field before then throws. <c>Tests/DefReferenceTests.cs</c> holds every field to a def of its
    /// type in the mod's XML, because a renamed def otherwise leaves the field null with only a line
    /// in the startup log.
    /// </remarks>
    [DefOf]
    public static class CaravansCriticallyEnhancedDefOf
    {
        /// <summary>The Load work type, the column next to Haul.</summary>
        public static WorkTypeDef CCE_Loading;

        /// <summary>A helper leading a caravan's animal to the gathering spot.</summary>
        public static JobDef CCE_RopeAnimalToCaravan;

        /// <summary>
        /// Ensures the fields are bound, as every <c>[DefOf]</c> class does.
        /// </summary>
        static CaravansCriticallyEnhancedDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CaravansCriticallyEnhancedDefOf));
        }
    }
}
