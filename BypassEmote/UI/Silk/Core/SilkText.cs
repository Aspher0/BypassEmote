using Dalamud.Bindings.ImGui;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public enum SilkWeight
{
    Regular = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
}

public static class SilkText
{
    public static SilkFace Face(SilkWeight weight, bool mono = false)
    {
        if (mono)
            return weight >= SilkWeight.SemiBold ? SilkFace.MonoBold
                : weight >= SilkWeight.Medium ? SilkFace.Mono500
                : SilkFace.Mono400;

        return weight switch
        {
            SilkWeight.Regular => SilkFace.Ui400,
            SilkWeight.Medium => SilkFace.Ui500,
            SilkWeight.SemiBold => SilkFace.Ui600,
            SilkWeight.Bold => SilkFace.Ui700,
            _ => SilkFace.Ui800,
        };
    }

    public static Vector2 Measure(string text, float cssPx, SilkWeight weight = SilkWeight.Medium, float trackingCss = 0f, bool mono = false, float maxWidth = 0f)
    {
        var face = Face(weight, mono);

        if (string.IsNullOrEmpty(text))
            return new Vector2(0f, SilkFonts.LineHeight(face, cssPx));

        return SilkFonts.Measure(face, cssPx, text, trackingCss, maxWidth);
    }

    public static float Width(string text, float cssPx, SilkWeight weight = SilkWeight.Medium, float trackingCss = 0f, bool mono = false)
        => Measure(text, cssPx, weight, trackingCss, mono).X;

    public static Vector2 Draw(Vector2 topLeft, string text, float cssPx, SilkWeight weight, Vector4 color, float trackingCss = 0f, bool mono = false, float maxWidth = 0f)
    {
        if (string.IsNullOrEmpty(text) || color.W <= 0f)
            return Measure(text, cssPx, weight, trackingCss, mono, maxWidth);

        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return Vector2.Zero;

        var face = Face(weight, mono);
        var at = new Vector2(MathF.Round(topLeft.X), Snap(face, cssPx, topLeft.Y));

        return SilkFonts.Draw(drawList, at, SilkPalette.U32(color), face, cssPx, text, trackingCss, maxWidth);
    }

    private static float Snap(SilkFace face, float cssPx, float top)
    {
        var baseline = MathF.Round(top + Ascent(face, cssPx));
        var rendered = SilkFonts.Face(face) is { } font ? font.RenderedAscent(SilkFonts.Em(cssPx)) : SilkFonts.Ascent(face, cssPx);

        return MathF.Round(baseline - rendered);
    }

    private static float Ascent(SilkFace face, float cssPx) => MathF.Round(SilkFonts.Ascent(face, cssPx));

    private static float Content(SilkFace face, float cssPx)
    {
        var ascent = SilkFonts.Ascent(face, cssPx);

        return MathF.Round(ascent) + MathF.Round(SilkFonts.LineHeight(face, cssPx) - ascent);
    }

    public static float Ascent(float cssPx, SilkWeight weight = SilkWeight.Medium, bool mono = false)
        => SilkFonts.Ascent(Face(weight, mono), cssPx);

    public static float NaturalLine(float cssPx, SilkWeight weight = SilkWeight.Medium, bool mono = false)
        => SilkFonts.LineHeight(Face(weight, mono), cssPx);

    public static float LineBox(float cssPx, float lineHeight = 0f, bool mono = false, SilkWeight weight = SilkWeight.Medium)
        => lineHeight > 0f ? SilkFonts.LineBox(cssPx, lineHeight) : Content(Face(weight, mono), cssPx);

    public static float GlyphTop(float boxTop, float boxHeight, float cssPx, SilkWeight weight = SilkWeight.Medium, float lineHeight = 0f, bool mono = false)
    {
        var face = Face(weight, mono);
        var line = lineHeight > 0f ? SilkFonts.LineBox(cssPx, lineHeight) : Content(face, cssPx);
        var lineTop = boxTop + ((boxHeight - line) * 0.5f);

        return lineTop + MathF.Floor((line - Content(face, cssPx)) * 0.5f);
    }

    public static float Baseline(float boxTop, float boxHeight, float cssPx, SilkWeight weight = SilkWeight.Medium, float lineHeight = 0f, bool mono = false)
        => GlyphTop(boxTop, boxHeight, cssPx, weight, lineHeight, mono) + Ascent(Face(weight, mono), cssPx);

    public static Vector2 DrawCentered(Vector2 min, Vector2 max, string text, float cssPx, SilkWeight weight, Vector4 color, float trackingCss = 0f, bool mono = false)
        => DrawInBox(min, max, text, cssPx, weight, color, UiAlign.Center, trackingCss, mono);

