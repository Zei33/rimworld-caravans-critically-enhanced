# Architecture

This describes the code as it stands. The plan it is being built to, with the reasoning behind each
rule below, is `docs/caravans/PLAN.md` in the workspace that holds this repository.

## Layout

| Folder | Holds |
|---|---|
| `1.6/ModEntry.cs` | The `Mod` subclass: settings, `Harmony.PatchAll()`, the version log in dev mode |
| `1.6/Core/` | Decisions and state: the settings, the stage of a forming caravan, which lords the mod may act on |
| `1.6/Utils/` | Thin access to vanilla: private field accessors, the message helper |
| `1.6/Jobs/` | Work givers and job drivers, from Slice 1 |
| `1.6/Patches/` | Harmony patch classes and XML PatchOperations, from Slice 1 |
| `1.6/Languages/` | Nine languages; `README.md` there is the translators' note |
| `Tests/` | NUnit against the real game assembly; not in the solution |

## The rules every part follows

**The vanilla lord, exactly.** A caravan forms under a `Lord` running `LordJob_FormAndSendCaravan`.
Two widely used mods subclass that job for their own caravans. `FormingLords.IsVanilla` accepts the
player's faction running exactly that type and nothing else, and every feature asks it first.

**Stages are toil positions.** `LordJob_FormAndSendCaravan.CreateGraph` adds thirteen toils in a fixed
order: gather animals, gather items, gather downed pawns, wait, collect animals and leave, each
followed by a pause, then the end toil. The game saves the current toil by its position and restores
it by the same position, so `FormingStages.FromToilIndex` can name the stage from the position
alone. The mod never adds a toil, because that would shift every position after it in a save. It
does not add transitions either: they would not move a toil's position, but the graph is vanilla's
and other mods read it, and leaving it alone is what keeps a save loadable after the mod is removed.

**No components.** Nothing is added to `def.comps`, the map or the game. State that has to survive a
save is written into the lord job's own node by an `ExposeData` postfix, under names starting
`cce_`, which vanilla ignores when the mod is removed.

**One patch class per vanilla method.** When two features need the same method, one patch class
calls each feature in turn and each feature checks its own switch.

**Messages stay out of the History tab.** Every message goes through `CaravanMessages`, which always
passes `historical: false`.

## Settings

`SettingToggles.All` is a table with one row per switch: the section it is drawn in, the node name in
the settings file, its default, its label and tooltip keys, and the switch it depends on, if any.
`CaravansCriticallyEnhancedSettings` stores the values in an array indexed by the switch, so reading
one on a hot path is an array read. A switch whose parent is off reads as off but keeps its own
value. Four numbers sit beside the switches: two threat distances in cells, the grace period for
unreachable items and the stall alert delay, both in ticks.

The window is a single-column scroll view with one heading per section, children indented and
greyed out under a parent that is off, sliders for the numbers and a reset button. The two duration
sliders move in whole hours, because the game's duration text shows nothing finer above an hour.
