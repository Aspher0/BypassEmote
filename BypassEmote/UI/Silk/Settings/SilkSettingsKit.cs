using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BypassEmote.UI.Silk.Settings;

public enum SilkParagraphTone
{
    Body,
    Strong,
    Muted,
    Warn,
}

public readonly record struct SilkParagraph(string Text, SilkParagraphTone Tone = SilkParagraphTone.Body);

public static class SilkSettingsKit
{
    public static void FadeSince(ImDrawListPtr drawList, int start, float alpha)
    {
        if (drawList.IsNull || alpha >= 0.999f)
            return;

        var vertices = drawList.VtxBuffer.AsSpan();

        for (var i = start; i < vertices.Length; i++)
        {
            ref var vertex = ref vertices[i];
            var packed = vertex.Col;
            var faded = (uint)((((packed >> 24) & 0xFFu) * alpha) + 0.5f);
            vertex.Col = (packed & 0x00FFFFFFu) | (faded << 24);
        }
    }

    public static int VertexCount(ImDrawListPtr drawList) => drawList.IsNull ? 0 : drawList.VtxBuffer.Size;

    public static void OuterShadow(Vector2 min, Vector2 max, Vector2 offset, float blur, float spread, Vector4 color, float radius)
    {
        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull || color.W <= 0f)
            return;

        var reach = blur + MathF.Abs(spread) + MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y)) + 4f;
        var outerMin = min - new Vector2(reach, reach);
        var outerMax = max + new Vector2(reach, reach);

        Span<Vector4> bands =
        [
            new(outerMin.X, outerMin.Y, outerMax.X, min.Y),
            new(outerMin.X, max.Y, outerMax.X, outerMax.Y),
            new(outerMin.X, min.Y, min.X, max.Y),
            new(max.X, min.Y, outerMax.X, max.Y),
        ];

        foreach (var band in bands)
        {
            if (band.Z <= band.X || band.W <= band.Y)
                continue;

            drawList.PushClipRect(new Vector2(band.X, band.Y), new Vector2(band.Z, band.W), true);
            SilkPaint.BoxShadow(min, max, offset, blur, spread, color, radius);
            drawList.PopClipRect();
        }
    }

    public static float CssLine(float cssPx, SilkWeight weight, bool mono = false)
    {
        var scale = SilkUi.Scale;
        return MathF.Round(SilkText.NaturalLine(cssPx, weight, mono) / scale) * scale;
    }

    public static float Section(string title, Vector2 pos, float width)
    {
        var scale = SilkUi.Scale;
        var top = pos.Y + (20f * scale);
        var x = pos.X + (2f * scale);
        var right = pos.X + width - (2f * scale);
        var upper = SilkFonts.Upper(title);
        var height = CssLine(11f, SilkWeight.Bold);
        var size = SilkText.Draw(new Vector2(x, SilkText.GlyphTop(top, height, 11f, SilkWeight.Bold)), upper, 11f, SilkWeight.Bold, SilkPalette.SectText, 1f);
        var lineX = x + size.X + (10f * scale);
        var lineY = MathF.Round(top + (height * 0.5f));

        if (right > lineX)
            SilkPaint.HorizontalGradient(new Vector2(lineX, lineY), new Vector2(right, lineY + scale), SilkPalette.Line2, SilkPalette.Alpha(SilkPalette.Line2, 0f), 0f);

        return (20f * scale) + height + (8f * scale);
    }

    public static float NoticeHeight(SilkParagraph[] paragraphs, float width, float gapCss = 6f)
    {
        var scale = SilkUi.Scale;
        var inner = width - (28f * scale);
        var height = 24f * scale;

        for (var i = 0; i < paragraphs.Length; i++)
        {
            height += SilkText.ParagraphHeight(paragraphs[i].Text, 12f, Weight(paragraphs[i].Tone), inner, 1.5f);

            if (i > 0)
                height += gapCss * scale;
        }

        return height;
    }

    public static float Notice(SilkNoticeKind kind, SilkParagraph[] paragraphs, Vector2 pos, float width, float alpha = 1f)
    {
        var scale = SilkUi.Scale;
        var height = NoticeHeight(paragraphs, width);
        var max = pos + new Vector2(width, height);
        var radius = 12f * scale;
        var warn = kind == SilkNoticeKind.Warn;

        SilkPaint.Fill(pos, max, SilkPalette.Fade(warn ? SilkPalette.WarnBg : SilkPalette.BadBg, alpha), radius);
        SilkPaint.InsetRing(pos, max, SilkPalette.Fade(warn ? SilkPalette.WarnRing : SilkPalette.BadRing, alpha), radius, scale);

        var inner = width - (28f * scale);
        var y = pos.Y + (12f * scale);
        var baseColor = warn ? SilkPalette.WarnText : SilkPalette.BadText;

        for (var i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0)
                y += 6f * scale;

            var tone = paragraphs[i].Tone;
            var color = tone == SilkParagraphTone.Muted ? SilkPalette.Ink3 : baseColor;
            y += SilkText.DrawParagraph(new Vector2(pos.X + (14f * scale), y), paragraphs[i].Text, 12f, Weight(tone), SilkPalette.Fade(color, alpha), inner, 1.5f);
        }

        return height;
    }

    public static SilkWeight Weight(SilkParagraphTone tone) => tone == SilkParagraphTone.Strong || tone == SilkParagraphTone.Warn ? SilkWeight.Bold : SilkWeight.Medium;

    public static bool Hovered(Vector2 min, Vector2 max)
        => ImGui.IsWindowHovered() && SilkPaint.Contains(min, max, ImGui.GetMousePos()) && InClip(ImGui.GetMousePos());

    public static bool InClip(Vector2 point)
    {
        var drawList = ImGui.GetWindowDrawList();
        var clip = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        return point.X >= clip.X && point.X < clipMax.X && point.Y >= clip.Y && point.Y < clipMax.Y;
    }

    public static bool Hit(string id, Vector2 min, Vector2 max, out bool hovered)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        hovered = ImGui.IsItemHovered();

        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return clicked;
    }

    public static void Glyph(Vector2 min, Vector2 max, uint iconId, float rounding, float alpha = 1f)
    {
        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        if (!Main.SilkGameIcon.Draw(drawList, iconId, min, max, rounding, alpha))
            SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.HoverWash, alpha), rounding);
    }

    public static Vector4 Vivid(uint iconId, float alpha = 1f) => Main.SilkVivid.Rgba(Main.SilkVivid.Get(iconId), alpha);
}

