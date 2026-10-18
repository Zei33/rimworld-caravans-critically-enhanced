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
| `LeadPolicyTests` | Who keeps leads (one yes in the whole table), vanilla's other breaking conditions, releasing an animal at every hunger and rest level, the auto-undraft, the stages where undrafting resumes, the drop button, the take-lead checks in order |
| `LeadKeepScopeTests` | Push, pop, nesting, the same pawn twice, popping a pawn never pushed |
| `LeadPatchBodyTests` | The rope tick leaving first for a pawn leading nothing, then the policy, the fatal break, needs and speed in order; the readers through the policy (and not reading drafted as fatal); the draft setter's scope and its finalizer closing it first; `DropRopes` refused only inside the scope and while spawned; the previous roper let go; vanilla's follow job; the auto-undraft and stage-change patches through the policy and the switch; the gizmo, button, menu option and take-lead driver to their rules |
| `RopeCandidatesTests` | Which animals wait to be led: the whole truth table has one yes, and only the gathering stage itself counts |
| `RopeJobSurfaceTests` | The index asking the candidate rule with the real stage about animals only, and reading the spot from the animal's duty; the rope giver above the Load giver and never to mechs; the cancel prefix ending the helpers' rope jobs; the rope giver's shape as the Load giver's (no thing request, own entry points, one memoised decision, early exit, skipping on colonist, lord, switch and gathering, forgetting when idle), its checks in order, the job def and `Job.lord`; the driver a rope driver, each hook declared, arrival against target B, tying at the spot, pick-ups by the search rule, and the fail condition ahead of the base toils reading the map's lord list and the stage |
| `PatchTargetTests` | Every patch class resolved the way Harmony will, every parameter bound by name, one class per target, the guard running last, the injected private fields |
| `LoadingDefsTests` | The Load column directly left of Haul against every shipped work type; Hauling's tags; the mech patch matching the Lifter alone in the shipped game |
| `DefReferenceTests`, `TypeReferenceTests` | Every defName and class the mod's XML names resolves to the right type; the DefOf field binds; every def carries the prefix |
| `ExitPlannerTests` | Each ranking step settling the choice in turn (threatened, score, regions away, distance, then the route's side, which only settles an exact tie); an unusable or unreachable cell never chosen; a mover that reaches no edge reported and blocking nobody; each blocker with the movers it names and the fallback beside it; the area rule off; the threat radii exclusive at their edge and the distance to the route measured to its nearest point; the current exit judged by the same rules, valid and reachable apart; the safe exit for a move: the nearest clear of the wider distances rather than the nearest at the set ones, its way judged at the wider route distance too, the least inside the wider distances when none is clear of them, only ever one scoring nothing at the set distances (two hostiles), inside the areas, at index 0 like any other, and none when nothing is safe or threats are off; the same readings always giving the same exit; threats ignored with their switch off; an area whose only edges are out of reach blocking like one with none; regions away beating distance, and distance beating the route's side |
| `ExitPolicyTests` | Who an area restricts, where either vanilla area control hides it or the area is empty; the stages the rule applies in; every row of what the pass does, held and not held, including a quiet move off an exit nobody reaches and a held caravan's fallback kept current; when a waiting caravan goes back for its downed pawns |
| `ExitSurfaceTests` (Slice 4c additions) | The threat move asked only after the area decision and through the policy; the map read for threats only in a movable stage with a hostile about, measured from the colonists in collecting and leaving; one plan, which widens both distances itself (the pass calls no `Clearance`), and the move taken to its safe exit, every index getter in order; colonists counted near the exit the caravan has, and both downed counts passed in their own places, at that exit by vanilla's own test less a drafted carrier's pawn; the cooldown stored at a move, the alert's flag set or cleared, never an order to a drafted pawn; collecting and leaving routed through `CaravanRoute` with workers only; the threat alert reading only the flag |
| `ExitSurfaceTests` | Run on real toils: the copies written, held (the downed stage's copy at the meeting point, the others at the fallback) and released, and the meeting point never. Run directly: the starting tile accessor; the state made once per caravan, read without being made, and the first-pass flag; the handoff forgotten on close; the exit search answering only inside a send, on its map, once; both gate patches and the baby patch leaving vanilla's answer alone with no held caravan. Pinned by IL, so by which calls appear and in what order, never by branch: the saved node names as literals; the search planning from the window's pawns and storing the hold flag (1 on an area block, 0 in its catch); a new caravan's set-up behind `IsVanilla`; the pass reading the map only after `AreaRuleApplies` and deciding through the policy; which plan index each action takes; every write keeping the lord job, the copies, the duties and the stale walks in step, and recording what it wrote; the stale-walk stage test first; a hold putting carried pawns down; the waiting stage going back for downed pawns through the rule; the first-tick pass ahead of the interval; a foreign exit or an override never written over; a missing field clearing the hold; the readings' region search and area gate; the alert planning nothing and naming nobody dead or gone |
| `LeaveFromHereTests` | Who gets the button (one shown row in sixteen, drafted and controlled read from the pawn), greyed past 10 cells, hidden once the caravan has gone; the route as a whole table over fetchable strays, roping and workers, the same in every stage; the 12-cell circle; whom Force depart leaves. By IL, so by which calls appear and in what order: the button built by the gizmo postfix, field tests before the lord; one press and one click sound; the spot from the planner over those who will go, areas and threats overridden; the confirmation built from vanilla's with the Force depart list kept out; the departure's order; drafted members ordered as vanilla orders them; nothing undrafting, forcing vanilla's departure or setting a walk-off; the walk-off and carry-to-exit patches reading the caravan and the switch; the Force depart list through the policy, counting only an undrafted carrier |
| `ThreatPolicyTests` | The stages a threat move may happen in (working stages, never a pause); the commit rule at half the colonists in every stage but gathering downed pawns, for a downed pawn already at the exit while waiting and for downed pawns to take along from collecting on, as whole tables over the stages; the clearance factor; the hour's cooldown at its edge; the move's gates as a whole table (one row in 128) and its score (any current threat, only with a safe exit open); which stages measure from the colonists and need routing again |

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
| The hold and blocked clocks swapped inside `Decide` | `TheHoldAndBlockedCountsAreReadFromTheirOwnFields` and three more |
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

Mutations measured on Slice 2, all caught:

| Mutation | Caught by |
|---|---|
| Candidates accepted in the gathering pause too | `OnlyTheGatheringStageItselfCounts` |
| Animals that walk by themselves accepted | `AnAnimalThatWalksByItselfIsNot` |
| Animals already on a rope accepted | `AnAnimalAlreadyOnSomebodysRopeIsNot` |
| The rope giver no longer skipping non-colonists | `SkippingIsDecidedByTheSettingTheColonistTheMembershipAndTheGathering` |
| The rope giver keeping its memo when nothing gathers | `SkippingIsDecidedByTheSettingTheColonistTheMembershipAndTheGathering` |
| The rope giver ignoring its switch | `SkippingIsDecidedByTheSettingTheColonistTheMembershipAndTheGathering` |
| The spot's area no longer checked | `TheJobDecisionChecksInTheOrderTheReasonsNeed` |
| `job.lord` no longer set on the rope job | `TheJobIsTheModsRopeJobPointedAtTheAnimalsCaravan` |
| An arrived animal tied at its duty's spot | `AnArrivedAnimalIsTiedAtTheSpot` |
| The fail condition no longer added | `TheToilsAreTheBaseToilsBehindOneFailCondition` |
| A removed lord no longer ending the job | `TheFailConditionAsksWhetherTheCaravanIsStillGathering` |
| Pick-ups no longer obeying the search rule | `PickingUpOnTheWayObeysTheSearchRule` |
| The search set always empty | `TheSearchSetHasItsOwnEarlyExitBeforeTheSetIsBuilt` |
| The rope work giver def naming the Load giver's class | `TheDefsPointAtTheseClasses` |

After the Slice 2 audit, which showed the same defects one layer up, in the index that reads the
game, passed every test:

| Mutation | Caught by |
|---|---|
| The index no longer asking the candidate rule | `TheIndexAppliesTheCandidateRuleToEachAnimal` |
| The index passing a constant gathering stage, so helpers start in the pause | `TheIndexAppliesTheCandidateRuleToEachAnimal` |
| "Tied" read as tied to any spot in the index | `TheIndexAppliesTheCandidateRuleToEachAnimal` |
| The index no longer limited to animals | `TheIndexAppliesTheCandidateRuleToEachAnimal` |
| The destination no longer read from the animal's duty | `TheDestinationIsTheAnimalsOwnDutySpot` |
| Pick-ups no longer limited to the job's caravan | `PickingUpOnTheWayObeysTheSearchRule` |
| Pick-ups ignoring the helper's area | `PickingUpOnTheWayObeysTheSearchRule` |
| An animal taken out of the caravan on the way tied at the spot anyway | `AnArrivedAnimalIsTiedAtTheSpot` |
| `canBeDoneByMechs` false removed from the rope giver | `TheRopeGiverComesBeforeLoadingAndNeverToMechs` |
| The rope giver below the Load giver in the column | `TheRopeGiverComesBeforeLoadingAndNeverToMechs` |
| The cancel prefix no longer letting go | `CancellingACaravanEndsItsHelpersRopeJobsBeforeVanillaRuns` |
| `LetGoFor` no longer ending the jobs | `CancellingACaravanEndsItsHelpersRopeJobsBeforeVanillaRuns` |
| A forced ask taking an animal's reservation over from a member | `ARightClickNeverTakesAnAnimalOverFromAnotherPawn` |
| A cancel ending current rope jobs only, leaving queued ones holding their reservations | `CancellingACaravanEndsItsHelpersRopeJobsBeforeVanillaRuns` |
| A cancel ending other caravans' rope jobs too | `CancellingACaravanEndsItsHelpersRopeJobsBeforeVanillaRuns` |
| The destination trusting this tick's set instead of asking the rule again | `TheDestinationIsTheAnimalsOwnDutySpot` |

Cost-only, and not pinned: disabling the index's cache, keying it on the tick alone, or letting
`AnyGathering` accept any stage all pass, and are for the profiler checks (12 and 26). Also not
pinned: `LetGoFor` ending the helpers' jobs without starting new ones in the same tick (check 21);
the call's last argument is the pool flag, so the walker cannot see the start flag.

Mutations measured on Slice 3, all caught:

| Mutation | Caught by |
|---|---|
| Leads kept outside a forming caravan | `OnlyADraftedCaravanColonistLeadingAnimalsIsHeld` |
| Asleep no longer breaking the ropes | `EachOfVanillasOtherBreakingConditionsStillBreaks` |
| Animals released only when starving | `AnAnimalIsLetGoOnlyWhenUrgentlyHungryOrExhausted` |
| The auto-undraft spared for a drafted member holding nothing | `TheAutoUndraftIsSuppressedOnlyForALeadHolderInACaravan` |
| Undrafting resuming in the gathering pause | `VanillaResumesLeadingOnlyInTheGatherAndCollectStages` |
| A lone held animal getting a menu | `TheDropButtonDropsALoneAnimalAndOtherwiseOffersAMenu` |
| The reservation no longer checked before taking a lead | `TakingALeadNeedsTheCaravanThenACellThenTheReservationThenThePath` |
| Popping the oldest scope instead of the pawn's own | `ScopesNestAndAnotherPawnsScopeIsNotThisOnes` |
| The rope tick no longer breaking on a fatal condition | `TheRopeTickStillBreaksOnVanillasOtherConditionsAndLooksAfterTheAnimals` |
| The rope tick no longer releasing needy animals | `TheRopeTickStillBreaksOnVanillasOtherConditionsAndLooksAfterTheAnimals` |
| `DropRopes` refused while despawned too | `DroppingRopesIsRefusedOnlyInsideTheScopeAndWhileSpawned` |
| The draft finalizer no longer closing the scope | `DraftingOpensTheScopeAndTheFinalizerAlwaysClosesIt` |
| The previous roper no longer let go | `RopingAnAnimalLetsItsPreviousRoperGoFirst` |
| The follow job without vanilla's re-check on expiry | `AnAnimalFollowsADraftedRoperWithVanillasOwnJob` |
| A stage change ending drafted members' jobs too | `AStageChangeEndsTheJobsOfUndraftedMembersOfVanillaCaravansOnly` |
| Being drafted read as a breaking condition | `TheRopersReadingsGoThroughThePolicy` |
| The drop button on pawns the player does not control | `TheGizmoPostfixAddsTheDropButtonWhereTheRuleSays` |
| The drop button's click sound played once per holder (`alsoClickIfOtherInGroupClicked` left on; the menu is built once either way) | `TheDropButtonDecidesThroughThePolicyAndSoundsOnce` |
| The take-lead job carrying on after the animal left the caravan | `TheTakeLeadJobRopesWithVanillasToilsAndFailsWhenItNoLongerApplies` |

Measured after the two reviews of Slice 3, on the fixes they asked for and on the auditor's own
mutations (`docs/caravans/wip/tools/muts-s3-fixes.json`, `muts-s3-review-fixes.json`,
`muts-s3-review2.json` and the remeasured `muts-s3-audit*.json` in the workspace): 80 more, 59
caught. The ones caught, in short:

| Mutation | Caught by |
|---|---|
| A dog, or a starving animal outside the leaving stage, offered for taking; the stage not read | `OnlyARopeAnimalThatCouldKeepTheLeadCanBeTaken`, `ANeedyAnimalCanBeTakenOnlyWhileLeaving`, `TheTakeLeadOptionDecidesThroughThePolicyAndOrdersTheModsJob` |
| The take-lead job's undraft or animal checks gone | `TheTakeLeadJobRopesWithVanillasToilsAndFailsWhenItNoLongerApplies` |
| The let-go postfix ignoring the switch, a null animal, or the speed match | `ALetGoAnimalStopsFollowingAndNoLongerSetsThePace`, `TheLetGoPostfixIgnoresANullAnimal` |
| A cancel no longer letting drafted holders go, or letting undrafted ones go too | `CancellingACaravanHasItsDraftedMembersLetGoBeforeVanillaLeadsTheAnimalsHome` |
| The auto-undraft spared in gathering, collecting or leaving, or regardless of the stage | `TheAutoUndraftStillRescuesTheCaravanWhereAHolderWouldStallIt`, `TheAutoUndraftIsSuppressedThroughThePolicy` |
| Needy animals let go while leaving | `NeedyAnimalsAreNotLetGoWhileLeaving`, `NeedyAnimalsAreLetGoWithAMessageAndTheRoperMatchesTheSlowest` |
| The scope's pop moved below the early return, or popping a scope it did not open | `TheFinalizerClosesItsOwnScopeWhicheverWayThePawnWent`, `TheFinalizerLeavesAScopeItDidNotOpen` |
| `DropRopes` refused outside the scope; every non-roper, or every roper not held, skipping vanilla | `DroppingRopesRunsVanillaOutsideTheScopeAndForAnUnspawnedPawnInsideIt`, `TheRopeTickHandsEveryRoperTheModDoesNotHoldToVanilla` |
| `KeepsLeads` reading the settings before the cheap exits | `TheRopersReadingsGoThroughThePolicy` |
| Dead, burning or not in the caravan no longer read | `TheRopersReadingsGoThroughThePolicy` |
| The follow job's expiry, the group-click setting or the needs interval changed | `AnAnimalFollowsADraftedRoperWithVanillasOwnJob`, `TheDropButtonDecidesThroughThePolicyAndSoundsOnce`, `TheRopeTickStillBreaksOnVanillasOtherConditionsAndLooksAfterTheAnimals` |
| The undraft message ignoring the switch, posted from the setter, or shown for a break | `DraftingOpensTheScopeAndTheFinalizerAlwaysClosesIt`, `TheUndraftMessageWaitsForTheRopeTickThatDropsTheLeadsAndSkipsABreak` |
| A notice kept past its window, from an older save, taken twice, or left behind | `LeadUndraftNoticesTests` (all four) |
| A leaving caravan going back in another stage, for a led or walking animal, when nobody would fetch it, without the memo, with a misspelt memo, or not at all | `OnlyALeavingCaravanWatchesForStrandedAnimals`, `APenAnimalIsStrandedWhenNobodyInTheCaravanLeadsItAndItCannotWalkOut`, `ALeavingCaravanGoesBackOnlyWhenCollectingCouldFinish`, `ALeavingCaravanGoesBackForAStrandedAnimalOnlyWhenAColonistWouldFetchIt` |
| The scope's lookup skipping its first entry | `LeadKeepScopeTests` (four of them) |

Not pinned, the 19 that survive, and each the kind the walker cannot see: control flow, a constant
returned, or an argument. Each has the in-game check that covers it:

- the rope tick prefix returning true after it handled a held roper, which turns the feature off
  (check 27), or false from its `catch`, which only an exception reaches;
- the draft prefix opening the scope on an undraft rather than a draft (checks 27 and 28);
- the auto-undraft postfix's result inverted (check 31);
- the stage-change prefix letting vanilla run as well, or its vanilla-lord test inverted (check 33);
- the follow postfix overriding vanilla's own follow job, or its `KeepsLeads` test inverted (check 27);
- `CreateRope`'s duplicate guard inverted (check 30);
- the let-go postfix ending a follow job aimed at somebody else, or `ForgetSpeedOf` clearing the
  wrong animal's speed match (check 29);
- the speed match picking the fastest animal (check 27);
- the needy release's test inverted, its loop run forwards while it removes, or its leaving test
  inverted (check 32);
- the take-lead option's caravan test inverted (check 30);
- `IsFatal`'s asleep reading inverted, which lets every awake holder go (check 27);
- the drop button shown for a holder or a controlled pawn rather than both (check 29);
- the stranded-animal pass forgetting that a loose animal could not be fetched, a store to a local
  (check 37, its walled-in half).

Two more are equivalent: `KeepsLeads` passing `true` for "leading anything" to the policy, which its
own early exit has already made true; and `LeadUndraftNotices` not treating a collected pawn as
expired, which differs only after a garbage collection, when nothing can match it anyway.

Not pinned, because the walker reads calls and not control flow, and so left to the in-game checks:
`LetGo` skipping a row with no things (check 13), the lord tick's `finally` that clears a verdict
even when letting go throws, and the refusal reasons being built only for a forced ask.

Mutations measured on Slice 4a (`docs/caravans/wip/tools/muts-s4a*.json` in the workspace, logs
beside the Slice 4 notes): 79 on the first draft, 63 caught once eight weak tests were strengthened;
27 on the first round of review fixes, 22 caught; 9 on the second, 7 caught, and the two it missed
caught after new pins; 5 on the third, 3 caught; then the go-back was narrowed to passes that moved
the exit, and 4 more measured on that, 2 caught; then it was made a saved step retried every pass,
and 6 more measured on that, 5 caught. The ones caught, in short:

| Mutation | Caught by |
|---|---|
| Each ranking step reversed or reordered (threatened, score, regions away, distance, route side) | `AnUnthreatenedExitBeatsANearerThreatenedOne`, `WhenEveryExitIsThreatenedTheLeastThreatenedIsChosenAndSaidToBe`, `TheExitFewestRegionsAwayWinsAndTiesGoToTheNearest`, `TheWorldRouteSettlesOnlyAnOtherwiseExactTie` |
| An unusable cell, a stranded mover, nobody reaching an edge, or no usable edge mishandled | `AnUnusableCellIsNeverChosen`, `AMoverThatReachesNoEdgeIsReportedAndBlocksNobody`, `NobodyReachingAnyEdgeIsUnreachableAndSoIsNoSharedEdge`, `NoUsableEdgeCellIsItsOwnBlocker` |
| Areas applied with the rule off, threats with their switch off, the fallback taken from valid cells, the current exit judged valid when only reachable (or the reverse) | `AreasAreIgnoredWhenTheRuleIsOff`, `ThreatsAreIgnoredWhenTheSwitchIsOff`, `AnAreaWithNoReachableEdgeNamesItsPawnAndStillGivesAFallback`, `TheCurrentExitIsJudgedByTheSameRules` |
| The blockers swapped, or an area cell out of reach counting | `AnAreaWithNoReachableEdgeNamesItsPawnAndStillGivesAFallback`, `AnAreaWhoseOnlyEdgesAreOutOfReachHoldsNoReachableEdge` |
| The route term gone, or the distance to the route unclamped | `AThreatNearTheRouteAloneCounts`, `TheDistanceToARouteMeasuresToItsNearestPoint` |
| Either area control's test, the empty area, a stage of the rule, any row of `Decide`, or the go-back rule changed | `ExitPolicyTests` (all seven) |
| The foreign mark, the override, the record of what was written, or Begin's foreign test gone | `AForeignExitOrAPlayerOverrideIsNeverOverwritten`, `EveryExitWriteKeepsTheCopiesDutiesAndWalksInStep` |
| The copies written before the duties, the stale walks or their stage test gone, the hold flag stored wrongly, a hold's copies all at the meeting point, the gathering toil's destination moved | `EveryExitWriteKeepsTheCopiesDutiesAndWalksInStep`, `HeldOnlyTheDownedStagesCopyPointsAtTheMeetingPoint`, `MovingTheExitWritesEveryToilCopyButNeverTheMeetingPoint` |
| A plan index swapped, the quiet fallback update gone, a missing field leaving the hold on, the pass flag unset, a message added to a quiet move | `EachActionTakesTheCellItsPlanMeans` |
| The search answering twice, on another map, without its hold flag or with the flag left set by its catch | `TheExitSearchIsVanillasOutsideASendOnAnotherMapAndAfterItsFirstAnswer`, `TheExitSearchPlansFromTheWindowsPawnsAndHoldsWhenOnlyAreasStandInTheWay` |
| The send's finalizer not closing, or the threat switch not opening the handoff | `TheSendOpensTheHandoffOnlyForAnExitSwitchAndAlwaysClosesIt` |
| A new caravan set up for a subclass lord, or held without the send's flag | `ANewCaravanIsSetUpOnlyWhenItIsANewVanillaLord` |
| The pass or its first-tick run gone from the lord tick, the map read before the stage test | `ThePassReadsTheMapOnlyWhileTheRuleAppliesAndDecidesThroughThePolicy` |
| A saved node renamed, or saved for a subclass lord | `TheSavedNodeNamesNeverChange` |
| The first-pass test ignoring the hold or the pass | `ACaravansStateIsMadeOnceAndAskingWhetherItIsHeldMakesNone` |
| The downed stage's gate inverted | `AHeldCaravanNeitherSetsOffNorCarriesItsDownedToTheEdge` |
| A hold not putting carried pawns down, or putting down a drafted pawn's | `EveryExitWriteKeepsTheCopiesDutiesAndWalksInStep` |
| The go-back without its reach test or its `GotoToil`, without its skip of a pawn in a member's arms, counting an unspawned pawn reachable, armed without a write, never armed, or forgotten while some still wait | `AWaitingCaravanGoesBackForDownedPawnsLeftAwayFromItsExit`, `AWaitingCaravanGoesBackForDownedPawnsAwayFromItsExitWhenItCanReachThemAll`, `EveryActionButNothingWritesTheExit` |
| A hold not stopping a pick-up under way, or putting a carried pawn down away from the spot, or not on every held pass | `EveryExitWriteKeepsTheCopiesDutiesAndWalksInStep` |
| The alert naming the dead or those gone from the caravan, leaving its named pawns out of the culprits (which silences it), or not reading the hold | `TheAlertReadsTheHeldStateAndPlansNothing` |
| The region search ignoring doors or fences, pen animals searched as themselves, threats not filtered | `TheReadingsSearchRegionsAsVanillasSendCheckWouldAndAskTheAreaGate` |
| The field check without the meeting point | `TheExitIsMovedOnlyWithEveryFieldItWrites` |

Equivalent, five: the two threat radii at `<=` (a hostile exactly at the radius adds 0 either way);
parking without the fallback guard (an area blocker always has a fallback); holding on any blocker in
the search (the others never have one); and reading threats with their switch off (the planner then
ignores them, so it costs time only).

Not pinned, twenty-two, each control flow or an argument, with the in-game check that covers it:
`Explain`'s tie between equally wide cells going to the last (check 40, the names); the held tail of
the pass run when not held (40, 45); the old lord taken for the new one (39); the trigger postfix
testing the hold before vanilla's answer (49, cost) or letting a held caravan through (40); the downed
giver's and the baby postfix's results not withdrawn (43); the caravan's own movers counted as threats
(48); the reader passing a constant for the host faction (none: guests do not join caravans here);
fogged edge cells counted usable (40, fog); the roper being any first mover rather than a colonist
(39, the pen); downed pawns counted as movers (43); the blame list cleared on a pass that cannot name
anybody (40); regions ranked by discovery order rather than depth (39); the held-copies check given
the wrong cells (45); the go-back taken from any stage, skipped from the waiting stage's pause, or
sent from that pause straight to the gathering stage, its pending flag cleared on a retry, armed
while held, or its early return inverted, which turns it off (44);
and a carried pawn put down whatever its carrier is doing, feeding included (43).

Mutations measured on Slice 4b (`muts-s4b.json` and `muts-s4b-fixes.json` in the workspace): 26 on
the first draft, 21 caught; 19 on the rework after its reviews, 15 caught; 7 on the fixes after the
rework's review (`muts-s4b-fixes2.json`), 6 caught.

| Mutation | Caught by |
|---|---|
| The button shown to an uncontrolled pawn, its band shrunk, a pause left out of its stages, the spot's circle shrunk, a gone pawn listed by Force depart | `LeaveFromHereTests` (the policy tests) |
| The route ignoring whether anybody undrafted is left to fetch or rope | `TheCaravanGoesWhereTheWorkLeftIsAndStraightToLeavingWithNobodyToDoIt` |
| The button's cheap tests reordered or its edge distance ignored | `TheButtonIsBuiltByTheGizmoPostfixFromTheRuleAndCheapTestsComeFirst` |
| The click sounding once per member | `TheButtonGroupsSoundsOnceAndOffersOnceForTheClickedCaravan` |
| Areas not overridden, every edge cell ruled out | `TheSpotIsTheExitPlannersOverwhatEveryoneCanReachNearTheSelectedPawnsOverridingAreasAndThreats` |
| The confirmation flag left set, or the Force depart list not kept out | `TheConfirmationIsVanillasOwnWithTheForceDepartListKeptOut` |
| The broken kept or planned around, the exit not written, no override, jobs not ended, tied animals not untied, undrafted members ordered over, a mech ordered beyond range, the order threshold at 10, workers or roping misread | `TheDepartureRemovesTheBrokenWritesTheExitRoutesAndCallsTheFarDraftedOver` |
| A carrier's draft or work ignored when counting strays, fetching by anybody rather than workers | `AWaitingCaravanGoesBackForDownedPawnsLeftAwayFromItsExit` |
| Babies not marked, or a drafted carrier's pawn counted as going, in Force depart's list | `ForceDepartListsTheDownedItLeavesThroughThePolicy` |
| A forced order not counted, or the walk-off's exit flag left set | `TheWalkOffIsStoppedOnlyForTheCaravansOwnPlayerOrders`, `OnlyAMechBeyondCommandIsUndraftedAndNothingForcesTheVanillaDepartureOrSetsAWalkOff` |

The departure's fetchers argument was unpinned in the rework's run and is pinned since, by the literal
before the call. Not pinned, eight, each control flow or an argument: the untying run when not going
straight to leaving, and the leaving route landing on another toil (check 53); the route counting
unfetchable downed pawns as fetchable, and the unfetchable not named in the question (52); the walk-off stopped but vanilla run anyway, and "Carry to exit" not
hidden (56); a failed goto search ordering a drafted member onto its own cell (rare: the spot is one
everybody can reach); and the button looking the lord up for every pawn, which costs time only. The first
draft's group filter by caravan went with the rework, which presses for one caravan only.

Mutations measured on Slice 4c (`muts-s4c.json` in the workspace, run while the translations were
still missing, so two language tests failed throughout; a catch counts only when the named test
failed): 19 on the first draft, 13 caught, and one more once the chosen exit's score was pinned on a
threatened plan. The rework after the reviews was measured again (`muts-s4c-fixes.json`): 10, 8
caught; the round after its review (`muts-s4c-fixes2.json`): 4, 3 caught; the third
(`muts-s4c-fixes3.json`): 13, 12 caught; the fourth (`muts-s4c-fixes4.json`): 15, 13 caught; and
the fifth (`muts-s4c-fixes5.json`): 9, all caught, and two more after its review
(`muts-s4c-fixes6.json`), both caught.

| Mutation | Caught by |
|---|---|
| A stage, the commit rule's half, the cooldown's edge or length, the commit radius, the hold gate, the score test, the stages routed again | `ThreatPolicyTests` |
| The cooldown not started, the caravan not routed again, everybody counted as at the exit | `TheExitMovesAwayFromAThreatOnlyWhenTheAreaRuleDidNothingAndThroughThePolicy` |
| The threat alert not reading the flag | `TheThreatAlertReadsTheFlagAndPlansNothing` |
| The current or chosen exit's score not reported | `ThePlanReportsTheThreatOnTheExitItHasAndOnTheOneItChose`, `WhenEveryExitIsThreatenedTheLeastThreatenedIsChosenAndSaidToBe` |
| The commit rule without the downed at the exit, or with downed pawns to take along in every stage; the safe exit not limited to exits scoring 0, ranked at the set distances, its way judged at the set route distance, or given with threats off | `HalfTheColonistsAtTheExitOrDownedAlreadyAtItCommitsIt`, `TheSafeExitForAMoveIsClearOfTheWiderDistancesNotAtTheDangersRim`, `TheWayToTheSafeExitIsJudgedAtTheWiderRouteDistanceToo` |
| The move sent to the plan's choice rather than its safe exit; the downed at the exit not passed to the commit rule, or counted wherever they lie; the distances never widened | `EachActionTakesTheCellItsPlanMeans`, `TheExitMovesAwayFromAThreatOnlyWhenTheAreaRuleDidNothingAndThroughThePolicy` |
| The safe exit's wider score flattened to a flag, ranked over every valid exit, or missing the start's z from the wider input; a safe exit reported for -1 | `AHostileNearTheStartStillLeavesASafeExitToMoveTo`, `TheSafeExitIsOnlyEverOneScoringNothingAtTheSetDistances`, `TheSafeExitForAMoveIsClearOfTheWiderDistancesNotAtTheDangersRim` |
| The downed at the exit committing in every stage or only from collecting; the move without a safe exit open | `HalfTheColonistsAtTheExitOrDownedAlreadyAtItCommitsIt`, `TheExitMovesOnlyForACurrentThreatToASafeExitWithNothingElseInTheWay` |
| Either distance widened from the other, or into the other's slot (in the pass, before the fifth round moved the widening into the planner); the two downed counts swapped at the commit rule; a safe exit assumed open; a drafted carrier's pawn, or a pawn not downed, counted at the exit | `TheExitMovesAwayFromAThreatOnlyWhenTheAreaRuleDidNothingAndThroughThePolicy` |
| The colonists at the exit committing while downed pawns are gathered; the downed at the exit committing outside waiting; the safe exit at index 0 reported as none; the planner's widening left out, given the other distance, or with threats off; the downed and the colonists counted from the meeting point | `HalfTheColonistsAtTheExitOrDownedAlreadyAtItCommitsIt`, `TheFirstCandidateCanBeTheSafeExit`, `TheSafeExitForAMoveIsClearOfTheWiderDistancesNotAtTheDangersRim`, `TheWayToTheSafeExitIsJudgedAtTheWiderRouteDistanceToo`, `TheExitMovesAwayFromAThreatOnlyWhenTheAreaRuleDidNothingAndThroughThePolicy` |
| The move to any safer exit rather than a safe one, the downed commitment from any stage or none, the read with no hostile about, the way out measured from the meeting point while leaving, pets counted for the commit rule, a drafted carrier's pawn counted as one to take along, the alert's flag never cleared | `ThreatPolicyTests`, `TheExitMovesAwayFromAThreatOnlyWhenTheAreaRuleDidNothingAndThroughThePolicy` |

Equivalent, two: moving with no threat on the current exit, since no safe score is below 0; and a move
refused for a downed pawn nobody could fetch, which the commit rule had already ruled out (the guard
was removed). One of the third round's catches was of an equivalent mutation too, caught only by the
IL: the safe exit compared with the current one through the plan's choice instead, since under the
guard both always differed from a threatened current exit. That comparison went with the fourth,
whose `HasSafeExit` replaced it. Not pinned, six, each control
flow: the threat move taken over an area action (check 59); the alert's flag kept after an area
move, on a map with no hostile left, or through a pause once none is left (63); and in
`DownedAtExit`, a drafted carrier's pawn counted rather than skipped (61), and the pawns away from
the exit counted rather than those at it (67). The third round's survivor, the route distance
widened from the exit distance, was caught once the fourth pinned the widening's calls; since the
fifth moved the widening into the planner, `TheWayToTheSafeExitIsJudgedAtTheWiderRouteDistanceToo`
catches it there, and `Clearance` rounding to the nearest even number rather than up is caught by
`TheDistancesAMoveIsRankedByAreHalfAsFarAgain` (both `muts-s4c-fixes6.json`). The first
draft's areas-off mutation went with the rework, which keeps areas in every stage. The second
round's clearance plan, which vetoed every exit inside the wider distances, went with the third,
which ranks the safe exits by them (`ExitPlan.SafeIndex`) and committed a caravan with a downed pawn
already at its exit in any stage; the fourth narrowed that to waiting on, and the fifth to waiting
alone, kept the colonists waiting at the exit while downed pawns are gathered from committing it,
and moved the widening into the planner, where nothing in the pass can skip it.

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

### Slice 2

19. **Fifteen distant muffalo.** Form a caravan of two colonists and fifteen muffalo grazing in a
    pen across the map, with four colonists not going who have Load on. The helpers lead muffalo,
    several at a time, tie them at the spot, and gathering finishes well before the two members
    could have done it alone. An elephant in the same caravan walks over by itself and is never
    roped by a helper. Turn off "Helpers bring pen animals": only the members fetch.
20. **Force depart mid-gathering.** While helpers are leading animals, Force depart: every helper
    lets go at once, the animals walk to the exit with the caravan as they do when its members let
    go, and the caravan leaves without stalling.
21. **Cancel mid-gathering.** While helpers are leading animals, cancel the caravan: the helpers let
    go at once, and the caravan's colonists lead every pen animal back to the pen, the ones the
    helpers held included. Animals already tied at the spot stay tied, as in vanilla. Repeat with a
    helper walking to a far muffalo (its rope job is often queued behind a haul it picked up on the
    way): no red "Could not reserve" error, and every animal is still penned.
22. **Removed on the way.** While a helper leads a muffalo, remove that muffalo from the caravan in
    its tab: the helper lets it go at the spot rather than tying it there.
23. **Area on the way.** A helper restricted to an area roping a muffalo inside it does not pick up
    a second one just outside it.
24. **A mental break while animals gather.** A member breaks while a helper is mid-way with a
    muffalo: the helper finishes, the muffalo is tied at the spot, not let loose, and gathering
    resumes after the break.
25. **Prioritize and the area.** Right-click a caravan muffalo with a colonist not going:
    "Prioritize roping" works. On a muffalo a member is walking to, the option is greyed with
    "reserved by" that member, and the member's rope train stays together. Restrict that
    colonist to an area that excludes the caravan's spot:
    the option is greyed with "the caravan's spot is outside the allowed area". A caravan member, a
    guest and a colony mech never get the option.
26. **Cost.** As check 12, while a caravan with twenty pen animals is gathering them: no measurable
    difference with "Helpers bring pen animals" on and off.

### Slice 3

27. **Drafting mid-rope.** While a caravan member leads three muffalo to the spot, draft it: it
    keeps all three, and they follow it wherever it is ordered, at the slowest one's pace with no
    stop-start. With "Keep leads when drafted" off, drafting drops them, as in vanilla.
28. **Undrafting in each stage.** Undraft a lead holder while animals are gathering: it walks on to
    the spot with them. While animals are collected before leaving: the same. While items are
    gathering, or during a pause: it lets go with a message.
29. **Drop lead.** One held animal: the button lets it go at once, it stops following straight
    away, and the colonist walks on at its own pace. Three held: a menu lists each and "All of them". Two colonists selected, each holding
    animals: one button, one menu, rows saying who holds each.
30. **Take the lead.** With two drafted members selected, right-click a caravan muffalo nobody leads:
    the nearer one walks to it and takes its lead, untying it from the spot if tied. A drafted
    colonist not going with the caravan gets the option greyed, "not going with this caravan". An
    animal somebody is leading gets no option, nor does a dog, nor an urgently hungry muffalo until
    the caravan is leaving. Undraft the colonist on its way: it stops.
31. **A long hold.** Keep a lead holder drafted and idle for 10000 ticks with no enemy about while
    items are gathering: it is not undrafted. A drafted colonist holding nothing still is, and so is
    a holder while animals are gathered (it then leads them on to the spot) and while leaving.
32. **A hungry animal.** Hold a muffalo until it is urgently hungry: it is let go with a message, and
    goes to eat. A sleeping animal still breaks its own rope, as in vanilla.
33. **A stage change.** While one member stands drafted holding animals and the others finish loading,
    the caravan moves on: the drafted member's order is not cancelled.
34. **Handlers.** A handler leading animals to a pen, drafted: it drops them, as in vanilla. A Load
    helper leading caravan animals, drafted: it drops them too.
35. **Despawn and departure.** A drafted lead holder entering a transport pod or leaving the map with
    the caravan: no rope survives, and nothing is logged.
36. **Cost.** With fifty pawns on the map and nobody leading anything, the rope tick patch costs
    nothing measurable in the dev profiler; with three drafted lead holders, likewise.
37. **Stranded while leaving.** While the caravan walks to the exit, have a drafted holder drop a
    muffalo in a room behind an ordinary door or a fence gate, not an animal flap, which pen animals
    open themselves. Within about 250 ticks a message says the caravan is collecting its animals
    again; a colonist fetches the muffalo, the others retie theirs, and the caravan leaves. Drop one
    in the open instead: it walks to the exit by itself and the caravan does not go back. With the
    first one still shut in, wall a second one in where no colonist can reach: nothing happens, and
    the caravan waits at the exit as vanilla's would.
38. **A break while holding.** With dev mode, give a drafted lead holder a mental break while items
    are gathering: the break's own letter and vanilla's "caravan formation paused" message, but no
    "let go of the animals" message. Undraft one by hand in the same stage: that message, once.

### Slice 4a

The exit is not drawn anywhere; watch where the caravan walks once it collects its animals and
leaves.

39. **A restricted colonist steers the exit.** Restrict one caravan colonist to an area that takes in
    a strip of a far map edge but not the edge nearest the caravan. Send: no message, and the caravan
    leaves by that strip. Turn "Leave inside allowed areas" off and send again: it leaves by the
    nearest edge, as in vanilla. A pen animal in a fenced pen with a gate does not change either
    result.
40. **Held for want of an exit.** Restrict every member to the home area, touching no edge. Send: a
    message says the caravan waits where it gathered. It gathers animals and items, then waits there,
    and "Caravan waiting for an exit" appears, naming every restricted member; clicking it jumps to
    them. Nobody carries anything or anybody towards an edge. Paint a strip of edge into the home area:
    within about 250 ticks a message says the caravan stops waiting, the alert goes, and it leaves by
    that strip. Repeat, letting it go each of these ways instead: setting every member to
    Unrestricted (one at a time: the alert drops each name, and the caravan goes with the last),
    clearing the area, and an area that excluded the edges until it was inverted. Then restrict one
    member to Home and another to an area with an edge strip: only the Home one is named. Paint the
    strip behind a wall, or in fog: still held, and the message says an edge the caravan can reach.
    Switch the game to Japanese and to Chinese: the alert and messages show no raw key.
41. **An area changed while forming.** Send with an exit inside everyone's area, then while items
    gather, change the area so the exit falls outside it and another edge stays inside: a message says
    the exit moved, and the caravan leaves by the new one. Shrink it again so no edge is inside: it is
    held, as in check 40. Build a wall over the exit instead (area untouched): the exit moves with no
    message. Change the area once the caravan has started collecting its animals: nothing moves,
    nothing is held, and it leaves by the exit it had.
42. **Force depart while held.** Hold a caravan, then wall off the edge nearest its spot, the one it
    would use ignoring areas, and wait 250 ticks. Force depart: the alert goes, and it leaves by
    another edge it can reach. Edit the areas while it walks out: nothing changes for it.
43. **Downed pawns and babies while held.** Hold a caravan at Send that includes a downed colonist in
    a bed near the caravan's spot and a baby in a crib, with more colonists than there is work. It
    waits in the stage that gathers downed pawns; nobody carries either towards an edge, any colonist
    who set off for the baby as the caravan started turns back (one who had already picked it up puts
    it down where it stands), and the stage does not end. Release it: colonists carry both to the new
    exit, and the caravan leaves with them. Hold one while a colonist is already carrying a downed
    pawn towards the edge (narrow an area mid-carry): the carrier walks back to the spot carrying it,
    puts it down there, and after the release it is picked up again and carried to the new exit. A
    colonist feeding the caravan's baby during a pause keeps it until done, and a drafted colonist
    holding a baby keeps it. A baby can still be taken for safety towards the held caravan's fallback
    edge after a feed; vanilla's baby-safety job reads the exit directly, and that is accepted.
44. **Moved while waiting for a sleeper.** Let a caravan with a downed adult colonist reach the stage
    where it waits for a very tired member to wake, with the downed one already put down at the exit.
    Change the area so the exit falls outside it: the exit moves, the caravan goes back to gathering
    downed pawns, the downed colonist is carried to the new exit, and the caravan leaves with it (the
    sleeper may be woken early by the step forward again). If instead the downed one is still in its
    carrier's arms when the area changes, there is no going back: it leaves with its carrier. Repeat
    with no edge inside the area: held, still waiting, the downed colonist left where it lies; after
    the release the caravan goes back and carries it to the new exit. Repeat with one colonist and two
    babies, one in its arms: the other is carried to the new exit too. Hold a caravan waiting for a
    sleeper, draft every member, then fix the area: the caravan goes back at once, and nobody carries
    the downed colonist until the members are undrafted. With the area left alone, a caravan waiting
    for a sleeper never goes back, whoever lies where. Change the area while a member has a mental
    break in that stage: the caravan goes to the gathering stage's pause and gathers once the break
    ends. Down an adult member while a caravan is held waiting for a sleeper: it too is carried once
    released.
45. **Save and reload while held.** Save a held caravan in each of its waiting stages (gathering items,
    gathering downed pawns, waiting for sleepers) and reload: still held, the members stay at the spot
    and walk to no edge in the first seconds, the alert names the same pawns from the first tick
    after unpausing, and fixing an area still releases it.
46. **Removing the mod while held.** Save a held caravan waiting for sleepers, remove the mod and load:
    no red error, and the caravan collects its animals and leaves by the best edge ignoring areas.
    Repeat held in the stage that gathers downed pawns, with the downed pawn far from the spot: it is
    carried to that edge and the caravan leaves with it. With the downed pawn within 7 cells of the
    spot, the caravan can stay in that stage until something refreshes its duties (a mental break's
    pause, or the pawn getting up); the store page's removal note has to say so.
47. **Another mod's exit.** With Enter Here (or any mod that sets the caravan's exit), send: the
    caravan leaves where that mod says, is never held, and this mod posts no exit message, whatever
    the areas are.
48. **Threats.** With "Avoid threats when leaving" on, put hostiles near the nearest edge and send: the
    caravan leaves by an edge away from them. Ring the map so every edge is near a hostile: it leaves
    by the least threatened. Turn both exit switches off: vanilla's nearest edge. With hostiles near
    the edge inside a restricted member's area and another in-area edge clear, an area change that
    moves the exit picks the clear one.
49. **Cost.** In the dev profiler on a large map, with a twenty-member caravan gathering items while its
    members are spread across the map (each start region costs its own region search), the 250-tick
    pass is not measurable against the tick; nor while twenty members are held together; with no
    caravan forming, nothing at all.

### Slice 4b

50. **The button.** Draft a colonist going with a forming caravan: "Leave from here" shows, greyed
    with "Stand within 10 cells of a map edge" until it walks within 10 cells of one. Select three
    drafted members and one drafted colonist not going: one button, one press, one click sound, one
    question. Turn "Leave from here" off: no button.
51. **From each stage, with help.** With one member drafted at the edge and the rest undrafted, press
    it and confirm from gathering animals, gathering items, gathering downed pawns, waiting for a
    sleeper, collecting animals and leaving, and from a pause (give a member a mental break with dev
    mode first). Each time: the caravan stops loading, leaves by an edge within about 12 cells of the
    drafted colonist, the undrafted lead every pen animal there, nobody is undrafted, a drafted lead
    holder keeps its animals, a drafted member more than 7 cells from the edge walks over, and the
    member in a mental break is left behind, as Force depart leaves it. The world route and
    destination are unchanged.
52. **Downed pawns and babies.** With a downed colonist in a bed and a baby in a crib, both far from
    the edge, press it from gathering items: they are carried to the new edge and leave with the
    caravan. Press it while an undrafted colonist is carrying the downed one somewhere else: it is
    carried to the new edge, not dropped. Wall one of them in where nobody can reach: the question
    names it as left behind, the caravan leaves without it, and a colonist not going rescues it.
53. **Everyone drafted.** Draft every colonist going, with pen animals tied at the spot and a downed
    colonist in a bed, and press it: the question names the downed colonist as left behind, the
    caravan goes straight to leaving, the tied animals are untied and walk to the edge, and it leaves.
    With pen animals still shut in a pen, it waits collecting its animals until a colonist is
    undrafted, then leads them over. Add a drafted mech with its mechanitor (undrafted, going or not)
    more than 25 cells from the edge: the mech is undrafted and walks over by itself; with the
    mechanitor drafted at the edge, the mech is ordered over and stays drafted.
54. **Areas and threats.** With "Leave inside allowed areas" on and a member restricted to an area
    that misses this edge, the question names that member, and the caravan leaves by the edge anyway.
    A caravan held for want of an exit stops waiting and leaves by the chosen edge. Hostiles near the
    edge do not stop it.
55. **Nowhere to go.** Press it beside an edge cut off by a wall from the rest of the caravan: a
    message says no edge near here can be reached by everyone, and nothing else happens.
56. **Walking off alone.** On a map that is not your home, form a caravan at a hitching spot, then let
    enemies arrive so the map's exit is open. Order a drafted member onto the map edge: it stops
    there, with a message naming it and pointing at Leave from here, and does not leave as a caravan
    of one. Right-click a downed colonist with that member: no "Carry to exit". A colonist not in the
    caravan still gets both. Turn "Leave from here" off: both work as in vanilla.
57. **Force depart's list.** With a downed colonist in a bed and a baby in a crib listed with the
    caravan, press Force depart: the question lists both under "will be left behind", the baby marked
    as a baby. Have an undrafted member carry the baby first: it is not listed. Have a drafted member
    carry it instead: it is listed, and Force depart does leave it. Turn "Warn about pawns left
    behind" off: vanilla's question alone. Leave from here's question never shows that list.
58. **Removing the mod after leaving from here.** Save while a caravan sent from here is collecting,
    remove the mod and load: it leaves by the chosen edge.

### Slice 4c

59. **A raid while gathering.** Send a caravan with "Avoid threats when leaving" on, then, while items
    gather, spawn raiders near its exit with dev mode: within about 250 ticks a message says it now
    leaves by an edge clear of them and to move drafted colonists yourself, and it does. With a member
    restricted to an area whose only edge is the threatened one, it stays, and "Caravan's way out
    threatened" shows.
60. **A raid while collecting and while leaving.** Repeat once the caravan is collecting its animals,
    and again while it walks out, with no downed pawns or babies going and raiders arriving before
    half the colonists reach the exit (colonists with nothing to lead walk ahead to it, and once half
    are there the caravan is committed, check 61): undrafted colonists let go
    and lead the animals to the new edge, a drafted lead holder keeps its animals, and the caravan
    leaves by the new edge. Spawn the raiders behind the walking caravan, at the colony, instead:
    it does not turn round. With a baby going, or a downed colonist at the old exit, it keeps its exit
    and the alert shows, and the same while it waits for a sleeper. A raider walking along the edge
    just past the exit never moves the caravan to the cell beside it: the edge it picks is clear of
    the raiders by half as far again as the threat distances where such an edge is open.
61. **Committed.** Let half the caravan's colonists reach the exit, then spawn raiders there: it does
    not move, and the alert shows. Pets and prisoners already at the exit do not count, and nor do
    colonists waiting at the exit while downed pawns are gathered. With at least three colonists going,
    while the caravan waits for a sleeper, draft one holding its only baby and stand it at the exit,
    the rest at the meeting point more than 25 cells away: raiders there move the exit, and the message
    asks you to bring that colonist over.
62. **Cooldown and small gains.** Spawn a raid near the exit, let the caravan move, then spawn
    another near the new exit within the hour: it stays until the hour is up, then moves if a clear
    edge exists. With hostiles that leave no edge clear of them, it never moves, however slightly
    one edge is better.
63. **The alert.** Whenever the exit the caravan keeps is threatened, "Caravan's way out threatened"
    shows, pointing at the exit; it stays through a mental break's pause; kill the raiders, or
    finish the caravan, and it goes.
64. **Overrides.** After Force depart, or after Leave from here, raiders near the exit move nothing.
    With Enter Here's exit, nothing moves. Turn "Avoid threats when leaving" off: nothing moves, and
    the alert never shows.
65. **A break.** Give a member a mental break while raiders sit near the exit: nothing moves during
    the pause; once it ends, the next pass moves the exit.
66. **Cost.** With no hostile on the map, a caravan collecting and leaving costs nothing measurable in
    the dev profiler; with a raid on, the pass every 250 ticks is not measurable against the tick.
67. **A raid while waiting, and downed pawns.** With the exit about 25 cells or more from the meeting
    point (the waiting colonists wander up to 5 cells around it), down a colonist with dev mode near the meeting point while the caravan waits for a
    sleeper, then spawn raiders near the exit: the exit moves, and the
    caravan goes back to gathering downed pawns, carries that colonist to the new edge, and waits
    again. Repeat with the downed colonist already within a few cells of the exit: the caravan keeps
    its exit and the alert shows. And while downed pawns are still being gathered, with one already
    at the exit, raiders there move the exit, and the one at the old exit is carried to the new one.
