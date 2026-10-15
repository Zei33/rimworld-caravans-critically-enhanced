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
landed. **As of Slice 0 the mod does nothing in game except show its settings window.**

## Architecture

| Type | File | Role |
|---|---|---|
| `CaravansCriticallyEnhancedMod : Mod` | `1.6/ModEntry.cs` | Loads the settings, `PatchAll()`, dev-mode version log |
| `CaravansCriticallyEnhancedSettings : ModSettings` | `1.6/Core/CaravansCriticallyEnhancedSettings.cs` | The switches and four numbers, the scroll-view settings window |
| `SettingToggles`, `ToggleSpec`, `Toggle`, `SettingGroup` | `1.6/Core/SettingToggles.cs` | One table holding every switch: section, scribe key, default, translation keys, parent |
| `FormingStage`, `FormingStages` | `1.6/Core/FormingStages.cs` | The vanilla forming graph's toil index as a named stage. Pure |
| `FormingLords` | `1.6/Core/FormingLords.cs` | `IsVanilla`: the exact-type test every feature asks before touching a lord. `StageOf`. The decisions take a lord's parts; the `Lord` overloads delegate |
| `CaravanLordAccess` | `1.6/Utils/CaravanLordAccess.cs` | `FieldRefAccess` to the lord job's `exitSpot` and `meetingPoint` and the toils' copies of the exit, each resolved lazily and on its own, null if its field has gone |
| `CaravanMessages` | `1.6/Utils/CaravanMessages.cs` | The only caller of `Messages.Message`, always `historical: false` |
| `JobMemo<TAsker, TSubject, TAnswer>` | `1.6/Core/JobMemo.cs` | Copied from Simple Improve: lets a work giver's `HasJobOnThing` and `JobOnThing` answer from one computation |

No defs and no Harmony patches yet. `PatchAll()` runs over an assembly with no `[HarmonyPatch]` class.

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

## Defect register

Nothing shipped, nothing reported.

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
