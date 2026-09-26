using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public enum SilkButtonKind
{
    Normal,
    Primary,
}

public enum SilkNoticeKind
{
    Warn,
    Bad,
}

public sealed class SilkComboItem
{
    public SilkComboItem(string label, string? note = null, Vector4? dot = null, string? tooltip = null)
    {
        Label = label;
        Note = note;
        Dot = dot;
        Tooltip = tooltip;
    }

    public string Label { get; set; }

    public string? Note { get; set; }

    public Vector4? Dot { get; set; }

    public string? Tooltip { get; set; }
}

public static class SilkControls
{
    public const float SwitchWidth = 40f;
    public const float SwitchHeight = 22f;
    public const float SegmentedHeight = 30f;
    public const float StepperHeight = 30f;
    public const float ComboHeight = 32f;
    public const float CheckboxSize = 16f;
    public const float HelpSize = 17f;
    public const float ButtonHeight = 36f;
    public const float SearchHeight = 38f;
    public const float FieldHeight = 38f;
    public const float RowMinHeight = 44f;
    public const float RowHelpWidth = 30f;
    public const float RowControlMax = 270f;
    public const float RowControlShare = 0.46f;

    private const string Anim = "SilkControls";

    private static readonly ToggleStyle SwitchStyle = new()
    {
        Height = SwitchHeight,
        WidthRatio = SwitchWidth / SwitchHeight,
        AnimationDuration = 0.35f,
        AnimationCurve = SilkUi.EaseOut.Curve,
        CustomDraw = static args => PaintSwitch(args, false),
    };

    private static readonly ToggleStyle DangerSwitchStyle = new()
    {
        Height = SwitchHeight,
        WidthRatio = SwitchWidth / SwitchHeight,
        AnimationDuration = 0.35f,
        AnimationCurve = SilkUi.EaseOut.Curve,
        CustomDraw = static args => PaintSwitch(args, true),
    };

    private static readonly ButtonStyle SilkButtonStyle = new() { CustomDraw = static args => PaintButton(args) };

    private static readonly Dictionary<string, (int Value, string Unit, string Text)> StepTexts = new(StringComparer.Ordinal);

    private static SilkButtonKind buttonKind;
    private static SilkIcon? buttonIcon;
    private static bool buttonDisabled;
    private static bool buttonHold;

    public static Vector2 Cursor => ImGui.GetCursorScreenPos();

    public static float AvailableWidth => ImGui.GetContentRegionAvail().X;

