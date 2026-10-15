using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Checks that read the repository's own files as text or XML.
    /// </summary>
    /// <remarks>
    /// Adapted from Great Escape's fixture of the same name.
    /// </remarks>
    [TestFixture]
    public class SourceTests
    {
        [Test]
        public void EveryModXmlFileParses()
        {
            // A def file that fails to parse loses every def in it in game, with one red line at startup.
            var files = Directory.GetFiles(Repo.PathOf("1.6"), "*.xml", SearchOption.AllDirectories)
                .Concat(new[] { Repo.PathOf("About", "About.xml") })
                .ToList();

            Assert.That(files.Count, Is.GreaterThanOrEqualTo(10), "Nine Keyed files and About.xml at the least.");

            var broken = new List<string>();
            foreach (string file in files)
            {
                try
                {
                    XDocument.Load(file);
                }
                catch (System.Xml.XmlException e)
                {
                    broken.Add(file + ": " + e.Message);
                }
            }

            Assert.That(broken, Is.Empty);
        }

        [Test]
        public void NoDashesInShippedTextOrComments()
        {
            // House rule for anything that ships, for docs and for code comments: no em or en dashes.
            string root = Repo.Root;
            string[] extensions = { ".xml", ".cs", ".sh", ".md", ".csproj", ".sln", ".bbcode", ".json" };
            char separator = Path.DirectorySeparatorChar;

            List<string> scanned = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f)))
                .Where(f => !f.Contains(separator + "obj" + separator)
                    && !f.Contains(separator + "bin" + separator)
                    && !f.Contains(separator + ".git" + separator))
                .ToList();

            Assert.That(scanned.Count, Is.GreaterThan(20), "Files scanned.");

            List<string> offenders = scanned
                .Where(f => File.ReadAllText(f).IndexOfAny(new[] { (char)0x2014, (char)0x2013 }) >= 0)
                .Select(f => f.Substring(root.Length))
                .ToList();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void AboutNamesTheModPlainlyAndPointsAtTheSource()
        {
            // The in-game name never carries a version suffix; the Workshop title gets [1.6] by hand.
            // The url is the GPL source pointer the other mods lack.
            XElement about = XDocument.Load(Repo.PathOf("About", "About.xml")).Root;

            Assert.That(about.Element("name").Value, Is.EqualTo("Caravans Critically Enhanced"));
            Assert.That(about.Element("packageId").Value, Is.EqualTo("Zei33.CaravansCriticallyEnhanced"));
            Assert.That(about.Element("url").Value, Is.EqualTo("https://github.com/Zei33/rimworld-caravans-critically-enhanced"));
            Assert.That(
                about.Element("modDependencies").Elements("li").Select(li => li.Element("packageId").Value),
                Is.EqualTo(new[] { "brrainz.harmony" }));
        }

        [Test]
        public void TheHarmonyIdIsTheHouseForm()
        {
            Assert.That(CaravansCriticallyEnhancedMod.HarmonyId, Is.EqualTo("com.zei33.caravanscriticallyenhanced"));
        }
    }
}
