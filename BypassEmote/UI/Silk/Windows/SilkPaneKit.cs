using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Windows;

internal sealed class SilkScrollPane
{
    private const float WheelStep = 60f;

    private bool dragging;
    private float dragStartMouse;
    private float dragStartOffset;

    public float Offset { get; private set; }

    public float Content { get; private set; }

    public float View { get; private set; }

    public bool Overflows => Content > View + 0.5f;

    public void Reset() => Offset = 0f;

    public void EnsureVisible(float contentTop, float contentBottom)
    {
        if (contentTop < Offset)
            Offset = contentTop;
        else if (contentBottom > Offset + View)
            Offset = contentBottom - View;
    }

    public void Update(string id, Vector2 min, Vector2 max, float content)
    {
        View = max.Y - min.Y;
        Content = content;

        var mouse = ImGui.GetMousePos();

        if (SilkPaint.Contains(min, max, mouse) && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
        {
            var wheel = ImGui.GetIO().MouseWheel;

            if (wheel != 0f)
                Offset -= wheel * WheelStep * SilkUi.Scale;
        }

        if (Overflows)
        {
            var (thumbMin, thumbMax) = Thumb(min, max);
            var hitMin = new Vector2(max.X - (10f * SilkUi.Scale), min.Y);

            ImGui.SetCursorScreenPos(hitMin);
            ImGui.InvisibleButton(id, new Vector2(max.X - hitMin.X, MathF.Max(1f, View)));

            if (ImGui.IsItemActivated())
            {
                dragging = true;
                dragStartMouse = mouse.Y;
                dragStartOffset = Offset;

                if (mouse.Y < thumbMin.Y || mouse.Y > thumbMax.Y)
                {
                    var ratio = (mouse.Y - min.Y) / MathF.Max(1f, View);
                    Offset = (ratio * Content) - (View * 0.5f);
                    dragStartOffset = Offset;
                }
            }

            if (dragging && ImGui.IsItemActive())
            {
                var track = View - (thumbMax.Y - thumbMin.Y);
                var range = Content - View;

                if (track > 0f)
                    Offset = dragStartOffset + ((mouse.Y - dragStartMouse) * range / track);
            }
            else
            {
                dragging = false;
            }
        }

        Offset = Math.Clamp(Offset, 0f, MathF.Max(0f, Content - View));
    }

    public void DrawBar(Vector2 min, Vector2 max)
    {
        if (!Overflows)
            return;

        var (thumbMin, thumbMax) = Thumb(min, max);
        SilkPaint.Fill(thumbMin, thumbMax, SilkPalette.Rgb(191, 211, 236, 0.15f), 3f * SilkUi.Scale);
    }

    private (Vector2 Min, Vector2 Max) Thumb(Vector2 min, Vector2 max)
    {
        var scale = SilkUi.Scale;
        var height = MathF.Max(24f * scale, View * View / MathF.Max(1f, Content));
        var range = MathF.Max(1f, Content - View);
        var top = min.Y + ((View - height) * Math.Clamp(Offset / range, 0f, 1f));
        var right = max.X - (2f * scale);
        return (new Vector2(right - (6f * scale), top), new Vector2(right, top + height));
    }
}

internal static class SilkPaneKit
{
    private static readonly Vector4 BtnHoverBg = SilkPalette.Rgb(191, 211, 236, 0.07f);

    public static float Round(float value) => MathF.Round(value);

