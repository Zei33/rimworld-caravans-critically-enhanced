using System.Collections.Generic;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Utils;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Jobs
{
    /// <summary>
    /// The button that sends a forming caravan off from the map edge its drafted colonists stand by.
    /// </summary>
    /// <remarks>
    /// One button for every selected member: the copies group, and a click on the group runs
    /// <c>ProcessGroupInput</c> once, on the first ready copy, whose colonist the edge cell is found
    /// near; the caravan sent is that colonist's. <c>alsoClickIfOtherInGroupClicked</c> is off, so the
    /// click sounds once.
    /// Identified by its type and <see cref="GroupKey"/>, never by its label, which is translated.
    /// </remarks>
    public class Command_LeaveFromHere : Command
    {
        /// <summary>
        /// The group key every copy shares.
        /// </summary>
        public const int GroupKey = 0x43434502;

        private readonly Pawn pawn;

        /// <summary>
        /// Initializes a new instance of the <see cref="Command_LeaveFromHere"/> class.
        /// </summary>
        /// <param name="pawn">The drafted member.</param>
        /// <param name="state">Whether the button is ready or the pawn stands too far from an edge.</param>
        public Command_LeaveFromHere(Pawn pawn, LeaveButton state)
        {
            this.pawn = pawn;
            defaultLabel = "CaravansCriticallyEnhanced_LeaveFromHere".Translate();
            defaultDesc = "CaravansCriticallyEnhanced_LeaveFromHereDesc".Translate();
            icon = FormCaravanComp.FormCaravanCommand;
            groupKey = GroupKey;
            alsoClickIfOtherInGroupClicked = false;
            if (state == LeaveButton.TooFar)
            {
                Disable("CaravansCriticallyEnhanced_LeaveFromHereTooFar".Translate(LeaveFromHerePolicy.EdgeBand));
            }
        }

        /// <inheritdoc />
        public override void ProcessGroupInput(Event ev, List<Gizmo> group)
        {
            base.ProcessGroupInput(ev, group);

            // One press for the group. The edge cell is found near this copy's colonist, the first ready
            // one the grid took; the caravan is its caravan.
            Lord lord = pawn.GetLord();
            if (!Disabled && lord != null)
            {
                LeaveFromHere.Offer(lord, pawn);
            }
        }
    }
}