public ref struct SilkSettingRows
{
    private const int MaxRows = 32;

    private static readonly float[] RowTops = new float[MaxRows + 1];

    private readonly Vector2 origin;
    private readonly float width;
    private readonly ImDrawListPtr drawList;
    private int count;
    private float y;
    private bool split;

    public SilkSettingRows(Vector2 origin, float width)
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

    public readonly float ControlColumn => MathF.Min(SilkControls.RowControlMax * SilkUi.Scale, width * SilkControls.RowControlShare);

    public readonly float Bottom => y;

    public Vector2 HelpMin { get; private set; }

    public Vector2 HelpMax { get; private set; }

    public SilkRow Row(string name, string? help, float controlHeightCss, string? alarm = null, string? helpId = null)
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

        var nameMin = new Vector2(origin.X + (15f * scale), top + ((height - nameHeight) * 0.5f));
        var alarmed = !string.IsNullOrEmpty(alarm);
        SilkText.DrawParagraph(nameMin, name, 13f, SilkWeight.SemiBold, alarmed ? SilkPalette.Bad : SilkPalette.Ink, nameWidth, 1.4f);

        if (alarmed && SilkSettingsKit.Hovered(nameMin, new Vector2(nameMin.X + MathF.Min(nameWidth, SilkText.Width(name, 13f, SilkWeight.SemiBold)), nameMin.Y + nameHeight)))
            SilkTooltip.Hover(alarm, nameMin, new Vector2(nameMin.X + nameWidth, nameMin.Y + nameHeight), true);

        var columnX = origin.X + width - helpWidth - control;
        var centreY = top + (height * 0.5f);

        var markSize = SilkControls.HelpSize * scale;
        var markCentre = new Vector2(origin.X + width - (helpWidth * 0.5f), centreY);
        HelpMin = new Vector2(MathF.Round(markCentre.X - (markSize * 0.5f)), MathF.Round(markCentre.Y - (markSize * 0.5f)));
        HelpMax = HelpMin + new Vector2(markSize, markSize);

        if (!string.IsNullOrEmpty(help))
            SilkControls.HelpMark(helpId ?? name, help, markCentre);

        y += height;

        return new SilkRow(
            new Vector2(origin.X, top),
            new Vector2(origin.X + width, top + height),
            new Vector2(columnX + (16f * scale), MathF.Round(centreY - (controlHeight * 0.5f))),
            control - (16f * scale),
            controlHeight);
    }

    public void Note(string text)
    {
        var scale = SilkUi.Scale;
        var textWidth = MathF.Max(1f, width - (30f * scale));
        var height = SilkText.ParagraphHeight(text, 12f, SilkWeight.Medium, textWidth, 1.5f);

        SilkText.DrawParagraph(new Vector2(origin.X + (15f * scale), y), text, 12f, SilkWeight.Medium, SilkPalette.Ink3, textWidth, 1.5f);
        y += height + (8f * scale);
    }

    public float End()
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

        return y - origin.Y;
    }
}