    public static void Advance(Vector2 from, float width, float height)
    {
        ImGui.SetCursorScreenPos(from);
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), MathF.Max(0f, height)));
    }

    #region Checkbox

    public static float CheckboxWidth(string label, float labelRoom = 0f)
        => SilkUi.Px(CheckboxSize + 8f) + LabelWidth(label, labelRoom);

    private static float LabelWidth(string label, float labelRoom)
    {
        var width = SilkText.Width(label, 12.5f, SilkWeight.Medium);
        return labelRoom > 0f ? MathF.Min(width, labelRoom) : width;
    }

    public static bool Checkbox(string id, string label, ref bool value, Vector2 pos, float labelRoom = 0f)
    {
        var scale = SilkUi.Scale;
        var box = CheckboxSize * scale;
        var textWidth = LabelWidth(label, labelRoom);
        var height = MathF.Max(box, SilkText.NaturalLine(12.5f, SilkWeight.Medium));
        var width = box + (8f * scale) + textWidth;

        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));

        if (clicked)
            value = !value;

        var hovered = ImGui.IsItemHovered();
        var t = SilkUi.Ease(id, "ck", value ? 1f : 0f, 0.25f);

        var boxMin = new Vector2(pos.X, MathF.Round(pos.Y + ((height - box) * 0.5f)));
        var boxMax = boxMin + new Vector2(box, box);
        var radius = 5f * scale;

        if (t > 0f)
        {
            SilkPaint.Glow(boxMin, boxMax, 12f * scale, -2f * scale, SilkPalette.Fade(SilkPalette.LogoGlow, t), radius);
            SilkPaint.Fill(boxMin, boxMax, SilkPalette.Fade(SilkPalette.Ice, t), radius);
        }

        if (t < 1f)
            SilkPaint.InsetRing(boxMin, boxMax, SilkPalette.Fade(SilkPalette.Line2, 1f - t), radius, 1.5f * scale);

        if (t > 0f)
        {
            var glyph = 10f * scale * (0.4f + (0.6f * t));
            SilkIcons.DrawCentered(SilkIcon.Check, (boxMin + boxMax) * 0.5f, glyph, SilkPalette.Fade(SilkPalette.PriText, t));
        }

        var color = value || hovered ? SilkPalette.Ink : SilkPalette.Ink2;
        SilkText.DrawInBox(new Vector2(boxMax.X + (8f * scale), pos.Y), new Vector2(pos.X + width, pos.Y + height), label, 12.5f, SilkWeight.Medium, color);

        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return clicked;
    }

    #endregion

    #region Switch

    public static bool Switch(string id, ref bool value, Vector2 pos, bool danger = false)
    {
        ImGui.SetCursorScreenPos(pos);
        var changed = NoireButtons.Toggle(id, ref value, danger ? DangerSwitchStyle : SwitchStyle);

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return changed;
    }

    private static void PaintSwitch(UiToggleDraw args, bool danger)
    {
        var scale = SilkUi.Scale;
        var min = args.Min;
        var max = args.Max;
        var radius = (max.Y - min.Y) * 0.5f;
        var t = Math.Clamp(args.Travel, 0f, 1f);

        if (t < 1f)
        {
            SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.SwitchOff, 1f - t), radius);
            SilkPaint.InsetRing(min, max, SilkPalette.Fade(SilkPalette.Line2, 1f - t), radius, scale);
        }

        if (t > 0f)
        {
            if (danger)
            {
                SilkPaint.Glow(min, max, 16f * scale, -4f * scale, SilkPalette.Fade(SilkPalette.Bad, t), radius);
                SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.Bad, t), radius);
            }
            else
            {
                SilkPaint.Glow(min, max, 16f * scale, -4f * scale, SilkPalette.Fade(SilkPalette.Ice, t), radius);
                SilkPaint.HorizontalGradient(min, max, SilkPalette.Fade(SilkPalette.Vio, t), SilkPalette.Fade(SilkPalette.Ice, t), radius);
            }
        }

        var knob = 16f * scale;
        var inset = 3f * scale;
        var centre = new Vector2(min.X + inset + (knob * 0.5f) + (18f * scale * t), (min.Y + max.Y) * 0.5f);
        SilkPaint.Circle(centre, knob * 0.5f, SilkPalette.Mix(SilkPalette.SwitchKnob, SilkPalette.White, t));
    }

    #endregion

    #region Segmented

    public static bool Segmented(string id, ref int selected, string[] options, Vector2 pos, float width, float buttonPaddingX = 4f)
    {
        var scale = SilkUi.Scale;
        var height = SegmentedHeight * scale;
        var pad = 2f * scale;
        var min = pos;
        var max = pos + new Vector2(width, height);

        SilkPaint.Fill(min, max, SilkPalette.Sunken, 9f * scale);
        SilkPaint.InsetRing(min, max, SilkPalette.Line2, 9f * scale, scale);

        var count = Math.Max(1, options.Length);
        var inner = width - (pad * 2f);
        var cell = inner / count;
        var changed = false;

        if (selected >= 0 && selected < options.Length)
        {
            var left = SilkUi.Ease(id, "pl", pad + (cell * selected), 0.4f);
            var pillMin = new Vector2(pos.X + left, pos.Y + pad);
            var pillMax = pillMin + new Vector2(cell, height - (pad * 2f));
            SilkPaint.Fill(pillMin, pillMax, SilkPalette.IceWash16, 7f * scale);
            SilkPaint.InsetRing(pillMin, pillMax, SilkPalette.IceRing30, 7f * scale, scale);
        }

        ImGui.PushID(id);

        for (var i = 0; i < options.Length; i++)
        {
            var cellMin = new Vector2(pos.X + pad + (cell * i), pos.Y + pad);
            var cellMax = cellMin + new Vector2(cell, height - (pad * 2f));

            ImGui.SetCursorScreenPos(cellMin);

            if (ImGui.InvisibleButton(options[i], cellMax - cellMin) && selected != i)
            {
                selected = i;
                changed = true;
            }

            var hovered = ImGui.IsItemHovered();

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                if (SilkText.Width(options[i], 11.5f, SilkWeight.SemiBold) > cell - (buttonPaddingX * 2f * scale))
                    SilkTooltip.Hover(options[i], cellMin, cellMax);
            }

            var textPad = buttonPaddingX * scale;
            SilkText.DrawInBox(cellMin + new Vector2(textPad, 0f), cellMax - new Vector2(textPad, 0f), options[i], 11.5f, SilkWeight.SemiBold,
                i == selected ? SilkPalette.Ink : SilkPalette.Ink3, UiAlign.Center, 0f, false, 0f);
        }

        ImGui.PopID();
        ImGui.SetCursorScreenPos(new Vector2(pos.X, max.Y));
        return changed;
    }

    #endregion

    #region Stepper

    public static bool Stepper(string id, ref int value, Vector2 pos, float width, int min = 0, int max = int.MaxValue, string unit = "", int step = 1)
    {
        var scale = SilkUi.Scale;
        var height = StepperHeight * scale;
        var boxMax = pos + new Vector2(width, height);
        var button = 28f * scale;
        var changed = false;

        SilkPaint.Fill(pos, boxMax, SilkPalette.Sunken, 9f * scale);
        SilkPaint.InsetRing(pos, boxMax, SilkPalette.Line2, 9f * scale, scale);

        ImGui.PushID(id);

        ImGui.SetCursorScreenPos(pos);

        if (ImGui.InvisibleButton("-", new Vector2(button, height)) && value - step >= min)
        {
            value -= step;
            changed = true;
        }

        var minusHovered = ImGui.IsItemHovered();

        ImGui.SetCursorScreenPos(new Vector2(boxMax.X - button, pos.Y));

        if (ImGui.InvisibleButton("+", new Vector2(button, height)) && value + step <= max)
        {
            value += step;
            changed = true;
        }

        var plusHovered = ImGui.IsItemHovered();

        ImGui.PopID();

        if (minusHovered || plusHovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        SilkText.DrawInBox(pos, new Vector2(pos.X + button, boxMax.Y), "-", 13f, SilkWeight.Regular, minusHovered ? SilkPalette.Ink : SilkPalette.Ink3, UiAlign.Center);
        SilkText.DrawInBox(new Vector2(boxMax.X - button, pos.Y), boxMax, "+", 13f, SilkWeight.Regular, plusHovered ? SilkPalette.Ink : SilkPalette.Ink3, UiAlign.Center);
        SilkText.DrawInBox(new Vector2(pos.X + button, pos.Y), new Vector2(boxMax.X - button, boxMax.Y), StepText(id, value, unit), 12f, SilkWeight.Medium,
            SilkPalette.Ink, UiAlign.Center, 0f, true);

        ImGui.SetCursorScreenPos(new Vector2(pos.X, boxMax.Y));
        return changed;
    }

    private static string StepText(string id, int value, string unit)
    {
        if (StepTexts.TryGetValue(id, out var cached) && cached.Value == value && ReferenceEquals(cached.Unit, unit))
            return cached.Text;

        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture) + unit;
        StepTexts[id] = (value, unit, text);
        return text;
    }

    #endregion

    #region Combo

    public static bool Combo(string id, ref int selected, SilkComboItem[] items, Vector2 pos, float width)
    {
        var scale = SilkUi.Scale;
        var height = ComboHeight * scale;
        var max = pos + new Vector2(width, height);
        var changed = false;

        ImGui.SetCursorScreenPos(pos);

        if (ImGui.InvisibleButton(id, max - pos))
        {
            ImGui.OpenPopup(id);
            ComboTopLayer = SilkUi.Settings.AlwaysOnTop;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        SilkPaint.Fill(pos, max, SilkPalette.Sunken, 9f * scale);
        SilkPaint.InsetRing(pos, max, SilkPalette.Line2, 9f * scale, scale);

        var current = selected >= 0 && selected < items.Length ? items[selected] : null;
        var x = pos.X + (12f * scale);

        if (current?.Dot is { } dot)
        {
            var radius = 3.5f * scale;
            var centre = new Vector2(x + radius, (pos.Y + max.Y) * 0.5f);
            SilkPaint.Glow(centre - new Vector2(radius, radius), centre + new Vector2(radius, radius), 8f * scale, 0f, dot, radius);
            SilkPaint.Circle(centre, radius, dot);
            x += (radius * 2f) + (10f * scale);
        }

        var chevron = 12f * scale;
        var chevronX = max.X - (10f * scale) - chevron;

        if (current != null)
            SilkText.DrawInBox(new Vector2(x, pos.Y), new Vector2(chevronX - (10f * scale), max.Y), current.Label, 12.5f, SilkWeight.SemiBold, SilkPalette.Ink, UiAlign.Start, 0f, false, 0f);

        SilkIcons.Draw(SilkIcon.Chevron, new Vector2(chevronX, MathF.Round(((pos.Y + max.Y) * 0.5f) - (chevron * 0.5f))), chevron, SilkPalette.Ink3);

        if (!ImGui.IsPopupOpen(id))
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X, max.Y));
            return false;
        }

        var itemHeight = 32f * scale;
        var pad = 4f * scale;
        var popupHeight = (items.Length * itemHeight) + (pad * 2f);

        ImGui.SetNextWindowPos(new Vector2(pos.X, pos.Y + (36f * scale)), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, popupHeight), ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 10f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Zero);

        var open = ImGui.BeginPopup(id, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(4);

        if (open)
        {
            if (ComboTopLayer || SilkUi.Settings.AlwaysOnTop)
                NoireWindowChrome.KeepInFront();

            var popupMin = ImGui.GetWindowPos();
            var popupMax = popupMin + new Vector2(width, popupHeight);

            SilkPaint.PushClipExpanded(popupMin, popupMax, 48f * scale);
            SilkPaint.BoxShadow(popupMin, popupMax, new Vector2(0f, 16f * scale), 40f * scale, -8f * scale, SilkPalette.PopupShadow, 10f * scale);
            SilkPaint.OuterRing(popupMin, popupMax, SilkPalette.Line2, 10f * scale, scale);
            SilkPaint.Fill(popupMin, popupMax, SilkPalette.Popup, 10f * scale);
            SilkPaint.PopClip();

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var itemMin = popupMin + new Vector2(pad, pad + (i * itemHeight));
                var itemMax = itemMin + new Vector2(width - (pad * 2f), itemHeight);

                ImGui.SetCursorScreenPos(itemMin);

                if (ImGui.InvisibleButton(item.Label, itemMax - itemMin))
                {
                    if (selected != i)
                    {
                        selected = i;
                        changed = true;
                    }

                    ImGui.CloseCurrentPopup();
                }

                var hovered = ImGui.IsItemHovered();

                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    SilkPaint.Fill(itemMin, itemMax, SilkPalette.IceWash10, 7f * scale);

                    if (item.Tooltip != null)
                        SilkTooltip.Hover(item.Tooltip, itemMin, itemMax);
                }

                var textMin = itemMin + new Vector2(10f * scale, 0f);
                var textMax = itemMax - new Vector2(10f * scale, 0f);

                if (item.Note != null)
                {
                    var note = SilkText.DrawInBox(textMin, textMax, item.Note, 10.5f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.End);
                    textMax.X -= note.X + (8f * scale);
                }

                SilkText.DrawInBox(textMin, textMax, item.Label, 12.5f, SilkWeight.SemiBold, hovered || i == selected ? SilkPalette.Ink : SilkPalette.Ink2,
                    UiAlign.Start, 0f, false, 0f);
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }

        ImGui.SetCursorScreenPos(new Vector2(pos.X, max.Y));
        return changed;
    }

    private static bool ComboTopLayer { get; set; }

    #endregion

    #region Help mark

    public static void HelpMark(string id, string? text, Vector2 centre)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var scale = SilkUi.Scale;
        var size = HelpSize * scale;
        var min = new Vector2(MathF.Round(centre.X - (size * 0.5f)), MathF.Round(centre.Y - (size * 0.5f)));
        var max = min + new Vector2(size, size);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();

        SilkPaint.InsetRing(min, max, hovered ? SilkPalette.IceRing60 : SilkPalette.Line2, size * 0.5f, scale);
        SilkText.DrawInBox(min, max, "?", 10f, SilkWeight.Bold, hovered ? SilkPalette.Ice : SilkPalette.Ink3, UiAlign.Center);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            SilkTooltip.Hover(text, min, max, true);
        }
    }

    #endregion

    #region Buttons

    public static float ButtonWidth(string label, SilkIcon? icon = null)
    {
        var scale = SilkUi.Scale;
        var width = (32f * scale) + SilkText.Width(VisibleLabel(label), 13f, SilkWeight.Bold);

        if (icon.HasValue)
            width += 22f * scale;

        return MathF.Ceiling(width);
    }

    public static bool Button(string label, Vector2 pos, SilkButtonKind kind = SilkButtonKind.Normal, SilkIcon? icon = null, bool disabled = false, float width = 0f)
    {
        var size = new Vector2(width > 0f ? width : ButtonWidth(label, icon), ButtonHeight * SilkUi.Scale);

        buttonKind = kind;
        buttonIcon = icon;
        buttonDisabled = disabled;
        buttonHold = false;

        ImGui.SetCursorScreenPos(pos);

        if (disabled)
            ImGui.BeginDisabled();

        var clicked = NoireButtons.Button(label, SilkButtonStyle, size);

        if (disabled)
            ImGui.EndDisabled();
        else if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return clicked && !disabled;
    }

    public static bool Hold(string label, Vector2 pos, SilkIcon? icon = null, float seconds = 1.2f, float width = 0f)
    {
        var size = new Vector2(width > 0f ? width : ButtonWidth(label, icon), ButtonHeight * SilkUi.Scale);

        buttonKind = SilkButtonKind.Normal;
        buttonIcon = icon;
        buttonDisabled = false;
        buttonHold = true;

        ImGui.SetCursorScreenPos(pos);
        var done = NoireButtons.HoldToConfirm(label, seconds, SilkButtonStyle, size);

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return done;
    }

    private static void PaintButton(UiButtonDraw args)
    {
        var scale = SilkUi.Scale;
        var radius = 10f * scale;
        var min = args.Min;
        var max = args.Max;
        var fade = buttonDisabled ? 0.4f : 1f;
        var hovered = args.Hovered && !buttonDisabled;
        Vector4 text;

        if (buttonKind == SilkButtonKind.Primary)
        {
            SilkPaint.BoxShadow(min, max, new Vector2(0f, 3f * scale), 8f * scale, -4f * scale, SilkPalette.Fade(SilkPalette.PriShadow, fade), radius);
            SilkPaint.Fill(min, max, SilkPalette.Fade(hovered ? SilkPalette.PriHover : SilkPalette.Ice, fade), radius);
            SilkPaint.InsetRing(min, max, SilkPalette.Fade(SilkPalette.PriRing, fade), radius, scale);
            text = SilkPalette.Fade(SilkPalette.PriText, fade);
        }
        else
        {
            if (hovered)
                SilkPaint.Fill(min, max, SilkPalette.HoverWash, radius);

            if (buttonHold && args.Progress > 0f)
            {
                var fill = new Vector2(min.X + ((max.X - min.X) * Math.Clamp(args.Progress, 0f, 1f)), max.Y);
                SilkPaint.Fill(min, fill, SilkPalette.HoldFill, radius, args.Progress >= 1f ? RectCorners.All : RectCorners.Left);
            }

            SilkPaint.InsetRing(min, max, SilkPalette.Fade(SilkPalette.Line2, fade), radius, scale);
            text = SilkPalette.Fade(hovered ? (buttonHold ? SilkPalette.BadText : SilkPalette.Ink) : SilkPalette.Ink2, fade);
        }

        var label = args.Label;
        var textWidth = SilkText.Width(label, 13f, SilkWeight.Bold);
        var iconSize = 14f * scale;
        var gap = 8f * scale;
        var content = textWidth + (buttonIcon.HasValue ? iconSize + gap : 0f);
        var x = MathF.Round(min.X + ((max.X - min.X - content) * 0.5f));

        if (buttonIcon is { } icon)
        {
            SilkIcons.Draw(icon, new Vector2(x, MathF.Round(((min.Y + max.Y) * 0.5f) - (iconSize * 0.5f))), iconSize, text);
            x += iconSize + gap;
        }

        SilkText.DrawInBox(new Vector2(x, min.Y), new Vector2(max.X, max.Y), label, 13f, SilkWeight.Bold, text);
    }

    private static readonly Dictionary<string, string> Visible = new(ReferenceEqualityComparer.Instance);

    public static string VisibleLabel(string label)
    {
        if (Visible.TryGetValue(label, out var text))
            return text;

        var cut = label.IndexOf("##", StringComparison.Ordinal);
        text = cut < 0 ? label : label[..cut];
        Visible[label] = text;
        return text;
    }

    #endregion

    #region Text inputs

    public static bool Search(string id, ref string text, Vector2 pos, float width, string placeholder, float height = SearchHeight, int maxLength = 256)
        => Input(id, ref text, pos, width, height, placeholder, SilkIcon.Search, 15f, 11f, 13.5f, SilkWeight.Regular, maxLength);

    public static bool Field(string id, ref string text, Vector2 pos, float width, string placeholder, SilkIcon? icon = null, float height = FieldHeight, int maxLength = 256)
        => Input(id, ref text, pos, width, height, placeholder, icon, 14f, 10f, 13f, SilkWeight.Medium, maxLength);

    private static bool Input(string id, ref string text, Vector2 pos, float width, float height, string placeholder, SilkIcon? icon, float iconCss, float radiusCss,
        float fontCss, SilkWeight weight, int maxLength)
    {
        var scale = SilkUi.Scale;
        var height2 = height * scale;
        var max = pos + new Vector2(width, height2);
        var radius = radiusCss * scale;
        var focused = UiFocusState(id);
        var focus = SilkUi.Css(id, "focus", focused ? 1f : 0f, 0.25f);

        SilkPaint.Fill(pos, max, SilkPalette.Sunken, radius);

        if (focus > 0f)
            SilkPaint.OuterRing(pos - new Vector2(1.5f * scale, 1.5f * scale), max + new Vector2(1.5f * scale, 1.5f * scale), SilkPalette.Fade(SilkPalette.FocusHalo, focus), radius + (1.5f * scale), 3f * scale);

        SilkPaint.InsetRing(pos, max, SilkPalette.Mix(SilkPalette.Line2, SilkPalette.IceRing60, focus), radius, scale);

        var x = pos.X + (12f * scale);

        if (icon is { } glyph)
        {
            var size = iconCss * scale;
            SilkIcons.Draw(glyph, new Vector2(x, MathF.Round(((pos.Y + max.Y) * 0.5f) - (size * 0.5f))), size, SilkPalette.Ink3);
            x += size + (10f * scale);
        }

        var inputWidth = MathF.Max(1f, max.X - (12f * scale) - x);
        var face = SilkText.Face(weight);
        var mouse = ImGui.GetMousePos();

        if (!focused && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && SilkPaint.Contains(pos, max, mouse) && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            ImGui.SetKeyboardFocusHere();

        bool changed;

        using (SilkFonts.PushInput(face, fontCss))
        {
            var fontHeight = ImGui.GetFontSize();
            ImGui.SetCursorScreenPos(new Vector2(x, MathF.Round(((pos.Y + max.Y) * 0.5f) - (fontHeight * 0.5f))));
            ImGui.SetNextItemWidth(inputWidth);

            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, SilkPalette.Ink);
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, SilkPalette.Ink3);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, SilkPalette.IceRing30);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);

            changed = ImGui.InputTextWithHint(id, placeholder, ref text, maxLength);

            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(6);
        }

        SetFocusState(id, ImGui.IsItemActive());

        if (SilkPaint.Contains(pos, max, mouse) && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);

        ImGui.SetCursorScreenPos(new Vector2(pos.X, max.Y));
        return changed;
    }

    private static readonly Dictionary<string, bool> FocusStates = new(StringComparer.Ordinal);

    private static bool UiFocusState(string id) => FocusStates.TryGetValue(id, out var value) && value;

    private static void SetFocusState(string id, bool value)
    {
        if (UiFocusState(id) != value)
            FocusStates[id] = value;
    }

    #endregion

    #region Layout

    public static float Section(string title, Vector2 pos, float width)
    {
        var scale = SilkUi.Scale;
        var top = pos.Y + (20f * scale);
        var x = pos.X + (2f * scale);
        var right = pos.X + width - (2f * scale);
        var upper = SilkFonts.Upper(title);
        var height = SilkText.NaturalLine(11f, SilkWeight.Bold);
        var size = SilkText.Draw(new Vector2(x, top), upper, 11f, SilkWeight.Bold, SilkPalette.SectText, 1f);
        var lineX = x + size.X + (10f * scale);
        var lineY = MathF.Round(top + (height * 0.5f));

        if (right > lineX)
            SilkPaint.HorizontalGradient(new Vector2(lineX, lineY), new Vector2(right, lineY + scale), SilkPalette.Line2, SilkPalette.Alpha(SilkPalette.Line2, 0f), 0f);

        return (20f * scale) + height + (8f * scale);
    }

    public static void Section(string title)
    {
        var pos = Cursor;
        var width = AvailableWidth;
        Advance(pos, width, Section(title, pos, width));
    }

    public static void Card(Vector2 min, Vector2 max)
    {
        var radius = 12f * SilkUi.Scale;
        SilkPaint.Fill(min, max, SilkPalette.Panel, radius);
        SilkPaint.InsetRing(min, max, SilkPalette.Line, radius, SilkUi.Scale);
    }

    public static float Notice(SilkNoticeKind kind, string text, string? muted = null, bool boldFirst = false, float marginTop = 10f)
    {
        var pos = Cursor;
        var width = AvailableWidth;
        var height = Notice(kind, text, muted, boldFirst, pos + new Vector2(0f, marginTop * SilkUi.Scale), width) + (marginTop * SilkUi.Scale);
        Advance(pos, width, height);
        return height;
    }

    public static float Notice(SilkNoticeKind kind, string text, string? muted, bool boldFirst, Vector2 pos, float width)
    {
        var scale = SilkUi.Scale;
        var padX = 14f * scale;
        var padY = 12f * scale;
        var inner = width - (padX * 2f);
        var weight = boldFirst ? SilkWeight.Bold : SilkWeight.Medium;
        var body = SilkText.ParagraphHeight(text, 12f, weight, inner, 1.5f);
        var extra = string.IsNullOrEmpty(muted) ? 0f : (6f * scale) + SilkText.ParagraphHeight(muted!, 12f, SilkWeight.Medium, inner, 1.5f);
        var height = body + extra + (padY * 2f);
        var max = pos + new Vector2(width, height);
        var radius = 12f * scale;
        var warn = kind == SilkNoticeKind.Warn;

        SilkPaint.Fill(pos, max, warn ? SilkPalette.WarnBg : SilkPalette.BadBg, radius);
        SilkPaint.InsetRing(pos, max, warn ? SilkPalette.WarnRing : SilkPalette.BadRing, radius, scale);

        var y = pos.Y + padY;
        y += SilkText.DrawParagraph(new Vector2(pos.X + padX, y), text, 12f, weight, warn ? SilkPalette.WarnText : SilkPalette.BadText, inner, 1.5f);

        if (!string.IsNullOrEmpty(muted))
            SilkText.DrawParagraph(new Vector2(pos.X + padX, y + (6f * scale)), muted!, 12f, SilkWeight.Medium, SilkPalette.Ink3, inner, 1.5f);

        return height;
    }

    public static float InfoBox(string text, float marginTop = 14f)
    {
        var scale = SilkUi.Scale;
        var pos = Cursor;
        var width = AvailableWidth;
        var top = pos + new Vector2(0f, marginTop * scale);
        var inner = width - (28f * scale);
        var height = SilkText.ParagraphHeight(text, 13f, SilkWeight.Regular, inner, 1.4f) + (24f * scale);
        var max = top + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(top, max, SilkPalette.InfoBg, radius);
        SilkPaint.InsetRing(top, max, SilkPalette.InfoRing, radius, scale);
        SilkText.DrawParagraph(top + new Vector2(14f * scale, 12f * scale), text, 13f, SilkWeight.Regular, SilkPalette.Ink2, inner, 1.4f);

        Advance(pos, width, height + (marginTop * scale));
        return height + (marginTop * scale);
    }

    public static int Gate(string id, string title, string text, string[] buttons, Vector2 pos, float width, out float height)
    {
        var scale = SilkUi.Scale;
        var padX = 14f * scale;
        var gap = 12f * scale;
        var buttonsWidth = 0f;

        for (var i = 0; i < buttons.Length; i++)
            buttonsWidth += SmallButtonWidth(buttons[i], 12f, SilkWeight.Bold) + (i > 0 ? gap : 0f);

        var textX = pos.X + padX + (8f * scale) + gap;
        var textWidth = MathF.Max(1f, pos.X + width - padX - buttonsWidth - gap - textX);
        var titleHeight = SilkText.ParagraphHeight(title, 12.5f, SilkWeight.Bold, textWidth, 1.45f);
        var bodyHeight = SilkText.ParagraphHeight(text, 12f, SilkWeight.Medium, textWidth, 1.45f);
        var content = MathF.Max(titleHeight + bodyHeight, 30f * scale);
        height = content + (22f * scale);

        var max = pos + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(pos, max, SilkPalette.GateBg, radius);
        SilkPaint.InsetRing(pos, max, SilkPalette.GateRing, radius, scale);

        var centreY = (pos.Y + max.Y) * 0.5f;
        var dot = new Vector2(pos.X + padX + (4f * scale), centreY);
        SilkPaint.Glow(dot - new Vector2(4f * scale), dot + new Vector2(4f * scale), 10f * scale, 0f, SilkPalette.Warn, 4f * scale);
        SilkPaint.Circle(dot, 4f * scale, SilkPalette.Warn);

        var textTop = centreY - ((titleHeight + bodyHeight) * 0.5f);
        SilkText.DrawParagraph(new Vector2(textX, textTop), title, 12.5f, SilkWeight.Bold, SilkPalette.WarnText, textWidth, 1.45f);
        SilkText.DrawParagraph(new Vector2(textX, textTop + titleHeight), text, 12f, SilkWeight.Medium, SilkPalette.Ink2, textWidth, 1.45f);

        var clicked = -1;
        var x = max.X - padX - buttonsWidth;

        ImGui.PushID(id);

        for (var i = 0; i < buttons.Length; i++)
        {
            var w = SmallButtonWidth(buttons[i], 12f, SilkWeight.Bold);

            if (SmallButton(buttons[i], new Vector2(x, centreY - (15f * scale)), w, 30f, 12f, SilkWeight.Bold, SilkPalette.Ink, SilkPalette.Ink, SilkPalette.HoverWashStrong))
                clicked = i;

            x += w + gap;
        }

        ImGui.PopID();
        return clicked;
    }

    public static bool Approval(string id, string text, string button, Vector2 pos, float width, out float height)
    {
        var scale = SilkUi.Scale;
        height = MathF.Max(28f * scale, SilkText.NaturalLine(12.5f, SilkWeight.Medium)) + (22f * scale);
        var max = pos + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(pos, max, SilkPalette.OkBg, radius);
        SilkPaint.InsetRing(pos, max, SilkPalette.OkRing, radius, scale);

        var centreY = (pos.Y + max.Y) * 0.5f;
        var dot = new Vector2(pos.X + (14f * scale) + (3.5f * scale), centreY);
        SilkPaint.Glow(dot - new Vector2(3.5f * scale), dot + new Vector2(3.5f * scale), 8f * scale, 0f, SilkPalette.Ok, 3.5f * scale);
        SilkPaint.Circle(dot, 3.5f * scale, SilkPalette.Ok);

        var w = SmallButtonWidth(button, 12f, SilkWeight.SemiBold);
        var buttonX = max.X - (14f * scale) - w;
        var textX = dot.X + (3.5f * scale) + (10f * scale);

        SilkText.DrawInBox(new Vector2(textX, pos.Y), new Vector2(buttonX - (10f * scale), max.Y), text, 12.5f, SilkWeight.Medium, SilkPalette.Ink, UiAlign.Start, 0f, false, 0f);

        ImGui.PushID(id);
        var clicked = SmallButton(button, new Vector2(buttonX, centreY - (14f * scale)), w, 28f, 12f, SilkWeight.SemiBold, SilkPalette.Ink2, SilkPalette.Ink, SilkPalette.HoverWash);
        ImGui.PopID();

        return clicked;
    }

    public static bool Danger(string id, string title, string text, ref bool value, Vector2 pos, float width, out float height)
    {
        var scale = SilkUi.Scale;
        var padX = 15f * scale;
        var textWidth = width - (padX * 2f) - (14f * scale) - (SwitchWidth * scale);
        var titleHeight = SilkText.ParagraphHeight(title, 13f, SilkWeight.Bold, textWidth, 1.4f);
        var bodyHeight = SilkText.ParagraphHeight(text, 12f, SilkWeight.Regular, textWidth, 1.4f);
        var content = MathF.Max(titleHeight + (2f * scale) + bodyHeight, SwitchHeight * scale);
        height = content + (28f * scale);

        var max = pos + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(pos, max, SilkPalette.DangerBg, radius);
        SilkPaint.InsetRing(pos, max, SilkPalette.DangerRing, radius, scale);

        var top = pos.Y + (14f * scale) + ((content - (titleHeight + (2f * scale) + bodyHeight)) * 0.5f);
        SilkText.DrawParagraph(new Vector2(pos.X + padX, top), title, 13f, SilkWeight.Bold, SilkPalette.Ink, textWidth, 1.4f);
        SilkText.DrawParagraph(new Vector2(pos.X + padX, top + titleHeight + (2f * scale)), text, 12f, SilkWeight.Regular, SilkPalette.Ink3, textWidth, 1.4f);

        var switchPos = new Vector2(max.X - padX - (SwitchWidth * scale), MathF.Round(((pos.Y + max.Y) * 0.5f) - (SwitchHeight * scale * 0.5f)));
        return Switch(id, ref value, switchPos, true);
    }

    public static float SmallButtonWidth(string label, float cssPx, SilkWeight weight)
        => MathF.Ceiling(SilkText.Width(VisibleLabel(label), cssPx, weight) + (24f * SilkUi.Scale));

    public static bool SmallButton(string label, Vector2 pos, float width, float heightCss, float cssPx, SilkWeight weight, Vector4 text, Vector4 hoverText, Vector4 hoverFill)
    {
        var scale = SilkUi.Scale;
        var size = new Vector2(width, heightCss * scale);
        var max = pos + size;

        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton(label, size);
        var hovered = ImGui.IsItemHovered();

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            SilkPaint.Fill(pos, max, hoverFill, 8f * scale);
        }

        SilkPaint.InsetRing(pos, max, SilkPalette.Line2, 8f * scale, scale);
        SilkText.DrawInBox(pos, max, VisibleLabel(label), cssPx, weight, hovered ? hoverText : text, UiAlign.Center);
        return clicked;
    }

    #endregion
}

