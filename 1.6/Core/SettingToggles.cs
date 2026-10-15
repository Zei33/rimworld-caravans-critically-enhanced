using System.Collections.Generic;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The settings window's sections, in the order they are drawn.
    /// </summary>
    public enum SettingGroup
    {
        /// <summary>The Load work type and who carries what.</summary>
        Loading,

        /// <summary>Leads held while drafted.</summary>
        Leads,

        /// <summary>Choosing where the caravan leaves the map.</summary>
        Exits,

        /// <summary>Cargo already carried by animals when the window opens.</summary>
        Cargo,

        /// <summary>Caravans that stop making progress.</summary>
        StuckCaravans,

        /// <summary>Caravans coming home, and loading by hand.</summary>
        ReturnAndManualLoading,
    }

    /// <summary>
    /// One switch per feature, so a player can turn off whichever part collides with another mod.
    /// </summary>
    public enum Toggle
    {
        /// <summary>The Load column: colonists who are not going help load the caravan.</summary>
        LoadingWorkType,

        /// <summary>Helpers may load onto colonist members when no animal or prisoner has room.</summary>
        ColonistCarriers,

        /// <summary>Helpers start loading while animals are still being gathered.</summary>
        EarlyLoading,

        /// <summary>Helpers lead pen animals to the meeting point.</summary>
        HelpersRopeAnimals,

        /// <summary>A drafted colonist keeps the animals it is leading.</summary>
        LeadsSurviveDrafting,

        /// <summary>The exit lies inside every restricted pawn's allowed area, or the caravan waits.</summary>
        AreaAwareExit,

        /// <summary>The exit avoids hostile pawns, and moves when a threat appears near it.</summary>
        ThreatAwareExit,

        /// <summary>The Leave from here button.</summary>
        LeaveFromHere,

        /// <summary>Force depart warns about downed pawns and babies left behind.</summary>
        DepartureWarnings,

        /// <summary>The formation window lists cargo already on animals. Master switch for the cargo group.</summary>
        ListAnimalCargo,

        /// <summary>An icon beside animals that already carry cargo.</summary>
        CargoIcon,

        /// <summary>Selecting or deselecting an animal adds or removes its cargo.</summary>
        AutoAdjustCargo,

        /// <summary>Animals keep cargo that is going, and unload only the rest.</summary>
        KeepLoadedCargo,

        /// <summary>Colonists move cargo from animals that are not going onto animals that are.</summary>
        TransferCargo,

        /// <summary>Cargo stays listed when its carrier is removed or downed, and outsiders do not eat it.</summary>
        ProtectCargo,

        /// <summary>Cargo that is not going is dropped beside the animal at once. Off by default.</summary>
        InstantSurplusUnload,

        /// <summary>Loading does not finish while listed items are still on the ground.</summary>
        CompletionGuard,

        /// <summary>Lost or merged items are replaced or dropped from the list.</summary>
        ManifestReconcile,

        /// <summary>What a forming caravan is waiting for, shown in the inspect pane, a tab and an alert.</summary>
        StallReadout,

        /// <summary>Returning pack animals are unloaded at a stockpile before they are penned.</summary>
        UnloadReturningAnimals,

        /// <summary>Loading by hand fills animals first, then the colonist, then other members.</summary>
        LoadOntoCaravanFallback,

        /// <summary>Items loaded by hand count against the caravan's list.</summary>
        ManualLoadsCount,
    }

    /// <summary>
    /// Everything the settings class needs to know about one <see cref="Toggle"/>.
    /// </summary>
    public sealed class ToggleSpec
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToggleSpec"/> class.
        /// </summary>
        /// <param name="toggle">The switch.</param>
        /// <param name="group">The section it is drawn in.</param>
        /// <param name="scribeKey">The node name in the settings file. Never rename one: a renamed key reverts every player's choice to the default.</param>
        /// <param name="defaultOn">The value a fresh install and a settings file without the key both get.</param>
        /// <param name="labelKey">The Keyed translation key of the checkbox label.</param>
        /// <param name="tooltipKey">The Keyed translation key of the checkbox tooltip.</param>
        /// <param name="parent">The switch this one depends on, if any.</param>
        public ToggleSpec(Toggle toggle, SettingGroup group, string scribeKey, bool defaultOn, string labelKey, string tooltipKey, Toggle? parent = null)
        {
            Toggle = toggle;
            Group = group;
            ScribeKey = scribeKey;
            DefaultOn = defaultOn;
            LabelKey = labelKey;
            TooltipKey = tooltipKey;
            Parent = parent;
        }

        /// <summary>Gets the switch.</summary>
        public Toggle Toggle { get; }

        /// <summary>Gets the section the switch is drawn in.</summary>
        public SettingGroup Group { get; }

        /// <summary>Gets the node name in the settings file.</summary>
        public string ScribeKey { get; }

        /// <summary>Gets the default.</summary>
        public bool DefaultOn { get; }

        /// <summary>Gets the translation key of the label.</summary>
        public string LabelKey { get; }

        /// <summary>Gets the translation key of the tooltip.</summary>
        public string TooltipKey { get; }

        /// <summary>
        /// Gets the switch this one depends on. While the parent is off this one reads as off, and the
        /// window draws it greyed out under its parent.
        /// </summary>
        public Toggle? Parent { get; }
    }

    /// <summary>
    /// The table of every switch, in drawing order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One table rather than a field per switch, so that the default a fresh install gets and the
    /// default a settings file missing the key gets are the same value by construction. Those are read
    /// by different players: <c>LoadedModManager.ReadModSettings</c> hands a player with no settings
    /// file a freshly constructed object without calling <c>ExposeData</c>, while a player whose file
    /// predates a key gets the default passed to <c>Scribe_Values.Look</c>. Written twice, the two
    /// drift apart and nothing reports it.
    /// </para>
    /// <para>
    /// <c>Scribe_Values.Look</c> never writes a value equal to its default, so "a file without the
    /// key" is also every player who never touched that switch. Changing a default here therefore
    /// changes it for all of them, not only for new installs; treat it as a release note.
    /// </para>
    /// <para>
    /// The translation keys are written out in full rather than built from the scribe key, because
    /// <c>Tests/LanguageParityTests.cs</c> finds every key the mod uses by reading string literals out
    /// of the compiled IL, and a key built by concatenation is not a literal.
    /// </para>
    /// </remarks>
    public static class SettingToggles
    {
        /// <summary>
        /// Every switch, each once, in the order the window draws them.
        /// </summary>
        public static readonly IReadOnlyList<ToggleSpec> All = new[]
        {
            new ToggleSpec(Toggle.LoadingWorkType, SettingGroup.Loading, "loadingWorkType", true,
                "CaravansCriticallyEnhanced_SettingLoadingWorkType", "CaravansCriticallyEnhanced_SettingLoadingWorkTypeTip"),
            new ToggleSpec(Toggle.ColonistCarriers, SettingGroup.Loading, "colonistCarriers", true,
                "CaravansCriticallyEnhanced_SettingColonistCarriers", "CaravansCriticallyEnhanced_SettingColonistCarriersTip",
                Toggle.LoadingWorkType),
            new ToggleSpec(Toggle.EarlyLoading, SettingGroup.Loading, "earlyLoading", true,
                "CaravansCriticallyEnhanced_SettingEarlyLoading", "CaravansCriticallyEnhanced_SettingEarlyLoadingTip",
                Toggle.LoadingWorkType),
            new ToggleSpec(Toggle.HelpersRopeAnimals, SettingGroup.Loading, "helpersRopeAnimals", true,
                "CaravansCriticallyEnhanced_SettingHelpersRopeAnimals", "CaravansCriticallyEnhanced_SettingHelpersRopeAnimalsTip",
                Toggle.LoadingWorkType),

            new ToggleSpec(Toggle.LeadsSurviveDrafting, SettingGroup.Leads, "leadsSurviveDrafting", true,
                "CaravansCriticallyEnhanced_SettingLeadsSurviveDrafting", "CaravansCriticallyEnhanced_SettingLeadsSurviveDraftingTip"),

            new ToggleSpec(Toggle.AreaAwareExit, SettingGroup.Exits, "areaAwareExit", true,
                "CaravansCriticallyEnhanced_SettingAreaAwareExit", "CaravansCriticallyEnhanced_SettingAreaAwareExitTip"),
            new ToggleSpec(Toggle.ThreatAwareExit, SettingGroup.Exits, "threatAwareExit", true,
                "CaravansCriticallyEnhanced_SettingThreatAwareExit", "CaravansCriticallyEnhanced_SettingThreatAwareExitTip"),
            new ToggleSpec(Toggle.LeaveFromHere, SettingGroup.Exits, "leaveFromHere", true,
                "CaravansCriticallyEnhanced_SettingLeaveFromHere", "CaravansCriticallyEnhanced_SettingLeaveFromHereTip"),
            new ToggleSpec(Toggle.DepartureWarnings, SettingGroup.Exits, "departureWarnings", true,
                "CaravansCriticallyEnhanced_SettingDepartureWarnings", "CaravansCriticallyEnhanced_SettingDepartureWarningsTip"),

            new ToggleSpec(Toggle.ListAnimalCargo, SettingGroup.Cargo, "listAnimalCargo", true,
                "CaravansCriticallyEnhanced_SettingListAnimalCargo", "CaravansCriticallyEnhanced_SettingListAnimalCargoTip"),
            new ToggleSpec(Toggle.CargoIcon, SettingGroup.Cargo, "cargoIcon", true,
                "CaravansCriticallyEnhanced_SettingCargoIcon", "CaravansCriticallyEnhanced_SettingCargoIconTip",
                Toggle.ListAnimalCargo),
            new ToggleSpec(Toggle.AutoAdjustCargo, SettingGroup.Cargo, "autoAdjustCargo", true,
                "CaravansCriticallyEnhanced_SettingAutoAdjustCargo", "CaravansCriticallyEnhanced_SettingAutoAdjustCargoTip",
                Toggle.ListAnimalCargo),
            new ToggleSpec(Toggle.KeepLoadedCargo, SettingGroup.Cargo, "keepLoadedCargo", true,
                "CaravansCriticallyEnhanced_SettingKeepLoadedCargo", "CaravansCriticallyEnhanced_SettingKeepLoadedCargoTip",
                Toggle.ListAnimalCargo),
            new ToggleSpec(Toggle.TransferCargo, SettingGroup.Cargo, "transferCargo", true,
                "CaravansCriticallyEnhanced_SettingTransferCargo", "CaravansCriticallyEnhanced_SettingTransferCargoTip",
                Toggle.ListAnimalCargo),
            new ToggleSpec(Toggle.ProtectCargo, SettingGroup.Cargo, "protectCargo", true,
                "CaravansCriticallyEnhanced_SettingProtectCargo", "CaravansCriticallyEnhanced_SettingProtectCargoTip",
                Toggle.ListAnimalCargo),
            new ToggleSpec(Toggle.InstantSurplusUnload, SettingGroup.Cargo, "instantSurplusUnload", false,
                "CaravansCriticallyEnhanced_SettingInstantSurplusUnload", "CaravansCriticallyEnhanced_SettingInstantSurplusUnloadTip",
                Toggle.ListAnimalCargo),

            new ToggleSpec(Toggle.CompletionGuard, SettingGroup.StuckCaravans, "completionGuard", true,
                "CaravansCriticallyEnhanced_SettingCompletionGuard", "CaravansCriticallyEnhanced_SettingCompletionGuardTip"),
            new ToggleSpec(Toggle.ManifestReconcile, SettingGroup.StuckCaravans, "manifestReconcile", true,
                "CaravansCriticallyEnhanced_SettingManifestReconcile", "CaravansCriticallyEnhanced_SettingManifestReconcileTip"),
            new ToggleSpec(Toggle.StallReadout, SettingGroup.StuckCaravans, "stallReadout", true,
                "CaravansCriticallyEnhanced_SettingStallReadout", "CaravansCriticallyEnhanced_SettingStallReadoutTip"),

            new ToggleSpec(Toggle.UnloadReturningAnimals, SettingGroup.ReturnAndManualLoading, "unloadReturningAnimals", true,
                "CaravansCriticallyEnhanced_SettingUnloadReturningAnimals", "CaravansCriticallyEnhanced_SettingUnloadReturningAnimalsTip"),
            new ToggleSpec(Toggle.LoadOntoCaravanFallback, SettingGroup.ReturnAndManualLoading, "loadOntoCaravanFallback", true,
                "CaravansCriticallyEnhanced_SettingLoadOntoCaravanFallback", "CaravansCriticallyEnhanced_SettingLoadOntoCaravanFallbackTip"),
            new ToggleSpec(Toggle.ManualLoadsCount, SettingGroup.ReturnAndManualLoading, "manualLoadsCount", true,
                "CaravansCriticallyEnhanced_SettingManualLoadsCount", "CaravansCriticallyEnhanced_SettingManualLoadsCountTip",
                Toggle.LoadOntoCaravanFallback),
        };

        /// <summary>
        /// Gets the translation key of a section heading.
        /// </summary>
        /// <param name="group">The section.</param>
        /// <returns>The key.</returns>
        public static string HeadingKey(SettingGroup group)
        {
            switch (group)
            {
                case SettingGroup.Loading:
                    return "CaravansCriticallyEnhanced_GroupLoading";
                case SettingGroup.Leads:
                    return "CaravansCriticallyEnhanced_GroupLeads";
                case SettingGroup.Exits:
                    return "CaravansCriticallyEnhanced_GroupExits";
                case SettingGroup.Cargo:
                    return "CaravansCriticallyEnhanced_GroupCargo";
                case SettingGroup.StuckCaravans:
                    return "CaravansCriticallyEnhanced_GroupStuckCaravans";
                case SettingGroup.ReturnAndManualLoading:
                    return "CaravansCriticallyEnhanced_GroupReturnAndManualLoading";
                default:
                    // A section added without a heading fails the parity tests, which ask for every one.
                    throw new System.ArgumentOutOfRangeException(nameof(group), group, "No heading for this settings section.");
            }
        }

        /// <summary>
        /// The number of switches, which is also the length of any array indexed by <see cref="Toggle"/>.
        /// </summary>
        public static readonly int Count = System.Enum.GetValues(typeof(Toggle)).Length;

        /// <summary>
        /// The entries indexed by <c>(int)toggle</c>, so a lookup on a hot path is one array read.
        /// Declared after <see cref="All"/> because static initialisers run in textual order.
        /// </summary>
        private static readonly ToggleSpec[] ByToggle = Index();

        /// <summary>
        /// Finds the entry for one switch.
        /// </summary>
        /// <param name="toggle">The switch.</param>
        /// <returns>Its entry.</returns>
        /// <exception cref="KeyNotFoundException">The table has no entry for it, which a test forbids.</exception>
        public static ToggleSpec SpecOf(Toggle toggle)
        {
            int index = (int)toggle;
            if (index < 0 || index >= ByToggle.Length || ByToggle[index] == null)
            {
                throw new KeyNotFoundException("No settings entry for " + toggle + ".");
            }

            return ByToggle[index];
        }

        private static ToggleSpec[] Index()
        {
            var index = new ToggleSpec[Count];
            foreach (ToggleSpec spec in All)
            {
                index[(int)spec.Toggle] = spec;
            }

            return index;
        }
    }
}