    public static bool Hit(string id, Vector2 min, Vector2 max, out bool hovered, out bool held)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, new Vector2(MathF.Max(1f, max.X - min.X), MathF.Max(1f, max.Y - min.Y)));
        hovered = ImGui.IsItemHovered();
        held = ImGui.IsItemActive();

        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return clicked;
    }

    public static bool Switch(string animKey, string id, Vector2 min, bool on)
    {
        var s = SilkUi.Scale;
        var max = min + new Vector2(40f * s, 22f * s);
        var clicked = Hit(id, min, max, out _, out _);

        var t = SilkUi.Ease(animKey, id, on ? 1f : 0f, 0.35f);
        var bg = SilkUi.Css(animKey, id + ".bg", on ? 1f : 0f, 0.3f);
        var radius = 11f * s;

        if (bg < 1f)
        {
            SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.SwitchOff, 1f - bg), radius);
            SilkPaint.InsetRing(min, max, SilkPalette.Fade(SilkPalette.Line2, 1f - bg), radius, s);
        }

        if (bg > 0f)
        {
            SilkPaint.Glow(min, max, 16f * s, -4f * s, SilkPalette.Fade(SilkPalette.Ice, bg), radius);
            SilkPaint.HorizontalGradient(min, max, SilkPalette.Fade(SilkPalette.Vio, bg), SilkPalette.Fade(SilkPalette.Ice, bg), radius);
        }

        var knob = 16f * s;
        var knobMin = min + new Vector2((3f + (18f * t)) * s, 3f * s);
        SilkPaint.Fill(knobMin, knobMin + new Vector2(knob, knob), SilkPalette.Mix(SilkPalette.SwitchKnob, SilkPalette.White, bg), knob * 0.5f);

        return clicked;
    }

    public static float ButtonWidth(string text, SilkIconShape? icon, float padX, float textCss, float iconCss, float gap)
    {
        var s = SilkUi.Scale;
        var width = (padX * 2f * s) + SilkText.Width(text, textCss, SilkWeight.Bold);

        if (icon != null)
            width += (iconCss + gap) * s;

        return width;
    }

    public static bool Button(string id, Vector2 min, Vector2 max, string text, SilkIconShape? icon, bool enabled = true,
        float padX = 16f, float textCss = 13f, float iconCss = 14f, float gap = 8f, float radius = 10f, Vector4? hoverText = null, float fill = 0f, float press = 1f,
        float iconSpin = 0f, SilkIconShape? liftedIcon = null, float lift = 0f)
    {
        var s = SilkUi.Scale;
        var clicked = Hit(id, min, max, out var hovered, out _);

        if (!enabled)
            clicked = false;

        var active = enabled && hovered;
        var color = active ? hoverText ?? SilkPalette.Ink : SilkPalette.Ink2;

        if (press != 1f)
        {
            var centre = (min + max) * 0.5f;
            min = centre + ((min - centre) * press);
            max = centre + ((max - centre) * press);
        }

        var r = radius * s;

        if (active)
            SilkPaint.Fill(min, max, BtnHoverBg, r);

        if (fill > 0f)
        {
            PushClip(min, new Vector2(min.X + ((max.X - min.X) * Math.Clamp(fill, 0f, 1f)), max.Y));
            SilkPaint.Fill(min, max, SilkPalette.HoldFill, r);
            PopClip();
        }

        SilkPaint.InsetRing(min, max, SilkPalette.Line2, r, s);

        var x = min.X + (padX * s);

        if (icon != null)
        {
            var size = iconCss * s;
            var iconPos = new Vector2(x, MathF.Round(((min.Y + max.Y) * 0.5f) - (size * 0.5f)));
            var list = NoireLib.UI.NoireShapes.DrawList;
            var startVertex = list.IsNull ? 0 : list.VtxBuffer.Size;
            SilkIcons.Draw(icon, iconPos, size, color);

            if (iconSpin != 0f && !list.IsNull)
                Main.SilkMainDraw.Transform(list, startVertex, iconPos + new Vector2(size * 0.5f), Vector2.One, iconSpin, Vector2.Zero);

            if (liftedIcon != null)
                SilkIcons.Draw(liftedIcon, iconPos - new Vector2(0f, lift * s), size, color);

            x += size + (gap * s);
        }

        SilkText.Draw(new Vector2(x, SilkText.GlyphTop(min.Y, max.Y - min.Y, textCss, SilkWeight.Bold)), text, textCss, SilkWeight.Bold, color);
        return clicked;
    }

    public static bool PrimaryButton(string id, Vector2 min, Vector2 max, string text, bool enabled)
    {
        var s = SilkUi.Scale;
        var clicked = Hit(id, min, max, out var hovered, out _) && enabled;
        var r = 10f * s;
        var alpha = enabled ? 1f : 0.4f;

        SilkPaint.BoxShadow(min, max, new Vector2(0f, 3f * s), 8f * s, -4f * s, SilkPalette.Fade(SilkPalette.PriShadow, alpha), r);
        SilkPaint.Fill(min, max, SilkPalette.Fade(enabled && hovered ? SilkPalette.PriHover : SilkPalette.Ice, alpha), r);
        SilkPaint.InsetRing(min, max, SilkPalette.Fade(SilkPalette.PriRing, alpha), r, s);
        SilkText.DrawInBox(min, max, text, 13f, SilkWeight.Bold, SilkPalette.Fade(SilkPalette.PriText, alpha), NoireLib.UI.UiAlign.Center);
        return clicked;
    }

    public static void PushClip(Vector2 min, Vector2 max) => ImGui.PushClipRect(min, max, true);

    public static void PopClip() => ImGui.PopClipRect();

    public static SilkIconShape Icon(SilkIcon icon) => SilkIcons.Get(icon);

    public static string Cached(Dictionary<int, string> cache, int value)
    {
        if (!cache.TryGetValue(value, out var text))
        {
            text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            cache[value] = text;
        }

        return text;
    }
}
