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
| `LanguageParityTests` | Nine Keyed files with identical keys, all prefixed, none empty, none left in English bar the mod's name; matching placeholders; every key the code asks for declared; every declared key still loaded by the compiled IL; every switch with a label and tooltip; the DefInjected files: every translatable field the defs set injected in all nine languages, none left in English, none naming a def that does not exist |
| `PackagingTests` | `build.sh` stages by name, ships only the mod's DLL, never a source folder or `Workshop/`, and stages every content folder on disk |
| `SourceTests` | Every XML file parses; no em or en dash anywhere in the repo; `About.xml` name, packageId, url and dependency; the Harmony id |
| `StartupAttributeTests` | The game's own missing `[StaticConstructorOnStartup]` heuristic over every mod type |
| `ReachabilityTests` | No private or internal method that nothing calls |
| `ILCallsTests` | The IL walker's own integrity: every mod method walks to its end, `switch` operands, a branch into the middle of an instruction. The walker also records field reads and writes and the opcode before each call |
| `HarnessTests` | The boundary above |
| `CarrierRuleTests` | The whole truth table of who a helper may hand an item to, 896 rows, against the rule written out longhand |
| `CompletionGuardTests` | Every verdict, the grace boundary, the watchdog, both counted in time spent holding: a reading where vanilla says not done pauses them, progress starts them again, a caravan that slept is not let go on waking, and a simulated caravan whose members keep stopping to eat still drops its blocked item; release never reached while takeable units remain inside the watchdog, swept; the two counts read from their own fields; the stage the guard acts in; the drafted exemption, members and takers; a carried thing blocked, never gone; when an expiry is due and when it is confirmed |
| `LoadingMigrationTests` | The first-load copy over a stale slot value, the disabled pawn, a colonist's later choice, the colony mech raise; `MakeRoom` at the first, middle, last and an outside slot |
| `LoadingCandidatesTests` | No nulls, first owner wins, eligibility asked once |
| `WorkGiverSurfaceTests` | No thing request; every scan entry point declared on the class; both answer from `JobFor`, which goes through the memo; `BuildJob` checks in order, carrier second, and reads `JobDefOf.PrepareCaravan_GatherItems` and stores `Job.lord`; `ShouldSkip` reads the lord and the Load and early-loading switches, and forgets what it held when nothing is open; `PotentialWorkThingsGlobal` exits early before building the set |
| `LoadingPatchBodyTests` | Each patch body still hands over to its decision, the helper's fail condition included, which sits in a hoisted lambda; which switches each body reads, by opcode (`ToggleReads`); the guard's reader through every pure rule, in order; the migration reading its flag, realigning first, and its node name as a literal; the patches applied class by class |
| `FormingStagesTests` (Slice 1 additions) | Where helpers may carry on: both gathering stages and their pauses, nothing else |
| `PatchTargetTests` | Every patch class resolved the way Harmony will, every parameter bound by name, one class per target, the guard running last, the injected private fields |
| `LoadingDefsTests` | The Load column directly left of Haul against every shipped work type; Hauling's tags; the mech patch matching the Lifter alone in the shipped game |
| `DefReferenceTests`, `TypeReferenceTests` | Every defName and class the mod's XML names resolves to the right type; the DefOf field binds; every def carries the prefix |

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

Mutations measured on Slice 1:

| Mutation | Caught by |
|---|---|
| `CarrierRule` stops refusing an overloaded colonist | `TheWholeTableAgreesWithTheStatedRule`, `AnOverloadedColonistIsRefusedAlthoughVanillaWouldAcceptThem` |
| The watchdog counted from the last progress alone | `TheWatchdogCountsFromTheLaterOfTheHoldAndTheLastProgress` |
| Blocked units expiring on first sight | `BlockedUnitsHoldUntilTheGraceRunsOut` |
| The migration copying only into a zero slot | `TheFirstLoadOverwritesAStaleValueInTheSlot` |
| The search set no longer skipping a thing already claimed | `AThingListedTwiceBelongsToTheFirstListThatNamedIt` |
| Eligibility asked before the claimed check | `EligibilityIsNotAskedOfAThingAlreadyClaimed` |
| `HasJobOnThing` calling `BuildJob` directly | `BothScanEntryPointsAnswerFromTheSameDecisionAndNothingElse`, `TheDecisionGoesThroughTheMemo` |
| `job.lord` no longer set | `TheJobIsVanillasGatherJobPointedAtTheItemsCaravan` |
| The carrier postfix acting on any lord, not only the exact vanilla one | `TheCarrierPostfixAppliesTheCarrierRule` |
| The guard's `HarmonyPriority(Priority.Last)` removed | `TheCompletionGuardRunsAfterEveryOtherPostfix` |
| `___priorities` misspelt in the migration patch | `EveryPatchBindsTheWayHarmonyWill` |
| The mech xpath matching Construction lists | `TheMechPatchAddsLoadToTheLifterAndNothingElseInTheShippedGame` |
| `naturalPriority` 350, equal to Odyssey's Fishing | `TheLoadColumnSitsDirectlyLeftOfHaul` |

