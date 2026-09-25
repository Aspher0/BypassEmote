using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk;

internal sealed class SilkChrome : IChromeSkin
{
    public const float HeaderHeight = 50f;
    public const float CollapsedHeight = 52f;
    public const float Radius = 16f;

    private const float SubtitleBreakpoint = 540f;
    private const float GripSize = 16f;
    private const float EdgeGrab = 5f;

    private readonly NoireSkinnedWindowBase window;
    private readonly bool group;
    private readonly float backdropQuiet;
    private readonly string animKey;
    private readonly string menuId;
    private readonly string menuKey;
    private readonly string collapseKey;
    private readonly string closeKey;

    private bool menuOpenLastFrame;
    private SilkBackdrop? backdrop;

    internal SilkChrome(NoireSkinnedWindowBase window, bool group, float backdropQuiet)
    {
        this.window = window;
        this.group = group;
        this.backdropQuiet = backdropQuiet;

        animKey = "SilkWindow." + window.Id;
        menuId = window.Id + ".menu";
        menuKey = "menu";
        collapseKey = "collapse";
        closeKey = "close";
    }

    public bool Native => false;

    public ChromeMetrics Metrics { get; } = new(HeaderHeight, Radius, CollapsedHeight, GripSize, EdgeGrab);

    public WindowMenuStyle? MenuStyle
    {
        get
        {
            SilkWindowMenu.Style.TextScale = SilkUi.TextScale;
            return SilkWindowMenu.Style;
        }
    }

    public SilkBackdrop Backdrop => backdrop ??= group ? SilkBackdrop.CreateGroup() : SilkBackdrop.CreateSingle(backdropQuiet);

    internal Func<Vector2, bool>? AlsoInside { get; set; }

