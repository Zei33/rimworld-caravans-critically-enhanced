using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the Load work type and its mech patch to the installed game's own XML.
    /// </summary>
    [TestFixture]
    public class LoadingDefsTests
    {
        private const string Loading = "CCE_Loading";

        [Test]
        public void TheLoadColumnSitsDirectlyLeftOfHaul()
        {
            // The Work tab draws columns in descending naturalPriority. Directly left of Haul means
            // above Hauling's and below every other shipped work type that is above it, and equal to
            // none, so the order never depends on load order.
            List<(string Name, int Priority)> shipped = DefXml.GameDefNodes
                .Where(n => n.Name.LocalName == "WorkTypeDef" && n.Element("defName") != null && n.Element("naturalPriority") != null)
                .Select(n => (n.Element("defName").Value.Trim(), int.Parse(n.Element("naturalPriority").Value.Trim())))
                .ToList();

            int hauling = shipped.Single(w => w.Name == "Hauling").Priority;
            int nextAbove = shipped.Where(w => w.Priority > hauling).Min(w => w.Priority);
            int loading = int.Parse(ModWorkType().Element("naturalPriority").Value);

            Assert.That(shipped.Count, Is.GreaterThan(20), "Shipped work types read.");
            Assert.That(loading, Is.GreaterThan(hauling));
            Assert.That(loading, Is.LessThan(nextAbove));
            Assert.That(shipped.Select(w => w.Priority), Does.Not.Contain(loading));
        }

        [Test]
        public void LoadIsRuledOutByExactlyWhatRulesOutHauling()
        {
            // Backstories and traits disable work by tag, so equal tags mean an equal set of pawns
            // who cannot do it.
            XElement hauling = DefXml.CoreDefNodes.Single(n => n.Name.LocalName == "WorkTypeDef" && n.Element("defName")?.Value == "Hauling");

            Assert.That(Tags(ModWorkType()), Is.EquivalentTo(Tags(hauling)));
            Assert.That(Tags(hauling), Is.Not.Empty);
        }

        [Test]
        public void LoadStartsOnAndAddsNoStartPageBlock()
        {
            XElement loading = ModWorkType();

            Assert.That(loading.Element("alwaysStartActive")?.Value, Is.EqualTo("true"));
            Assert.That(loading.Element("requireCapableColonist")?.Value, Is.EqualTo("false"),
                "requireCapableColonist is read only by the scenario start page, where true adds a block.");
            Assert.That(loading.Element("relevantSkills"), Is.Null, "Loading has no skill, like hauling.");
        }

        [Test]
        public void TheMechPatchAddsLoadToTheLifterAndNothingElseInTheShippedGame()
        {
            XElement operation = MechOperation();
            string xpath = operation.Element("xpath").Value;

            XmlDocument combined = DefXml.CombinedGameDefs(coreOnly: false);
            XmlNodeList selected = combined.SelectNodes(xpath);

            Assert.That(selected, Is.Not.Null);
            var owners = new List<string>();
            foreach (XmlNode node in selected)
            {
                owners.Add(node.ParentNode.ParentNode["defName"]?.InnerText);
            }

            // Patches run before XmlInheritance.Resolve, so only lists a def declares itself are seen.
            // In 1.6.4871 that is the Lifter alone; a change here is a decision, not a fix.
            Assert.That(owners, Is.EqualTo(new[] { "Mech_Lifter" }));
        }

        [Test]
        public void TheMechPatchIsSilentWithoutBiotechAndAppendsOnlyLoad()
        {
            XElement operation = MechOperation();

            Assert.That(operation.Attribute("Class")?.Value, Is.EqualTo("PatchOperationAdd"));
            Assert.That(operation.Attribute("success"), Is.Null, "success as an attribute is read by nothing.");
            Assert.That(operation.Element("success")?.Value, Is.EqualTo("Always"));
            Assert.That(operation.Attribute("MayRequire"), Is.Null, "MayRequire on an Operation is read by nothing.");
            Assert.That(operation.Element("xpath").Value, Does.Contain("/race/mechEnabledWorkTypes"),
                "PatchOperationAdd appends to what the xpath selects, so it must end at the list.");
            Assert.That(operation.Element("xpath").Value.Trim(), Does.EndWith("]"));
            Assert.That(operation.Element("value").Elements("li").Select(li => li.Value), Is.EqualTo(new[] { Loading }));
        }

        [Test]
        public void TheVanillaHelperStillShipsWhereTheMigrationLooksForIt()
        {
            // The migration copies the priority of whatever work type HelpGatheringItemsForCaravan is
            // in, and the prefix stands that giver down. Both need it to exist under this name.
            XElement helper = DefXml.CoreDefNodes.Single(n => n.Name.LocalName == "WorkGiverDef"
                && n.Element("defName")?.Value == Patches.Patch_Pawn_WorkSettings_ExposeData.VanillaHelperGiver);

            Assert.That(helper.Element("workType")?.Value, Is.EqualTo("Hauling"));
            Assert.That(helper.Element("giverClass")?.Value, Is.EqualTo("WorkGiver_HelpGatheringItemsForCaravan"));
        }

        private static XElement ModWorkType()
        {
            return DefXml.ModDefNodes.Single(n => n.Name.LocalName == "WorkTypeDef" && n.Element("defName")?.Value == Loading);
        }

        private static XElement MechOperation()
        {
            return XDocument.Load(Repo.PathOf("1.6", "Patches", "MechWorkTypes.xml")).Root.Elements("Operation").Single();
        }

        private static List<string> Tags(XElement workType)
        {
            return workType.Element("workTags").Elements("li").Select(li => li.Value.Trim()).ToList();
        }
    }
}