Mutations measured after the three adversarial reviews of Slice 1, each the auditor's or a fix's.
Ten of the first twelve passed every test before the review; all are caught now.

| Mutation | Caught by |
|---|---|
| `LoadingCompletion.Answer` returning vanilla's answer straight back | `TheGuardsReaderAsksThePureDecision` |
| The migration's flag reading replaced by `false` | `TheMigrationReadsWhetherThePawnWasMigratedBefore` |
| `FlagNode` renamed | `TheMigrationFlagNodeIsPinnedAsALiteral` |
| The Load giver's `ShouldSkip` no longer asking the Load switch | `SkippingIsDecidedByTheSettingTheMembershipAndTheOpenCaravans` |
| The carrier postfix narrowing members' own carrier choice too | `TheCarrierPostfixNarrowsHelpersOnlyAndOnlyWithLoadOn` |
| The drafted exemption no longer gated on "Keep leads when drafted" | `TheCompletionPostfixStandsAsideOutsideItemGatheringAndGatesTheExemption` |
| The hold and blocked clocks swapped inside `Decide` | `TheHoldAndBlockedClocksAreReadFromTheirOwnFields` and three more |
| The lord tick's re-count before letting go removed | `TheLordTickConfirmsAnExpiryBeforeLettingGo` |
| `HelpersMayContinue` always true | `HelpersCarryOnThroughBothGatheringStagesAndTheirPausesOnly` |
| `MemberState` ignoring drafted | `AMemberCountsOnlyAsAnUndraftedColonist` |
| The reader passing `false` for drafted | `TheExemptionsReaderCountsMembersThroughThePureRules` |
| The exemption no longer needing one member waiting | `TheDraftedExemptionNeedsSomebodyWaitingAndNobodyBusyOrHauling` |
| A carried thing counted as gone | `ACarriedThingIsBlockedNeverGone` |
| `ForgetEverything` no longer called | `WithNothingToLoadTheGiverLetsGoOfWhatItHeld` |
| The carrier postfix passing `false` for unavailable | `TheCarrierPostfixNarrowsHelpersOnlyAndOnlyWithLoadOn` |
| `CarrierRule` ignoring unavailable | `ADraftedOrBrokenMemberIsNeverACarrierForAHelper` |
| The completion postfix applying in every stage | `TheCompletionPostfixStandsAsideOutsideItemGatheringAndGatesTheExemption` |
| `Applies` accepting the gathering pause | `TheGuardAppliesOnlyWhileItemsAreGathered` |
| `IsTaker` ignoring Manipulation | `ATakerIsASpawnedUndraftedColonistOnItsFeetWithHands` |
| `ApplyPatches` replaced by `PatchAll` | `ThePatchesAreAppliedClassByClassAndNeverAllAtOnce` |
| `ConfirmLetGo` ignoring hauls in progress | `AnExpiryIsConfirmedAgainstTheCaravanAsItStandsNow` |
| `LetGoDue` accepting a stale verdict | `TheLordTickActsOnlyOnAFreshExpiry` |
| `ClearEmptyRows` no longer called on the completion test | `RowsWithNothingLeftAreClearedSoVanillasListCannotThrow` |
| `LetGo` no longer taking the row off before labelling it | `LettingGoTakesARowOffTheListBeforeLabellingItAndNeverReadsAnEmptyRowsLabel` |
| `AnyMemberWouldHaul` replaced by "any taker exists" | `TheCountsGoThroughTheTakerAndUnitRules` |
| `IsTaker` ignoring drafted | `ATakerIsASpawnedUndraftedColonistOnItsFeetWithHands` |
| The taker reader passing `false` for drafted | `TheCountsGoThroughTheTakerAndUnitRules` |
| Helpers taking from a closed haul source | `HelpersLeaveClosedHaulSourcesAlone` |
| "Tied" read as tied to any spot | `ARopeAnimalCountsAsTiedOnlyAtItsOwnCaravansSpot` |
| The carrier postfix ignoring the helper's area | `TheCarrierPostfixNarrowsHelpersOnlyAndOnlyWithLoadOn` |
| The Load giver skipping caravan members only, not every pawn in a lord | `SkippingIsDecidedByTheSettingTheMembershipAndTheOpenCaravans` |
| `MakeRoom` not moving later values | `MakingRoomMovesEveryLaterValueOnAndZeroesTheSlot` |
| The first load no longer realigning | `AFirstLoadRealignsTheShiftedPrioritiesBeforeAnythingReadsThem` |
| A not-done reading restarting both counts, the defect the third review found | `ABlockedItemIsDroppedEvenWhenTheMembersKeepStoppingToEat`, `AReadingWhereVanillaSaysNotDonePausesBothCounts` |
| The gap after a paused reading counted as held time | `TheGapSinceAHoldingReadingCountsAndTheGapAfterAPausedOneDoesNot` |
| Progress no longer starting the held count again | `ProgressSinceTheLastReadingStartsBothCountsAgain` |
| Progress no longer starting the blocked count again | `ProgressSinceTheLastReadingStartsBothCountsAgain` |
| "Tied" read against the duty's spot, which the pause duty does not have | `ARopeAnimalCountsAsTiedOnlyAtItsOwnCaravansSpot` |
| Empty rows no longer cleared on the lord tick | `RowsWithNothingLeftAreClearedSoVanillasListCannotThrow` |
| Every class handed to Harmony, not only those with a Harmony attribute | `ThePatchesAreAppliedClassByClassAndNeverAllAtOnce` |

