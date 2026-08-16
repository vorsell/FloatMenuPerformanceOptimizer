using System;
using System.Globalization;
using UnityEngine;
using Verse;

namespace FloatMenuRevalidationControl
{
    internal enum RevalidationMode
    {
        Disabled,
        Periodic,
        Adaptive,
        Lazy
    }

    internal sealed class FloatMenuRevalidationSettings : ModSettings
    {
        internal RevalidationMode Mode = RevalidationMode.Disabled;
        internal int PeriodicIntervalFrames =
            RevalidationRuntime.DefaultTimedIntervalFrames;
        internal int AdaptiveIntervalHundredths =
            RevalidationRuntime.DefaultAdaptiveIntervalHundredths;
        internal bool ShowClickValidationFailureMessage = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Mode, "mode", RevalidationMode.Disabled);
            Scribe_Values.Look(
                ref PeriodicIntervalFrames,
                "periodicIntervalFrames",
                RevalidationRuntime.DefaultTimedIntervalFrames);
            Scribe_Values.Look(
                ref AdaptiveIntervalHundredths,
                "adaptiveIntervalHundredths",
                RevalidationRuntime.DefaultAdaptiveIntervalHundredths);
            Scribe_Values.Look(
                ref ShowClickValidationFailureMessage,
                "showLazyValidationFailureMessage",
                true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PeriodicIntervalFrames = Math.Max(
                    RevalidationRuntime.MinimumIntervalFrames,
                    PeriodicIntervalFrames);
                AdaptiveIntervalHundredths = Math.Max(
                    0,
                    AdaptiveIntervalHundredths);
            }
        }
    }

    public sealed class FloatMenuRevalidationControlMod : Mod
    {
        private const float ControlHeight = 30f;
        private const float ControlGap = 6f;
        private const float SectionDividerHeight = 18f;
        private const float DescriptionIndent = 18f;
        private const float TimedTextWidth = 92f;
        private const float AdaptiveTextWidth = 104f;

        internal static FloatMenuRevalidationSettings CurrentSettings;

        private string periodicIntervalBuffer;
        private string adaptiveIntervalBuffer;

        public FloatMenuRevalidationControlMod(ModContentPack content)
            : base(content)
        {
            CurrentSettings = GetSettings<FloatMenuRevalidationSettings>();
        }

        public override string SettingsCategory()
        {
            return T("FMRC_SettingsCategory");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            FloatMenuRevalidationSettings settings = CurrentSettings;
            EnsureBuffers(settings);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label(T("FMRC_ModeLabel"));
            listing.GapLine(6f);

            if (listing.RadioButton(
                T("FMRC_Disabled"),
                settings.Mode == RevalidationMode.Disabled,
                tooltip: T("FMRC_DisabledTooltip")))
            {
                settings.Mode = RevalidationMode.Disabled;
            }

            DrawSectionDivider(listing);

            if (listing.RadioButton(
                T("FMRC_Timed"),
                settings.Mode == RevalidationMode.Periodic,
                tooltip: T("FMRC_TimedTooltip")))
            {
                settings.Mode = RevalidationMode.Periodic;
            }

            if (settings.Mode == RevalidationMode.Periodic)
            {
                DrawTimedSettings(listing, settings);
            }

            DrawSectionDivider(listing);

            if (listing.RadioButton(
                T("FMRC_Adaptive"),
                settings.Mode == RevalidationMode.Adaptive,
                tooltip: T("FMRC_AdaptiveTooltip")))
            {
                settings.Mode = RevalidationMode.Adaptive;
            }

            if (settings.Mode == RevalidationMode.Adaptive)
            {
                DrawAdaptiveSettings(listing, settings);
            }

            DrawSectionDivider(listing);

            if (listing.RadioButton(
                T("FMRC_Lazy"),
                settings.Mode == RevalidationMode.Lazy,
                tooltip: T("FMRC_LazyTooltip")))
            {
                settings.Mode = RevalidationMode.Lazy;
            }

            listing.Gap(12f);
            listing.GapLine(6f);
            listing.CheckboxLabeled(
                T("FMRC_LazyMessage"),
                ref settings.ShowClickValidationFailureMessage,
                T("FMRC_LazyMessageTooltip"));
            DrawIndentedLabel(
                listing,
                T("FMRC_ClickValidationExplanation"));

            listing.Gap(12f);
            listing.GapLine(6f);
            listing.Label(T("FMRC_ChangesApply"));

            listing.Gap(4f);
            Rect resetRow = listing.GetRect(Window.CloseButSize.y);
            string resetLabel = T("FMRC_ResetSettings");
            float resetWidth = Mathf.Min(
                resetRow.width,
                Mathf.Max(
                    Window.CloseButSize.x,
                    Text.CalcSize(resetLabel).x + 24f));
            Rect resetButton = new Rect(
                resetRow.x,
                resetRow.y,
                resetWidth,
                Window.CloseButSize.y);
            if (Widgets.ButtonText(resetButton, resetLabel))
            {
                ResetSettingsToDefaults();
            }

            listing.End();
        }

        internal void ResetSettingsToDefaults()
        {
            FloatMenuRevalidationSettings settings = CurrentSettings;
            settings.Mode = RevalidationMode.Disabled;
            settings.PeriodicIntervalFrames =
                RevalidationRuntime.DefaultTimedIntervalFrames;
            settings.AdaptiveIntervalHundredths =
                RevalidationRuntime.DefaultAdaptiveIntervalHundredths;
            settings.ShowClickValidationFailureMessage = true;
            periodicIntervalBuffer =
                settings.PeriodicIntervalFrames.ToString(
                    CultureInfo.InvariantCulture);
            adaptiveIntervalBuffer =
                FormatHundredths(settings.AdaptiveIntervalHundredths);
        }

        private void DrawTimedSettings(
            Listing_Standard listing,
            FloatMenuRevalidationSettings settings)
        {
            listing.Gap(4f);
            DrawIndentedLabel(listing, T("FMRC_TimedIntervalLabel"));

            Rect row = listing.GetRect(ControlHeight);
            float buttonWidth = Mathf.Clamp(
                (row.width - TimedTextWidth - (ControlGap * 6f)) / 6f,
                48f,
                68f);
            float totalWidth = (buttonWidth * 6f)
                + TimedTextWidth
                + (ControlGap * 6f);
            float x = row.x + Mathf.Max(0f, (row.width - totalWidth) / 2f);

            if (DrawStepButton(row, ref x, buttonWidth, "-100"))
            {
                ApplyPeriodicDelta(settings, -100);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "-10"))
            {
                ApplyPeriodicDelta(settings, -10);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "-1"))
            {
                ApplyPeriodicDelta(settings, -1);
            }

            Rect textRect = new Rect(x, row.y, TimedTextWidth, ControlHeight);
            ApplyPeriodicText(
                settings,
                Widgets.TextField(textRect, periodicIntervalBuffer));
            x += TimedTextWidth + ControlGap;

            if (DrawStepButton(row, ref x, buttonWidth, "+1"))
            {
                ApplyPeriodicDelta(settings, 1);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "+10"))
            {
                ApplyPeriodicDelta(settings, 10);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "+100", addGap: false))
            {
                ApplyPeriodicDelta(settings, 100);
            }

            DrawIndentedLabel(listing, T("FMRC_TimedExplanation"));
            if (settings.PeriodicIntervalFrames < RevalidationRuntime.VanillaIntervalFrames)
            {
                DrawIndentedLabel(listing, T("FMRC_TimedLowWarning"));
            }
            else if (settings.PeriodicIntervalFrames > 2000)
            {
                DrawIndentedLabel(listing, T("FMRC_TimedLongSuggestion"));
            }
            listing.Gap(4f);
        }

        private void DrawAdaptiveSettings(
            Listing_Standard listing,
            FloatMenuRevalidationSettings settings)
        {
            listing.Gap(4f);
            DrawIndentedLabel(listing, T("FMRC_AdaptiveIntervalLabel"));

            Rect row = listing.GetRect(ControlHeight);
            float buttonWidth = Mathf.Clamp(
                (row.width - AdaptiveTextWidth - (ControlGap * 6f)) / 6f,
                48f,
                68f);
            float totalWidth = (buttonWidth * 6f)
                + AdaptiveTextWidth
                + (ControlGap * 6f);
            float x = row.x + Mathf.Max(0f, (row.width - totalWidth) / 2f);

            if (DrawStepButton(row, ref x, buttonWidth, "-1"))
            {
                ApplyAdaptiveDelta(settings, -100);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "-0.5"))
            {
                ApplyAdaptiveDelta(settings, -50);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "-0.1"))
            {
                ApplyAdaptiveDelta(settings, -10);
            }

            Rect textRect = new Rect(x, row.y, AdaptiveTextWidth, ControlHeight);
            ApplyAdaptiveText(
                settings,
                Widgets.TextField(textRect, adaptiveIntervalBuffer));
            x += AdaptiveTextWidth + ControlGap;

            if (DrawStepButton(row, ref x, buttonWidth, "+0.1"))
            {
                ApplyAdaptiveDelta(settings, 10);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "+0.5"))
            {
                ApplyAdaptiveDelta(settings, 50);
            }
            if (DrawStepButton(row, ref x, buttonWidth, "+1", addGap: false))
            {
                ApplyAdaptiveDelta(settings, 100);
            }

            DrawIndentedLabel(listing, T("FMRC_AdaptiveExplanation"));
            if (settings.AdaptiveIntervalHundredths == 0)
            {
                DrawIndentedLabel(listing, T("FMRC_AdaptiveZeroWarning"));
            }
            else if (settings.AdaptiveIntervalHundredths <= 3)
            {
                DrawIndentedLabel(listing, T("FMRC_AdaptiveLowWarning"));
            }
            else if (settings.AdaptiveIntervalHundredths > 1000)
            {
                DrawIndentedLabel(listing, T("FMRC_AdaptiveLongSuggestion"));
            }
            listing.Gap(4f);
        }

        private static void DrawSectionDivider(Listing_Standard listing)
        {
            Rect row = listing.GetRect(SectionDividerHeight);
            float originalWidth = Mathf.Min(240f, row.width * 0.42f);
            float width = Mathf.Min(row.width, originalWidth * 3f);
            Widgets.DrawLineHorizontal(
                row.x,
                row.center.y,
                width);
        }

        private static void DrawIndentedLabel(
            Listing_Standard listing,
            string text)
        {
            float width = Mathf.Max(1f, listing.ColumnWidth - DescriptionIndent);
            float height = Text.CalcHeight(text, width);
            Rect row = listing.GetRect(height);
            Widgets.Label(
                new Rect(
                    row.x + DescriptionIndent,
                    row.y,
                    width,
                    height),
                text);
        }

        private static bool DrawStepButton(
            Rect row,
            ref float x,
            float width,
            string label,
            bool addGap = true)
        {
            bool clicked = Widgets.ButtonText(
                new Rect(x, row.y, width, ControlHeight),
                label);
            x += width;
            if (addGap)
            {
                x += ControlGap;
            }

            return clicked;
        }

        private void EnsureBuffers(FloatMenuRevalidationSettings settings)
        {
            if (periodicIntervalBuffer == null)
            {
                periodicIntervalBuffer =
                    settings.PeriodicIntervalFrames.ToString(CultureInfo.InvariantCulture);
            }

            if (adaptiveIntervalBuffer == null)
            {
                adaptiveIntervalBuffer =
                    FormatHundredths(settings.AdaptiveIntervalHundredths);
            }
        }

        private void ApplyPeriodicDelta(
            FloatMenuRevalidationSettings settings,
            int delta)
        {
            long next = (long)settings.PeriodicIntervalFrames + delta;
            settings.PeriodicIntervalFrames = (int)Math.Max(
                RevalidationRuntime.MinimumIntervalFrames,
                Math.Min(int.MaxValue, next));
            periodicIntervalBuffer =
                settings.PeriodicIntervalFrames.ToString(CultureInfo.InvariantCulture);
        }

        private void ApplyPeriodicText(
            FloatMenuRevalidationSettings settings,
            string edited)
        {
            if (!ContainsOnlyAsciiDigits(edited))
            {
                return;
            }

            periodicIntervalBuffer = edited;
            if (edited.Length == 0)
            {
                return;
            }

            int parsed;
            if (!int.TryParse(
                edited,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                settings.PeriodicIntervalFrames = int.MaxValue;
                periodicIntervalBuffer =
                    int.MaxValue.ToString(CultureInfo.InvariantCulture);
                return;
            }

            settings.PeriodicIntervalFrames = Math.Max(
                RevalidationRuntime.MinimumIntervalFrames,
                parsed);
            if (parsed < RevalidationRuntime.MinimumIntervalFrames)
            {
                periodicIntervalBuffer =
                    RevalidationRuntime.MinimumIntervalFrames.ToString(
                        CultureInfo.InvariantCulture);
            }
        }

        private void ApplyAdaptiveDelta(
            FloatMenuRevalidationSettings settings,
            int deltaHundredths)
        {
            long next = (long)settings.AdaptiveIntervalHundredths
                + deltaHundredths;
            settings.AdaptiveIntervalHundredths = (int)Math.Max(
                0,
                Math.Min(int.MaxValue, next));
            adaptiveIntervalBuffer =
                FormatHundredths(settings.AdaptiveIntervalHundredths);
        }

        private void ApplyAdaptiveText(
            FloatMenuRevalidationSettings settings,
            string edited)
        {
            if (!IsValidAdaptiveText(edited))
            {
                return;
            }

            adaptiveIntervalBuffer = edited;
            if (edited.Length == 0 || edited == ".")
            {
                return;
            }

            decimal parsed;
            if (!decimal.TryParse(
                edited,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                settings.AdaptiveIntervalHundredths = int.MaxValue;
                adaptiveIntervalBuffer = FormatHundredths(int.MaxValue);
                return;
            }

            decimal hundredths = parsed * 100m;
            if (hundredths >= int.MaxValue)
            {
                settings.AdaptiveIntervalHundredths = int.MaxValue;
                adaptiveIntervalBuffer = FormatHundredths(int.MaxValue);
                return;
            }

            settings.AdaptiveIntervalHundredths = (int)hundredths;
        }

        private static bool ContainsOnlyAsciiDigits(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] < '0' || text[index] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidAdaptiveText(string text)
        {
            int decimalPointIndex = -1;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (character == '.')
                {
                    if (decimalPointIndex >= 0)
                    {
                        return false;
                    }

                    decimalPointIndex = index;
                    continue;
                }

                if (character < '0' || character > '9')
                {
                    return false;
                }

                if (decimalPointIndex >= 0 && index - decimalPointIndex > 2)
                {
                    return false;
                }
            }

            return true;
        }

        private static string FormatHundredths(int hundredths)
        {
            int whole = hundredths / 100;
            int fraction = hundredths % 100;
            if (fraction == 0)
            {
                return whole.ToString(CultureInfo.InvariantCulture);
            }

            if (fraction % 10 == 0)
            {
                return whole.ToString(CultureInfo.InvariantCulture)
                    + "."
                    + (fraction / 10).ToString(CultureInfo.InvariantCulture);
            }

            return whole.ToString(CultureInfo.InvariantCulture)
                + "."
                + fraction.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string T(string key)
        {
            return key.Translate().ToString();
        }
    }
}
