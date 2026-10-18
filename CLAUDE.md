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
landed. **As of Slice 4c the Load work type, the completion guard, helpers leading pen animals, drafted
caravan colonists keeping their leads, the exit chosen at Send with the area hold, the exit moved away
from threats that turn up while forming, Leave from here, the solo walk-off block and the Force depart
list of pawns left behind are in; nothing else changes the game yet.**

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
| `CaravansCriticallyEnhancedDefOf` | `1.6/Core/CaravansCriticallyEnhancedDefOf.cs` | `CCE_Loading`, `CCE_RopeAnimalToCaravan`, `CCE_TakeLead` |
| `LeadPolicy`, `LeadRelease`, `DropLeadAction`, `TakeLeadBlocker` | `1.6/Core/LeadPolicy.cs` | Who keeps leads while drafted (a drafted colonist of a vanilla forming caravan, not itself roped), the conditions that still make it let go, releasing a starving or exhausted animal (not while leaving), sparing a lead holder the auto-undraft (by stage: not while animals gather or are collected, nor while leaving), the stages where undrafting resumes, the drop button's action, which animals may be taken (only pen animals that could keep the lead; a needy one only while leaving), the take-lead checks, and which animals a leaving caravan goes back for. Pure |
| `LeadKeepScope` | `1.6/Core/LeadKeepScope.cs` | The pawns being drafted whose ropes `DropRopes` must keep; nested, by reference |
| `LeadUndraftNotices` | `1.6/Core/LeadUndraftNotices.cs` | The colonists just undrafted whose leads drop on their next rope tick, kept for ten ticks so that tick can tell the player, and stay quiet for a mental break, which the draft setter cannot yet see |
| `LeadUtility` | `1.6/Utils/LeadUtility.cs` | Reads a roper for `LeadPolicy` and acts: needy releases with a message, speed matched to the slowest animal, whether the drop button shows, the undraft message, and sending a leaving caravan back to collect a pen animal stranded behind a door |
| `Command_DropLead : Command` | `1.6/Jobs/Command_DropLead.cs` | The drop-lead button: one group across every selected holder, one menu or a direct drop |
| `FloatMenuOptionProvider_TakeLead` | `1.6/Jobs/FloatMenuOptionProvider_TakeLead.cs` | Right-click "Take the lead of" on a caravan animal nobody leads, ordered to the nearest selected drafted member that can |
| `JobDriver_CCE_TakeLead` | `1.6/Jobs/JobDriver_CCE_TakeLead.cs` | Vanilla's roping toils behind three fail conditions: undrafted, the animal out of the caravan, somebody else leading it |
| `WorkGiver_CCE_LoadCaravan : WorkGiver_Scanner` | `1.6/Jobs/WorkGiver_CCE_LoadCaravan.cs` | The Load giver. No thing request; `ShouldSkip` skips any pawn in a lord and is free when nothing is forming, and then forgets what it held; the search set from `LoadingIndex`; one memoised `BuildJob` behind both scan entry points, carrier first; hands out vanilla's own `PrepareCaravan_GatherItems` job with `job.lord` set |
| `LoadingIndex`, `LoadingClaim` | `1.6/Core/LoadingIndex.cs` | Per map, rebuilt at most once per tick and frame: which listed items helpers may fetch (not in a closed haul source) and which caravan and row each belongs to |
| `LoadingCandidates` | `1.6/Core/LoadingCandidates.cs` | The search set's rules: no nulls, first caravan to list a thing owns it, a `List`. Pure |
| `CarrierRule` | `1.6/Core/CarrierRule.cs` | Which member a helper may hand an item to: none drafted, in a mental state or outside the helper's area; no overloaded colonists, colonists only with the setting, none while animals gather, rope animals only once tied at their own spot. Pure |
| `CompletionGuard`, `CompletionVerdict` | `1.6/Core/CompletionGuard.cs` | Whether a caravan vanilla calls loaded really is: hold while items are takeable, hold blocked items for the grace period, let go after the watchdog. Also every rule its reader applies: the stage it acts in, who counts as a waiting member and as a taker, how a listed thing counts, when an expiry is due and confirmed. Clocks travel as one `GuardClocks` value. Pure |
| `LoadingCompletion` | `1.6/Core/LoadingCompletion.cs` | The guard's reader: asks vanilla's own `FindThingToHaul` for each undrafted member, counts live units, keeps the clocks per lord job, applies the drafted-member exemption, clears rows with no things left while holding, and lets expired items go with a message |
| `WorkGiver_CCE_RopeCaravanAnimals : WorkGiver_Scanner` | `1.6/Jobs/WorkGiver_CCE_RopeCaravanAnimals.cs` | The Load column's second giver: colonists not in any lord lead a vanilla forming caravan's pen animals to its spot while it gathers animals. Same shape as the Load giver; the set from `AnimalGatherIndex`; checks area, reservation, then vanilla's `CanRoperTakeAnimalToDest` |
| `JobDriver_CCE_RopeAnimalToCaravan : JobDriver_RopeToDestination` | `1.6/Jobs/JobDriver_CCE_RopeAnimalToCaravan.cs` | Vanilla's gather-animals driver with the spot in target B instead of the roper's duty, which a helper lacks. Fails once the caravan is gone or past gathering animals, so the base finish action drops every rope |
| `AnimalGatherIndex` | `1.6/Core/AnimalGatherIndex.cs` | Per map, per tick and frame: the rope animals of vanilla lords gathering animals that nobody is leading, and each one's spot, read from its own duty |
| `RopeCandidates` | `1.6/Core/RopeCandidates.cs` | Which animals wait to be led: rope-managed, not on a rope, not tied at the spot, with the gather duty, in the gathering stage itself. Pure |
| `LoadingMigration` | `1.6/Core/LoadingMigration.cs` | The one-time Load priority copy, and `MakeRoom`, which puts later work types' priorities back in their own slots on the first load. Pure |
| `ExitPlanner`, `ExitPlanInput`, `ExitPlan`, `ExitBlocker`, `EdgeCandidate`, `MoverReading`, `ThreatReading`, `ThreatGeometry` | `1.6/Core/ExitPlanner.cs` | Chooses the exit cell from plain readings: reachable by every mover that reaches an edge at all, inside every restricted mover's area, then least threatened, fewest regions away, nearest, facing the world route; or a typed blocker, the movers to blame, and the best cell ignoring areas. Also whether the current exit is still valid, and still reachable, the threat scores of the current exit and the choice, and for a threat move the safe exit ranked first at distances it widens by half (`SafeIndex`, `HasSafeExit`). No randomness. Pure |
| `ExitPolicy`, `ExitAction` | `1.6/Core/ExitPolicy.cs` | Which pawns an area restricts (both vanilla area controls must show it), the stages the area rule applies in (before collecting), what the exit pass does (park, keep the fallback current, release, move with or without the area message, or nothing), and when a waiting caravan goes back for its downed pawns. Pure |
| `CaravanExitState`, `CaravanExitStates` | `1.6/Core/CaravanExitState.cs` | Per caravan, in a `ConditionalWeakTable` on the lord job: held, the exit last written, override, foreign; saved as `cce_exit*` nodes |
| `ExitReadings`, `ExitReading` | `1.6/Utils/ExitReadings.cs` | Reads a map for the planner: edge cells, one region search per group of movers sharing a start region and door and fence rules (pen animals as led by a colonist), each edge region's depth in regions, area masks through `ExitPolicy.IsRestricted`, threats, the route side |
| `ExitDirector` | `1.6/Utils/ExitDirector.cs` | The exit pass every 250 ticks, and at once on the first tick after a held caravan is loaded; the start of a caravan; Force depart. The writes that keep the lord job's exit, the three toil copies and the duties in step; putting down carried downed pawns on a hold; sending a waiting caravan back for downed pawns left away from its exit |
| `ExitHandoff` | `1.6/Utils/ExitHandoff.cs` | Carries the formation window's pawns and route into the exit search inside it, and its hold to the new caravan |
| `Alert_CaravanNoPermittedExit : Alert` | `1.6/Jobs/Alert_CaravanNoPermittedExit.cs` | Names the pawns whose areas hold a caravan and how to let it go; reads the held state only |
| `LeaveFromHerePolicy`, `LeaveButton`, `LeaveRoute` | `1.6/Core/LeaveFromHerePolicy.cs` | Who gets the Leave from here button and when it is greyed, which stage the caravan goes to (gathering downed pawns when one can be fetched, else collecting), how near an edge cell must be, and whom vanilla's Force depart leaves behind. Pure |
| `LeaveFromHere` | `1.6/Utils/LeaveFromHere.cs` | Reads a pawn for the button; finds the edge cell with the exit planner (areas and threats overridden); builds the confirmation from vanilla's own Force depart text plus the downed pawns nobody can reach and the areas this edge is outside; departs: the broken left out as vanilla does, the exit written as an override, the stage chosen, the far drafted ordered over |
| `ThreatPolicy` | `1.6/Core/ThreatPolicy.cs` | When an exit already chosen is given up for a safe one: a working stage, not a pause, the current exit threatened and one with no threat valid, not committed (half the colonists at the exit, except while downed pawns are gathered; while waiting, a downed pawn already brought there; from collecting on, downed pawns to take along), not within an hour of the last such move; and which stages measure from the colonists and need routing again. Pure |
| `CaravanRoute` | `1.6/Utils/CaravanRoute.cs` | Shared by Leave from here and the threat move: who works the duties, whether pen animals need leading or are fenced in, untying from spots, and `GoTo`, which moves the lord to a route's stage and ends the undrafted members' jobs |
| `Alert_CaravanExitThreatened : Alert` | `1.6/Jobs/Alert_CaravanExitThreatened.cs` | The exit a forming caravan keeps is threatened (no safe one open, committed, or cooling down); reads the flag the exit pass keeps |
| `Command_LeaveFromHere : Command` | `1.6/Jobs/Command_LeaveFromHere.cs` | The button: one group across the selected members, one press for the caravan of the first ready copy, whose colonist the edge is found near; greyed beyond 10 cells from an edge |