public sealed class SilkScrollArea
{
    private float offset;
    private float target;
    private float content;
    private float viewport;
    private bool dragging;
    private float dragStartMouse;
    private float dragStartOffset;
    private float hover;

    public float Offset => offset;

    public void Reset()
    {
        offset = 0f;
        target = 0f;
    }

    public void ToEnd() => target = float.MaxValue * 0.25f;

    public void Snap() => offset = target;

    public float Begin(Vector2 min, Vector2 max, bool allowWheel)
    {
        viewport = max.Y - min.Y;
        var limit = MathF.Max(0f, content - viewport);

        if (allowWheel && ImGui.IsWindowHovered() && SilkPaint.Contains(min, max, ImGui.GetMousePos()))
        {
            var wheel = ImGui.GetIO().MouseWheel;

            if (wheel != 0f)
                target -= wheel * 80f * SilkUi.Scale;
        }

        target = Math.Clamp(target, 0f, limit);

        if (SilkUi.ReducedMotion)
            offset = target;
        else
            offset += (target - offset) * MathF.Min(1f, ImGui.GetIO().DeltaTime * 18f);

        if (MathF.Abs(target - offset) < 0.25f)
            offset = target;

        offset = Math.Clamp(offset, 0f, limit);
        return MathF.Round(offset);
    }

    public void End(Vector2 min, Vector2 max, float contentHeight)
    {
        content = contentHeight;
        var limit = content - viewport;

        if (limit <= 0.5f)
        {
            dragging = false;
            return;
        }

        var scale = SilkUi.Scale;
        var track = viewport - (8f * scale);
        var thumb = MathF.Max(28f * scale, track * (viewport / content));
        var thumbTop = min.Y + (4f * scale) + ((track - thumb) * (offset / limit));
        var thumbMin = new Vector2(max.X - (7f * scale), thumbTop);
        var thumbMax = new Vector2(max.X - (3f * scale), thumbTop + thumb);
        var mouse = ImGui.GetMousePos();
        var over = ImGui.IsWindowHovered() && SilkPaint.Contains(min, max, mouse);

        var restore = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(max.X - (12f * scale), min.Y));
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        ImGui.InvisibleButton("##silkscroll", new Vector2(12f * scale, MathF.Max(1f, max.Y - min.Y)));
        var pressed = ImGui.IsItemActivated();
        ImGui.PopID();
        ImGui.SetCursorScreenPos(restore);

        if (pressed && mouse.Y >= thumbMin.Y && mouse.Y <= thumbMax.Y)
        {
            dragging = true;
            dragStartMouse = mouse.Y;
            dragStartOffset = offset;
        }

        if (dragging)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                dragging = false;
            else
            {
                var travel = MathF.Max(1f, track - thumb);
                target = offset = Math.Clamp(dragStartOffset + ((mouse.Y - dragStartMouse) / travel * limit), 0f, limit);
            }
        }

        hover += ((over || dragging ? 1f : 0f) - hover) * (SilkUi.ReducedMotion ? 1f : MathF.Min(1f, ImGui.GetIO().DeltaTime * 12f));

        if (hover > 0.01f)
            SilkPaint.Fill(thumbMin, thumbMax, SilkPalette.Rgb(191, 211, 236, (dragging ? 0.3f : 0.15f) * hover), 2f * scale);
    }
}
