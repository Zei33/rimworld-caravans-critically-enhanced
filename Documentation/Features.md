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

As of Slice 0 none of the switches changes anything in game yet.