Defs: `CCE_Loading` (WorkTypeDef, naturalPriority 310, the column directly left of Haul, Hauling's
work tags), `CCE_LoadCaravan` (WorkGiverDef, priorityInType 110), `CCE_RopeAnimalsToCaravan`
(WorkGiverDef, priorityInType 120, so animals come before items, `canBeDoneByMechs` false, verb and
gerund copied from vanilla's `TakeToPen`) and `CCE_RopeAnimalToCaravan` (JobDef, copied from vanilla's
`PrepareCaravan_GatherAnimals` but for the driver). `1.6/Patches/MechWorkTypes.xml`
adds `CCE_Loading` to every `mechEnabledWorkTypes` list that names Hauling, which in the shipped game
is the Lifter alone.

Harmony surface, one class per vanilla target in `1.6/Patches/`, each applied on its own by `ApplyPatches`:

| Class | Target | Kind | Does |
|---|---|---|---|
| `Patch_WorkGiver_HelpGatheringItemsForCaravan_NonScanJob` | `WorkGiver_HelpGatheringItemsForCaravan.NonScanJob` | Prefix | Stands vanilla's helper down while Load is on, unless a subclass lord is forming on the map |
| `Patch_JobDriver_PrepareCaravan_GatherItems_IsUsableCarrier` | `JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier` | Postfix | `CarrierRule` for helpers only; hot path, early exits first |
| `Patch_JobDriver_PrepareCaravan_GatherItems_MakeNewToils` | `JobDriver_PrepareCaravan_GatherItems.MakeNewToils` | Postfix | A helper's job fails once the caravan leaves the gathering stages |
| `Patch_CaravanFormingUtility_AllItemsLoadedOntoCaravan` | `CaravanFormingUtility.AllItemsLoadedOntoCaravan` | Postfix, last | The single owner of the answer, in the item-gathering stage only: drafted-member exemption, then the completion guard |
| `Patch_LordJob_FormAndSendCaravan_LordJobTick` | `LordJob_FormAndSendCaravan.LordJobTick` | Postfix | Every 250 ticks per lord, staggered: the progress clock and letting expired items go; then, each with its own error key, going back for animals stranded while leaving, and the exit pass |
| `Patch_CaravanFormingUtility_StopFormingCaravan` | `CaravanFormingUtility.StopFormingCaravan` | Prefix | Ends the animal helpers' rope jobs, current and queued, and has drafted members let go, before vanilla cancels, so vanilla's `LeadAnimalsToPen`, which skips any roped animal, leads their animals home too, and no queued job's reservation breaks the pen lord's rope jobs |
| `Patch_Pawn_RopeTracker_RopingTick` | `Pawn_RopeTracker.RopingTick` | Prefix | For a roper `LeadUtility.KeepsLeads` accepts: vanilla's breaking checks less being drafted and not being in a rope job, needy releases every 250 ticks, speed matching. Hottest patch: leaves at once for a pawn leading nothing. For a roper it does not hold, tells the player about an undraft notice waiting for it |
| `Patch_Pawn_DraftController_Drafted` | `Pawn_DraftController.Drafted` setter | Prefix and finalizer | Opens a `LeadKeepScope` across drafting a roper the mod will hold; the finalizer closes it and, on an undraft that will drop the leads, leaves a `LeadUndraftNotices` entry for the rope tick |
| `Patch_Pawn_RopeTracker_DropRopes` | `Pawn_RopeTracker.DropRopes` | Prefix | Refuses inside the scope while spawned, so the rope job's finish action keeps the leads |
| `Patch_Pawn_RopeTracker_DropRope` | `Pawn_RopeTracker.DropRope` | Postfix | Ends the let-go animal's follow job and clears the roper's speed match on it |
| `Patch_Pawn_RopeTracker_CreateRope` | `Pawn_RopeTracker.CreateRope` (private static) | Prefix | Always on: lets the previous roper go first, which vanilla forgets |
| `Patch_JobGiver_FollowRoper_TryGiveJob` | `JobGiver_FollowRoper.TryGiveJob` | Postfix | Vanilla's follow job, for a roper the mod holds |
| `Patch_AutoUndrafter_ShouldAutoUndraft` | `AutoUndrafter.ShouldAutoUndraft` (private) | Postfix | Spares a lead holder in a vanilla forming caravan while items, downed pawns or sleepers are waited for and in the pauses before leaving; not while animals gather or are collected, where undrafting resumes leading, nor while leaving |
| `Patch_TransitionAction_EndAllJobs_DoAction` | `TransitionAction_EndAllJobs.DoAction` | Prefix | For a vanilla forming caravan, ends every undrafted member's job and leaves the drafted to their orders |
| `Patch_Pawn_GetGizmos` | `Pawn.GetGizmos` | Postfix, pass-through | Adds the drop-lead and Leave from here buttons |
| `Patch_Pawn_WorkSettings_ExposeData` | `Pawn_WorkSettings.ExposeData` | Postfix | Saves `cceLoadingInit`; on the first load without it, realigns later work types' priorities and copies the helper's work type priority into Load |
| `Patch_Dialog_FormCaravan_TryFormAndSendCaravan` | `Dialog_FormCaravan.TryFormAndSendCaravan` (private) | Prefix and finalizer | Opens `ExitHandoff` for the send when either exit switch is on; the finalizer closes it |
| `Patch_RCellFinder_TryFindClosestEdgeCellTo` | `RCellFinder.TryFindClosestEdgeCellTo` | Prefix | Inside a send only, once: answers with the plan, or with the best exit ignoring areas and a hold when areas block; vanilla when nothing is reachable. Its sole caller is the send |
| `Patch_CaravanFormingUtility_StartFormingCaravan` | `CaravanFormingUtility.StartFormingCaravan` | Prefix and postfix | The shared start: notes the first pawn's lord before, and sets up the new lord's exit state, holding it at once if the send said so |
| `Patch_LordJob_FormAndSendCaravan_ExposeData` | `LordJob_FormAndSendCaravan.ExposeData` | Postfix | The shared save: the exit state, vanilla's exact type only |
| `Patch_Trigger_NoPawnsVeryTiredAndSleeping_ActivateOn` | `Trigger_NoPawnsVeryTiredAndSleeping.ActivateOn` | Postfix | A held caravan never moves from waiting to collecting |
| `Patch_JobGiver_PrepareCaravan_GatherDownedPawns_TryGiveJob` | `JobGiver_PrepareCaravan_GatherDownedPawns.TryGiveJob` (protected) | Postfix | A held caravan's colonists carry no downed pawn to the fallback edge |
| `Patch_CaravanFormingUtility_ForceCaravanDepart` | `CaravanFormingUtility.ForceCaravanDepart` | Prefix | Force depart overrides the area rule: release to the fallback, and the rule leaves that caravan alone |
| `Patch_LordToil_PrepareCaravan_GatherDownedPawns_CheckAllPawnsArrived` | `LordToil_PrepareCaravan_GatherDownedPawns.CheckAllPawnsArrived` (private) | Prefix | A held caravan's downed pawns never count as gathered, so one in bed near the spot is not left behind |
| `Patch_JobGiver_GotoTravelDestination_CaravanBabyToCarry` | `JobGiver_GotoTravelDestination.CaravanBabyToCarry` (private) | Postfix | A held caravan's colonists pick up no baby to hold for the length of the hold |
| `Patch_CaravanFormingUtility_GetForceDepartWarningMessage` | `CaravanFormingUtility.GetForceDepartWarningMessage` | Postfix | Appends, in vanilla's shape, the downed pawns and babies Force depart leaves behind (every one not in a member's arms); not for Leave from here's own use of the method |
| `Patch_JobDriver_Goto_TryExitMap` | `JobDriver_Goto.TryExitMap` (private) | Prefix | A forming caravan's colonist ordered onto an exit cell (drafted or a forced job) stops at the edge instead of leaving alone; throttled message pointing at Leave from here |
| `Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt` | `FloatMenuOptionProvider_CarryPawnToExit.AppliesInt` (protected) | Postfix | No "Carry to exit" for a member of a forming caravan |

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
- **A helper never holds an animal into the collect stage.** Vanilla's `IsRopedByCaravanPawn`
  counts an animal as led only when its roper is in the same lord, so a helper still holding one
  there would stall collection. The rope driver fails as soon as the caravan is past gathering
  animals (or gone: `Lord.Cleanup` leaves the toil and member list as they were, so the test is the
  map's lord list, as in vanilla's gather-items driver), and the base finish action drops the ropes.
