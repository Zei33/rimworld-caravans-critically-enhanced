# Features

What a player can see and do. Each entry is written when its slice lands, from the code that
implements it, so this file never describes a feature the mod does not have.

## Settings window

Options > Mod options > Caravans Critically Enhanced. Six sections, one per group of features:
loading, leads, leaving the map, cargo already on animals, stuck caravans, and coming home and loading
by hand. Every feature has its own switch with a tooltip. A switch that depends on another, such as
the cargo options on "List cargo on animals", is indented beneath it and greyed out, and cannot be
changed, while the parent is off; its own setting is kept for when the parent is turned back on.
Every switch starts on except "Drop unwanted cargo at once".

Four sliders:

| Setting | Default | Range |
|---|---|---|
| Threat distance from an exit | 25 cells | 5 to 60 |
| Threat distance from the route | 12 cells | 3 to 30 |
| Grace period for unreachable items | 1 hour | 1 to 12 hours, in hours |
| Alert after no progress for | 3 hours | 1 hour to 1 day, in hours |

"Reset to defaults" puts every switch and slider back.

As of Slice 2 the Loading switches "Load work type", "Load into colonists", "Load while animals
gather" and "Helpers bring pen animals", "Keep leads when drafted" (for the drafted-member exemption
only) and "Wait for every item" take effect, as does the grace period, and the watchdog of twice the
grace period. The others do nothing yet.

## The Load column

A Work tab column, Load, directly left of Haul. Colonists with it enabled who are not going with a
forming caravan carry its listed items to a pack animal, prisoner or colonist in the caravan. Help
starts when items are being gathered, or as soon as animals are being gathered with "Load while
animals gather" on, and stops when the caravan moves on. Helpers:

- respect forbidden items and their allowed area, for the item and for the caravan member they hand
  it to; caravan members still ignore both, as in vanilla;
- leave items on a shelf or stand the player has closed alone, such as an outfit stand that does not
  allow removing, as caravan members do;
- hand items to a colonist member only with "Load into colonists" on, and never to one who is
  already over its carrying capacity;
- never hand items to a drafted member or one having a mental break;
- while animals are still being gathered, hand items only to rope animals already tied at the
  caravan's spot, to pack animals that walk there without a rope (elephants, and every pack animal
  away from home), and to prisoners;
- can be given the job by hand: right-click a listed item, "Prioritize loading".

Every pawn in an existing save is given the Load priority their Hauling priority had, once, on the
first load with the mod (Transport's, under FSF Complex Jobs). The same load puts back the priorities
of work types from mods loaded after this one, which adding a work type would otherwise move one
column along. Removing the mod moves them back the other way, as removing any work type mod does. A
colony Lifter can do Load. Vanilla's own Hauling helper stands down while the Load column is on.

## Leading animals

With "Helpers bring pen animals" on, colonists with the Load column enabled who are not going with a
forming caravan also lead its pen animals to the caravan's spot and tie them there, while the caravan
gathers its animals, as its own members do. In vanilla only the members fetch them. Helpers:

- take only animals that have to be led on a rope; elephants, dogs and anything else that walks to
  the spot by itself are left to do so, as in vanilla;
- skip an animal somebody is already leading or that is already tied at the spot;
- after roping one animal, pick up more of the same caravan's within ten cells of them, up to ten at
  once, as vanilla's ropers do, and only inside their allowed area;
- do not start when the caravan's spot is outside their allowed area, and say so on the right-click
  menu;
- let go of every animal as soon as the caravan moves past gathering its animals. On Force depart the
  animals then walk to the exit with the caravan, as they do when its own members let go. On a
  cancel the helpers let go first, so the caravan's colonists lead every pen animal back to its pen,
  the helpers' too; animals already tied at the spot stay tied there, as in vanilla;
- can be given the job by hand: right-click a caravan animal, "Prioritize roping".

Colony mechs never do this, as in vanilla, where only colonists rope animals.

## Waiting for every item

Vanilla ends loading when every colonist member is idle and nobody is hauling for the caravan, without
looking at the list. With helpers that can happen with items still on the ground. With "Wait for every
item" on, the caravan keeps loading while a member's own search would still find a listed item to
fetch. Items still on the list that nobody in the caravan can take, because they are walled off,
claimed by someone else, carried by a pawn who is not going, or on a closed stand, are dropped from
the list after the grace period. If nothing at all is loaded for twice that, everything left is
dropped, even items a member could still fetch. Either way a message lists what was left behind.
Force depart still leaves at once, and its confirmation lists the items still to load.

With "Keep leads when drafted" on, loading no longer waits for drafted caravan members, as long as at
least one colonist member is undrafted. In vanilla a drafted member holds loading open until it is
undrafted. With every colonist member drafted, loading waits, as in vanilla.
