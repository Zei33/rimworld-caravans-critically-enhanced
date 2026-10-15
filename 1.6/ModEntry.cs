using System;
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
        /// </remarks>
        public CaravansCriticallyEnhancedMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<CaravansCriticallyEnhancedSettings>();

            ApplyPatches(new Harmony(HarmonyId));

            // Read from the metadata rather than written out, so the version lives in About.xml alone.
            // Gated on dev mode: informational logging is for whoever is debugging.
            if (Prefs.DevMode)
            {
                Log.Message($"[CaravansCriticallyEnhanced] Loaded version {pack.ModMetaData.ModVersion}.");
            }
        }

        /// <summary>
        /// Applies every patch class in this assembly, one class at a time.
        /// </summary>
        /// <param name="harmony">The mod's Harmony instance.</param>
        /// <remarks>
        /// <para>
        /// <c>Harmony.PatchAll</c> walks the same classes with no catch, so the first class that throws
        /// stops every class after it, and which ones those are depends on type order. After a game
        /// update that renames one target, that would leave an arbitrary half of the mod applied.
        /// Here each class stands or falls alone and says which one failed. Every target is also
        /// resolved by <c>Tests/PatchTargetTests.cs</c> the way Harmony will resolve it, so a failure
        /// here means the game changed under a released build.
        /// </para>
        /// <para>
        /// Only classes carrying a Harmony attribute are processed, as <c>PatchAll</c> does in the
        /// Harmony players run (2.4). Its class processor no longer stops early for a class without
        /// one, so handing it every type would run any method that happened to be called
        /// <c>Prepare</c>, <c>Cleanup</c> or <c>TargetMethods</c>, anywhere in the mod, at startup.
        /// </para>
        /// </remarks>
        private static void ApplyPatches(Harmony harmony)
        {
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(CaravansCriticallyEnhancedMod).Assembly))
            {
                if (!Attribute.IsDefined(type, typeof(HarmonyAttribute), true))
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception exception)
                {
                    Log.Error("[CaravansCriticallyEnhanced] Could not apply " + type.FullName + ": " + exception);
                }
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