- **The rope giver never takes over a reservation, even forced.** A reserved caravan animal is one a
  member or another helper is walking to; vanilla's forced takeover ends that pawn's rope job, and
  the job's finish action lets go of every animal it already leads.
- **Removing the mod mid-rope leaves a helper idle until something interrupts it.** The rope job's
  def and driver fail to load, and `Pawn_JobTracker`'s cleanup of a job with no def throws, leaving
  the pawn with a dead job (its ropes break by themselves). Every mod with its own job has this;
  it belongs in the store page's removal note. Slice 1 has none, because it reuses vanilla's job.
- **A slow helper can make the caravan wait.** Members cannot take an animal a helper has reserved,
  so gathering waits for the helper, as it would for a slow member. Nothing can stick.
- **The rope driver ties at target B, not at the animal's duty.** It carries on through the pause
  for a mental break, where every duty is the pause duty, which has no spot.
- **Leads are kept only for a drafted colonist of a vanilla forming caravan.** A handler drafted while
  leading animals to a pen, or a Load helper, drops them as in vanilla; the setting's tooltip says
  "for a forming caravan". Vanilla cuts a drafted roper's leads in four places (the draft ending the
  rope job and its finish action, `RopingTick`'s drafted check, its not-in-a-rope-job check, and
  `JobGiver_FollowRoper`'s rope-job check), and all four are covered together.
