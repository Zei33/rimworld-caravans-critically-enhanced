# Tests

```sh
export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api
dotnet test Tests/CaravansCriticallyEnhanced.Tests.csproj
```

net472 NUnit, running against the real `Assembly-CSharp.dll` from the installed game rather than a
stub. `RimWorldDir` must be set; the workspace `.claude/settings.json` and `~/.zshrc` both export it.

This project is deliberately not in `rimworld-caravans-critically-enhanced.sln`, so `dotnet build` on
the solution builds only the mod and stays at zero warnings. The mod's own `.csproj` removes
`Tests/**` from its compile items, so nothing here can reach the shipped assembly.

## How it is wired

The mod's sources are compiled into the test assembly rather than referenced as a built DLL. That
keeps private and internal members reachable without an `InternalsVisibleTo`, and it avoids a
`ProjectReference` rebuilding the mod into `1.6/Assemblies/net472` in Debug, which is where
`build.sh` expects to stage a Release artefact from.

## What can and cannot be tested

Measured, not assumed:

- **Reachable:** `ModSettings` subclasses, `LordJob_FormAndSendCaravan` through either constructor,
  every caravan lord toil, an uninitialised `Faction` with its `def` set, `StateGraph`, and
  `AccessTools.FieldRefAccess`, which reads and writes the private fields of real game objects here.
  `System.IO` and the game's def XML on disk.
- **Not reachable:** a `Lord`. Its static initialiser loads a material, so `new Lord()` throws, and
  whether an uninitialised one can be built depends on which test ran first. `HarnessTests` records
  the measurement. Anything under `Find.*` or `Current.Game`, a spawned pawn, a map, `DefDatabase`,
  any `DefOf` member, and Harmony patching itself, which cannot run on this runtime at all.
- So the mod is written the way the other suites here taught: the game is read into plain values by
  a thin reader, and a pure function decides over them. The readers are not tested and do not need
  to be; the decisions are the half that goes wrong.
- `ILCalls` walks compiled IL, the mod's and the game's. It holds an unrunnable body to calling the
  decision it delegates to, and pins vanilla facts the mod depends on, such as the order
  `LordJob_FormAndSendCaravan.CreateGraph` adds its toils. It pins which calls appear and in what
  order, never their arguments or the control flow around them.

## Covered

| Fixture | What it holds |
|---|---|
| `FormingStagesTests` | The toil index to stage mapping; the vanilla toil order read from the real `CreateGraph` IL, constructor then `AddToil` alternating; no flee toil on the player lord; pauses; when loading is open |
| `FormingLordsTests` | The exact-type rule against a subclass and an unrelated job; the player faction rule; the stage read from a toil's position, unknown for a toil outside the graph; IL holding each `Lord` overload to the tested decision |
| `CaravanLordAccessTests` | Every private field name on its declaring type; every accessor resolving, reading a real lord job and real toils, and writing back to the field it names and no other |
| `CaravanMessagesTests` | `CaravanMessages.Post` is the only caller of `Messages.Message` in the mod, and hands it a literal `false` for `historical` |
| `SettingsTests` | Every switch in the table once, numbered without gaps; all twenty-six settings node names pinned as literals; defaults; parents drawn first, in the same section, one level deep; a child reads off under an off parent; reset; the game's own saver writing nothing for defaults and every changed value under its pinned name; the clamp; whole-hour duration steps |
| `JobMemoTests` | Copied with `JobMemo` from Simple Improve |
| `LanguageParityTests` | Nine Keyed files with identical keys, all prefixed, none empty, none left in English bar the mod's name; matching placeholders; every key the code asks for declared; every declared key still loaded by the compiled IL; every switch with a label and tooltip |
| `PackagingTests` | `build.sh` stages by name, ships only the mod's DLL, never a source folder or `Workshop/`, and stages every content folder on disk |
| `SourceTests` | Every XML file parses; no em or en dash anywhere in the repo; `About.xml` name, packageId, url and dependency; the Harmony id |
| `StartupAttributeTests` | The game's own missing `[StaticConstructorOnStartup]` heuristic over every mod type |
| `ReachabilityTests` | No private or internal method that nothing calls |
| `ILCallsTests` | The IL walker's own integrity: every mod method walks to its end, `switch` operands, a branch into the middle of an instruction. The walker also records field reads and writes and the opcode before each call |
| `HarnessTests` | The boundary above |

