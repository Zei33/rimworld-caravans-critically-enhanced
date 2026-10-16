using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CaravansCriticallyEnhanced.Core;
using CaravansCriticallyEnhanced.Jobs;
using CaravansCriticallyEnhanced.Patches;
using CaravansCriticallyEnhanced.Utils;
using NUnit.Framework;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Leave from here, the walk-off block, the hidden carry to exit and the Force depart list: the
    /// rules as whole tables, and the game-facing half held to them by IL.
    /// </summary>
    /// <remarks>
    /// A <c>Lord</c> and a <c>Map</c> cannot be had here, so the button, the departure and the patches
    /// that need one are pinned by which calls they make and in what order, never by their arguments
    /// or branches; <c>Tests/README.md</c> lists the in-game checks for the rest.
    /// </remarks>
    [TestFixture]
    public class LeaveFromHereTests
    {
        private static readonly FormingStage[] Forming = Enumerable.Range(0, 12).Select(i => (FormingStage)i).ToArray();

        [Test]
        public void TheButtonShowsOnlyForADraftedControlledMemberAndWorksOnlyNearAnEdge()
        {
            var yes = new List<string>();
            foreach (bool featureOn in new[] { false, true })
            {
                foreach (bool member in new[] { false, true })
                {
                    foreach (bool drafted in new[] { false, true })
                    {
                        foreach (bool controlled in new[] { false, true })
                        {
                            LeaveButton state = LeaveFromHerePolicy.Button(featureOn, member, drafted, controlled, FormingStage.GatherItems, 3);
                            if (state != LeaveButton.Hidden)
                            {
                                yes.Add($"{featureOn} {member} {drafted} {controlled}");
                            }
                        }
                    }
                }
            }

            Assert.That(yes, Is.EqualTo(new[] { "True True True True" }), "One shown row in sixteen.");

            Assert.That(LeaveFromHerePolicy.Button(true, true, true, true, FormingStage.GatherItems, LeaveFromHerePolicy.EdgeBand), Is.EqualTo(LeaveButton.Enabled), "At the band's edge.");
            Assert.That(LeaveFromHerePolicy.Button(true, true, true, true, FormingStage.GatherItems, LeaveFromHerePolicy.EdgeBand + 1), Is.EqualTo(LeaveButton.TooFar), "Shown, greyed, with the reason.");
            Assert.That(LeaveFromHerePolicy.Button(true, true, true, true, FormingStage.End, 0), Is.EqualTo(LeaveButton.Hidden), "The caravan has gone.");
            Assert.That(LeaveFromHerePolicy.Button(true, true, true, true, FormingStage.Unknown, 0), Is.EqualTo(LeaveButton.Hidden));
            Assert.That(Forming.Select(s => LeaveFromHerePolicy.Button(true, true, true, true, s, 0)), Is.All.EqualTo(LeaveButton.Enabled), "Every forming stage and pause.");
        }

        [Test]
        public void TheCaravanGoesWhereTheWorkLeftIsAndStraightToLeavingWithNobodyToDoIt()
        {
            var table = new List<string>();
            foreach (bool strays in new[] { false, true })
            {
                foreach (bool roping in new[] { false, true })
                {
                    foreach (bool worker in new[] { false, true })
                    {
                        foreach (bool penned in new[] { false, true })
                        {
                            LeaveRoute route = LeaveFromHerePolicy.RouteFor(FormingStage.GatherItems, strays, roping, worker, penned);
                            Assert.That(Forming.Select(s => LeaveFromHerePolicy.RouteFor(s, strays, roping, worker, penned)), Is.All.EqualTo(route), "The same in every forming stage and pause.");
                            table.Add($"{strays} {roping} {worker} {penned} {route}");
                        }
                    }
                }
            }

            Assert.That(table, Is.EqualTo(new[]
            {
                "False False False False LeaveStage",
                "False False False True LeaveStage",
                "False False True False LeaveStage",
                "False False True True LeaveStage",
                "False True False False LeaveStage",
                "False True False True CollectStage",
                "False True True False CollectStage",
                "False True True True CollectStage",
                "True False False False LeaveStage",
                "True False False True LeaveStage",
                "True False True False DownedStage",
                "True False True True DownedStage",
                "True True False False LeaveStage",
                "True True False True CollectStage",
                "True True True False DownedStage",
                "True True True True DownedStage",
            }), "Downed pawns first when somebody undrafted can fetch them; then animals to rope, when somebody can or one is fenced in and must wait for somebody; otherwise straight to leaving, as Force depart.");
            Assert.That(LeaveFromHerePolicy.RouteFor(FormingStage.End, true, true, true, true), Is.EqualTo(LeaveRoute.Refuse));
            Assert.That(LeaveFromHerePolicy.RouteFor(FormingStage.Unknown, false, false, false, false), Is.EqualTo(LeaveRoute.Refuse));
        }

        [Test]
        public void AnEdgeCellIsNearWithinTheSpotRadius()
        {
            int r = LeaveFromHerePolicy.SpotRadius;
            Assert.That(LeaveFromHerePolicy.Near(10 + r, 10, 10, 10), Is.True, "On the radius.");
            Assert.That(LeaveFromHerePolicy.Near(10 + r + 1, 10, 10, 10), Is.False);
            Assert.That(LeaveFromHerePolicy.Near(10 + 8, 10 + 8, 10, 10), Is.True, "On a diagonal, 128 of 144.");
            Assert.That(LeaveFromHerePolicy.Near(10 + 9, 10 + 8, 10, 10), Is.False, "145 of 144: a circle, not a square.");
        }

        [Test]
        public void ForceDepartLeavesEveryDownedPawnNotInAMembersArms()
        {
            Assert.That(LeaveFromHerePolicy.LeftByForceDepart(gone: false, carriedByWorker: false), Is.True, "Lying, or held by a member Force depart undrafts or leaves out.");
            Assert.That(LeaveFromHerePolicy.LeftByForceDepart(gone: false, carriedByWorker: true), Is.False, "It goes with its carrier.");
            Assert.That(LeaveFromHerePolicy.LeftByForceDepart(gone: true, carriedByWorker: false), Is.False, "Vanilla's lord tick takes it off the list anyway.");
        }

        [Test]
        public void TheButtonIsBuiltByTheGizmoPostfixFromTheRuleAndCheapTestsComeFirst()
        {
            // A pass-through postfix is an iterator, so its body is a hoisted MoveNext.
            List<string> builders = ILCalls.CalledAnywhereInTheMod(typeof(Command_LeaveFromHere), ".ctor").Select(ILCalls.Describe).ToList();
            List<string> askers = ILCalls.CalledAnywhereInTheMod(typeof(LeaveFromHere), nameof(LeaveFromHere.ButtonFor)).Select(ILCalls.Describe).ToList();
            Assert.That(builders.Any(c => c.Contains("Patch_Pawn_GetGizmos")), Is.True, string.Join(", ", builders));
            Assert.That(askers.Any(c => c.Contains("Patch_Pawn_GetGizmos")), Is.True, string.Join(", ", askers));

            List<string> calls = Calls(typeof(LeaveFromHere), nameof(LeaveFromHere.ButtonFor));
            Assert.That(calls.Take(4), Is.EqualTo(new[] { "Verse.Pawn.get_Drafted", "Verse.Pawn.get_IsColonistPlayerControlled", "Verse.Thing.get_Spawned", "Verse.AI.Group.LordUtility.GetLord" }),
                "Asked every frame for every selected pawn: field tests before the lord, and all of them go to the rule.");
            Assert.That(calls, Is.SupersetOf(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Utils.CaravanLordAccess.get_AllExitFieldsAvailable",
                "Verse.IntVec3Utility.DistanceToEdge",
                "CaravansCriticallyEnhanced.Core.LeaveFromHerePolicy.Button",
            }));
            Assert.That(ToggleReads.AskedBy(Method(typeof(LeaveFromHere), nameof(LeaveFromHere.ButtonFor))), Is.EqualTo(new Toggle?[] { Toggle.LeaveFromHere }));
        }

        [Test]
        public void TheButtonGroupsSoundsOnceAndOffersOnceForTheClickedCaravan()
        {
            WalkResult ctor = ILCalls.Read(typeof(Command_LeaveFromHere).GetConstructors()[0]);
            Assert.That(ctor.Fields.Where(f => f.IsStore).Select(f => f.Field.Name), Is.SupersetOf(new[] { "groupKey", "alsoClickIfOtherInGroupClicked", "icon" }));
            Assert.That(ctor.Fields.Where(f => f.IsStore && f.Field.Name == "alsoClickIfOtherInGroupClicked").Select(f => f.IntBefore), Is.EqualTo(new int?[] { 0 }));
            Assert.That(Calls(typeof(Command_LeaveFromHere), ".ctor"), Does.Contain("Verse.Gizmo.Disable"), "Too far: greyed with the reason.");
            Assert.That(Command_LeaveFromHere.GroupKey, Is.Not.EqualTo(Command_DropLead.GroupKey));

            List<string> group = Calls(typeof(Command_LeaveFromHere), nameof(Command_LeaveFromHere.ProcessGroupInput));
            Assert.That(group, Does.Contain("Verse.AI.Group.LordUtility.GetLord"), "The caravan of the copy the grid took.");
            Assert.That(group.Count(c => c == "CaravansCriticallyEnhanced.Utils.LeaveFromHere.Offer"), Is.EqualTo(1));
            Assert.That(group.Last(), Is.EqualTo("CaravansCriticallyEnhanced.Utils.LeaveFromHere.Offer"));
        }

        [Test]
        public void TheOfferFindsTheSpotThenAsksAndOnlyTheAnswerDeparts()
        {
            List<string> offer = Calls(typeof(LeaveFromHere), nameof(LeaveFromHere.Offer));
            int find = offer.IndexOf("CaravansCriticallyEnhanced.Utils.LeaveFromHere.FindSpot");
            int text = offer.IndexOf("CaravansCriticallyEnhanced.Utils.LeaveFromHere.Confirmation");
            int ask = offer.IndexOf("Verse.Dialog_MessageBox.CreateConfirmation");
            Assert.That(new[] { find, text, ask }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", offer));
            Assert.That(new[] { find, text, ask }, Is.Ordered);
            Assert.That(offer, Does.Not.Contain("CaravansCriticallyEnhanced.Utils.LeaveFromHere.Depart"), "Only from the dialog's accept.");
            List<string> departers = ILCalls.CalledAnywhereInTheMod(typeof(LeaveFromHere), nameof(LeaveFromHere.Depart)).Select(ILCalls.Describe).ToList();
            Assert.That(departers, Has.Count.EqualTo(1).And.All.Contains("LeaveFromHere"), "The accept lambda, hoisted, is the only caller.");
        }

        [Test]
        public void TheSpotIsTheExitPlannersOverwhatEveryoneCanReachNearTheSelectedPawnsOverridingAreasAndThreats()
        {
            List<string> calls = Calls(typeof(LeaveFromHere), nameof(LeaveFromHere.FindSpot));
            int read = calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitReadings.Read");
            int near = calls.IndexOf("CaravansCriticallyEnhanced.Core.LeaveFromHerePolicy.Near");
            int plan = calls.IndexOf("CaravansCriticallyEnhanced.Core.ExitPlanner.Plan");
            Assert.That(new[] { read, near, plan }, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { read, near, plan }, Is.Ordered);

            WalkResult walk = ILCalls.Read(Method(typeof(LeaveFromHere), nameof(LeaveFromHere.FindSpot)));
            var overrides = new List<(string, int?)>();
            for (int i = 0; i < walk.Calls.Count; i++)
            {
                if (walk.Calls[i].Name == "set_ApplyAreas" || walk.Calls[i].Name == "set_ApplyThreats")
                {
                    overrides.Add((walk.Calls[i].Name, walk.IntBeforeCall[i]));
                }
            }

            Assert.That(overrides, Is.EqualTo(new[] { ("set_ApplyAreas", (int?)0), ("set_ApplyThreats", (int?)0) }), "The player's choice overrides both.");
        }

        [Test]
        public void TheConfirmationIsVanillasOwnWithTheForceDepartListKeptOut()
        {
            WalkResult walk = ILCalls.Read(Method(typeof(LeaveFromHere), "Confirmation"));
            List<string> calls = walk.Calls.Select(c => c.Name).ToList();
            int on = calls.IndexOf("set_Confirming");
            int vanilla = calls.IndexOf("GetForceDepartWarningMessage");
            int off = calls.LastIndexOf("set_Confirming");
            Assert.That(new[] { on, vanilla, off }, Is.Ordered.And.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(new[] { walk.IntBeforeCall[on], walk.IntBeforeCall[off] }, Is.EqualTo(new int?[] { 1, 0 }));
            Assert.That(Calls(typeof(LeaveFromHere), "Confirmation"), Does.Contain("CaravansCriticallyEnhanced.Utils.ExitDirector.CountStrays"), "The downed pawns nobody can reach are listed.");

            Assert.That(Calls(typeof(Patch_CaravanFormingUtility_GetForceDepartWarningMessage), "Postfix").First(), Is.EqualTo("CaravansCriticallyEnhanced.Utils.LeaveFromHere.get_Confirming"));
        }

        [Test]
        public void TheDepartureRemovesTheBrokenWritesTheExitRoutesAndCallsTheFarDraftedOver()
        {
            List<string> calls = Calls(typeof(LeaveFromHere), nameof(LeaveFromHere.Depart));
            string[] order =
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Utils.LeaveFromHere.FindSpot",
                "Verse.AI.Group.Lord.RemovePawn",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.LeaveFrom",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.CountStrays",
                "CaravansCriticallyEnhanced.Jobs.JobDriver_CCE_RopeAnimalToCaravan.LetGoFor",
                "CaravansCriticallyEnhanced.Utils.LeaveFromHere.NeedsRoping",
                "CaravansCriticallyEnhanced.Utils.LeaveFromHere.AnyWorker",
                "CaravansCriticallyEnhanced.Utils.LeaveFromHere.UntieFromSpots",
                "CaravansCriticallyEnhanced.Utils.LeaveFromHere.GoTo",
                "CaravansCriticallyEnhanced.Utils.LeaveFromHere.OrderDraftedOver",
            };
            int[] at = order.Select(c => calls.IndexOf(c)).ToArray();
            Assert.That(at, Is.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(at, Is.Ordered, "Re-checked, the broken left out as vanilla does, the exit written, then the stage, then the drafted.");
            Assert.That(calls.Count(c => c == "CaravansCriticallyEnhanced.Core.LeaveFromHerePolicy.RouteFor"), Is.EqualTo(2), "The stage checked before anything, and the route chosen after the exit.");
            Assert.That(calls, Does.Contain("Verse.AI.Group.LordManager.RemoveLord"), "Nobody left: the caravan ends as vanilla's does.");
            Assert.That(calls.IndexOf("System.Collections.Generic.List`1.RemoveAll"), Is.InRange(calls.IndexOf("CaravansCriticallyEnhanced.Utils.ExitDirector.CountStrays"), calls.IndexOf("CaravansCriticallyEnhanced.Utils.LeaveFromHere.GoTo")),
                "Those nobody can reach come off the list before the stage is chosen, or the gathering stage would wait for them for good.");

            List<string> go = Calls(typeof(LeaveFromHere), "GoTo");
            Assert.That(go.IndexOf("Verse.AI.Group.Lord.GotoToil"), Is.InRange(0, go.IndexOf("Verse.AI.Pawn_JobTracker.EndCurrentJob")));
            Assert.That(go, Does.Contain("Verse.Pawn.get_Drafted"), "Drafted members keep their orders and leads.");

            List<string> over = Calls(typeof(LeaveFromHere), "OrderDraftedOver");
            Assert.That(over, Is.SupersetOf(new[]
            {
                "Verse.Pawn.get_Drafted",
                "Verse.IntVec3.InHorDistOf",
                "RimWorld.RCellFinder.BestOrderedGotoDestNear",
                "MechanitorUtility.InMechanitorCommandRange",
                "Verse.AI.Pawn_JobTracker.TryTakeOrderedJob",
            }), "As vanilla's drafted move: a cell nobody else is sent to, and a mech only within range.");
            Assert.That(LeaveFromHerePolicy.OrderOverBeyond, Is.EqualTo(ExitDirector.DownedNearExit), "Within 7: the downed stage counts a carried pawn by its carrier's cell.");

            Assert.That(Calls(typeof(LeaveFromHere), "AnyWorker"), Does.Contain("CaravansCriticallyEnhanced.Utils.ExitDirector.IsWorker"),
                "A worker is one who works the duties: undrafted, unbroken, with hands.");
            Assert.That(Calls(typeof(LeaveFromHere), "NeedsRoping"), Is.SupersetOf(new[]
            {
                "Verse.AnimalPenUtility.NeedsToBeManagedByRope",
                "RimWorld.Pawn_RopeTracker.get_RopedByPawn",
                "Verse.Pawn.get_Drafted",
                "Verse.ReachabilityUtility.CanReach",
            }), "Roping is needed while a pen animal is off every drafted member's lead (undrafted leads drop with their jobs); fenced in when it cannot walk to the exit.");

            foreach (string caller in new[] { nameof(LeaveFromHere.Depart), "Confirmation" })
            {
                WalkResult walk = ILCalls.Read(Method(typeof(LeaveFromHere), caller));
                int counting = walk.Calls.ToList().FindIndex(c => c.Name == nameof(ExitDirector.CountStrays));
                Assert.That(walk.IntBeforeCall[counting], Is.EqualTo(1), caller + ": only those who work the duties fetch, for leaving now.");
            }

            WalkResult regather = ILCalls.Read(Method(typeof(ExitDirector), "RegatherDowned"));
            int counted = regather.Calls.ToList().FindIndex(c => c.Name == nameof(ExitDirector.CountStrays));
            Assert.That(regather.IntBeforeCall[counted], Is.EqualTo(0), "Waiting: a drafted member is waited for in the gathering stage.");
            Assert.That(Calls(typeof(LeaveFromHere), "UntieFromSpots"), Does.Contain("RimWorld.Pawn_RopeTracker.UnropeFromSpot"), "Straight to leaving: tied animals walk, as Force depart has them.");
            List<string> find = Calls(typeof(LeaveFromHere), nameof(LeaveFromHere.FindSpot));
            Assert.That(find.IndexOf("CaravansCriticallyEnhanced.Utils.LeaveFromHere.Going"), Is.InRange(0, find.IndexOf("CaravansCriticallyEnhanced.Utils.ExitReadings.Read")),
                "Planned over those who will go.");
            Assert.That(Calls(typeof(LeaveFromHere), "Going"), Does.Contain("Verse.Pawn.get_InMentalState"));

            WalkResult leave = ILCalls.Read(Method(typeof(ExitDirector), nameof(ExitDirector.LeaveFrom)));
            Assert.That(leave.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.PlayerOverride)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 1 }));
            Assert.That(leave.Fields.Where(f => f.IsStore && f.Field.Name == nameof(CaravanExitState.RegatherPending)).Select(f => f.IntBefore), Is.EqualTo(new int?[] { 0 }));
            Assert.That(leave.Calls.Select(ILCalls.Describe).Last(), Is.EqualTo("CaravansCriticallyEnhanced.Utils.ExitDirector.Release"));
        }

        [Test]
        public void OnlyAMechBeyondCommandIsUndraftedAndNothingForcesTheVanillaDepartureOrSetsAWalkOff()
        {
            Assert.That(ILCalls.CalledAnywhereInTheMod(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.ForceCaravanDepart)), Is.Empty);
            Assert.That(ILCalls.CalledAnywhereInTheMod(typeof(Pawn_DraftController), "set_Drafted").Select(m => m.Name), Is.EqualTo(new[] { "OrderDraftedOver" }),
                "Only a mech beyond its mechanitor's command, which nothing else would ever bring over.");
            WalkResult over = ILCalls.Read(Method(typeof(LeaveFromHere), "OrderDraftedOver"));
            int undraft = over.Calls.ToList().FindIndex(c => c.Name == "set_Drafted");
            Assert.That(over.IntBeforeCall[undraft], Is.EqualTo(0));
            Assert.That(over.Calls.ToList().FindIndex(c => c.Name == "InMechanitorCommandRange"), Is.LessThan(undraft));

            var stores = new List<(string, int?)>();
            foreach (MethodBase method in ILCalls.AllModMethods())
            {
                foreach (FieldUse use in ILCalls.Read(method).Fields)
                {
                    if (use.IsStore && use.Field.Name == nameof(Job.exitMapOnArrival))
                    {
                        stores.Add((method.DeclaringType.Name, use.IntBefore));
                    }
                }
            }

            Assert.That(stores, Is.EqualTo(new[] { (nameof(Patch_JobDriver_Goto_TryExitMap), (int?)0) }), "The block's own store is the control: the scan sees field stores.");
        }

        [Test]
        public void TheWalkOffIsStoppedOnlyForTheCaravansOwnPlayerOrders()
        {
            List<string> calls = Calls(typeof(Patch_JobDriver_Goto_TryExitMap), "Prefix");
            Assert.That(calls, Is.SupersetOf(new[]
            {
                "Verse.Pawn.get_Drafted",
                "CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf",
                "CaravansCriticallyEnhanced.Utils.CaravanMessages.PostThrottled",
            }));
            Assert.That(ILCalls.Read(Method(typeof(Patch_JobDriver_Goto_TryExitMap), "Prefix")).Fields.Any(f => !f.IsStore && f.Field.Name == nameof(Job.playerForced)), Is.True,
                "A forced order counts too; a mental break's exit does not.");
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_JobDriver_Goto_TryExitMap), "Prefix")), Is.EqualTo(new Toggle?[] { Toggle.LeaveFromHere }));

            var driver = (JobDriver_Goto)FormatterServices.GetUninitializedObject(typeof(JobDriver_Goto));
            Assert.That(Patch_JobDriver_Goto_TryExitMap.Prefix(driver), Is.True, "No job: vanilla's.");
        }

        [Test]
        public void CarryToExitIsHiddenOnlyForACaravanMember()
        {
            Assert.That(Calls(typeof(Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt), "Postfix"), Does.Contain("CaravansCriticallyEnhanced.Core.FormingLords.VanillaJobOf"));
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt), "Postfix")), Is.EqualTo(new Toggle?[] { Toggle.LeaveFromHere }));

            bool yes = true;
            Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt.Postfix(null, ref yes);
            Assert.That(yes, Is.True, "No pawn in a caravan: vanilla's answer stands.");
            bool no = false;
            Patch_FloatMenuOptionProvider_CarryPawnToExit_AppliesInt.Postfix(null, ref no);
            Assert.That(no, Is.False);
        }

        [Test]
        public void ForceDepartListsTheDownedItLeavesThroughThePolicy()
        {
            List<string> calls = Calls(typeof(Patch_CaravanFormingUtility_GetForceDepartWarningMessage), "Postfix");
            Assert.That(calls, Is.SupersetOf(new[]
            {
                "CaravansCriticallyEnhanced.Core.FormingLords.IsVanillaJob",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.CarrierOf",
                "CaravansCriticallyEnhanced.Utils.ExitDirector.IsWorker",
                "CaravansCriticallyEnhanced.Core.LeaveFromHerePolicy.LeftByForceDepart",
                "Verse.DevelopmentalStageExtensions.Baby",
                "Verse.GenText.ToLineList",
            }));
            Assert.That(ToggleReads.AskedBy(Method(typeof(Patch_CaravanFormingUtility_GetForceDepartWarningMessage), "Postfix")), Is.EqualTo(new Toggle?[] { Toggle.DepartureWarnings }));

            var job = new LordJob_FormAndSendCaravan(null, new List<Pawn>(), IntVec3.Zero, IntVec3.Zero, PlanetTile.Invalid, PlanetTile.Invalid);
            string text = "vanilla";
            Patch_CaravanFormingUtility_GetForceDepartWarningMessage.Postfix(job, ref text);
            Assert.That(text, Is.EqualTo("vanilla"), "Nobody downed: vanilla's text alone.");
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, type.Name + " has no " + name + ".");
            return method;
        }

        private static List<string> Calls(Type type, string name)
        {
            IEnumerable<MethodBase> methods = name == ".ctor"
                ? type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                : new MethodBase[] { Method(type, name) };
            return methods.SelectMany(ILCalls.CalledBy).Select(Generic).ToList();
        }

        private static string Generic(MethodBase method)
        {
            Type type = method.DeclaringType;
            if (type != null && type.IsGenericType)
            {
                type = type.GetGenericTypeDefinition();
            }

            return type == null ? method.Name : type.FullName + "." + method.Name;
        }
    }
}
