using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Reads def XML from disk: the installed game's shipped defs, and this mod's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This parses files rather than going through <c>DefDatabase</c>, which only a running game
    /// populates. The point of every check built on it is that a name written in this mod's XML
    /// refers to a def or a node that really ships, and reading the files is the only way to answer
    /// that out here.
    /// </para>
    /// <para>
    /// Copied from Great Escape's fixture of the same name; only the repository root changed.
    /// </para>
    /// <para>
    /// It is not the game's loader. The all-module views read every installed DLC's XML whether or
    /// not the DLC is active, and nothing here applies patches. The Core-only views leave out Core's
    /// top-level defs gated by MayRequire, and are what hold the mod to needing no DLC; MayRequire
    /// on a nested element is not modelled. Anomaly is not installed on this machine, so "every DLC"
    /// here means every installed one.
    /// </para>
    /// </remarks>
    internal static class DefXml
    {
        private static readonly Lazy<List<XElement>> gameDefNodes =
            new Lazy<List<XElement>>(() => LoadTopLevelNodes(GameDefFiles(coreOnly: false)).ToList());

        private static readonly Lazy<List<XElement>> coreDefNodes =
            new Lazy<List<XElement>>(() => LoadTopLevelNodes(GameDefFiles(coreOnly: true))
                .Where(n => n.Attribute("MayRequire") == null && n.Attribute("MayRequireAnyOf") == null)
                .ToList());

        private static readonly Lazy<List<XElement>> modDefNodes =
            new Lazy<List<XElement>>(() => LoadTopLevelNodes(ModFiles("Defs")).ToList());

        /// <summary>
        /// The root of this repository, found by walking up from the test assembly.
        /// </summary>
        public static string RepoRoot
        {
            get
            {
                return Repo.Root;
            }
        }

        /// <summary>
        /// Every top-level node in the game's shipped Defs, every installed DLC included, abstract
        /// parents included.
        /// </summary>
        public static IReadOnlyList<XElement> GameDefNodes => gameDefNodes.Value;

        /// <summary>
        /// Every top-level node in Data/Core/Defs alone, less those gated on a DLC by MayRequire:
        /// what the game has with no DLC active.
        /// </summary>
        public static IReadOnlyList<XElement> CoreDefNodes => coreDefNodes.Value;

        /// <summary>
        /// Every top-level node in this mod's Defs.
        /// </summary>
        public static IReadOnlyList<XElement> ModDefNodes => modDefNodes.Value;

        /// <summary>
        /// The defNames of one def type, from the game and this mod together.
        /// </summary>
        /// <param name="defType">The def type's element name, for example <c>ThingDef</c>.</param>
        /// <param name="coreOnly">Whether to leave out every DLC.</param>
        public static ISet<string> DefNamesOfType(string defType, bool coreOnly = false)
        {
            return new HashSet<string>(
                (coreOnly ? CoreDefNodes : GameDefNodes).Concat(ModDefNodes)
                    .Where(n => n.Name.LocalName == defType)
                    .Select(n => n.Element("defName")?.Value?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// The values of the Name attribute on nodes of one def type, the handles ParentName uses.
        /// </summary>
        public static ISet<string> InheritanceNamesOfType(string defType, bool includeMod, bool coreOnly = false)
        {
            IEnumerable<XElement> game = coreOnly ? CoreDefNodes : GameDefNodes;
            IEnumerable<XElement> nodes = includeMod ? game.Concat(ModDefNodes) : game;
            return new HashSet<string>(
                nodes.Where(n => n.Name.LocalName == defType)
                    .Select(n => n.Attribute("Name")?.Value)
                    .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// One XmlDocument holding shipped defs under a single &lt;Defs&gt; root, which is the shape
        /// RimWorld applies PatchOperations to.
        /// </summary>
        /// <param name="coreOnly">Whether to leave out every DLC.</param>
        public static XmlDocument CombinedGameDefs(bool coreOnly)
        {
            var combined = new XmlDocument();
            XmlElement root = combined.CreateElement("Defs");
            combined.AppendChild(root);

            foreach (string file in GameDefFiles(coreOnly))
            {
                var document = new XmlDocument();
                try
                {
                    document.Load(file);
                }
                catch (XmlException)
                {
                    continue;
                }

                if (document.DocumentElement == null)
                {
                    continue;
                }

                foreach (XmlNode node in document.DocumentElement.ChildNodes)
                {
                    if (node is XmlElement)
                    {
                        root.AppendChild(combined.ImportNode(node, deep: true));
                    }
                }
            }

            return combined;
        }

        /// <summary>
        /// A def and its ParentName chain, root first, looked up in this mod and in Core.
        /// </summary>
        public static List<XElement> InheritanceChain(string defType, string defName)
        {
            IEnumerable<XElement> pool = ModDefNodes.Concat(CoreDefNodes).Where(n => n.Name.LocalName == defType).ToList();
            var chain = new List<XElement>();
            XElement node = pool.Single(n => n.Element("defName")?.Value.Trim() == defName);

            while (node != null)
            {
                chain.Insert(0, node);
                string parent = node.Attribute("ParentName")?.Value;
                node = parent == null ? null : pool.Single(n => n.Attribute("Name")?.Value == parent);
            }

            return chain;
        }

        /// <summary>
        /// The items of one list field on a def after XmlInheritance, which appends a child's list
        /// items to its parent's unless the child's list says Inherit="False".
        /// </summary>
        public static List<string> InheritedList(string defType, string defName, string field)
        {
            var items = new List<string>();
            foreach (XElement level in InheritanceChain(defType, defName))
            {
                XElement list = level.Element(field);
                if (list == null)
                {
                    continue;
                }

                if (string.Equals(list.Attribute("Inherit")?.Value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    items.Clear();
                }

                items.AddRange(list.Elements("li").Select(li => li.Value.Trim()));
            }

            return items;
        }

        /// <summary>
        /// The text of one scalar field on a def after XmlInheritance, where the nearest definition
        /// wins, or null when no level sets it.
        /// </summary>
        public static string InheritedValue(string defType, string defName, string field)
        {
            return InheritanceChain(defType, defName)
                .Select(level => level.Element(field)?.Value.Trim())
                .LastOrDefault(v => v != null);
        }

        /// <summary>
        /// The XML files under one folder of this mod's 1.6 load folder.
        /// </summary>
        public static IEnumerable<string> ModFiles(string folder)
        {
            string path = Path.Combine(RepoRoot, "1.6", folder);
            return Directory.Exists(path)
                ? Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
                : Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GameDefFiles(bool coreOnly)
        {
            string rimWorldDir = Environment.GetEnvironmentVariable("RimWorldDir");
            if (string.IsNullOrEmpty(rimWorldDir))
            {
                throw new InvalidOperationException(
                    "RimWorldDir is not set. The tests need it to find the game's Defs, and the build " +
                    "needs it to resolve the game assemblies.");
            }

            string dataDir = Path.Combine(rimWorldDir, "Data");
            if (!Directory.Exists(dataDir))
            {
                throw new DirectoryNotFoundException("No Data directory under RimWorldDir: " + dataDir);
            }

            foreach (string module in Directory.GetDirectories(dataDir).OrderBy(d => d, StringComparer.Ordinal))
            {
                if (coreOnly && Path.GetFileName(module) != "Core")
                {
                    continue;
                }

                string defsDir = Path.Combine(module, "Defs");
                if (!Directory.Exists(defsDir))
                {
                    continue;
                }

                foreach (string file in Directory.GetFiles(defsDir, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    yield return file;
                }
            }
        }

        private static IEnumerable<XElement> LoadTopLevelNodes(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(file);
                }
                catch (XmlException)
                {
                    // A shipped file the game itself would reject is not this mod's problem. A file of
                    // this mod's that fails to parse is caught by SourceTests.EveryModXmlFileParses.
                    continue;
                }

                if (document.Root == null)
                {
                    continue;
                }

                foreach (XElement node in document.Root.Elements())
                {
                    yield return node;
                }
            }
        }
    }
}