- **Undrafting resumes leading only in the gather and collect stages,** whose duties give a rope job
  that reuses the held animals. Anywhere else the next rope tick drops them and a message says so,
  from the rope tick rather than the draft setter: `MentalStateHandler.TryStartMentalState` undrafts
  before it records the break, so inside the setter a break looks like any other undraft.
- **Vanilla's leaving stage waits for every member at the exit, with no timeout, and a dropped lead
  sends no signal.** A pen animal let go there (the drop button, or the automatic undraft, which is not
  suppressed while leaving) walks out by itself if it can, but one behind a door it cannot open never
  would. So every 250 ticks a leaving caravan with such an animal is sent vanilla's own
  `AddedRopeAnimal` memo, back to collecting, but only when every pen animal no member leads passes
  vanilla's collecting test (`JobGiver_PrepareCaravan_CollectPawns.DoesAnimalNeedGathering`) for some
  undrafted colonist member: collecting finishes only once every pen animal is led, so otherwise
  going back would only move the wait from the exit to collecting. That memo's
  transition ends every undrafted member's job, so their ropes drop and are retied; vanilla does the
  same when a pen animal joins.
- **The area rule stops once animal collection starts** (Matthew, 2026-10-16). Vanilla hides Force
  depart in the leaving stage (it still shows in that stage's pause, where its memo has no transition
  and does nothing but undraft and clear the downed list), so a caravan held there could only be ended
  by Leave from here, removing a pawn or cancelling. `ExitPolicy.AreaRuleApplies` is the four gathering
  and waiting stages and their pauses; past them a held caravan is released by its fallback, and
  nothing parks it.
