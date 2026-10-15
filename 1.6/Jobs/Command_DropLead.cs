using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// The button that lets a drafted caravan colonist drop one or all of its leads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One button for every selected holder: the copies group, and a click on the group builds a single
    /// menu over every animal any of them holds, or drops the one animal directly when only one is
    /// held (<see cref="LeadPolicy.DropLead"/>). The grid calls <c>ProcessGroupInput</c> once per
    /// click on the copy clicked, whatever the flags say. <c>alsoClickIfOtherInGroupClicked</c> only
    /// decides whether the other copies also get <c>ProcessInput</c>, which for a command plays the
    /// click sound, so it is off to play that once.
    /// </para>
    /// <para>
    /// Identified by its type and <see cref="GroupKey"/>, never by its label, which is translated.
    /// </para>
    /// </remarks>
    public class Command_DropLead : Command
    {
        /// <summary>
        /// The group key every copy shares.
        /// </summary>
        public const int GroupKey = 0x43434501;

        private readonly Pawn roper;

        /// <summary>
        /// Initializes a new instance of the <see cref="Command_DropLead"/> class.
        /// </summary>
        /// <param name="roper">The drafted colonist holding the leads.</param>
        public Command_DropLead(Pawn roper)
        {
            this.roper = roper;
            defaultLabel = "CaravansCriticallyEnhanced_DropLead".Translate();
            defaultDesc = "CaravansCriticallyEnhanced_DropLeadDesc".Translate();
            icon = TexCommand.ReleaseAnimals;
            groupKey = GroupKey;
            alsoClickIfOtherInGroupClicked = false;
        }

        /// <inheritdoc />
        public override void ProcessGroupInput(Event ev, List<Gizmo> group)
        {
            base.ProcessGroupInput(ev, group);

            var held = new List<(Pawn Roper, Pawn Animal)>();
            var ropers = new HashSet<Pawn>();
            foreach (Gizmo gizmo in group)
            {
                if (gizmo is Command_DropLead command && !command.Disabled && command.roper.roping != null)
                {
                    foreach (Pawn animal in command.roper.roping.Ropees)
                    {
                        held.Add((command.roper, animal));
                        ropers.Add(command.roper);
                    }
                }
            }

            switch (LeadPolicy.DropLead(held.Count))
            {
                case DropLeadAction.DropDirectly:
                    held[0].Roper.roping.DropRope(held[0].Animal);
                    break;

                case DropLeadAction.Menu:
                    Find.WindowStack.Add(new FloatMenu(MenuFor(held, ropers.Count > 1)));
                    break;
            }
        }

        private static List<FloatMenuOption> MenuFor(List<(Pawn Roper, Pawn Animal)> held, bool severalRopers)
        {
            var options = new List<FloatMenuOption>();
            foreach ((Pawn holder, Pawn animal) in held)
            {
                string label = severalRopers
                    ? "CaravansCriticallyEnhanced_DropLeadOf".Translate(animal.LabelShort, holder.LabelShort).Resolve()
                    : animal.LabelShortCap;

                options.Add(new FloatMenuOption(label, () =>
                {
                    if (holder.roping != null && holder.roping.Ropees.Contains(animal))
                    {
                        holder.roping.DropRope(animal);
                    }
                }));
            }

            options.Add(new FloatMenuOption("CaravansCriticallyEnhanced_DropLeadAll".Translate(), () =>
            {
                foreach ((Pawn holder, Pawn animal) in held)
                {
                    if (holder.roping != null && holder.roping.Ropees.Contains(animal))
                    {
                        holder.roping.DropRope(animal);
                    }
                }
            }));

            return options;
        }
    }
}
