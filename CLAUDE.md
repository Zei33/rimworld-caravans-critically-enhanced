# Caravans Critically Enhanced

No Workshop id yet; unpublished, About.xml `modVersion` 0.1.0. packageId
`Zei33.CaravansCriticallyEnhanced`, Harmony id `com.zei33.caravanscriticallyenhanced`, namespace
`CaravansCriticallyEnhanced` with `.Core`, `.Jobs`, `.Patches` and `.Utils` beneath it. defNames carry
the prefix `CCE_`; Keyed keys carry `CaravansCriticallyEnhanced_`.

**Read the plan before changing anything.** It lives in the workspace, not here, because the
research behind it quotes reddit threads and describes other modders' patch surfaces:
`/Users/matthewscott/Programming/rimworld/docs/caravans/README.md` is the index, `PLAN.md` beside it is
the plan of record, `design/` holds the three detailed subsystem designs and `research/` the vanilla
mechanics reports with their verifier corrections. Where a design and `PLAN.md` disagree, `PLAN.md`
wins. Where a research report and its verifier disagree, the verifier wins.

## What it does

Fixes caravan forming. When finished it will:

1. add a Load work type, next to Haul, so colonists who are not going help load a forming caravan;
2. keep a drafted colonist's leads, with drop-lead and take-lead controls;
3. and 4. list cargo already on animals in the formation window, and move it between animals;
5. choose an exit inside every restricted pawn's allowed area and away from threats, wait with an
   alert when none exists, and let drafted colonists leave from the edge they stand at;

plus stuck-caravan fixes, loaded-cargo protection, unloading returning pack animals at a stockpile,
and a better manual load. The work is in slices; `docs/ROADMAP.md` in the workspace says which have
landed. **As of Slice 1 the Load work type and the completion guard are in; nothing else changes
the game yet.**

## Architecture

| Type | File | Role |
|---|---|---|
| `CaravansCriticallyEnhancedMod : Mod` | `1.6/ModEntry.cs` | Loads the settings, applies each patch class on its own with an error per failure, dev-mode version log |
| `CaravansCriticallyEnhancedSettings : ModSettings` | `1.6/Core/CaravansCriticallyEnhancedSettings.cs` | The switches and four numbers, the scroll-view settings window |
| `SettingToggles`, `ToggleSpec`, `Toggle`, `SettingGroup` | `1.6/Core/SettingToggles.cs` | One table holding every switch: section, scribe key, default, translation keys, parent |
| `FormingStage`, `FormingStages` | `1.6/Core/FormingStages.cs` | The vanilla forming graph's toil index as a named stage. Pure |
| `FormingLords` | `1.6/Core/FormingLords.cs` | `IsVanilla`: the exact-type test every feature asks before touching a lord. `StageOf`. The decisions take a lord's parts; the `Lord` overloads delegate |
| `CaravanLordAccess` | `1.6/Utils/CaravanLordAccess.cs` | `FieldRefAccess` to the lord job's `exitSpot` and `meetingPoint` and the toils' copies of the exit, each resolved lazily and on its own, null if its field has gone |
| `CaravanMessages` | `1.6/Utils/CaravanMessages.cs` | The only caller of `Messages.Message`, always `historical: false` |
| `JobMemo<TAsker, TSubject, TAnswer>` | `1.6/Core/JobMemo.cs` | Copied from Simple Improve: lets a work giver's `HasJobOnThing` and `JobOnThing` answer from one computation |
| `CaravansCriticallyEnhancedDefOf` | `1.6/Core/CaravansCriticallyEnhancedDefOf.cs` | `CCE_Loading` |
| `WorkGiver_CCE_LoadCaravan : WorkGiver_Scanner` | `1.6/Jobs/WorkGiver_CCE_LoadCaravan.cs` | The Load giver. No thing request; `ShouldSkip` skips any pawn in a lord and is free when nothing is forming, and then forgets what it held; the search set from `LoadingIndex`; one memoised `BuildJob` behind both scan entry points, carrier first; hands out vanilla's own `PrepareCaravan_GatherItems` job with `job.lord` set |
| `LoadingIndex`, `LoadingClaim` | `1.6/Core/LoadingIndex.cs` | Per map, rebuilt at most once per tick and frame: which listed items helpers may fetch (not in a closed haul source) and which caravan and row each belongs to |
| `LoadingCandidates` | `1.6/Core/LoadingCandidates.cs` | The search set's rules: no nulls, first caravan to list a thing owns it, a `List`. Pure |
| `CarrierRule` | `1.6/Core/CarrierRule.cs` | Which member a helper may hand an item to: none drafted, in a mental state or outside the helper's area; no overloaded colonists, colonists only with the setting, none while animals gather, rope animals only once tied at their own spot. Pure |
| `CompletionGuard`, `CompletionVerdict` | `1.6/Core/CompletionGuard.cs` | Whether a caravan vanilla calls loaded really is: hold while items are takeable, hold blocked items for the grace period, let go after the watchdog. Also every rule its reader applies: the stage it acts in, who counts as a waiting member and as a taker, how a listed thing counts, when an expiry is due and confirmed. Clocks travel as one `GuardClocks` value. Pure |
| `LoadingCompletion` | `1.6/Core/LoadingCompletion.cs` | The guard's reader: asks vanilla's own `FindThingToHaul` for each undrafted member, counts live units, keeps the clocks per lord job, applies the drafted-member exemption, clears rows with no things left while holding, and lets expired items go with a message |
| `LoadingMigration` | `1.6/Core/LoadingMigration.cs` | The one-time Load priority copy, and `MakeRoom`, which puts later work types' priorities back in their own slots on the first load. Pure |

