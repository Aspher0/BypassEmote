using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.Enums;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed class SilkRowCards
{
    internal const float Width = 300f;
    internal const float Pad = 12f;

    private SilkEmoteEntry? pending;
    private float hoverSince;
    private int hoverFrame = -10;
    private bool suppressed;
    private Vector2 anchorMin;
    private Vector2 anchorMax;

    private SilkEmoteEntry? shown;
    private Vector2 shownAnchorMin;
    private Vector2 shownAnchorMax;
    private Tween presence;
    private Tween lift;

    private readonly List<List<string>> sourceLines = new();
    private SilkEmoteEntry? linesFor;
    private int linesSources = -1;
    private float linesWidth;
    private float linesScale;

    internal void Hover(SilkEmoteEntry entry, Vector2 min, Vector2 max, float now, bool immediate = false)
    {
        hoverFrame = ImGui.GetFrameCount();
        anchorMin = min;
        anchorMax = max;

        if (ReferenceEquals(entry, pending))
            return;

        pending = entry;
        hoverSince = immediate ? now - 1f : now;
        suppressed = false;
        FadeOut(now);
    }

    internal void Leave()
    {
        if (pending == null)
            return;

        pending = null;
        FadeOut(SilkUi.Time);
    }

    internal void Hide()
    {
        suppressed = true;
        FadeOut(SilkUi.Time);
    }

    private void FadeOut(float now)
    {
        if (presence.Target <= 0f)
            return;

        presence.Go(0f, now, SilkUi.ReducedMotion ? 0f : 0.15f, UiCubicBezier.Ease);
        lift.Go(4f, now, SilkUi.ReducedMotion ? 0f : 0.2f, SilkUi.EaseOut);
    }

    internal void Draw(SilkMainPainter view, ImDrawListPtr fg, float s, float now, bool reduced)
    {
        if (pending != null && hoverFrame < ImGui.GetFrameCount() - 1)
        {
            pending = null;
            FadeOut(now);
        }

        if (pending != null && !suppressed && presence.Target <= 0f)
        {
            shown = pending;
            shownAnchorMin = anchorMin;
            shownAnchorMax = anchorMax;
            presence.Snap(0f);
            lift.Snap(4f);
            presence.Go(1f, now, reduced ? 0f : 0.15f, UiCubicBezier.Ease);
            lift.Go(0f, now, reduced ? 0f : 0.2f, SilkUi.EaseOut);
        }

        var alpha = presence.Value(now);

        if (shown == null || alpha <= 0.001f)
            return;

        var height = DrawCard(view, default, shown, Vector2.Zero, s, false) + Pad * s;
        var width = Width * s;
        var viewport = ImGui.GetMainViewport();
        var vMin = viewport.Pos;
        var vMax = viewport.Pos + viewport.Size;
        var x = shownAnchorMax.X + 10f * s;

        if (x + width > vMax.X - 8f * s)
            x = shownAnchorMin.X - width - 10f * s;

        if (x < vMin.X + 8f * s)
            x = MathF.Min(vMax.X - width - 8f * s, MathF.Max(vMin.X + 8f * s, shownAnchorMin.X));

        var y = (shownAnchorMin.Y + shownAnchorMax.Y) * 0.5f - height * 0.5f;
        y = MathF.Max(vMin.Y + 8f * s, MathF.Min(vMax.Y - height - 8f * s, y));
        y += lift.Value(now) * s;

        var min = new Vector2(MathF.Round(x), MathF.Round(y));
        var max = min + new Vector2(width, height);
        var start = fg.VtxBuffer.Size;
        var r = 12f * s;

        SilkMainDraw.Shadow(fg, min, max, r, SilkMainDraw.Col(0, 0, 0, 0.95f), 50f * s, -12f * s, new Vector2(0f, 20f * s));
        fg.AddRectFilled(min, max, SilkMainDraw.Col(10, 15, 26, 0.98f), r);
        SilkMainDraw.OuterRing(fg, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.16f));

        DrawCard(view, fg, shown, min, s);

        SilkMainDraw.MultiplyAlpha(fg, start, alpha);
    }

    private static float HeaderHeight()
        => SilkFonts.LineHeight(SilkFace.Ui700, 13.5f) + SilkFonts.LineBox(13f, 1.4f);

    private static float DrawHeader(ImDrawListPtr fg, SilkEmoteEntry entry, Vector2 min, float s, string bold, string code, bool draw)
    {
        var headH = HeaderHeight();
        var x = min.X + Pad * s;
        var y = min.Y + Pad * s;

        if (!draw)
            return y + headH;

        var iconTop = y + (headH - 34f * s) * 0.5f;

        if (SilkGameIcon.Draw(fg, entry.IconId, new Vector2(x, iconTop), new Vector2(x + 34f * s, iconTop + 34f * s), 8f * s))
            x += 44f * s;

        var boldH = SilkFonts.LineHeight(SilkFace.Ui700, 13.5f);
        var maxW = min.X + (Width - Pad) * s - x;
        SilkFonts.Draw(fg, new Vector2(MathF.Round(x), MathF.Round(y)), SilkPalette.U32(SilkPalette.Ink), SilkFace.Ui700, 13.5f, bold, 0f, maxW);

        var lineBox = SilkFonts.LineBox(13f, 1.4f);
        var codeLh = SilkFonts.LineHeight(SilkFace.Mono400, 11f);
        SilkFonts.Draw(fg, new Vector2(MathF.Round(x), MathF.Round(y + boldH + (lineBox - codeLh) * 0.5f)), SilkPalette.U32(SilkPalette.Ink3),
            SilkFace.Mono400, 11f, code, 0f, maxW);

        return y + headH;
    }

    private static float DrawHeading(ImDrawListPtr fg, string text, float x, float top, float s, bool draw)
    {
        top += 11f * s;

        if (draw)
            SilkFonts.Draw(fg, new Vector2(MathF.Round(x), MathF.Round(top)), SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui700, 9.5f, SilkFonts.Upper(text), 1f);

        return top + SilkFonts.LineHeight(SilkFace.Ui700, 9.5f) + 6f * s;
    }

    private static float Separator(ImDrawListPtr fg, float x, float right, float top, float s, bool draw)
    {
        top += 9f * s;

        if (draw)
            fg.AddRectFilled(new Vector2(x, top), new Vector2(right, top + 1f * s), SilkMainDraw.Col(191, 211, 236, 0.09f));

        return top + 1f * s + 8f * s;
    }

    private float DrawCard(SilkMainPainter view, ImDrawListPtr fg, SilkEmoteEntry entry, Vector2 min, float s, bool draw = true)
    {
        EnsureLines(entry, s);

        var y = DrawHeader(fg, entry, min, s, entry.Name, entry.AddedLabel, draw);
        var x = min.X + Pad * s;
        var right = min.X + (Width - Pad) * s;

        y = DrawHeading(fg, L.PlayableWhile.Text, x, y, s, draw);

        if (draw)
        {
            var conditions = entry.Conditions;
            var order = EmoteHelper.ConditionIconOrder;

            for (var i = 0; i < order.Count; i++)
                DrawConditionIcon(fg, order[i], new Vector2(x + i * 24f * s, y), 20f * s, (conditions & order[i]) == order[i]);
        }

        y += 20f * s;

        var sources = entry.Sources;

        if (sources.Length > 0)
        {
            y = DrawHeading(fg, L.Sources.Text, x, y, s, draw);
            var color = entry.Color;

            for (var i = 0; i < sources.Length; i++)
            {
                if (i > 0)
                    y += 4f * s;

                y = DrawSource(fg, sources[i].Type, sourceLines[i], color, x, y, s, draw);
            }
        }

        y = Separator(fg, x, right, y, s, draw);

        var (keys, texts) = SilkMainPainter.Hints();
        var spanH = SilkFonts.LineHeight(SilkFace.Ui500, 10.5f);
        y = view.DrawHintRow(fg, keys, texts, x, y, spanH, 9.5f, 10.5f, 5f, 3f, 4f, 10f * s, right, out _, 10f * s, draw);

        if (sources.Length > 0 && entry.OwnedShare is { } owned)
            y = DrawFoot(fg, owned, x, y + 9f * s, right, s, false, default, draw);

        return y;
    }

    private static void DrawConditionIcon(ImDrawListPtr dl, EmoteCondition condition, Vector2 min, float size, bool on)
    {
        if (EmoteHelper.GetConditionIcon(condition) is not { } icon)
            return;

        var width = size * icon.Size.X / MathF.Max(1f, icon.Size.Y);
        var at = new Vector2(min.X + (size - width) * 0.5f, min.Y);
        var tint = on ? SilkMainDraw.Col(255, 255, 255, 1f) : SilkMainDraw.Col(160, 160, 160, 0.14f);
        dl.AddImage(icon.Texture.Handle, at, at + new Vector2(width, size), icon.Uv0, icon.Uv1, tint);
    }

    private void EnsureLines(SilkEmoteEntry entry, float s)
    {
        var sources = entry.Sources;
        var textWidth = (Width - Pad * 2f) * s;

        if (ReferenceEquals(linesFor, entry) && linesSources == sources.Length && MathF.Abs(linesWidth - textWidth) < 0.5f
            && MathF.Abs(linesScale - SilkFonts.Em(1f)) < 0.001f)
        {
            return;
        }

        linesFor = entry;
        linesSources = sources.Length;
        linesWidth = textWidth;
        linesScale = SilkFonts.Em(1f);

        while (sourceLines.Count < Math.Max(1, sources.Length))
            sourceLines.Add(new List<string>());

        while (sourceLines.Count > Math.Max(1, sources.Length))
            sourceLines.RemoveAt(sourceLines.Count - 1);

        if (sources.Length == 0)
        {
            SilkText.Wrap(DefaultText, 12f, SilkWeight.Medium, textWidth - TagWidth(DefaultTag, s) - 8f * s, sourceLines[0]);
            return;
        }

        for (var i = 0; i < sources.Length; i++)
            SilkText.Wrap(sources[i].Text, 12f, SilkWeight.Medium, textWidth - TagWidth(sources[i].Type, s) - 8f * s, sourceLines[i]);
    }

    internal static string DefaultTag => L.DefaultTag.Text;
    internal static string DefaultText => L.DefaultText.Text;

    internal static float TagWidth(string type, float s)
        => SilkFonts.Measure(SilkFace.Ui700, 9.5f, SilkFonts.Upper(type), 0.6f).X + 12f * s;

    internal static float DrawSource(ImDrawListPtr fg, string type, List<string> lines, Vector3 color, float x, float top, float s, bool draw = true)
    {
        var upper = SilkFonts.Upper(type);
        var tagText = SilkFonts.Measure(SilkFace.Ui700, 9.5f, upper, 0.6f).X;
        var tagLh = SilkFonts.LineHeight(SilkFace.Ui700, 9.5f);
        var tagH = tagLh + 4f * s;
        var tagW = tagText + 12f * s;
        var line = SilkFonts.LineBox(12f, 1.35f);
        var textLh = SilkFonts.LineHeight(SilkFace.Ui500, 12f);
        var tagAbove = 2f * s + SilkFonts.Ascent(SilkFace.Ui700, 9.5f);
        var textAbove = (line - textLh) * 0.5f + SilkFonts.Ascent(SilkFace.Ui500, 12f);
        var baseline = top + MathF.Max(tagAbove, textAbove);
        var tagTop = baseline - tagAbove;
        var lineTop = baseline - textAbove;

        if (draw)
        {
            fg.AddRectFilled(new Vector2(x, tagTop), new Vector2(x + tagW, tagTop + tagH), SilkMainDraw.Col(color, 0.18f), 5f * s);
            SilkFonts.Draw(fg, new Vector2(MathF.Round(x + 6f * s), MathF.Round(tagTop + 2f * s)), SilkMainDraw.Col(color), SilkFace.Ui700, 9.5f, upper, 0.6f);

            var textX = x + tagW + 8f * s;

            for (var i = 0; i < lines.Count; i++)
            {
                SilkFonts.Draw(fg, new Vector2(MathF.Round(textX), MathF.Round(lineTop + (line - textLh) * 0.5f + i * line)),
                    SilkPalette.U32(SilkPalette.Ink), SilkFace.Ui500, 12f, lines[i]);
            }
        }

        return MathF.Max(tagTop + tagH, lineTop + line * Math.Max(1, lines.Count));
    }

    internal static float DrawFoot(ImDrawListPtr fg, string owned, float x, float top, float right, float s, bool dock,
        Vector3 accent = default, bool draw = true)
    {
        var px = dock ? 10.5f : 11f;

        if (draw)
        {
            fg.AddRectFilled(new Vector2(x, top), new Vector2(right, top + 1f * s), SilkMainDraw.Col(191, 211, 236, 0.09f));
            var textTop = top + 1f * s + 8f * s;
            var ink3 = SilkPalette.U32(SilkPalette.Ink3);
            var label = dock ? L.OpenPage.Text : L.FfxivCollect.Text;
            var rw = SilkFonts.FittedWidth(SilkFace.Ui500, px, label, (right - x) * 0.45f);
            SilkFonts.DrawFitted(fg, new Vector2(MathF.Round(right - rw), MathF.Round(textTop)), dock ? SilkMainDraw.Col(accent) : ink3, SilkFace.Ui500, px, label, rw);

            var stop = right - rw - 10f * s;
            var at = new Vector2(MathF.Round(x), MathF.Round(textTop));
            at.X += SilkFonts.DrawFitted(fg, at, ink3, SilkFace.Ui500, px, L.OwnedBy.Before, stop - at.X).X;
            at.X += SilkFonts.DrawFitted(fg, at, SilkPalette.U32(SilkPalette.Ink2), SilkFace.Ui500, px, owned, stop - at.X).X;

            if (stop - at.X > 1f)
                SilkFonts.DrawFitted(fg, at, ink3, SilkFace.Ui500, px, L.OwnedBy.After, stop - at.X);
        }

        return top + 1f * s + 8f * s + SilkFonts.LineHeight(SilkFace.Ui500, px);
    }
}
