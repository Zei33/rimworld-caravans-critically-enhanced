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

As of Slice 4a these switches take effect: the four Loading ones ("Load work type", "Load into
colonists", "Load while animals gather" and "Helpers bring pen animals"), "Keep leads when drafted" in
the Leads section, "Leave inside allowed areas" and "Avoid threats when leaving" in the leaving
section, with both threat distances, and "Wait for every item" among the stuck caravan fixes, together
with its grace period and the watchdog of twice the grace period. "Avoid threats when leaving" so far
ranks exits only when one is being chosen anyway: at Send, and when an area change moves the exit.
Moving away from a threat that appears while forming comes later. The others do nothing yet.

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

## Keeping leads when drafted

With "Keep leads when drafted" on, a colonist going with a forming caravan keeps hold of the animals
it is leading when you draft it, and you can walk them anywhere. In vanilla drafting drops every lead
at once. While drafted:

- the animals follow at the pace of the slowest of them, so the colonist does not keep stopping to
  wait for them;
- an animal that becomes urgently hungry or exhausted is let go, with a message, since an animal on a
  lead never stops to eat or sleep. Once the caravan is leaving it is kept instead: the colonist is
  walking it out, and you can drop it yourself;
- while the caravan waits for items, downed colonists or sleepers, and in any pause before it
  leaves, the colonist is not undrafted automatically after a while, as vanilla otherwise does. While
  animals are gathered or collected, and while leaving, it still is, so a colonist you forgot about
  cannot hold the caravan up for good;
- a "Drop lead" button lets go of one animal, or opens a menu over every animal the selected
  colonists hold, with "All of them" at the bottom;
- right-clicking a caravan's pen animal that nobody is leading offers "Take the lead of" to the
  nearest selected drafted colonist going with that caravan. Animals that walk by themselves, such
  as dogs, are never offered, and neither is one that is downed or broken. One that is starving or
  exhausted is offered only once the caravan is leaving, where held animals are not let go for
  hunger or tiredness;
- the caravan moving on a stage no longer cancels a drafted colonist's orders.

Undrafted in the stage where animals are gathered, or where they are collected before leaving, the
colonist carries on leading them as the caravan's own job. Undrafted anywhere else, it lets go, with
a message. A colonist that dies, is downed, catches fire, has a mental break or collapses asleep lets
go, as it would in vanilla. Handlers and other colonists not going with a forming caravan drop their
leads when drafted, as in vanilla.

Once the caravan is leaving, an animal let go by the button or by an automatic undraft walks to the
exit by itself if it can. One that cannot, shut in behind a door or a fence gate, or still tied up
somewhere, would leave vanilla's caravan waiting at the exit for good. Instead, as soon as the
caravan's undrafted colonists could fetch it, and every other animal nobody is leading, the caravan
goes back to collecting its animals, with a message, the same way vanilla does when a pen animal joins a leaving caravan: every undrafted
colonist leading animals lets go, and they rope them all again on the way out.

Two things follow from holding on. While animals are gathered, a drafted colonist's animals are not
at the caravan's spot, so the caravan waits for them until you undraft it, or until it is undrafted
automatically. And drafting the whole colony for a raid while a caravan forms takes the held pack
animals along into the fight.

The mod also fixes a vanilla slip that could let an animal fall off its new lead when it was roped by
somebody new: its previous holder is now made to let go first. That fix is always on.

## Choosing where the caravan leaves

Vanilla picks a caravan's exit as the first map edge a search from the pawns' average position
meets, at a random cell there, without asking whether every pawn can reach it, whether it is
fogged, whether enemies stand there, or whether anybody's allowed area includes it. With either of
"Leave inside allowed areas" and "Avoid threats when leaving" on, the mod chooses instead, when you
press Send:

- only edge cells every pawn going can reach, a pen animal counted as a colonist leading it would;
- with "Leave inside allowed areas" on, only cells inside the allowed area of every pawn restricted
  to one (pawns whose area you cannot set, such as pen animals, guests and mechanoids with no
  overseer, do not count, and nor does an area with no cells);
- with "Avoid threats when leaving" on, the cell with the fewest and farthest hostiles near it or near
  the straight way to it, using the two threat distances;
- then the one the fewest regions away from where the pawns stand (regions are the map's own
  pathfinding areas, up to 12 by 12 cells), then the nearest in a straight line to where the caravan
  sets out, then the one facing the caravan's route on the world map.

A pawn that cannot reach any map edge at all is left out of the choice. If nobody can reach one,
vanilla chooses, as before.

If no map edge the caravan can reach is inside every restricted pawn's area, the caravan still forms
and packs, but then waits where it gathered, with a message and an alert naming the pawns that keep it
in. An area that touches the edge only behind a wall, or in fog, does not count. Gathering animals and
items carries on as usual. Downed colonists and babies stay where they are rather than being carried
to the edge or held in someone's arms for the whole wait. One already being carried is put down once
its carrier is back where the caravan gathered; while items are gathered, the carrier's next haul sets
it down where it stands, as vanilla would. When the caravan is held from the moment it is sent, a
colonist who had already picked up a baby puts it down where it stands. A drafted colonist's carry is
yours to decide.
The caravan does not set off to collect its animals. Every 250 ticks it looks again, so it carries on
by itself once you extend an area to a reachable edge or remove a restriction. Force depart sends it
off at once by the best exit ignoring areas, kept up to date while it waits, and turning "Leave inside
allowed areas" off lets it go too.

While the caravan is still gathering, or waiting for a tired member to wake, an area change that puts
its exit outside someone's area moves the exit to one inside it, with a message, or holds the caravan
if there is none. If downed pawns or babies then lie away from the exit it will leave by, the caravan
goes back and carries them there, as soon as a colonist going with it can reach every one of them
(a baby being fed by somebody else is fetched if the feed ends while the caravan still waits). Going back ends with vanilla's own step
forward, which ends the members' jobs and can wake a sleeper early, so a caravan whose exit the mod
left alone never goes back. An exit that nobody can reach any more, because of a new wall or fog, is
replaced quietly. Once the caravan starts collecting its animals to leave, the exit it has stands,
whatever happens to the areas.

Areas are checked only for the exit cell. On the way there, a caravan's pawns ignore allowed areas, as
in vanilla, so the route can still cross outside them.

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
