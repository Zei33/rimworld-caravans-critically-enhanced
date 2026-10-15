using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;
using Verse;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Covers the settings table and the settings class's pure behaviour.
    /// </summary>
    [TestFixture]
    public class SettingsTests
    {
        [Test]
        public void EverySwitchHasExactlyOneEntry()
        {
            List<Toggle> listed = SettingToggles.All.Select(s => s.Toggle).ToList();

            Assert.That(listed, Is.EquivalentTo(Enum.GetValues(typeof(Toggle)).Cast<Toggle>()));
            Assert.That(listed.Distinct().Count(), Is.EqualTo(listed.Count));
            Assert.That(SettingToggles.Count, Is.EqualTo(listed.Count));
        }

        [Test]
        public void TheSwitchesAreNumberedFromZeroWithoutGaps()
        {
            // The settings store each switch at (int)toggle in an array, so an explicit enum value that
            // left a gap would index past the end.
            Assert.That(
                Enum.GetValues(typeof(Toggle)).Cast<int>().OrderBy(i => i),
                Is.EqualTo(Enumerable.Range(0, SettingToggles.Count)));
        }

        /// <summary>
        /// Every node name the settings file has ever been written with, in table order. Never edit an
        /// entry: a renamed node reads as absent, which silently puts every player's choice back to the
        /// default. A new switch adds a line.
        /// </summary>
        private static readonly string[] ShippedNodeNames =
        {
            "loadingWorkType", "colonistCarriers", "earlyLoading", "helpersRopeAnimals",
            "leadsSurviveDrafting",
            "areaAwareExit", "threatAwareExit", "leaveFromHere", "departureWarnings",
            "listAnimalCargo", "cargoIcon", "autoAdjustCargo", "keepLoadedCargo", "transferCargo", "protectCargo", "instantSurplusUnload",
            "completionGuard", "manifestReconcile", "stallReadout",
            "unloadReturningAnimals", "loadOntoCaravanFallback", "manualLoadsCount",
            "threatRadius", "routeThreatRadius", "completionGraceTicks", "stallAlertTicks",
        };

        [Test]
        public void TheSwitchesAreSavedUnderTheirShippedNodeNames()
        {
            // Pinned as literals rather than derived from the enum, so renaming a Toggle member for
            // readability does not push anybody towards renaming the node to match.
            Assert.That(SettingToggles.All.Select(s => s.ScribeKey), Is.EqualTo(ShippedNodeNames.Take(SettingToggles.All.Count)));
            Assert.That(ShippedNodeNames.Distinct().Count(), Is.EqualTo(ShippedNodeNames.Length));
        }

        [Test]
        public void SavingWritesOnlyWhatDiffersFromTheDefaultUnderItsShippedName()
        {
            // The game's own saver, run against a temporary file. Loading cannot run here: the loader
            // reaches a Unity logging call that does not exist outside the player, measured
            // 2026-10-15, so the read half and the clamp after it are in-game check 1.
            Assert.That(SavedNodes(new CaravansCriticallyEnhancedSettings()), Is.Empty,
                "Defaults are written as nothing, which is why a changed default reaches every player.");

            var settings = new CaravansCriticallyEnhancedSettings();
            foreach (ToggleSpec spec in SettingToggles.All)
            {
                settings.Set(spec.Toggle, !spec.DefaultOn);
            }

            SetField(settings, "threatRadius", 31);
            SetField(settings, "routeThreatRadius", 7);
            SetField(settings, "completionGraceTicks", 5000);
            SetField(settings, "stallAlertTicks", 10000);

            Dictionary<string, string> saved = SavedNodes(settings);

            Assert.That(saved.Keys, Is.EqualTo(ShippedNodeNames));
            foreach (ToggleSpec spec in SettingToggles.All)
            {
                Assert.That(saved[spec.ScribeKey], Is.EqualTo((!spec.DefaultOn).ToString()), spec.ScribeKey);
            }

            Assert.That(saved["threatRadius"], Is.EqualTo("31"));
            Assert.That(saved["routeThreatRadius"], Is.EqualTo("7"));
            Assert.That(saved["completionGraceTicks"], Is.EqualTo("5000"));
            Assert.That(saved["stallAlertTicks"], Is.EqualTo("10000"));
        }

        private static void SetField(object target, string name, int value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static Dictionary<string, string> SavedNodes(CaravansCriticallyEnhancedSettings settings)
        {
            string path = Path.Combine(Path.GetTempPath(), "cce-settings-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                Scribe.saver.InitSaving(path, "SettingsBlock");
                Scribe_Deep.Look(ref settings, "ModSettings");
                Scribe.saver.FinalizeSaving();

                return XDocument.Load(path).Root.Element("ModSettings").Elements()
                    .ToDictionary(e => e.Name.LocalName, e => e.Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void EverySwitchIsOnByDefaultExceptInstantSurplusUnload()
        {
            // The plan's defaults: every feature on, so a player gets the whole mod, except dropping
            // surplus cargo beside the animal, which changes how unloading looks and is opt in.
            Assert.That(
                SettingToggles.All.Where(s => !s.DefaultOn).Select(s => s.Toggle),
                Is.EqualTo(new[] { Toggle.InstantSurplusUnload }));
        }

        [Test]
        public void ParentsComeFirstInTheSameSectionAndAreThemselvesTopLevel()
        {
            // The window draws a child indented under its parent, so the parent has to be drawn first
            // and in the same section. One level only, which IsOn relies on to stay a short walk.
            List<ToggleSpec> all = SettingToggles.All.ToList();
            int children = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Parent == null)
                {
                    continue;
                }

                children++;
                ToggleSpec parent = SettingToggles.SpecOf(all[i].Parent.Value);

                Assert.That(all.IndexOf(parent), Is.LessThan(i), all[i].Toggle + " is drawn before its parent.");
                Assert.That(parent.Group, Is.EqualTo(all[i].Group), all[i].Toggle + " is in another section from its parent.");
                Assert.That(parent.Parent, Is.Null, all[i].Toggle + "'s parent has a parent of its own.");
            }

            Assert.That(children, Is.EqualTo(10), "Switches with a parent.");
        }

        [Test]
        public void SectionsAreContiguousAndInEnumOrder()
        {
            List<SettingGroup> order = SettingToggles.All.Select(s => s.Group).ToList();
            var distinctInOrder = new List<SettingGroup>();
            foreach (SettingGroup group in order)
            {
                if (distinctInOrder.Count == 0 || distinctInOrder.Last() != group)
                {
                    distinctInOrder.Add(group);
                }
            }

            Assert.That(distinctInOrder, Is.EqualTo(Enum.GetValues(typeof(SettingGroup)).Cast<SettingGroup>()),
                "A section is split, missing, or drawn out of order.");
        }

        [Test]
        public void ANewSettingsObjectHoldsTheTableDefaults()
        {
            var settings = new CaravansCriticallyEnhancedSettings();

            foreach (ToggleSpec spec in SettingToggles.All)
            {
                Assert.That(settings.StoredValue(spec.Toggle), Is.EqualTo(spec.DefaultOn), spec.Toggle.ToString());
            }

            Assert.That(settings.ThreatRadius, Is.EqualTo(25));
            Assert.That(settings.RouteThreatRadius, Is.EqualTo(12));
            Assert.That(settings.CompletionGraceTicks, Is.EqualTo(2500));
            Assert.That(settings.CompletionWatchdogTicks, Is.EqualTo(5000));
            Assert.That(settings.StallAlertTicks, Is.EqualTo(7500));
        }

        [Test]
        public void AChildReadsAsOffWhileItsParentIsOff()
        {
            var settings = new CaravansCriticallyEnhancedSettings();
            Assert.That(settings.IsOn(Toggle.TransferCargo), Is.True);

            settings.Set(Toggle.ListAnimalCargo, false);

            Assert.That(settings.IsOn(Toggle.TransferCargo), Is.False);
            Assert.That(settings.StoredValue(Toggle.TransferCargo), Is.True, "The child's own choice is kept for when the parent comes back.");
            Assert.That(settings.IsOn(Toggle.LeadsSurviveDrafting), Is.True, "Another section is unaffected.");

            settings.Set(Toggle.ListAnimalCargo, true);
            Assert.That(settings.IsOn(Toggle.TransferCargo), Is.True);
        }

        [Test]
        public void ResetRestoresEveryDefault()
        {
            var settings = new CaravansCriticallyEnhancedSettings();
            foreach (ToggleSpec spec in SettingToggles.All)
            {
                settings.Set(spec.Toggle, !spec.DefaultOn);
            }

            settings.ResetToDefaults();

            foreach (ToggleSpec spec in SettingToggles.All)
            {
                Assert.That(settings.StoredValue(spec.Toggle), Is.EqualTo(spec.DefaultOn), spec.Toggle.ToString());
            }
        }

        [Test]
        public void TwoSettingsObjectsDoNotShareTheirSwitches()
        {
            // The defaults array is built fresh for each object. A shared static array would make
            // ResetToDefaults and Set act on every instance at once.
            var first = new CaravansCriticallyEnhancedSettings();
            var second = new CaravansCriticallyEnhancedSettings();

            first.Set(Toggle.CargoIcon, false);

            Assert.That(second.StoredValue(Toggle.CargoIcon), Is.True);
        }

        [TestCase(30, 30)]
        [TestCase(5, 5)]
        [TestCase(60, 60)]
        [TestCase(4, 5)]
        [TestCase(1, 5)]
        [TestCase(61, 60)]
        [TestCase(0, 25)]
        [TestCase(-3, 25)]
        public void ALoadedNumberIsClampedAndACorruptOneTakesTheDefault(int loaded, int expected)
        {
            Assert.That(CaravansCriticallyEnhancedSettings.Clamp(loaded, 5, 60, 25), Is.EqualTo(expected));
        }

        [Test]
        public void EveryDefaultNumberIsInsideItsOwnRange()
        {
            Assert.That(CaravansCriticallyEnhancedSettings.DefaultThreatRadius,
                Is.InRange(CaravansCriticallyEnhancedSettings.MinimumThreatRadius, CaravansCriticallyEnhancedSettings.MaximumThreatRadius));
            Assert.That(CaravansCriticallyEnhancedSettings.DefaultRouteThreatRadius,
                Is.InRange(CaravansCriticallyEnhancedSettings.MinimumRouteThreatRadius, CaravansCriticallyEnhancedSettings.MaximumRouteThreatRadius));
            Assert.That(CaravansCriticallyEnhancedSettings.DefaultCompletionGraceTicks,
                Is.InRange(CaravansCriticallyEnhancedSettings.MinimumCompletionGraceTicks, CaravansCriticallyEnhancedSettings.MaximumCompletionGraceTicks));
            Assert.That(CaravansCriticallyEnhancedSettings.DefaultStallAlertTicks,
                Is.InRange(CaravansCriticallyEnhancedSettings.MinimumStallAlertTicks, CaravansCriticallyEnhancedSettings.MaximumStallAlertTicks));
        }

        [Test]
        public void EveryDurationTheSlidersCanReachHasALabelOfItsOwn()
        {
            // GenDate.ToStringTicksToPeriod shows whole hours from one hour up, so a step finer than an
            // hour put two different settings under one label. Every value a slider can reach is a
            // whole number of hours, at least one, and the defaults and ends sit on steps.
            int step = CaravansCriticallyEnhancedSettings.DurationStepTicks;
            Assert.That(step, Is.EqualTo(2500));

            foreach (int ticks in new[]
                     {
                         CaravansCriticallyEnhancedSettings.MinimumCompletionGraceTicks, CaravansCriticallyEnhancedSettings.DefaultCompletionGraceTicks,
                         CaravansCriticallyEnhancedSettings.MaximumCompletionGraceTicks, CaravansCriticallyEnhancedSettings.MinimumStallAlertTicks,
                         CaravansCriticallyEnhancedSettings.DefaultStallAlertTicks, CaravansCriticallyEnhancedSettings.MaximumStallAlertTicks,
                     })
            {
                Assert.That(ticks % step, Is.Zero, ticks.ToString());
                Assert.That(ticks, Is.GreaterThanOrEqualTo(step), ticks.ToString());
            }
        }

        [TestCase(2500f, 2500)]
        [TestCase(3749f, 2500)]
        [TestCase(3751f, 5000)]
        [TestCase(14999f, 15000)]
        public void ASliderValueSnapsToTheNearestStep(float slid, int expected)
        {
            Assert.That(CaravansCriticallyEnhancedSettings.SnapToStep(slid, 2500), Is.EqualTo(expected));
        }
    }
}
