using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the staging step in <c>build.sh</c> to an allow-list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It reads a shell script as text, which is a weak kind of test, and it cannot run the script:
    /// <c>build.sh</c> ends by deleting and replacing the installed mod folder. So it checks the one
    /// thing that tells the allow-list shape from the wholesale copy the other mods used to ship, and
    /// that every folder under <c>1.6</c> is either staged or deliberately not.
    /// </para>
    /// <para>
    /// Adapted from Simple Improve's fixture of the same name.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class PackagingTests
    {
        /// <summary>
        /// Folders under <c>1.6</c> that hold only C#, build output or compile-time references, and
        /// must never ship.
        /// </summary>
        private static readonly string[] NeverShipped = { "Core", "Jobs", "Utils", "Libraries", "Assemblies" };

        [Test]
        public void BuildDoesNotStageTheWholeSourceFolder()
        {
            Assert.That(
                Commands(), Does.Not.Contain("cp -r 1.6 release"),
                "build.sh copies 1.6 wholesale, which ships every C# source file and the vendored Harmony "
                + "reference to every subscriber. Stage the folders the game reads by name instead.");
        }

        [Test]
        public void BuildStagesOnlyTheModsOwnAssembly()
        {
            string script = Commands();

            Assert.That(script, Does.Contain("cp 1.6/Assemblies/net472/CaravansCriticallyEnhanced.dll"));
            Assert.That(script, Does.Not.Contain("cp -r 1.6/Assemblies"),
                "Copying the whole Assemblies folder ships whatever else the build left there, and "
                + "ModAssemblyHandler loads every .dll it finds.");
        }

        [Test]
        public void NoSourceFolderIsStaged()
        {
            string script = Commands();

            foreach (string folder in NeverShipped.Where(f => f != "Assemblies"))
            {
                Assert.That(
                    script.Contains("1.6/" + folder), Is.False,
                    "build.sh stages 1.6/" + folder + ", which the game does not read.");
            }
        }

        [Test]
        public void EveryFolderHoldingShippedContentIsStaged()
        {
            // The positive control for the tests above, which pass trivially if a new content folder
            // is added and forgotten. Each top-level folder under 1.6 is either named by build.sh or on
            // the not-shipped list, so adding 1.6/Textures fails here until somebody stages it.
            string script = Commands();
            string[] folders = Directory.GetDirectories(Repo.PathOf("1.6")).Select(Path.GetFileName).ToArray();

            Assert.That(folders, Does.Contain("Languages"), "The scan did not see the one folder that must ship.");

            foreach (string name in folders.Where(f => !NeverShipped.Contains(f) && f != "obj"))
            {
                Assert.That(
                    script, Does.Contain("1.6/" + name),
                    "1.6/" + name + " exists but build.sh never stages it, so it does not ship.");
            }
        }

        [Test]
        public void PatchesIsStagedAsItsXmlAlone()
        {
            // 1.6/Patches holds the C# Harmony patch classes as well as the XML PatchOperations, so
            // naming the folder is not enough: a cp -r of it would pass the folder check above and ship
            // every patch's source.
            string script = Commands();

            Assert.That(script, Does.Contain("cp 1.6/Patches/*.xml"));
            Assert.That(script, Does.Not.Contain("cp -r 1.6/Patches"));
            Assert.That(System.IO.Directory.GetFiles(Repo.PathOf("1.6", "Patches"), "*.cs"), Is.Not.Empty,
                "The premise: Patches holds C# as well as XML.");
        }

        [Test]
        public void TheWorkshopFolderNeverShips()
        {
            // Workshop/ holds the store copy and its sources. Nothing in it is read by the game, and the
            // nine generated pages would only add to the download.
            Assert.That(Commands(), Does.Not.Contain("Workshop"));
        }

        /// <summary>
        /// Reads <c>build.sh</c> with its comment lines removed.
        /// </summary>
        /// <returns>Only the lines the shell executes.</returns>
        /// <remarks>
        /// Required rather than tidy: the comment explaining why the wholesale copy was replaced names
        /// the very command these tests forbid.
        /// </remarks>
        private static string Commands()
        {
            return string.Join(
                "\n",
                File.ReadAllLines(Repo.PathOf("build.sh")).Where(line => !line.TrimStart().StartsWith("#")));
        }
    }
}