Not pinned, because the walker reads calls and not control flow, and so left to the in-game checks:
`LetGo` skipping a row with no things (check 13), the lord tick's `finally` that clears a verdict
even when letting go throws, and the refusal reasons being built only for a forced ask.

Not reachable here, and so left to in-game check 1: loading the settings file. The game's loader
reaches a Unity logging call that does not exist outside the player, so the read half of
`ExposeData` and the clamp after it run only in game. The saving half is tested.

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

### Slice 1

3. **A new colony.** The Work tab has a Load column directly left of Haul, in every language, and
   new colonists start with it on. With Biotech, an idle colony Lifter carries listed items to a
   forming caravan (the Work tab never lists mechs, so watch it rather than look for a column).
4. **An existing save.** Load a save made without the mod, with manual priorities on and some
   colonists at different Haul priorities: each colonist's Load equals their Haul. Set one colonist's
   Load to 0, save, reload: it stays 0. Repeat with FSF Complex Jobs: Load copies Transport.
5. **A caravan of colonists only.** Form a caravan of two colonists and a few items, no animals.
   Colonists not going, with Load on, carry items to the two members. Turn off "Load into colonists":
   they stop. Turn off "Load work type": vanilla behaviour, nobody outside helps.
6. **A member in a mental break.** While the caravan is paused, the broken member's friends who are
   in the caravan take no Load job, and nothing logs an `InvalidOperationException`.
7. **Prioritize.** Right-click a listed item with a colonist not going: "Prioritize loading" appears
   and works. With that colonist's Load at 0 it is greyed with vanilla's "not assigned" reason. A
   caravan member does not get the option. With every member out of reach, the greyed reason is "nobody
   in the caravan can take it".
8. **Several helpers, one big stack.** Form a caravan wanting 500 steel from one stockpile, with three
   helpers. Loading does not finish with steel still on the ground.
9. **A walled-in item.** List an item, then wall it in so no member can reach it. The caravan keeps
   loading the rest, then after the grace period drops the item with a message naming it, and goes.
   The History tab does not gain the message.
10. **Early loading.** With "Load while animals gather" on and pen animals still being led, helpers
    load rope animals already tied at the gathering spot, pack animals that walk without a rope
    (elephants), and prisoners, but not rope animals still on the way, nor one tied at a hitching post
    elsewhere on the map.
11. **Force depart.** Force depart while helpers are mid-delivery: they drop what they carry and
    stop; nobody follows the caravan to the edge.
12. **Cost.** With no caravan forming, open dev mode's performance tools (or watch the TPS) with
    twenty colonists: no measurable difference with the mod on and off. Right-clicking items stays
    instant. Repeat while a 30-item caravan of colonists only is gathering animals with "Load while
    animals gather" on (every helper refused for want of a carrier) and while it is gathering items:
    no measurable difference either. And once more while a walled-in item holds a caravan of five
    members on a large map: every 120 ticks each member runs vanilla's item search for the guard.
13. **An emptied row.** List a stack of simple meals, let a colonist eat them all, save and reload,
    then wall in another listed item. When the grace period ends the walled item is dropped with a
    message, the meals' row disappears without a label, and Force depart works throughout with no red
    error.
14. **A closed outfit stand.** List spare armour from an outfit stand with "allow removing" off.
    Helpers leave it; the caravan holds for the grace period, then drops it with the "could not take"
    message rather than waiting out the watchdog. Repeat with the grace period at 12 hours over a few
    in-game days: the members eat and sleep meanwhile, and the armour is still dropped in the end.
15. **A break while animals gather.** With "Load while animals gather" on, a helper is walking a
    stack to a muffalo tied at the spot when a member has a mental break: the helper still hands it
    over rather than dropping it on the way.
16. **Partly drafted.** With "Keep leads when drafted" on, draft one member while the others wait:
    loading finishes without it. Draft every colonist member: loading waits, and nothing is dropped
    from the list however long they stay drafted.
17. **Load order.** With FSF Complex Jobs loaded after this mod, add this mod to a save made without
    it: each colonist's Transport priority is unchanged, and Load equals it. Repeat with FSF loaded
    before this mod. With Fluffy's Work Tab active, Load matches the Haul value Work Tab shows; it
    keeps its own priorities per work giver and per hour, and the copy reads vanilla's raw value.
18. **A pawn in another lord.** A colonist assigned to a quest's defend-point lord takes no Load job
    and nothing logs an `InvalidCastException`.