Defs: `CCE_Loading` (WorkTypeDef, naturalPriority 310, the column directly left of Haul, Hauling's
work tags) and `CCE_LoadCaravan` (WorkGiverDef, priorityInType 110). `1.6/Patches/MechWorkTypes.xml`
adds `CCE_Loading` to every `mechEnabledWorkTypes` list that names Hauling, which in the shipped game
is the Lifter alone.

Harmony surface, one class per vanilla target in `1.6/Patches/`, each applied on its own by `ApplyPatches`:

| Class | Target | Kind | Does |
|---|---|---|---|
| `Patch_WorkGiver_HelpGatheringItemsForCaravan_NonScanJob` | `WorkGiver_HelpGatheringItemsForCaravan.NonScanJob` | Prefix | Stands vanilla's helper down while Load is on, unless a subclass lord is forming on the map |
| `Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier` | `JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier` | Postfix | `CarrierRule` for helpers only; hot path, early exits first |
| `Patch_JobDriver_PrepareCaravan_GatherItems_MakeNewToils` | `JobDriver_PrepareCaravan_GatherItems.MakeNewToils` | Postfix | A helper's job fails once the caravan leaves the gathering stages |
| `Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan` | `CaravanFormingUtility.AllItemsLoadedOntoCaravan` | Postfix, last | The single owner of the answer, in the item-gathering stage only: drafted-member exemption, then the completion guard |
| `Patch_LordJob_FormAndSendCaravan_LordJobTick` | `LordJob_FormAndSendCaravan.LordJobTick` | Postfix | Every 250 ticks per lord, staggered: the progress clock and letting expired items go |
| `Patch_Pawn_WorkSettings_ExposeData` | `Pawn_WorkSettings.ExposeData` | Postfix | Saves `cceLoadingInit`; on the first load without it, realigns later work types' priorities and copies the helper's work type priority into Load |

## Invariants and traps

The plan's architecture rules are binding on every slice; the short form:

- **One Harmony patch class per vanilla target.** Where several features need one method, that class
  calls each feature's module in turn. Two `[HarmonyPatch]` targets on one class merge field by field,
  which the workspace `CLAUDE.md` explains.
