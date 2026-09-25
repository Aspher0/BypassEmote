using BypassEmote.Localization;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public static class SilkWindowMenu
{
    private static readonly WindowMenuStyle style = BuildStyle();
    private static int textRevision = -1;

    public static WindowMenuStyle Style
    {
        get
        {
            if (textRevision != NoireLanguages.Revision)
            {
                textRevision = NoireLanguages.Revision;
                style.Note = L.MenuSharedNote.Text;
                style.ClickThroughNote = L.MenuClickThroughNote.Text;
                style.SetHint(WindowMenuToggle.StayInGpose, L.MenuHintGpose.Text);
                style.SetHint(WindowMenuToggle.StayWhenUiHidden, L.MenuHintUiHidden.Text);
                style.SetHint(WindowMenuToggle.StayInCutscenes, L.MenuHintCutscenes.Text);
                style.SetHint(WindowMenuToggle.StayAutoHide, L.MenuHintAutoHide.Text);
            }

            return style;
        }
    }

    private static WindowMenuStyle BuildStyle()
    {
        var style = new WindowMenuStyle
        {
            Width = 300f,
            PaddingX = 14f,
            PaddingTop = 14f,
            PaddingBottom = 12f,
            Rounding = 13f,
            Background = SilkPalette.MenuBg,
            BorderColor = SilkPalette.MenuRing,
            BorderSize = 1f,
            BorderOutside = true,
            ShadowColor = SilkPalette.MenuShadow,
            ShadowOffset = new Vector2(0f, 24f),
            ShadowBlur = 60f,
            ShadowInset = 12f,
            AnchorGap = 8f,
            ScreenMargin = 8f,
            OpenSeconds = 0.2f,
            FadeSeconds = 0.15f,
            OpenSlide = 4f,
            OpenCurve = SilkUi.EaseOut,
            TextScale = 1f,
            HeadingSizePx = 10f,
            HeadingTrackingEm = 0.14f,
            HeadingColor = SilkPalette.MenuHeading,
            HeadingLineHeight = 1.303f,
            HeadingInset = 2f,
            FirstHeadingTop = 2f,
            HeadingGapAbove = 14f,
            HeadingGapBelow = 9f,
            OpacityMin = 0.2f,
            OpacityMax = 1f,
            TextSteps = SilkPalette.TextSteps,
            SliderRowHeight = 30f,
            SliderGap = 0f,
            SliderInset = 2f,
            SliderSpacing = 10f,
            SliderLabelWidth = 64f,
            SliderLabelSizePx = 12f,
            SliderLabelColor = SilkPalette.MenuLabel,
            SliderValueWidth = 36f,
            SliderValueSizePx = 11f,
            SliderValueColor = SilkPalette.MenuValue,
            TrackHeight = 4f,
            TrackColor = SilkPalette.MenuTrack,
            TrackFillColor = SilkPalette.Acc,
            ThumbSize = 14f,
            ThumbColor = SilkPalette.White,
            ThumbRingWidth = 3f,
            ThumbRingColor = SilkPalette.MenuThumbRing,
            ThumbShadowColor = SilkPalette.MenuThumbShadow,
            ThumbShadowOffsetY = 2f,
            ThumbShadowBlur = 6f,
            ToggleColumnGap = 5f,
            ToggleRowGap = 5f,
            ToggleHeight = 32f,
            TogglePaddingX = 10f,
            ToggleRounding = 8f,
            ToggleSizePx = 11.5f,
            ToggleText = SilkPalette.MenuToggleText,
            ToggleActiveText = SilkPalette.WctlHoverText,
            ToggleFill = SilkPalette.MenuToggleBg,
            ToggleBorder = SilkPalette.MenuToggleRing,
            ToggleOnFill = SilkPalette.MenuToggleOnBg,
            ToggleOnBorder = SilkPalette.MenuToggleOnRing,
            DotSize = 6f,
            DotGap = 8f,
            DotColor = SilkPalette.MenuDotOff,
            DotOnColor = SilkPalette.Acc,
            DotGlowSpread = 8f,
            ToggleTransitionSeconds = 0.2f,
            NoteSizePx = 11f,
            NoteColor = SilkPalette.MenuHeading,
            NoteLineHeight = 1.45f,
            NoteGap = 10f,
            NoteInset = 2f,
            CustomDrawText = DrawText,
            MeasureText = MeasureText,
            CustomShowHint = static hint => SilkTooltip.Hover(hint.Text, hint.Min, hint.Max),
        };

        return style;
    }

    private static readonly System.Collections.Generic.List<string> NoteLines = [];
    private static string? noteWrapped;
    private static float noteWidth;
    private static float noteScale;

    private static (SilkWeight Weight, bool Mono) Face(WindowMenuTextRole role) => role switch
    {
        WindowMenuTextRole.Heading => (SilkWeight.SemiBold, false),
        WindowMenuTextRole.SliderValue => (SilkWeight.Medium, true),
        WindowMenuTextRole.Note => (SilkWeight.Regular, false),
        _ => (SilkWeight.Medium, false),
    };

    private static void DrawText(UiWindowMenuText text)
    {
        var (weight, mono) = Face(text.Role);
        var tracking = text.TrackingEm * Css(text);

        if (text.Role == WindowMenuTextRole.Note)
        {
            WrapNote(text, weight);

            var css = Css(text);
            var line = SilkText.LineBox(css, text.LineHeight);
            var y = text.BoxMin.Y;

            foreach (var row in NoteLines)
            {
                SilkText.Draw(new Vector2(text.BoxMin.X, SilkText.GlyphTop(y, line, css, weight, text.LineHeight)), row, css, weight, text.Color);
                y += line;
            }

            return;
        }

        SilkText.DrawInBox(text.BoxMin, text.BoxMax, text.Text, Css(text), weight, text.Color, text.Align, tracking, mono);
    }

    private static Vector2 MeasureText(UiWindowMenuText text)
    {
        var (weight, mono) = Face(text.Role);

        if (text.Role == WindowMenuTextRole.Note)
        {
            WrapNote(text, weight);
            return new Vector2(text.BoxMax.X - text.BoxMin.X, NoteLines.Count * SilkText.LineBox(Css(text), text.LineHeight));
        }

        return SilkText.Measure(text.Text, Css(text), weight, text.TrackingEm * Css(text), mono);
    }

    private static float Css(UiWindowMenuText text) => text.SizePx / MathF.Max(0.01f, Style.TextScale);

    private static void WrapNote(UiWindowMenuText text, SilkWeight weight)
    {
        var width = text.BoxMax.X - text.BoxMin.X;
        var scale = SilkUi.Scale * SilkUi.TextScale;

        if (ReferenceEquals(noteWrapped, text.Text) && MathF.Abs(noteWidth - width) < 0.5f && MathF.Abs(noteScale - scale) < 0.0001f)
            return;

        SilkText.Wrap(text.Text, Css(text), weight, width, NoteLines);
        noteWrapped = text.Text;
        noteWidth = width;
        noteScale = scale;
    }
}
