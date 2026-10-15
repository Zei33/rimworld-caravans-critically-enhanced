using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CaravansCriticallyEnhanced.Core;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the nine shipped languages to the same key set, and the key set to what the code uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A key added to English alone is a shipped bug rather than a warning: the player sees a raw key
    /// name in the UI, and nothing in the game fails loudly enough to catch it. Simple Improve shipped
    /// a whole family of that defect, a missing <c>DefInjected/WorkGiverDef</c>, while every Keyed key
    /// was present in all nine, which is why that half is checked too once the mod has defs.
    /// </para>
    /// <para>
    /// Adapted from Simple Improve's fixture of the same name.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class LanguageParityTests
    {
        [Test]
        public void AllNineLanguagesShipAKeyedFile()
        {
            foreach (string language in Repo.Languages)
            {
                Assert.That(
                    File.Exists(Repo.KeyedPath(language)), Is.True,
                    language + " has no Keyed file, so every string in it falls back to the key name.");
            }
        }

        [Test]
        public void EveryLanguageDeclaresExactlyTheSameKeys()
        {
            Dictionary<string, List<string>> byLanguage = Repo.Languages.ToDictionary(
                language => language,
                language => KeyedElements(language).Select(e => e.Name.LocalName).ToList());

            var english = new HashSet<string>(byLanguage["English"]);

            Assert.That(english.Count, Is.GreaterThan(0), "English declares no keys at all.");

            foreach (string language in Repo.Languages)
            {
                var keys = new HashSet<string>(byLanguage[language]);

                Assert.That(
                    byLanguage[language].Count, Is.EqualTo(keys.Count),
                    language + " declares a key twice: "
                    + string.Join(", ", byLanguage[language].GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key)));

                Assert.That(
                    english.Except(keys).ToList(), Is.Empty,
                    language + " is missing keys that English has, so those strings render as raw key names.");

                Assert.That(
                    keys.Except(english).ToList(), Is.Empty,
                    language + " declares keys English does not, usually a rename that landed in one file.");
            }
        }

        [Test]
        public void EveryKeyCarriesTheModPrefix()
        {
            // Keyed strings share one namespace across every mod a player runs, and the database keeps
            // whichever loads last. The prefix is what makes a collision impossible rather than unlikely.
            List<string> unprefixed = KeyedElements("English")
                .Select(e => e.Name.LocalName)
                .Where(key => !key.StartsWith("CaravansCriticallyEnhanced_", StringComparison.Ordinal))
                .ToList();

            Assert.That(unprefixed, Is.Empty);
        }

        [Test]
        public void NoShippedStringIsEmpty()
        {
            foreach (string language in Repo.Languages)
            {
                List<string> empty = KeyedElements(language)
                    .Where(e => string.IsNullOrWhiteSpace(e.Value))
                    .Select(e => e.Name.LocalName)
                    .ToList();

                Assert.That(empty, Is.Empty, language + " ships empty strings: " + string.Join(", ", empty));
            }
        }

        [Test]
        public void EveryKeyTheCodeAsksForIsDeclared()
        {
            // The forward direction: a key the code asks for and no language declares renders as its
            // raw name. The reverse is the next test.
            var declared = new HashSet<string>(KeyedElements("English").Select(e => e.Name.LocalName));

            HashSet<string> asked = KeysAskedForInSource();

            // The control sits on the source scan alone, before the table's keys are added: the table
            // supplies fifty, which would hide a source scan that had stopped finding anything.
            Assert.That(asked, Does.Contain("CaravansCriticallyEnhanced_ResetToDefaults"),
                "The source scan no longer finds a key the settings window asks for directly.");
            Assert.That(asked.Count, Is.GreaterThanOrEqualTo(10), "The source scan found almost no keys.");

            asked.UnionWith(KeysInTheSettingsTable());

            Assert.That(
                asked.Except(declared).ToList(), Is.Empty,
                "The code asks for keys no language declares, which render as the raw key name.");
        }

        [Test]
        public void EveryDeclaredKeyIsStillUsedByTheCompiledMod()
        {
            // The reverse direction, read from the compiled IL rather than the source. A text scan
            // cannot tell a key the code uses from a comment that quotes it, and in this direction that
            // failure is silent: a comment explaining why a key was removed would keep it alive.
            List<string> declared = KeyedElements("English").Select(e => e.Name.LocalName).ToList();

            ISet<string> literals = ILCalls.StringsAnywhereInTheMod();

            // Checked first so a scan fault is reported as one. If the IL scan reached less of the mod
            // than the source scan, keys used only in the part it missed would be reported as unused.
            List<string> missedByTheScan = KeysAskedForInSource().Where(key => !literals.Contains(key)).ToList();

            Assert.That(
                missedByTheScan, Is.Empty,
                "The IL scan does not see keys the source asks for, so it is not reading the whole mod.");

            Assert.That(
                declared.Where(key => !literals.Contains(key)).ToList(), Is.Empty,
                "These keys are declared but nothing in the compiled mod asks for them. Remove them from "
                + "all nine Keyed files, or find the call that was meant to use them.");
        }

        [Test]
        public void EveryTranslationCarriesTheSamePlaceholdersAsEnglish()
        {
            // A translation that drops or renumbers a placeholder still loads and passes every test
            // above; the player sees a sentence with the number missing or the wrong number in it.
            Dictionary<string, Dictionary<string, string>> byLanguage = ValuesByLanguage();

            var placeholder = new Regex("\\{[0-9]+\\}");
            var mismatches = new List<string>();

            foreach (KeyValuePair<string, string> english in byLanguage["English"])
            {
                List<string> expected = placeholder.Matches(english.Value).Cast<Match>()
                    .Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToList();

                foreach (string language in Repo.Languages)
                {
                    if (!byLanguage[language].TryGetValue(english.Key, out string translated))
                    {
                        continue;
                    }

                    List<string> found = placeholder.Matches(translated).Cast<Match>()
                        .Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToList();

                    if (!found.SequenceEqual(expected))
                    {
                        mismatches.Add(language + "/" + english.Key);
                    }
                }
            }

            // The control: English has to carry some placeholders for this to test anything. The four
            // settings sliders are the ones at Slice 0; raise the floor as messages arrive.
            Assert.That(
                byLanguage["English"].Values.Count(value => placeholder.IsMatch(value)), Is.GreaterThanOrEqualTo(4),
                "Almost no English string carries a placeholder, so this test is checking nothing.");

            Assert.That(mismatches, Is.Empty, "These translations do not carry the same {n} placeholders as the English.");
        }

        [Test]
        public void NoTranslationIsLeftInEnglish()
        {
            // A key copied into a language and never translated passes every test above. Only the mod's
            // own name may match the English, because it is a name and is not translated.
            var mayMatchEnglish = new HashSet<string>
            {
                "CaravansCriticallyEnhanced_SettingsCategory",
            };

            Dictionary<string, Dictionary<string, string>> byLanguage = ValuesByLanguage();
            Dictionary<string, string> english = byLanguage["English"];

            Assert.That(mayMatchEnglish.Where(key => !english.ContainsKey(key)).ToList(), Is.Empty,
                "An exempted key is no longer declared, so the exemption is stale.");

            var untranslated = new List<string>();
            int compared = 0;

            foreach (string language in Repo.Languages.Where(l => l != "English"))
            {
                foreach (KeyValuePair<string, string> entry in byLanguage[language])
                {
                    if (mayMatchEnglish.Contains(entry.Key) || !english.TryGetValue(entry.Key, out string source))
                    {
                        continue;
                    }

                    compared++;

                    if (entry.Value == source)
                    {
                        untranslated.Add(language + "/" + entry.Key);
                    }
                }
            }

            Assert.That(compared, Is.EqualTo(8 * (english.Count - mayMatchEnglish.Count)),
                "The comparison did not cover every key in every translation.");

            Assert.That(untranslated, Is.Empty, "These translations are identical to the English.");
        }

        [Test]
        public void EverySettingHasALabelAndATooltip()
        {
            // The settings table names its keys as literals. This names them all explicitly rather than
            // leaving them to the parity tests, which would also pass if a switch had no entry at all.
            var declared = new HashSet<string>(KeyedElements("English").Select(e => e.Name.LocalName));

            foreach (ToggleSpec spec in SettingToggles.All)
            {
                Assert.That(declared, Does.Contain(spec.LabelKey), spec.Toggle + " has no label.");
                Assert.That(declared, Does.Contain(spec.TooltipKey), spec.Toggle + " has no tooltip.");
            }

            foreach (SettingGroup group in Enum.GetValues(typeof(SettingGroup)))
            {
                Assert.That(declared, Does.Contain(SettingToggles.HeadingKey(group)), group + " has no heading.");
            }
        }

        [Test]
        public void EveryLanguageInjectsTheSameDefFields()
        {
            // The Keyed half alone is not enough: Simple Improve shipped every Keyed key in all nine and
            // no DefInjected/WorkGiverDef at all, and the float menu rendered part English everywhere.
            Dictionary<string, List<string>> english = InjectionsFor("English");
            Assert.That(english.Keys, Is.Not.Empty, "English injects nothing at all.");

            foreach (string language in Repo.Languages)
            {
                Dictionary<string, List<string>> injected = InjectionsFor(language);
                Assert.That(injected.Keys, Is.EquivalentTo(english.Keys), language + " injects different def types.");

                foreach (string defType in english.Keys.Where(injected.ContainsKey))
                {
                    Assert.That(injected[defType], Is.EquivalentTo(english[defType]), language + "/" + defType);
                }
            }
        }

        [Test]
        public void EveryTranslatableFieldTheDefsSetIsInjected()
        {
            // The fields are [MustTranslate] on their def types. A def setting one that no injection
            // covers renders English in the eight other languages, and nothing fails loudly.
            var mustTranslate = new Dictionary<string, string[]>
            {
                { "WorkTypeDef", new[] { "labelShort", "pawnLabel", "gerundLabel", "description", "verb", "label" } },
                { "WorkGiverDef", new[] { "label", "verb", "gerund", "description" } },
                { "JobDef", new[] { "reportString", "label", "description" } },
            };

            var expected = new List<string>();
            foreach (XElement def in DefXml.ModDefNodes)
            {
                if (!mustTranslate.TryGetValue(def.Name.LocalName, out string[] fields))
                {
                    continue;
                }

                string defName = def.Element("defName").Value;
                expected.AddRange(fields.Where(f => def.Element(f) != null).Select(f => def.Name.LocalName + "/" + defName + "." + f));
            }

            Dictionary<string, List<string>> english = InjectionsFor("English");
            List<string> injected = english.SelectMany(kv => kv.Value.Select(key => kv.Key + "/" + key)).ToList();

            Assert.That(expected.Count, Is.EqualTo(12), "Translatable fields set by the mod's defs.");
            Assert.That(injected, Is.EquivalentTo(expected));
        }

        [Test]
        public void NoInjectionIsLeftInEnglish()
        {
            Dictionary<string, string> english = InjectedValues("English");
            int compared = 0;
            var same = new List<string>();

            foreach (string language in Repo.Languages.Where(l => l != "English"))
            {
                foreach (KeyValuePair<string, string> entry in InjectedValues(language))
                {
                    compared++;
                    if (english.TryGetValue(entry.Key, out string source) && entry.Value == source)
                    {
                        same.Add(language + "/" + entry.Key);
                    }
                }
            }

            Assert.That(compared, Is.EqualTo(8 * english.Count));
            Assert.That(same, Is.Empty);
        }

        private static Dictionary<string, List<string>> InjectionsFor(string language)
        {
            string root = Repo.PathOf("1.6", "Languages", language, "DefInjected");
            var found = new Dictionary<string, List<string>>();
            if (!Directory.Exists(root))
            {
                return found;
            }

            foreach (string directory in Directory.GetDirectories(root))
            {
                found[Path.GetFileName(directory)] = Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories)
                    .SelectMany(f => XDocument.Load(f).Root.Elements().Select(e => e.Name.LocalName))
                    .ToList();
            }

            return found;
        }

        private static Dictionary<string, string> InjectedValues(string language)
        {
            var values = new Dictionary<string, string>();
            foreach (KeyValuePair<string, List<string>> kv in InjectionsFor(language))
            {
                foreach (string file in Directory.GetFiles(Repo.PathOf("1.6", "Languages", language, "DefInjected", kv.Key), "*.xml"))
                {
                    foreach (XElement element in XDocument.Load(file).Root.Elements())
                    {
                        values[kv.Key + "/" + element.Name.LocalName] = element.Value;
                    }
                }
            }

            return values;
        }

        /// <summary>
        /// Finds every key the mod's C# passes to <c>Translate</c> directly.
        /// </summary>
        /// <returns>The keys, each once.</returns>
        /// <remarks>
        /// The literal must be followed by <c>.Translate</c> to count. A key reached through a variable,
        /// as the settings table's are, is invisible here and is collected by
        /// <see cref="KeysInTheSettingsTable"/> instead. Comments are stripped first.
        /// </remarks>
        private static HashSet<string> KeysAskedForInSource()
        {
            var asked = new HashSet<string>();
            var pattern = new Regex("\"(CaravansCriticallyEnhanced_[A-Za-z0-9_]+)\"\\s*\\.\\s*Translate");

            foreach (string file in Repo.ModSourceFiles())
            {
                foreach (Match match in pattern.Matches(Repo.CodeWithoutComments(file)))
                {
                    asked.Add(match.Groups[1].Value);
                }
            }

            return asked;
        }

        private static IEnumerable<string> KeysInTheSettingsTable()
        {
            foreach (ToggleSpec spec in SettingToggles.All)
            {
                yield return spec.LabelKey;
                yield return spec.TooltipKey;
            }

            foreach (SettingGroup group in Enum.GetValues(typeof(SettingGroup)))
            {
                yield return SettingToggles.HeadingKey(group);
            }
        }

        private static Dictionary<string, Dictionary<string, string>> ValuesByLanguage()
        {
            return Repo.Languages.ToDictionary(
                language => language,
                language => KeyedElements(language).ToDictionary(e => e.Name.LocalName, e => e.Value));
        }

        private static IEnumerable<XElement> KeyedElements(string language)
        {
            return XDocument.Load(Repo.KeyedPath(language)).Root.Elements();
        }
    }
}