- **Only the vanilla lord type is touched.** `FormingLords.IsVanilla` is player faction plus
  `LordJob.GetType() == typeof(LordJob_FormAndSendCaravan)`, never `is`, so Vehicle Framework's
  `LordJob_FormAndSendVehicles` and Vanilla Trading Expanded's `LordJob_GrabItemsAndLeave` are left
  alone.
- **A stage is a toil index, and no toil or transition is ever added.** `Lord.ExposeData` saves the
  current toil as its index in `graph.lordToils` and restores it by index on load, so the order is part
  of the save format. `Tests/FormingStagesTests.cs` reads the order out of the real `CreateGraph` IL.
- **No MapComponent, GameComponent or added ThingComp.** Per-lord state goes in a
  `ConditionalWeakTable` keyed on the lord job and is saved by `ExposeData` postfixes as `cce_*` nodes,
  which vanilla ignores if the mod is removed.
- **Messages go through `CaravanMessages` only.** `Messages.Message` defaults to `historical: true`,
  which evicts real events from the History tab. A test fails on any other caller.
- **The settings are read at the call boundary only.** Decisions take what they need as parameters;
  `CaravansCriticallyEnhancedMod.Settings` is null in the test harness.
- **A usable `Lord` cannot be had in the test harness.** `new Lord()` runs the static `FlagTex`
  initialiser, which loads a material and fails. `FormatterServices.GetUninitializedObject` does
  allocate one, but setting its fields by reflection and reading a member ran the same initialiser
  and failed when nothing had touched the type before. Decisions take the lord's faction, job and toil
  list instead, and the `Lord` overloads are pinned by IL. `Tests/HarnessTests.cs` holds the
  measurement.
- **Moving the exit is three writes, not two.** The lord job's `exitSpot` (saved), the live toils'
  copies (not saved; the graph is rebuilt from the job on load), and every member's `PawnDuty` focus,
  which is saved with the pawn and is what the duty's job givers walk to. So a retarget ends with the
  current toil's `UpdateAllDuties`. `CaravanLordAccess` says which toil holds which copy. The meeting
  point is read only: three toils hold copies the mod has no accessor for.
- **`FormingLords.VanillaLordOf` is membership.** A downed member leaves the lord and waits in the lord
  job's `downedPawns`; for "which caravan is this pawn going with" ask vanilla's
  `CaravanFormingUtility.GetFormAndSendCaravanLord`.
- **`AccessTools.FieldRefAccess` works in the harness.** `Tests/CaravanLordAccessTests.cs` reads and
  writes through every accessor on a real lord job and real toils, so a field rename fails a test.
- **A settings node name must never change.** It is what the settings file is read by; a renamed
  node reads as absent and silently reverts every player's choice to the default. `SettingsTests` pins
  all twenty-six as literals, separately from the enum, so renaming a `Toggle` member does not push
  anybody into renaming its node. `Scribe_Values.Look` also never writes a value equal to its default,
  so changing a default changes it for every player who never touched that switch.
- **The settings window is one column, and its duration sliders step in whole hours.** `Listing`
  wraps into a clipped second column when the content outgrows the height it was given, and
  `ToStringTicksToPeriod` rounds anything over an hour to whole hours, so a finer step would put two
  values under one label. A slider value is snapped only when the player moves it.

- **Helpers run vanilla's job, unchanged, and that is load-bearing.** `TransferableCountHauledByOthers`
  counts hauls in progress by job def equality and casts the driver, and `AllItemsLoadedOntoCaravan`
  holds loading open while any pawn runs that driver for the lord. A job def of the mod's own would
  silently stop both. `job.lord` must be set: the driver casts it.
- **Helpers respect forbidden items and allowed areas; members still ignore both, as in vanilla.**
  `JobGiver_Work`'s scan validator tests `IsForbidden` on the item, and the carrier postfix refuses a
  member outside the helper's area, which vanilla's `FindBestCarrier` never asks. A changelog note,
  not a bug.