    public void PushWindowStyle(float scale)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Radius * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
    }

    public void Background(in ChromeFrame frame)
    {
        SilkUi.SyncTextScale();

        var min = frame.Min;
        var max = frame.Max;
        var scale = frame.Scale;
        var radius = Radius * scale;
        var opacity = frame.Opacity;

        SilkPaint.PushClipExpanded(min, max, 2f * scale);
        SilkPaint.Fill(min, max, SilkPalette.Alpha(SilkPalette.WindowBg, opacity), radius);
        SilkPaint.PopClip();

        Backdrop.Frozen = SilkUi.ReducedMotion;

        using (NoireUI.Profiler.Measure("Silk.Chrome.Backdrop"))
        {
            if (frame.Collapsed)
                DrawCollapsedBackdrop(min, max, opacity);
            else if (group)
                DrawGroupBackdrop(min, max, opacity);
            else
                DrawBackdrop(min, max, opacity);
        }

        using (NoireUI.Profiler.Measure("Silk.Chrome.Edges"))
        {
            SilkPaint.PushClipExpanded(min, max, 2f * scale);
            PaintEdges(min, max, radius, scale);
            SilkPaint.PopClip();
        }
    }

    public ChromeControl Header(in ChromeFrame frame, in ChromeTitle title, ReadOnlySpan<TitleButton> buttons, out Vector2 menuMin, out Vector2 menuMax)
    {
        using (NoireUI.Profiler.Measure("Silk.Chrome.Header"))
            return DrawHeader(frame, title, buttons, out menuMin, out menuMax);
    }

    public ChromeControl Collapsed(in ChromeFrame frame, in ChromeTitle title, ReadOnlySpan<TitleButton> buttons, out Vector2 menuMin, out Vector2 menuMax)
        => DrawHeader(frame, title, buttons, out menuMin, out menuMax);

    public void ResizeGrip(in ChromeFrame frame) => DrawGripMark(frame.Max, frame.Scale);

    public void ClickThroughOutline(in ChromeFrame frame)
        => SilkPaint.DashedRing(frame.Min, frame.Max, Radius * frame.Scale, SilkPalette.ClickThroughOutline, 3f * frame.Scale, 3f * frame.Scale);

    public void BlockedVeil(Vector2 bodyMin, Vector2 bodyMax, float scale)
    {
    }

    private void DrawBackdrop(Vector2 min, Vector2 max, float opacity)
    {
        var field = Backdrop;
        field.Frozen = SilkUi.ReducedMotion;
        field.Tick(min, max);
        field.Paint(min, max, Radius * SilkUi.Scale, opacity);
    }

    private void DrawGroupBackdrop(Vector2 min, Vector2 max, float opacity)
    {
        Vector2? mouse = null;

        if (ImGui.IsMousePosValid())
        {
            var position = ImGui.GetMousePos();
            var inMain = position.X >= min.X && position.Y >= min.Y && position.X < max.X && position.Y < max.Y;

            if (inMain || AlsoInside?.Invoke(position) == true)
                mouse = position;
        }

        Backdrop.Frozen = SilkUi.ReducedMotion;
        Backdrop.Tick(min, max, mouse);
        Backdrop.Paint(min, max, Radius, opacity);
    }

    private void DrawCollapsedBackdrop(Vector2 min, Vector2 max, float opacity)
    {
        var field = Backdrop;
        field.Tick(min, max);
        field.Paint(min, max, Radius * SilkUi.Scale, opacity);
    }

    private static void PaintEdges(Vector2 min, Vector2 max, float radius, float scale)
    {
        SilkPaint.OuterRing(min, max, SilkPalette.WindowRing, radius, scale);

        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        var y = min.Y + (0.5f * scale);
        var color = SilkPalette.U32(SilkPalette.WindowTopHighlight);
        var inset = radius;

        drawList.PathArcTo(new Vector2(min.X + radius, min.Y + radius), radius - (0.5f * scale), MathF.PI * 1.25f, MathF.PI * 1.5f, 8);
        drawList.PathLineTo(new Vector2(max.X - inset, y));
        drawList.PathArcTo(new Vector2(max.X - radius, min.Y + radius), radius - (0.5f * scale), MathF.PI * 1.5f, MathF.PI * 1.75f, 8);
        drawList.PathStroke(color, ImDrawFlags.None, scale);
    }

    private ChromeControl DrawHeader(in ChromeFrame frame, in ChromeTitle title, ReadOnlySpan<TitleButton> buttons, out Vector2 menuAnchorMin, out Vector2 menuAnchorMax)
    {
        var scale = frame.Scale;
        var min = frame.Min;
        var max = new Vector2(frame.Max.X, frame.Min.Y + (HeaderHeight * scale));
        var width = frame.Max.X - frame.Min.X;
        var control = ChromeControl.None;
        var height = max.Y - min.Y;
        var centreY = min.Y + (height * 0.5f);

        var logoMin = new Vector2(min.X + (16f * scale), MathF.Round(centreY - (12f * scale)));
        PaintLogo(logoMin, scale);

        var right = max.X - (10f * scale);
        var button = 28f * scale;
        var gap = 2f * scale;

        var closeMin = new Vector2(right - button, MathF.Round(centreY - (button * 0.5f)));
        var collapseMin = closeMin - new Vector2(button + gap, 0f);
        var menuMin = collapseMin - new Vector2(button + gap, 0f);

        var contentLeft = menuMin.X;

        if (frame.AlwaysOnTop)
        {
            var dotCentre = new Vector2(menuMin.X - gap - (4f * scale) - (3f * scale), centreY);
            SilkPaint.Circle(dotCentre, 3f * scale, SilkPalette.PinMark);

            var dotMin = dotCentre - new Vector2(3f * scale, 3f * scale);
            var dotMax = dotCentre + new Vector2(3f * scale, 3f * scale);

            if (SilkPaint.Contains(dotMin, dotMax, ImGui.GetMousePos()) && ImGui.IsWindowHovered())
                SilkTooltip.Hover(L.AlwaysOnTop.Text, dotMin, dotMax);

            contentLeft = menuMin.X - gap - (14f * scale);
        }

        var dividerX = MathF.Round(contentLeft - (8f * scale) - scale);
        SilkPaint.Fill(new Vector2(dividerX, closeMin.Y), new Vector2(dividerX + scale, closeMin.Y + button), SilkPalette.WctlDivider, 0f);

        var menuOpen = frame.MenuOpen;

        if (WctlButton(menuKey, menuMin, button, SilkIcon.Menu, L.WindowOptions.Text, menuOpen || menuOpenLastFrame, false))
            control = ChromeControl.Menu;

        menuAnchorMin = menuMin;
        menuAnchorMax = menuMin + new Vector2(button, button);

        if (WctlButton(collapseKey, collapseMin, button, frame.Collapsed ? SilkIcon.Expand : SilkIcon.Collapse, frame.Collapsed ? L.Expand.Text : L.Collapse.Text, false, false))
            control = ChromeControl.Collapse;

        if (WctlButton(closeKey, closeMin, button, SilkIcon.Close, L.Close.Text, false, true))
            control = ChromeControl.Close;

        var cursor = dividerX - (14f * scale);
        var cbtn = 30f * scale;

        for (var i = buttons.Length - 1; i >= 0; i--)
        {
            var header = buttons[i];
            var bMin = new Vector2(cursor - cbtn, MathF.Round(centreY - (cbtn * 0.5f)));

            if (HeaderButton(header, bMin, cbtn, scale))
                header.Click();

            cursor = bMin.X - (10f * scale);
        }

        var wordLeft = logoMin.X + (24f * scale) + (10f * scale);
        var wordRight = cursor - (10f * scale);
        PaintWord(wordLeft, wordRight, min.Y, height, width / scale, title.Subtitle);

        menuOpenLastFrame = menuOpen;
        return control;
    }

    private static void PaintLogo(Vector2 min, float scale)
    {
        var size = 24f * scale;
        var max = min + new Vector2(size, size);
        var radius = 7f * scale;

        SilkPaint.Glow(min, max, 16f * scale, -3f * scale, SilkPalette.LogoGlow, radius);
        SilkPaint.AngleGradient(min, max, 145f, SilkPalette.Ice, SilkPalette.LogoTo, radius);

        var glyph = 12f * scale;
        SilkIcons.Draw(SilkIcon.Logo, min + new Vector2((size - glyph) * 0.5f, (size - glyph) * 0.5f), glyph, SilkPalette.LogoGlyph);
    }

    private static void PaintWord(float left, float right, float top, float height, float cssWidth, string? subtitle)
    {
        if (right <= left)
            return;

        var baseline = SilkText.Baseline(top, height, 15f, SilkWeight.ExtraBold);
        var available = right - left;
        var word = SilkText.DrawOnBaseline(left, baseline, L.Brand.Text, 15f, SilkWeight.ExtraBold, SilkPalette.Ink, -0.3f, false, available);

        if (cssWidth <= SubtitleBreakpoint || string.IsNullOrEmpty(subtitle))
            return;

        var gap = 6f * SilkUi.Scale;
        var x = left + word.X + gap;

        SilkText.DrawOnBaseline(x, baseline, subtitle, 12.5f, SilkWeight.Medium, SilkPalette.Ink3, 0f, false, MathF.Max(0f, right - x));
    }

    private bool WctlButton(string key, Vector2 min, float size, SilkIcon icon, string tooltip, bool on, bool danger)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(key, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var max = min + new Vector2(size, size);

        var hover = SilkUi.Css(animKey, key, hovered || on ? 1f : 0f, 0.2f);
        var scale = SilkUi.Scale;

        Vector4 background;
        Vector4 color;

        if (danger)
        {
            background = SilkPalette.Fade(SilkPalette.CloseHoverBg, hover);
            color = SilkPalette.Mix(SilkPalette.WctlText, SilkPalette.CloseHoverText, hover);
        }
        else
        {
            background = SilkPalette.Fade(SilkPalette.WctlHoverBg, hover);
            color = SilkPalette.Mix(SilkPalette.WctlText, SilkPalette.WctlHoverText, hover);
        }

        SilkPaint.Fill(min, max, background, 8f * scale);

        var glyph = 14f * scale;
        SilkIcons.Draw(icon, min + new Vector2((size - glyph) * 0.5f, (size - glyph) * 0.5f), glyph, color);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            SilkTooltip.Hover(tooltip, min, max);
        }

        return clicked;
    }

    private bool HeaderButton(TitleButton button, Vector2 min, float size, float scale)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(button.Id, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var max = min + new Vector2(size, size);
        var hover = SilkUi.Css(animKey, button.Id, hovered ? 1f : 0f, 0.2f);

        SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.CbtnHoverBg, hover), 8f * scale);

        var glyph = 15f * scale;
        SilkIcons.Draw(IconOf(button.Icon), min + new Vector2((size - glyph) * 0.5f, (size - glyph) * 0.5f), glyph, SilkPalette.Mix(SilkPalette.Ink3, SilkPalette.Ink, hover));

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            SilkTooltip.Hover(button.Tooltip.Text, min, max);
        }

        return clicked;
    }

    private static SilkIcon IconOf(NoireIcon icon) => icon switch
    {
        NoireIcon.Logs => SilkIcon.Logs,
        NoireIcon.Changelog => SilkIcon.Changelog,
        _ => SilkIcon.Settings,
    };

    private static void DrawGripMark(Vector2 max, float scale)
    {
        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        var corner = max - new Vector2(4f * scale, 4f * scale);
        var arm = 7f * scale;
        var thickness = 1.5f * scale;
        var color = SilkPalette.U32(SilkPalette.ResizeMark);
        var bend = 3f * scale;

        drawList.PathLineTo(new Vector2(corner.X - (thickness * 0.5f), corner.Y - arm));
        drawList.PathLineTo(new Vector2(corner.X - (thickness * 0.5f), corner.Y - bend));
        drawList.PathArcTo(new Vector2(corner.X - bend, corner.Y - bend), bend - (thickness * 0.5f), 0f, MathF.PI * 0.5f, 6);
        drawList.PathLineTo(new Vector2(corner.X - arm, corner.Y - (thickness * 0.5f)));
        drawList.PathStroke(color, ImDrawFlags.None, thickness);
    }
}
