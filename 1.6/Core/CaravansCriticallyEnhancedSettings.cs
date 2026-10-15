using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace CaravansCriticallyEnhanced.Core
{
    /// <summary>
    /// The player's settings: one switch per feature, plus four numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing in this mod's decision code reads these on its own behalf. The pure decisions take
    /// their inputs as parameters, so they stay reachable from the test harness, which cannot
    /// construct a <see cref="Verse.Mod"/>. The static <c>CaravansCriticallyEnhancedMod.Settings</c>
    /// is read at the call boundary instead, by the patch or work giver that calls the decision.
    /// </para>
    /// <para>
    /// The switches live in <see cref="SettingToggles.All"/>; see there for why it is one table.
    /// </para>
    /// </remarks>
    public class CaravansCriticallyEnhancedSettings : ModSettings
    {
        /// <summary>The default distance, in cells, at which a hostile pawn threatens an exit.</summary>
        public const int DefaultThreatRadius = 25;

        /// <summary>The smallest threat distance the window allows.</summary>
        public const int MinimumThreatRadius = 5;

        /// <summary>The largest threat distance the window allows.</summary>
        public const int MaximumThreatRadius = 60;

        /// <summary>The default distance, in cells, at which a hostile pawn threatens the route to an exit.</summary>
        public const int DefaultRouteThreatRadius = 12;

        /// <summary>The smallest route threat distance the window allows.</summary>
        public const int MinimumRouteThreatRadius = 3;

        /// <summary>The largest route threat distance the window allows.</summary>
        public const int MaximumRouteThreatRadius = 30;

        /// <summary>The default time an unreachable item holds up loading, in ticks: one in-game hour.</summary>
        public const int DefaultCompletionGraceTicks = GenDate.TicksPerHour;

        /// <summary>
        /// The shortest grace period the window allows: one hour. Whole hours only, because
        /// <c>ToStringTicksToPeriod</c> rounds anything from an hour up to whole hours, so a finer
        /// step would show two different settings under one label.
        /// </summary>
        public const int MinimumCompletionGraceTicks = GenDate.TicksPerHour;

        /// <summary>The longest grace period the window allows: twelve hours.</summary>
        public const int MaximumCompletionGraceTicks = GenDate.TicksPerHour * 12;

        /// <summary>The step both duration sliders move in: one hour.</summary>
        public const int DurationStepTicks = GenDate.TicksPerHour;

        /// <summary>The default time without progress before the stalled caravan alert: three in-game hours.</summary>
        public const int DefaultStallAlertTicks = GenDate.TicksPerHour * 3;

        /// <summary>The shortest stall time the window allows: one hour.</summary>
        public const int MinimumStallAlertTicks = GenDate.TicksPerHour;

        /// <summary>The longest stall time the window allows: one day.</summary>
        public const int MaximumStallAlertTicks = GenDate.TicksPerDay;

        private readonly bool[] values = Defaults();

        private int threatRadius = DefaultThreatRadius;
        private int routeThreatRadius = DefaultRouteThreatRadius;
        private int completionGraceTicks = DefaultCompletionGraceTicks;
        private int stallAlertTicks = DefaultStallAlertTicks;

        private Vector2 scrollPosition;
        private float lastContentHeight = 1200f;

        /// <summary>Gets the distance, in cells, at which a hostile pawn threatens an exit.</summary>
        public int ThreatRadius => threatRadius;

        /// <summary>Gets the distance, in cells, at which a hostile pawn threatens the route to an exit.</summary>
        public int RouteThreatRadius => routeThreatRadius;

        /// <summary>Gets how long an unreachable item may hold up loading, in ticks.</summary>
        public int CompletionGraceTicks => completionGraceTicks;

        /// <summary>
        /// Gets how long loading may make no progress at all before the completion guard lets go, in
        /// ticks. Twice the grace period, so the guard can never wait forever.
        /// </summary>
        public int CompletionWatchdogTicks => completionGraceTicks * 2;

        /// <summary>Gets how long a forming caravan may make no progress before the alert, in ticks.</summary>
        public int StallAlertTicks => stallAlertTicks;

        /// <summary>
        /// Determines whether a switch is in effect: on itself, and its parent on too.
        /// </summary>
        /// <param name="toggle">The switch.</param>
        /// <returns><c>true</c> when the feature should run.</returns>
        public bool IsOn(Toggle toggle)
        {
            if (!values[(int)toggle])
            {
                return false;
            }

            Toggle? parent = SettingToggles.SpecOf(toggle).Parent;
            return parent == null || IsOn(parent.Value);
        }

        /// <summary>
        /// Reads the stored value of a switch, ignoring its parent.
        /// </summary>
        /// <param name="toggle">The switch.</param>
        /// <returns>What the checkbox shows.</returns>
        public bool StoredValue(Toggle toggle)
        {
            return values[(int)toggle];
        }

        /// <summary>
        /// Sets one switch.
        /// </summary>
        /// <param name="toggle">The switch.</param>
        /// <param name="on">The new value.</param>
        public void Set(Toggle toggle, bool on)
        {
            values[(int)toggle] = on;
        }

        /// <summary>
        /// Puts every switch and number back to its default.
        /// </summary>
        public void ResetToDefaults()
        {
            Array.Copy(Defaults(), values, values.Length);
            threatRadius = DefaultThreatRadius;
            routeThreatRadius = DefaultRouteThreatRadius;
            completionGraceTicks = DefaultCompletionGraceTicks;
            stallAlertTicks = DefaultStallAlertTicks;
        }

        /// <summary>
        /// Reads and writes the settings.
        /// </summary>
        /// <remarks>
        /// The numbers are clamped after loading because a hand-edited file can hold anything, and the
        /// window cannot produce a value out of range.
        /// </remarks>
        public override void ExposeData()
        {
            base.ExposeData();

            foreach (ToggleSpec spec in SettingToggles.All)
            {
                bool value = values[(int)spec.Toggle];
                Scribe_Values.Look(ref value, spec.ScribeKey, spec.DefaultOn);
                values[(int)spec.Toggle] = value;
            }

            Scribe_Values.Look(ref threatRadius, "threatRadius", DefaultThreatRadius);
            Scribe_Values.Look(ref routeThreatRadius, "routeThreatRadius", DefaultRouteThreatRadius);
            Scribe_Values.Look(ref completionGraceTicks, "completionGraceTicks", DefaultCompletionGraceTicks);
            Scribe_Values.Look(ref stallAlertTicks, "stallAlertTicks", DefaultStallAlertTicks);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                threatRadius = Clamp(threatRadius, MinimumThreatRadius, MaximumThreatRadius, DefaultThreatRadius);
                routeThreatRadius = Clamp(routeThreatRadius, MinimumRouteThreatRadius, MaximumRouteThreatRadius, DefaultRouteThreatRadius);
                completionGraceTicks = Clamp(completionGraceTicks, MinimumCompletionGraceTicks, MaximumCompletionGraceTicks, DefaultCompletionGraceTicks);
                stallAlertTicks = Clamp(stallAlertTicks, MinimumStallAlertTicks, MaximumStallAlertTicks, DefaultStallAlertTicks);
            }
        }

        /// <summary>
        /// Brings a loaded number inside its range.
        /// </summary>
        /// <param name="value">The loaded value.</param>
        /// <param name="minimum">The smallest allowed value.</param>
        /// <param name="maximum">The largest allowed value.</param>
        /// <param name="fallback">The default.</param>
        /// <returns>
        /// The value clamped, except that zero or below becomes the default rather than the minimum.
        /// </returns>
        /// <remarks>
        /// <c>Scribe_Values.Look</c> hands back its default only when the node is absent. A node that
        /// is present but does not parse comes back as <c>default(int)</c>, zero, so zero is the shape a
        /// corrupt file arrives in rather than a value anybody chose, and the minimum would be a
        /// surprising thing to replace it with.
        /// </remarks>
        public static int Clamp(int value, int minimum, int maximum, int fallback)
        {
            if (value <= 0)
            {
                return fallback;
            }

            return Math.Min(Math.Max(value, minimum), maximum);
        }

        /// <summary>
        /// Draws the settings window.
        /// </summary>
        /// <param name="inRect">The area available for drawing.</param>
        /// <remarks>
        /// <para>
        /// Inside a scroll view, because twenty-two switches and four sliders do not fit the window at
        /// the smallest UI scale. The content height is measured on each draw and used for the next.
        /// The listing is held to one column: <c>Listing</c> otherwise starts a new column, off to the
        /// right and clipped, as soon as the content passes the height it was given, and the measured
        /// height would then shrink each frame until most of the window was gone.
        /// </para>
        /// <para>
        /// Sliders rather than text fields for the numbers, because a slider cannot hold an
        /// unparseable buffer or be dragged out of range, and needs no commit point when the window
        /// closes. Simple Improve's text field needed both fixes.
        /// </para>
        /// </remarks>
        public void DoSettingsWindowContents(Rect inRect)
        {
            var viewRect = new Rect(0f, 0f, inRect.width - 16f, lastContentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            Text.Font = GameFont.Small;

            SettingGroup? drawnGroup = null;
            foreach (ToggleSpec spec in SettingToggles.All)
            {
                if (drawnGroup != spec.Group)
                {
                    if (drawnGroup != null)
                    {
                        DrawNumbersFor(listing, drawnGroup.Value);
                        listing.Gap(12f);
                    }

                    drawnGroup = spec.Group;
                    Text.Font = GameFont.Medium;
                    listing.Label(SettingToggles.HeadingKey(spec.Group).Translate());
                    Text.Font = GameFont.Small;
                    listing.GapLine(6f);
                }

                DrawToggle(listing, spec);
            }

            if (drawnGroup != null)
            {
                DrawNumbersFor(listing, drawnGroup.Value);
            }

            listing.Gap(12f);
            if (listing.ButtonText("CaravansCriticallyEnhanced_ResetToDefaults".Translate()))
            {
                ResetToDefaults();
            }

            lastContentHeight = listing.CurHeight + 12f;
            listing.End();
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws one switch, indented under its parent and greyed out while the parent is off.
        /// </summary>
        /// <remarks>
        /// <c>Widgets.CheckboxLabeled</c> with <c>disabled</c> stops the click and dims only the box,
        /// drawing the label at the current colour, so the label is dimmed here to match, and a row
        /// that cannot be changed is not highlighted under the mouse. The highlight goes first, as
        /// <c>Listing_Standard.CheckboxLabeled</c> draws it, so it sits under the text.
        /// </remarks>
        private void DrawToggle(Listing_Standard listing, ToggleSpec spec)
        {
            bool parentOff = spec.Parent != null && !IsOn(spec.Parent.Value);
            float indent = spec.Parent != null ? 24f : 0f;

            Rect row = listing.GetRect(Text.LineHeight);
            row.xMin += indent;

            if (!parentOff && Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            Color previous = GUI.color;
            if (parentOff)
            {
                GUI.color = Widgets.InactiveColor;
            }

            bool value = values[(int)spec.Toggle];
            Widgets.CheckboxLabeled(row, spec.LabelKey.Translate(), ref value, disabled: parentOff);
            values[(int)spec.Toggle] = value;
            GUI.color = previous;

            TooltipHandler.TipRegion(row, spec.TooltipKey.Translate());
            listing.Gap(listing.verticalSpacing);
        }

        private void DrawNumbersFor(Listing_Standard listing, SettingGroup group)
        {
            switch (group)
            {
                case SettingGroup.Exits:
                    listing.Gap(6f);
                    listing.Label(
                        "CaravansCriticallyEnhanced_SettingThreatRadius".Translate(threatRadius),
                        tooltip: "CaravansCriticallyEnhanced_SettingThreatRadiusTip".Translate());
                    threatRadius = Slide(listing, threatRadius, MinimumThreatRadius, MaximumThreatRadius, 1);
                    listing.Label(
                        "CaravansCriticallyEnhanced_SettingRouteThreatRadius".Translate(routeThreatRadius),
                        tooltip: "CaravansCriticallyEnhanced_SettingRouteThreatRadiusTip".Translate());
                    routeThreatRadius = Slide(listing, routeThreatRadius, MinimumRouteThreatRadius, MaximumRouteThreatRadius, 1);
                    break;

                case SettingGroup.StuckCaravans:
                    listing.Gap(6f);
                    listing.Label(
                        "CaravansCriticallyEnhanced_SettingCompletionGrace".Translate(completionGraceTicks.ToStringTicksToPeriod()),
                        tooltip: "CaravansCriticallyEnhanced_SettingCompletionGraceTip".Translate());
                    completionGraceTicks = Slide(listing, completionGraceTicks, MinimumCompletionGraceTicks, MaximumCompletionGraceTicks, DurationStepTicks);
                    listing.Label(
                        "CaravansCriticallyEnhanced_SettingStallAlert".Translate(stallAlertTicks.ToStringTicksToPeriod()),
                        tooltip: "CaravansCriticallyEnhanced_SettingStallAlertTip".Translate());
                    stallAlertTicks = Slide(listing, stallAlertTicks, MinimumStallAlertTicks, MaximumStallAlertTicks, DurationStepTicks);
                    break;
            }
        }

        /// <summary>
        /// Draws a slider over a whole number and returns the new value, or the old one untouched.
        /// </summary>
        /// <param name="listing">The listing.</param>
        /// <param name="current">The current value.</param>
        /// <param name="minimum">The slider's left end.</param>
        /// <param name="maximum">The slider's right end.</param>
        /// <param name="step">The step the value moves in.</param>
        /// <returns>The value.</returns>
        /// <remarks>
        /// Only a value the slider actually moved is snapped. Snapping every frame would rewrite a
        /// hand-edited value that sits between steps just because the window was opened.
        /// </remarks>
        private static int Slide(Listing_Standard listing, int current, int minimum, int maximum, int step)
        {
            float moved = listing.Slider(current, minimum, maximum);
            return moved == current ? current : SnapToStep(moved, step);
        }

        /// <summary>
        /// Rounds a slider value to the nearest whole number of steps.
        /// </summary>
        /// <param name="value">The slider's value.</param>
        /// <param name="step">The step.</param>
        /// <returns>The nearest multiple of <paramref name="step"/>.</returns>
        public static int SnapToStep(float value, int step)
        {
            return Mathf.RoundToInt(value / step) * step;
        }

        private static bool[] Defaults()
        {
            var defaults = new bool[SettingToggles.Count];
            foreach (ToggleSpec spec in SettingToggles.All)
            {
                defaults[(int)spec.Toggle] = spec.DefaultOn;
            }

            return defaults;
        }
    }
}
