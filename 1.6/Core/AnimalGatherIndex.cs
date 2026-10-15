using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The caravan animals helpers may lead to the gathering spot, per map.
    /// </summary>
    /// <remarks>
    /// Built and cached like <see cref="LoadingIndex"/>: at most once per tick and once per frame, for
    /// the same reasons. The gathering spot is read from each animal's own duty, which
    /// <c>LordToil_PrepareCaravan_RopeAnimals.UpdateAllDuties</c> gives every rope animal with the spot
    /// as its focus, so the lord job's private meeting point is never needed.
    /// </remarks>
    public static class AnimalGatherIndex
    {
        /// <summary>
        /// The empty set, shared. Nothing may add to it.
        /// </summary>
        public static readonly List<Thing> None = new List<Thing>();

        private static readonly ConditionalWeakTable<Map, MapIndex> Indices = new ConditionalWeakTable<Map, MapIndex>();

        /// <summary>
        /// Determines whether any vanilla forming caravan on a map is gathering its animals.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns><c>true</c> when at least one is.</returns>
        public static bool AnyGathering(Map map)
        {
            if (map == null)
            {
                return false;
            }

            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (FormingLords.StageOf(lords[i]) == FormingStage.GatherAnimals)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the animals waiting to be led, on a map.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The set, which callers must not change.</returns>
        public static List<Thing> AnimalsOn(Map map)
        {
            return map == null ? None : Fresh(map).Animals;
        }

        /// <summary>
        /// Finds the caravan an animal in the set belongs to and the spot it is going to.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="animal">The animal.</param>
        /// <param name="lord">The caravan, when found.</param>
        /// <param name="spot">The gathering spot, when found.</param>
        /// <returns><c>true</c> when the animal is in the set.</returns>
        public static bool TryGetDestination(Map map, Thing animal, out Lord lord, out IntVec3 spot)
        {
            lord = null;
            spot = IntVec3.Invalid;
            if (map == null || !(animal is Pawn pawn) || !Fresh(map).Members.Contains(pawn))
            {
                return false;
            }

            // The set is from earlier this tick and frame. A caravan cancelled or moved on since, or an
            // animal downed or taken out of it, has changed the animal's lord and duty, so the rule is
            // asked again rather than trusted.
            if (!NeedsLeading(pawn))
            {
                return false;
            }

            lord = pawn.GetLord();
            spot = pawn.mindState.duty.focus.Cell;
            return true;
        }

        /// <summary>
        /// Determines whether one animal is waiting to be led, as the index decides it.
        /// </summary>
        /// <param name="animal">The animal.</param>
        /// <returns><c>true</c> when a helper may offer to lead it.</returns>
        /// <remarks>
        /// Also used by the rope driver to pick up more animals on its way, so that an opportunistic
        /// pick-up obeys the same rule as the search.
        /// </remarks>
        public static bool NeedsLeading(Pawn animal)
        {
            Lord lord = animal.GetLord();
            if (!FormingLords.IsVanilla(lord))
            {
                return false;
            }

            PawnDuty duty = animal.mindState?.duty;
            bool hasGatherDuty = duty != null && duty.def == DutyDefOf.PrepareCaravan_GatherAnimals && duty.focus.Cell.IsValid;
            bool needsRope = AnimalPenUtility.NeedsToBeManagedByRope(animal);

            return RopeCandidates.NeedsLeading(
                FormingLords.StageOf(lord),
                needsRope,
                animal.roping.IsRopedByPawn,
                hasGatherDuty && animal.roping.IsRopedToSpot && animal.roping.RopedToSpot == duty.focus.Cell,
                hasGatherDuty);
        }

        private static MapIndex Fresh(Map map)
        {
            MapIndex index = Indices.GetValue(map, _ => new MapIndex());
            int tick = Find.TickManager.TicksGame;
            int frame = Time.frameCount;

            if (index.Tick == tick && index.Frame == frame)
            {
                return index;
            }

            index.Tick = tick;
            index.Frame = frame;
            index.Animals.Clear();
            index.Members.Clear();

            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                Lord lord = lords[i];
                if (FormingLords.StageOf(lord) != FormingStage.GatherAnimals)
                {
                    continue;
                }

                List<Pawn> members = lord.ownedPawns;
                for (int m = 0; m < members.Count; m++)
                {
                    Pawn member = members[m];
                    if (member.Spawned && member.Map == map && !member.Dead && member.RaceProps.Animal && NeedsLeading(member) && index.Members.Add(member))
                    {
                        index.Animals.Add(member);
                    }
                }
            }

            return index;
        }

        private sealed class MapIndex
        {
            public readonly List<Thing> Animals = new List<Thing>();

            public readonly HashSet<Pawn> Members = new HashSet<Pawn>();

            public int Tick = -1;

            public int Frame = -1;
        }
    }
}
