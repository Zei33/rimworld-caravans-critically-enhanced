using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// Says that the exit a forming caravan keeps has hostiles near it or on the way to it.
    /// </summary>
    /// <remarks>
    /// The caravan keeps it because no safe exit is open, or it is committed, or it moved too recently;
    /// this only warns. The report reads the flag the exit pass keeps
    /// (<see cref="CaravanExitStates.IsExitThreatened"/>) and plans nothing, since the game asks every
    /// alert for its report every few frames.
    /// </remarks>
    public class Alert_CaravanExitThreatened : Alert
    {
        private readonly List<GlobalTargetInfo> exits = new List<GlobalTargetInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Alert_CaravanExitThreatened"/> class.
        /// </summary>
        public Alert_CaravanExitThreatened()
        {
            defaultPriority = AlertPriority.Medium;
        }

        /// <inheritdoc />
        public override string GetLabel()
        {
            return "CaravansCriticallyEnhanced_AlertExitThreatened".Translate();
        }

        /// <inheritdoc />
        public override TaggedString GetExplanation()
        {
            return "CaravansCriticallyEnhanced_AlertExitThreatenedDesc".Translate();
        }

        /// <inheritdoc />
        public override AlertReport GetReport()
        {
            exits.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Lord> lords = maps[m].lordManager.lords;
                for (int i = 0; i < lords.Count; i++)
                {
                    LordJob_FormAndSendCaravan job = FormingLords.VanillaJobOf(lords[i]);
                    if (CaravanExitStates.IsExitThreatened(job))
                    {
                        exits.Add(new GlobalTargetInfo(job.ExitSpot, maps[m]));
                    }
                }
            }

            return exits.Count > 0 ? AlertReport.CulpritsAre(exits) : AlertReport.Inactive;
        }
    }
}