- **Held, the lord job and the collecting and leaving copies keep the fallback; only the downed-pawn
  stage's copy points at the meeting point.** Its duty's wander (`JobGiver_GotoTravelDestination`,
  focus index 2) walks idle colonists to that copy; the collecting and leaving copies are never
  reached while held, so they keep the fallback, and anything that sends the caravan on regardless
  leaves by it rather than vanishing from the meeting point. A save loaded without the mod rebuilds
  the toils from the lord job. The toils' copies are not saved, so a held caravan gets a whole pass on
  the first tick after any load (`FirstTickAfterLoad`, ahead of the 250-tick interval), which writes
  them back and fills the alert's names. While held, each pass also keeps the fallback current, so
  Force depart or the switch going off never sends the caravan to a cell walled off since.
- **A held caravan carries nobody.** Vanilla keeps a carried pawn through `Goto`, `GotoWander` and
  `Wait_Wander` (`dropThingBeforeJob` false), so a carry cut short would last the whole hold. The
  downed giver hands out no carry, `CaravanBabyToCarry` picks no baby, and the downed stage never
  counts its pawns as gathered, since its test reads the copy that now points at the meeting point
  and a pawn in bed there would count. On every held pass `StopCarrying` ends a pick-up still under
  way (vanilla rethinks every member's job inside the send, before the hold) and has an undrafted
  member already carrying one put it down only while it is wandering within 7 cells of the meeting
  point; when the hold starts with the send, anybody already carrying one puts it down where it
  stands (a colonist beside the crib finishes the pick-up within the send). The duties of gathering animals, of the held downed stage and
  of waiting walk carriers there; while items are gathered, and in the pauses, vanilla's next job
  drops the pawn where the carrier stands, as vanilla would. A job that uses the carry, such as
  feeding a baby, is left alone, and a drafted carrier is the player's. Put down where the carrier
  stood, an adult could lie at a far edge for the whole hold, since nothing rescues or feeds a downed
  adult its caravan is to take (babies can still be fed). Removing the mod while held in the downed
  stage can stick it: the saved duties' second focus is the meeting point, so the giver skips downed
  pawns near it while the rebuilt toil waits for them near the fallback, until something refreshes
  the duties (a pause, a downed pawn recovering). Since a carry under way at the hold ends with the
  pawn put down at the spot, that is the usual case, and the store page's removal note, written at
  Slice 9, has to say so.
- **Downed pawns are carried only in their own stage, so a waiting caravan goes back for them, but
  only after the mod wrote its exit.** A pass that wrote the exit (any action but none) in the waiting
  stage or its pause, leaving the caravan not held, sets `RegatherPending`, saved as
  `cce_exitRegather`; while held the lord job's exit is only the fallback, so the release's write arms
  it against the real one. Every pass then counts the
  listed downed pawns more than 7 cells from the exit and not held by a member who works no duties and
  is not in a mental state (a drafted carrier keeps its pawn and leaves with it; one a worker holds,
  such as the last pawn of the downed stage, which ends while it is still carried, is counted, and so
  is one a broken member holds, as one nobody can fetch), babies included
  (a member picks up only one baby while collecting and leaving). Once a colonist member with hands
  can reach every one of them (reach only: drafting and reservations pass, and the gathering stage
  waits them out), it sends the lord back to the downed stage with `Lord.GotoToil`, or from the pause
  to that stage's own pause, and clears the flag; with none left, or the caravan past waiting, it
  clears the flag; otherwise it asks again next pass. Going back ends in vanilla's own transition,
  whose job ending can wake a sleeper early, which is why a caravan whose exit the mod left alone is
  never sent back, whoever lies where. If the caravan finishes waiting before an unreachable one can
  be fetched, it is left, as vanilla would leave it.
- **Another mod's exit is never overwritten,** where "another mod's exit" means a lord job exit the mod
  did not write. The exit last written is saved; a lord job whose exit differs, at the start (Enter
  Here rewrites it while the graph is built) or later, is foreign for good, and a held one is released
  by whatever exit it has. A mod that writes only the toils' copies, or changes the argument to
  `StartFormingCaravan`, is not detected.
- **Vanilla's baby carry to safety still reads the live exit.** `JobDriver_BringBabyToSafety.FindBedForBaby`
  calls `FindRandomDropCell`, so a held caravan can have a baby carried toward the fallback edge. Accepted.
- **The planner ranks by threat, then region depth, then straight-line distance, then the route's
  side.** `ExitReadings` records each edge region's depth in the entry predicate of the breadth-first
  search (the first entry into a region comes from one nearer); discovery order would differ between
  regions at the same depth.
- **The exit moves away from a threat only to a safe one, only after the area rule had nothing to do,
  and never once committed.** With "Avoid threats when leaving" on and some hostile an active threat
  on the map (`GenHostility.AnyHostileActiveThreatToPlayer`, which keeps the read off a quiet map),
  the 250-tick pass plans in every working stage and, when the area decision is None, asks
  `ThreatPolicy.Moves`: never in a pause, not held, foreign or overridden; the current exit's threat
  score above 0 and a safe exit open (`ExitPlan.HasSafeExit`), which the move goes to (`SafeIndex`):
  of the valid exits scoring 0, the one ranked first with both threat distances widened by half
  (`ThreatPolicy.Clearance`, which the planner applies itself, so nothing in the pass can skip it), so
  the nearest (fewest regions away, then fewest cells) clear of the wider distances where there is one,
  otherwise the one least inside them, rather than the nearest just outside the danger; a small gain
  never moves it. The wider distances only rank: an earlier draft vetoed every exit inside them, and a
  hostile inside the wider route distance of the start then ruled out every way out. Not committed; an hour since
  the last threat move (`LastThreatMoveTick`, `cce_exitThreatMoved`). Areas count as the switch says in
  every stage, so a move never breaks an area the rule would then restore. Committed
  (`ThreatPolicy.Committed`) is half the colonist members within 15 cells of the exit (pets and
  prisoners walk ahead), except while downed pawns are gathered, whose duty walks every colonist with
  nothing to carry to the exit to wait (`JobGiver_GotoTravelDestination` on the duty's second focus),
  so standing there says nothing of how far along the caravan is; or, while waiting, when everybody
  is back at the meeting point, a listed downed pawn within vanilla's 7 cells of it
  (`IsDownedPawnNearExitPoint`, which reads a carried pawn at its carrier's cell) and not in a drafted
  member's arms, since a move would send carriers back into the threat for it; or, from collecting on,
  any listed downed pawn or baby not in a drafted member's arms, since going back for it would send the
  animals back to the colony too. Before waiting a pawn already at the exit does not commit: the
  downed stage fetches every listed pawn to wherever the exit is, so a move has the pawns already at
  the old exit carried again, mostly by the idle colonists the duty parked beside them (the giver takes
  the nearest first). That is accepted, and so is its worst case, a caravan with no pen animals whose
  last carry is nearly done, which a move sends the long way round. In the downed stage the way out
  is still measured from the meeting point, though the idle colonists stand at the old exit, so the
  route test there is rougher than in collecting and leaving. A waiting caravan whose downed pawns all lie away from
  its exit is not committed either: the move arms the go-back to the downed stage, which carries them
  to the new exit. One with some at the exit and some away is committed, and the ones away are left as
  vanilla leaves them.
  In collecting and leaving the way out is measured
  from the colonists' centre, since they have left the meeting point. Before collecting the write is
  the whole move; in collecting and leaving the move also goes through `CaravanRoute`, as Leave from
  here does, because rope jobs keep their old destination. Drafted members are not ordered; the
  message asks the player to move them. `Alert_CaravanExitThreatened` shows while the exit a caravan
  keeps is threatened (`ExitThreatened`, set from the plan, cleared when the rule stops watching or no
  hostile is left, kept through a pause).
- **Leave from here goes through vanilla's own stages, not Force depart's memo, and only to stages
  somebody can work.** `ForceDepartNow` jumps to leaving, skipping the stage that gathers downed
  pawns, and vanilla's Force depart clears the downed list and undrafts everyone. Leave from here
  writes the exit as an override (the area rule then leaves the caravan alone), removes members in a
  mental state as Force depart does, and goes by `Lord.GotoToil` to the stage still needed. Drafted
  pawns take orders, which `Humanlike.xml` puts above the lord duty, so only a worker
  (`ExitDirector.IsWorker`: an undrafted colonist with hands, not in a mental state) does a stage's
  work. With a worker: gathering downed pawns when a listed one lies, or is held by a worker, more
  than 7 cells from the new exit and a worker can fetch it (a rope job drops whatever its roper
  carries, so a carried pawn must be carried over first); else collecting when a pen animal is off
  every drafted member's lead (an undrafted roper's lead drops when its job is ended below). Without a
  worker, collecting only when such an animal is fenced in (cannot reach the exit itself), where it
  waits for an undraft; otherwise straight to leaving, with animals tied at the spot untied, as Force
  depart does. Downed pawns nobody undrafted can fetch come off the list first, as the question
  warned, since the gathering stage waits for every listed one. Then it ends every undrafted member's
  job, as a stage change does, because a collecting job under way keeps its old destination and never
  ends by itself. Drafted members keep their orders and leads. Vanilla's leaving stage waits for every
  member within 10 cells, and the downed stage counts a carried pawn by its carrier's cell, within 7,
  so drafted members more than 7 cells from the exit are ordered over (`BestOrderedGotoDestNear`,
  which gives back the pawn's own cell when it finds nothing). A drafted mech beyond its mechanitor's
  command range cannot be ordered and is never undrafted by vanilla's auto-undraft, so it is
  undrafted instead: the one draft change the mod makes. The spot is the exit planner's, over the
  same readings as the area rule for the members who will go, with edge cells more than 12 cells from
  the colonist whose button was used marked unusable and areas and threats overridden; the
  confirmation lists what that overrides. It is vanilla's own Force depart text, built while
  `LeaveFromHere.Confirming` is set so that the mod's Force depart postfix, which lists the downed it
  would abandon, stays out. A very tired member can still lie down in the collecting stage, as in
  vanilla.
- **The solo walk-off is stopped, never `Pawn.ExitMap`.** Vanilla's own departure calls `ExitMap`
  while the pawns are still members. The prefix on `JobDriver_Goto.TryExitMap` acts only for a
  player's order (drafted, or a forced job), clears the job's exit flag so it is not asked again every
  tick, and lets the walk finish; a mental break's exit is vanilla's. Both it and the hidden "Carry to
  exit" arise only on a map that is not a home with enemies about, where the exit grid is on.
- **Two vanilla behaviours stay as they are.** Drafting a member still drops the caravan items it
  hauled into its own inventory (`DropAllPackingCaravanThings`, in the draft setter); the plan
  considered covering that with the lead scope and decided against it: while items are gathered,
  vanilla's drop puts each item back on the caravan's list (`TryAddItemBackToTransferables`), so
  somebody fetches it again. And adding a led animal to a caravan by hand (`Notify_PawnAdded`) breaks
  its lead, as in vanilla.
- **Only a pen animal can be taken.** An animal that walks by itself, tied at the spot by an undrafted
  holder while animals gather, would never be untied, and the caravan would wait for it at the exit
  for good. `LeadPolicy.CanBeTaken` asks `NeedsToBeManagedByRope` first.
- **`Pawn` implements `IEquatable<Pawn>` by comparing def names as strings, then thing ids.** So
  `List<Pawn>.Contains` is a string compare per element, and it throws on a bare `new Pawn()` in the
  harness. Compare by reference where reference is meant (`LeadKeepScope`).
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
  watchdog. With "Keep leads when drafted" on, the AllItemsGathered transition it lets through no
  longer ends a drafted member's current order (`Patch_TransitionAction_EndAllJobs_DoAction`). The
  hold state is not saved: after a load the clocks restart, which can only lengthen a wait.
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
