using CaravansCriticallyEnhanced.Core;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CaravansCriticallyEnhanced
{
    /// <summary>
    /// The mod's entry point: loads the settings and applies the Harmony patches.
    /// </summary>
    public class CaravansCriticallyEnhancedMod : Mod
    {
        /// <summary>
        /// The Harmony instance id, lowercase as in every other mod of this author.
        /// </summary>
        public const string HarmonyId = "com.zei33.caravanscriticallyenhanced";

        /// <summary>
        /// Initializes a new instance of the <see cref="CaravansCriticallyEnhancedMod"/> class.
        /// </summary>
        /// <param name="pack">The mod's content pack.</param>
        /// <remarks>
        /// The settings are loaded before patching, so no patch can run before they exist.
        /// <c>PatchAll</c> walks this assembly's types and applies every class carrying
        /// <c>[HarmonyPatch]</c>; a class that throws at patch time stops every class after it, which
        /// is why every patch target is resolved by a test the way Harmony will resolve it.
        /// </remarks>
        public CaravansCriticallyEnhancedMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<CaravansCriticallyEnhancedSettings>();

            new Harmony(HarmonyId).PatchAll();

            // Read from the metadata rather than written out, so the version lives in About.xml alone.
            // Gated on dev mode: informational logging is for whoever is debugging.
            if (Prefs.DevMode)
            {
                Log.Message($"[CaravansCriticallyEnhanced] Loaded version {pack.ModMetaData.ModVersion}.");
            }
        }

        /// <summary>
        /// Gets the player's settings.
        /// </summary>
        /// <remarks>
        /// Read at call boundaries only, by the patch or work giver deciding whether its feature runs.
        /// Decisions take what they need as parameters, so that they stay reachable by the test
        /// harness, where this is null.
        /// </remarks>
        public static CaravansCriticallyEnhancedSettings Settings { get; private set; }

        /// <summary>
        /// Gets the name of the mod's entry in the mod options list.
        /// </summary>
        /// <returns>The translated name.</returns>
        public override string SettingsCategory()
        {
            return "CaravansCriticallyEnhanced_SettingsCategory".Translate();
        }

        /// <summary>
        /// Draws the settings window.
        /// </summary>
        /// <param name="inRect">The area available for drawing.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }
}