    public static Vector2 DrawInBox(Vector2 min, Vector2 max, string text, float cssPx, SilkWeight weight, Vector4 color, UiAlign align = UiAlign.Start,
        float trackingCss = 0f, bool mono = false, float lineHeight = 0f)
    {
        var width = max.X - min.X;
        var size = Measure(text, cssPx, weight, trackingCss, mono);
        var limit = 0f;

        if (size.X > width + 0.5f)
        {
            limit = MathF.Max(1f, width);
            size = Measure(text, cssPx, weight, trackingCss, mono, limit);

            if (ImGui.IsMouseHoveringRect(min, max))
                SilkTooltip.Hover(text, min, max);
        }

        var x = align switch
        {
            UiAlign.End => max.X - size.X,
            UiAlign.Center => min.X + ((width - size.X) * 0.5f),
            _ => min.X,
        };
        var y = GlyphTop(min.Y, max.Y - min.Y, cssPx, weight, lineHeight, mono);
        return Draw(new Vector2(x, y), text, cssPx, weight, color, trackingCss, mono, limit);
    }

    public static void Wrap(string text, float cssPx, SilkWeight weight, float maxWidth, List<string> lines, bool mono = false, float trackingCss = 0f)
    {
        lines.Clear();

        if (string.IsNullOrEmpty(text))
            return;

        foreach (var paragraph in text.Split('\n'))
        {
            if (paragraph.Length == 0 || maxWidth <= 0f || Width(paragraph, cssPx, weight, trackingCss, mono) <= maxWidth)
            {
                lines.Add(paragraph);
                continue;
            }

            LineBreakHelper.Break(paragraph, maxWidth, part => Width(part, cssPx, weight, trackingCss, mono), lines);
        }
    }

    private sealed class WrapEntry
    {
        public float Width;
        public float Scale;
        public float Css;
        public SilkWeight Weight;
        public bool Mono;
        public readonly List<string> Lines = [];
    }

    private static readonly Dictionary<string, WrapEntry> WrapCache = new(ReferenceEqualityComparer.Instance);

    public static List<string> Lines(string text, float cssPx, SilkWeight weight, float maxWidth, bool mono = false)
    {
        var scale = SilkUi.Scale * SilkFonts.TextScale;

        if (WrapCache.TryGetValue(text, out var entry)
            && MathF.Abs(entry.Width - maxWidth) < 0.5f && MathF.Abs(entry.Scale - scale) < 0.0001f
            && entry.Css == cssPx && entry.Weight == weight && entry.Mono == mono)
            return entry.Lines;

        if (entry == null)
        {
            if (WrapCache.Count > 1024)
                WrapCache.Clear();

            entry = new WrapEntry();
            WrapCache[text] = entry;
        }

        Wrap(text, cssPx, weight, maxWidth, entry.Lines, mono);
        entry.Width = maxWidth;
        entry.Scale = scale;
        entry.Css = cssPx;
        entry.Weight = weight;
        entry.Mono = mono;
        return entry.Lines;
    }

    public static float ParagraphHeight(string text, float cssPx, SilkWeight weight, float maxWidth, float lineHeight = 0f, bool mono = false)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        return Lines(text, cssPx, weight, maxWidth, mono).Count * LineBox(cssPx, lineHeight, mono, weight);
    }

    public static float DrawParagraph(Vector2 topLeft, string text, float cssPx, SilkWeight weight, Vector4 color, float maxWidth, float lineHeight = 0f,
        bool mono = false, UiAlign align = UiAlign.Start)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        var lines = Lines(text, cssPx, weight, maxWidth, mono);
        var line = LineBox(cssPx, lineHeight, mono, weight);
        var y = topLeft.Y;

        foreach (var row in lines)
        {
            var x = topLeft.X;

            if (align != UiAlign.Start)
            {
                var width = Width(row, cssPx, weight, 0f, mono);
                x += align == UiAlign.End ? maxWidth - width : (maxWidth - width) * 0.5f;
            }

            Draw(new Vector2(x, GlyphTop(y, line, cssPx, weight, lineHeight, mono)), row, cssPx, weight, color, 0f, mono);
            y += line;
        }

        return y - topLeft.Y;
    }

    public static Vector2 DrawOnBaseline(float x, float baseline, string text, float cssPx, SilkWeight weight, Vector4 color, float trackingCss = 0f, bool mono = false, float maxWidth = 0f)
        => Draw(new Vector2(x, baseline - Ascent(Face(weight, mono), cssPx)), text, cssPx, weight, color, trackingCss, mono, maxWidth);
}
