using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public static class SilkTooltip
{
    private const float Delay = 0f;
    private const float MaxWidth = 280f;
    private const float PadX = 10f;
    private const float PadY = 6f;
    private const float FontSize = 11.5f;
    private const float LineHeight = 1.45f;

    private static readonly List<string> Lines = [];

    private static int requestFrame = -1;
    private static string? requestText;
    private static Vector2 requestMin;
    private static Vector2 requestMax;
    private static bool requestNow;

    private static string? targetText;
    private static Vector2 targetMin;
    private static float targetSince;
    private static bool dismissed;

    private static string? shownText;
    private static Vector2 shownMin;
    private static Vector2 shownMax;
    private static float visibility;
    private static float shownAt;
    private static bool showing;

    private static string? wrappedText;
    private static float wrappedScale;
    private static Vector2 wrappedSize;

    public static void Hover(string? text, Vector2 min, Vector2 max, bool immediate = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        requestFrame = ImGui.GetFrameCount();
        requestText = text;
        requestMin = min;
        requestMax = max;
        requestNow = immediate;
    }

    public static bool HoverItem(string? text, bool immediate = false)
    {
        if (!ImGui.IsItemHovered())
            return false;

        Hover(text, ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), immediate);
        return true;
    }

    public static void Dismiss() => dismissed = true;

    public static void Render()
    {
        if (!NoireLib.NoireService.IsInitialized())
            return;

        if (!SilkUi.Active && !showing && targetText == null)
            return;

        var now = SilkUi.Time;
        var frame = ImGui.GetFrameCount();
        var requested = requestFrame >= frame - 1 && requestText != null && SilkUi.Active;

        if (!requested)
        {
            targetText = null;
            dismissed = false;
        }
        else if (!ReferenceEquals(requestText, targetText) || Vector2.DistanceSquared(requestMin, targetMin) > 1f)
        {
            targetText = requestText;
            targetMin = requestMin;
            targetSince = now;
            dismissed = false;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.GetIO().MouseWheel != 0f)
            dismissed = true;

        var wanted = requested && !dismissed && (requestNow || now - targetSince >= Delay);

        if (wanted)
        {
            if (!showing || !ReferenceEquals(shownText, requestText) || Vector2.DistanceSquared(shownMin, requestMin) > 1f)
                shownAt = now;

            shownText = requestText;
            shownMin = requestMin;
            shownMax = requestMax;
            showing = true;
        }
        else
        {
            showing = false;
        }

        visibility = SilkUi.EaseLinear("SilkTooltip", "on", showing ? 1f : 0f, 0.14f);

        if (visibility <= 0.001f || shownText == null)
            return;

        var slide = SilkUi.ReducedMotion ? 1f : UiCubicBezier.Ease.Evaluate(Math.Clamp((now - shownAt) / 0.16f, 0f, 1f));

        NoireShapes.On(ImGui.GetForegroundDrawList(), (visibility, slide), static s => Paint(s.visibility, s.slide));
    }

    private static void Paint(float alpha, float slide)
    {
        var text = shownText!;
        var scale = SilkUi.Scale;
        var size = Measure(text);
        var display = ImGui.GetIO().DisplaySize;
        var margin = 8f * scale;
        var gap = 10f * scale;
        var centreX = (shownMin.X + shownMax.X) * 0.5f;
        var left = Math.Clamp(centreX - (size.X * 0.5f), margin, MathF.Max(margin, display.X - size.X - margin));
        var below = shownMin.Y - gap - size.Y < margin;
        var top = below ? shownMax.Y + gap : shownMin.Y - gap - size.Y;
        var offset = (1f - slide) * 3f * scale * (below ? -1f : 1f);
        var min = new Vector2(MathF.Round(left), MathF.Round(top + offset));
        var max = min + size;
        var radius = 8f * scale;
        var arrowX = min.X + (centreX - left);

        SilkPaint.BoxShadow(min, max, new Vector2(0f, 10f * scale), 30f * scale, -6f * scale, SilkPalette.Fade(SilkPalette.TipShadow, alpha), radius);
        SilkPaint.OuterRing(min, max, SilkPalette.Fade(SilkPalette.TipRing, alpha), radius, scale);
        SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.TipBg, alpha), radius);

        var half = 4f * MathF.Sqrt(2f) * scale;
        var edge = below ? min.Y : max.Y;
        var shift = (below ? -1f : 1f) * MathF.Sqrt(2f) * scale;

        Diamond(new Vector2(arrowX, edge + shift), half, SilkPalette.Fade(SilkPalette.TipRing, alpha));
        Diamond(new Vector2(arrowX, edge), half, SilkPalette.Fade(SilkPalette.TipBg, alpha));

        var line = SilkText.LineBox(FontSize, LineHeight);
        var y = min.Y + (PadY * scale);
        var color = SilkPalette.Fade(SilkPalette.TipText, alpha);

        foreach (var row in Lines)
        {
            SilkText.Draw(new Vector2(left + (PadX * scale), y), row, FontSize, SilkWeight.Medium, color);
            y += line;
        }
    }

    private static Vector2 Measure(string text)
    {
        var key = SilkUi.Scale * SilkUi.TextScale;

        if (ReferenceEquals(text, wrappedText) && MathF.Abs(key - wrappedScale) < 0.0001f)
            return wrappedSize;

        var scale = SilkUi.Scale;
        var inner = (MaxWidth - (PadX * 2f)) * scale;

        SilkText.Wrap(text, FontSize, SilkWeight.Medium, inner, Lines);

        var widest = 0f;

        foreach (var row in Lines)
            widest = MathF.Max(widest, SilkText.Width(row, FontSize, SilkWeight.Medium));

        var width = MathF.Ceiling(MathF.Min(inner, widest) + (PadX * 2f * scale));
        var height = MathF.Ceiling((Lines.Count * SilkText.LineBox(FontSize, LineHeight)) + (PadY * 2f * scale));

        wrappedText = text;
        wrappedScale = key;
        wrappedSize = new Vector2(width, height);
        return wrappedSize;
    }

    private static void Diamond(Vector2 centre, float half, Vector4 color)
    {
        Span<Vector2> points =
        [
            new(centre.X, centre.Y - half),
            new(centre.X + half, centre.Y),
            new(centre.X, centre.Y + half),
            new(centre.X - half, centre.Y),
        ];

        NoireShapes.Fill(points, color);
    }
}
