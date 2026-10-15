# Architecture

This describes the code as it stands. The plan it is being built to, with the reasoning behind each
rule below, is `docs/caravans/PLAN.md` in the workspace that holds this repository.

## Layout

| Folder | Holds |
|---|---|
| `1.6/ModEntry.cs` | The `Mod` subclass: settings, each patch class applied on its own, so one failure cannot stop the rest, the version log in dev mode |
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

## Loading

The Load work giver is a scanner with no thing request, so no region search is built, and its
`ShouldSkip` is one pass over the map's lords. It skips any pawn in a lord, since a duty rewrites a
job's lord, and with nothing to load it drops what it remembered, because one instance lives all
session and would otherwise hold the last save's map. Its search set comes from `LoadingIndex`, rebuilt at
most once per tick and frame per map from every vanilla forming lord at a stage open to helpers:
every listed thing on the map, the first caravan to list a thing owning it. The job it hands out is
vanilla's own gather job pointed at that caravan, so vanilla's driver, its count of hauls in progress
and its completion check all treat the helper as they would vanilla's helper. A prefix stands
vanilla's helper down meanwhile.

Three patches shape what a helper does once it has the job: a postfix on the driver's carrier test
applies `CarrierRule`, a postfix on its `MakeNewToils` ends the job when the caravan moves past the
gathering stages, and the completion guard, a postfix on `AllItemsLoadedOntoCaravan`, keeps loading
open while listed items are still there to fetch. The guard acts only while items are being
gathered. It decides in `CompletionGuard`, which is pure and also holds every rule its reader
applies; `LoadingCompletion` reads the lord for it, asking vanilla's own `FindThingToHaul` whether
any undrafted member would fetch something, and from the lord job's tick takes expired items off
the list once a fresh look confirms nobody started hauling meanwhile.

The Load column's second giver leads pen animals the same way: a scanner with no thing request over
`AnimalGatherIndex`, the rope animals of every vanilla forming lord that is gathering animals, and a
job of its own whose driver subclasses vanilla's `JobDriver_RopeToDestination`. Vanilla's own
gather-animals driver reads its destination from the roper's duty, which a helper does not have, so
this one carries the spot as target B. The base class supplies the walk, the opportunistic pick-up
and the finish action that drops every rope, and a fail condition ends the job when the caravan
moves on, so a helper never holds an animal into the collect stage.

Keeping leads while drafted is four patches that have to agree, because vanilla cuts a drafted
roper's leads in four places: drafting ends the rope job, whose finish action drops the ropes; the
rope tick breaks a drafted roper's ropes; it breaks them again for a roper not in a rope job; and the
follow giver gives an animal nothing to do unless its roper is in a rope job. The draft setter opens
a `LeadKeepScope` that the `DropRopes` prefix honours, the rope tick prefix takes over for a held
roper, and the follow giver postfix hands out vanilla's own follow job. `LeadPolicy` decides each of
those, and `LeadUtility` reads the roper for it.

Two pieces exist because of where vanilla puts things. The message that undrafting dropped the leads
is posted by the rope tick, not the draft setter: a mental break undrafts before it is recorded, so
the setter leaves a `LeadUndraftNotices` entry and the next rope tick, which can tell, posts it or
not. And the shared lord tick postfix, after the loading pass, sends a leaving caravan back to
collecting when a pen animal let go there cannot reach the exit and every loose one would be
fetched, because vanilla's leaving stage has no timeout and nothing else would ever end the wait.

The first load of a save made without the mod also puts back the work priorities of mods loaded
after it, which the inserted work type shifts along by one, and then copies each pawn's hauling
priority into Load.

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