Mutations measured on Slice 0, each made by hand, run, and reverted:

| Mutation | Caught by |
|---|---|
| `FormingStages.FromToilIndex` bound `< ToilCount` changed to `<=` | `AnIndexOutsideTheGraphIsUnknown(13)` |
| `FormingLords.IsVanillaJob` `==` on the type changed to `IsAssignableFrom` | `OnlyTheExactVanillaLordJobCounts` |
| `FormingLords.IsVanilla(Faction, LordJob)` player test removed | `ALordMustBeThePlayersAndRunTheExactJob` |
| `FormingLords.StageOf(Lord)` computing the stage inline instead of through the parts overload | `TheLordOverloadsDelegateToTheTestedDecisions` |
| `CaravanLordAccess.ExitSpotField` changed to `"exitspot"` | `EachNamedFieldIsAnInstanceCellDeclaredOnThatType` and the accessor tests |
| A direct `Messages.Message` call added in `SettingToggles.HeadingKey` | `OnlyTheMessageHelperPostsMessages` |
| `IsOn` stops consulting the parent | `AChildReadsAsOffWhileItsParentIsOff` |
| A switch's default flipped in the table | `EverySwitchIsOnByDefaultExceptInstantSurplusUnload` |
| `CaravanMessages.Post` passing `historical: true` | `TheHelperPassesALiteralFalseForHistorical` |
| The threat radius saved under `"routeThreatRadius"` | `SavingWritesOnlyWhatDiffersFromTheDefaultUnderItsShippedName` |
| `ExposeData` saving each switch's default instead of its value | `SavingWritesOnlyWhatDiffersFromTheDefaultUnderItsShippedName` |
| The downed-pawn exit accessor pointed at `meetingPoint` | `TheToilAccessorsReadTheCellsTheConstructorsWroteAndWriteThemBack` |
| The reset button's key read through a variable, out of the source scan's sight | `EveryKeyTheCodeAsksForIsDeclared` |

Not reachable here, and so left to in-game check 1: loading the settings file. The game's loader
reaches a Unity logging call that does not exist outside the player, so the read half of
`ExposeData` and the clamp after it run only in game. The saving half is tested.

The house fixtures that read defs (`DefXml`, `DefReferenceTests`, `TypeReferenceTests`, and the
second half of `ReachabilityTests`) arrive with the mod's first defs in Slice 1.

## In-game checks

Nothing here runs RimWorld, so no behaviour claim counts until somebody has loaded a save and
looked. Dev mode on. Record the date, the build and what was seen beside each check.

### Slice 0

1. **The settings window.** Options > Mod options > Caravans Critically Enhanced. Six headed
   sections; every switch on except "Drop unwanted cargo at once"; the cargo switches indented under
   "List cargo on animals". Turn that off: its children's labels and boxes grey out, clicking them
   does nothing, they do not highlight under the mouse, and they keep their ticks. Scroll to the
   bottom: "Reset to defaults" is there and nothing is cut off on the right. Drag each duration
   slider step by step: every position shows a different label, in whole hours, and the stall alert
   reaches 1 day. Change a few values, close the game, start it again and reopen: the values loaded.
   Reset to defaults. Switch the game language to Japanese and to Chinese and reopen each time: no
   raw key names, and the window still scrolls to the reset button.
2. **The log.** With dev mode on, the log has `[CaravansCriticallyEnhanced] Loaded version 0.1.0.`
   and no red line from this mod.