public ref struct SilkRows
{
    private const int MaxRows = 64;

    private static readonly float[] RowTops = new float[MaxRows + 1];

    private readonly Vector2 origin;
    private readonly float width;
    private readonly ImDrawListPtr drawList;
    private int count;
    private float y;
    private bool split;

    private SilkRows(Vector2 origin, float width)
    {
        this.origin = origin;
        this.width = width;
        y = origin.Y;
        count = 0;
        drawList = ImGui.GetWindowDrawList();
        split = false;

        if (!drawList.IsNull)
        {
            drawList.ChannelsSplit(2);
            drawList.ChannelsSetCurrent(1);
            split = true;
        }
    }

    public static SilkRows Begin() => new(ImGui.GetCursorScreenPos(), ImGui.GetContentRegionAvail().X);

    public static SilkRows Begin(Vector2 origin, float width) => new(origin, width);

    public readonly float Width => width;

    public readonly float ControlColumn => MathF.Min(SilkControls.RowControlMax * SilkUi.Scale, width * SilkControls.RowControlShare);

    public SilkRow Row(string name, string? help, float controlHeightCss, string? helpId = null)
    {
        var scale = SilkUi.Scale;
        var control = ControlColumn;
        var helpWidth = SilkControls.RowHelpWidth * scale;
        var nameWidth = MathF.Max(1f, width - control - helpWidth - (15f * scale));
        var nameHeight = SilkText.ParagraphHeight(name, 13f, SilkWeight.SemiBold, nameWidth, 1.4f);
        var controlHeight = controlHeightCss * scale;
        var height = MathF.Max(SilkControls.RowMinHeight * scale, MathF.Max(nameHeight, controlHeight));
        var top = y;

        if (count < MaxRows)
            RowTops[count] = top;

        count++;

        SilkText.DrawParagraph(new Vector2(origin.X + (15f * scale), top + ((height - nameHeight) * 0.5f)), name, 13f, SilkWeight.SemiBold, SilkPalette.Ink, nameWidth, 1.4f);

        var columnX = origin.X + width - helpWidth - control;
        var centreY = top + (height * 0.5f);

        if (!string.IsNullOrEmpty(help))
            SilkControls.HelpMark(helpId ?? name, help, new Vector2(origin.X + width - (helpWidth * 0.5f), centreY));

        y += height;

        return new SilkRow(
            new Vector2(origin.X, top),
            new Vector2(origin.X + width, top + height),
            new Vector2(columnX + (16f * scale), MathF.Round(centreY - (controlHeight * 0.5f))),
            control - (16f * scale),
            controlHeight);
    }

    public void Space(float heightCss) => y += heightCss * SilkUi.Scale;

    public void Dispose() => End();

    public void End()
    {
        var scale = SilkUi.Scale;
        var max = new Vector2(origin.X + width, y);

        if (split)
        {
            drawList.ChannelsSetCurrent(0);
            NoireShapes.On(drawList, (origin, max), static s => SilkControls.Card(s.origin, s.max));

            var rows = Math.Min(count, MaxRows);

            for (var i = 1; i < rows; i++)
                drawList.AddRectFilled(new Vector2(origin.X, RowTops[i]), new Vector2(max.X, RowTops[i] + scale), SilkPalette.U32(SilkPalette.Line));

            drawList.ChannelsMerge();
            split = false;
        }

        SilkControls.Advance(origin, width, y - origin.Y);
    }
}

public readonly record struct SilkRow(Vector2 Min, Vector2 Max, Vector2 ControlMin, float ControlWidth, float ControlHeight)
{
    public Vector2 RightAligned(float widthCss) => new(ControlMin.X + ControlWidth - (widthCss * SilkUi.Scale), ControlMin.Y);
}