- **Helpers choose items at `NormalMaxDanger`, where vanilla's helper used `Danger.Deadly`.** The
  scanner default, and it only filters which item is chosen (`JobGiver_Work` passes `MaxPathDanger`
  to the search). The carrier test and the walk itself still use Deadly, as vanilla does. Also a
  changelog note.
- **A pawn in any lord is never a helper.** `ThinkNode_Duty` overwrites a job's lord with the pawn's
  own, and a forming caravan's pause duty, Defend, Follow and Build all run `JobGiver_Work`; the
  gather driver then works the wrong caravan or throws casting the wrong lord job.
- **The completion guard only ever turns done into not done, and cannot hold forever.** It acts in
  the item-gathering stage only, not its pause. It holds while any undrafted colonist member's own
  `FindThingToHaul` finds something, which is exactly when a member would act; any test of its own
  disagreed with that somewhere (closed outfit stands, haul sources beyond the search's region
  budget). Once no member would, live units are blocked: they hold for the grace period and come
  off the list. Held for twice the grace period with no loading progress, everything left comes off.
  Both are counted in time spent holding (`GuardClocks`): a reading where vanilla says not done,
  because a member went to eat or sleep, pauses them rather than restarting them, which with a long
  grace period would never have let a blocked item go. A
  thing in a pawn's hands is blocked, never gone. The drafted-member exemption needs one undrafted
  colonist member waiting, so an all-drafted caravan waits as vanilla does and never reaches the
  watchdog. Until Slice 3 patches `TransitionAction_EndAllJobs`, the AllItemsGathered transition the
  exemption lets through still ends a drafted member's current order once. The hold state is not
  saved: after a load the clocks restart, which can only lengthen a wait.
- **A list row can be left with a count and no things.** `TransferableOneWay.ExposeData` drops
  destroyed things on every save, and `Label` reads the first thing, so it throws on such a row,
  inside vanilla's Force-depart confirmation too. With the guard on, those rows are cleared on every completion test and every lord tick pass, and
  `LetGo` never reads a row's `Label`.
- **`cceLoadingInit` must never be renamed.** A renamed flag reads as absent, realigns priorities
  that were never shifted, and copies the hauling priority over every pawn's Load priority again.
  `DefMap` is positional by def index, which follows mod load order, so adding this mod would shift
  every later mod's saved work priorities one column along; the first load puts them back
  (`LoadingMigration.MakeRoom`). Removing the mod shifts them the other way, as removing any work
  type mod does, and that goes on the store page.

## Defect register

Nothing shipped, nothing reported. One known gap, inherited from vanilla's helper: removing an item
in the forming caravan tab ends the hauls of free colonists only
(`ITab_Pawn_FormingCaravan.EndJobForEveryoneHauling`), so a Lifter already carrying it still delivers
it.

## Open user reports

None. The mod has no Workshop page.

## Build and test

```sh
export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api
dotnet build rimworld-caravans-critically-enhanced.sln -c Release   # zero warnings
dotnet test Tests/CaravansCriticallyEnhanced.Tests.csproj           # see Tests/README.md
```

`./build.sh` stages into `$RimWorldDir/Mods/CaravansCriticallyEnhanced` and deletes what was there; the
workspace `CLAUDE.md` has the detail. Never run it just to check something.

## Repo conventions

- The house conventions in the workspace `CLAUDE.md` apply, and
  `docs/caravans/research/house-conventions.md` there records where the other repos disagree and
  which one this repo follows.
- Every string goes into all nine languages in the same commit. Game terms come from vanilla's own
  translations; `docs/caravans/glossary.md` in the workspace holds the ones already looked up.
- No em or en dashes anywhere in the repo, which `Tests/SourceTests.cs` enforces.
- Commits are local until Matthew says to push, and `git log origin/main..HEAD` is read first.
