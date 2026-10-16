using System.Collections.Generic;
using System.Text;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// Says that a forming caravan is waiting because no map edge is inside every restricted pawn's
    /// allowed area, names the pawns, and says how to let it go.
    /// </summary>
    /// <remarks>
    /// The game builds one of every alert class by reflection and asks each for its report every few
    /// frames, so the report only reads the held state the exit pass keeps (<see cref="CaravanExitStates"/>);
    /// it plans nothing.
    /// </remarks>
    public class Alert_CaravanNoPermittedExit : Alert
    {
        private readonly List<Pawn> culprits = new List<Pawn>();

        private readonly List<Pawn> blockers = new List<Pawn>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Alert_CaravanNoPermittedExit"/> class.
        /// </summary>
        public Alert_CaravanNoPermittedExit()
        {
            defaultPriority = AlertPriority.High;
        }

        /// <inheritdoc />
        public override string GetLabel()
        {
            return "CaravansCriticallyEnhanced_AlertNoPermittedExit".Translate();
        }

        /// <inheritdoc />
        public override TaggedString GetExplanation()
        {
            var names = new StringBuilder();
            foreach (Pawn pawn in blockers)
            {
                names.AppendLine("  - " + pawn.LabelShortCap);
            }

            return "CaravansCriticallyEnhanced_AlertNoPermittedExitDesc".Translate(names.ToString().TrimEndNewlines());
        }

        /// <inheritdoc />
        public override AlertReport GetReport()
        {
            culprits.Clear();
            blockers.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Lord> lords = maps[m].lordManager.lords;
                for (int i = 0; i < lords.Count; i++)
                {
                    LordJob_FormAndSendCaravan job = FormingLords.VanillaJobOf(lords[i]);
                    if (!CaravanExitStates.IsHolding(job))
                    {
                        continue;
                    }

                    // Somebody named by the last pass may have died or left the caravan since.
                    int before = blockers.Count;
                    List<Pawn> named = CaravanExitStates.For(job).BlockingPawns;
                    for (int n = 0; n < named.Count; n++)
                    {
                        if (!named[n].Dead && named[n].GetLord() == lords[i])
                        {
                            blockers.Add(named[n]);
                            culprits.Add(named[n]);
                        }
                    }

                    if (blockers.Count == before)
                    {
                        culprits.AddRange(lords[i].ownedPawns);
                    }
                }
            }

            return AlertReport.CulpritsAre(culprits);
        }
    }
}
