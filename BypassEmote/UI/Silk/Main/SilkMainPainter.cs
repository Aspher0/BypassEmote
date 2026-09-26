using BypassEmote.Enums;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed partial class SilkMainPainter : IDisposable
{
    private static readonly string[] CountText = BuildCounts();

    private static readonly TextList TabLabels = new(L.TabAll, L.TabGeneral, L.TabSpecial, L.TabExpressions, L.TabOther, L.TabFavourites, L.TabBlocked);
    private static readonly SilkIconShape[] TabIcons =
    [
        SilkMainIcons.All, SilkMainIcons.General, SilkMainIcons.Special, SilkMainIcons.Expressions, SilkMainIcons.Other,
        SilkMainIcons.Star, SilkMainIcons.Ban,
    ];

    private static readonly TextList CheckLabels = new(L.ShowAllEmotes, L.ShowInvalidEmotes, L.ShowIds);

    private readonly SilkEmoteModel model = new();
    private readonly SilkCat cat = new();
    private readonly SilkPlayPill pill = new();
    private readonly SilkRowCards cards = new();
    private readonly SilkRowMenu menu = new();
    private readonly SilkDock dock = new();
    private readonly SilkQuickAdd favAdd = new(L.AddFavouritePlaceholder, true);
    private readonly SilkQuickAdd blockAdd = new(L.BlockEmotePlaceholder, false);

    private readonly Tween[] toolbarHover = new Tween[3];
    private readonly Tween[] checkOn = new Tween[3];
    private readonly Tween[] tabHover = new Tween[7];
    private readonly Tween[] tabOn = new Tween[7];
    private readonly float[] tabX = new float[7];
    private readonly float[] tabW = new float[7];
    private readonly string?[] tabTips = new string?[7];
    private readonly int[] tabTipCounts = new int[7];
    private readonly int[] tabTipRevisions = new int[7];
    private Tween barX;
    private Tween barW;
    private Tween barColor;
    private Tween searchFocus;
    private float spinAt = -10f;
    private float syncAt = -10f;
    private float cubeAt = -10f;
    private string search = string.Empty;
    private bool searchActive;
    private int tabMode;
    private bool barPlaced;
    private SilkTab lastBarTab = (SilkTab)(-1);
    private int groupFront;
    private bool mainFocused;

    private float s;
    private float now;
    private bool reduced;
    private Vector2 winMin;
    private Vector2 winMax;
    private ImDrawListPtr dl;

    internal Action<Vector4?>? Lean { get; set; }

    internal Action<float?, Vector4?>? Wave { get; set; }

    internal bool Collapsed { get; set; }

    internal bool PassingClicks { get; set; }

    internal bool GroupFrontPending => groupFront > 0;

    internal bool MainFocused => mainFocused;

    internal void RequestGroupFront() => groupFront = 2;

    internal void MainFocusTick(bool focused)
    {
        if (focused && !mainFocused && dock.IsOpen)
            RequestGroupFront();

        mainFocused = focused;
    }

    internal static bool PopupBlocking()
        => ImGui.IsPopupOpen(""u8, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);

    internal SilkEmoteModel Model => model;

    internal SilkDock Dock => dock;

    internal SilkRowMenu Menu => menu;

    private static string[] BuildCounts()
    {
        var counts = new string[4096];

        for (var i = 0; i < counts.Length; i++)
            counts[i] = i.ToString();

        return counts;
    }

    internal static string Count(int value) => (uint)value < CountText.Length ? CountText[value] : value.ToString();

    private Vector2 P(float x, float y) => winMin + new Vector2(x, y) * s;

    private float WidthCss => (winMax.X - winMin.X) / s;

    private float HeightCss => (winMax.Y - winMin.Y) / s;

    internal void Draw(Vector2 windowMin, Vector2 windowMax)
    {
        s = SilkUi.Scale;
        now = SilkUi.Time;
        reduced = SilkUi.ReducedMotion;
        winMin = windowMin;
        winMax = windowMax;
        dl = ImGui.GetWindowDrawList();

        using (NoireUI.Profiler.Measure("Silk.Main.Model"))
            model.Update();

        var y = 50f;

        using (NoireUI.Profiler.Measure("Silk.Main.Toolbar"))
        {
            using (SilkProfile.Detail("Silk.Main.Bar"))
                y = DrawToolbar(y + 2f);

            using (SilkProfile.Detail("Silk.Main.Checks"))
                y = DrawChecks(y);

            using (SilkProfile.Detail("Silk.Main.Search"))
                y = DrawSearch(y + 6f);

            using (SilkProfile.Detail("Silk.Main.Tabs"))
                y = DrawTabs(y + 2f);
        }

        var supportTop = HeightCss - 50f;

        using (NoireUI.Profiler.Measure("Silk.Main.List"))
            DrawListBox(y, supportTop);

        using (NoireUI.Profiler.Measure("Silk.Main.Support"))
        {
            DrawSupport(supportTop);
            pill.Draw(dl, P(WidthCss * 0.5f, supportTop - 12f), s, now, reduced);
        }
    }

    internal void DrawOverlays()
    {
        s = SilkUi.Scale;
        now = SilkUi.Time;
        reduced = SilkUi.ReducedMotion;

        var fg = ImGui.GetForegroundDrawList();

        using (NoireUI.Profiler.Measure("Silk.Main.Cat"))
            DrawCatLower();

        if (reduced)
            SilkBurst.Clear();
        else
            SilkBurst.Draw(fg, now);

        using (NoireUI.Profiler.Measure("Silk.Main.Dock"))
            dock.DrawWindow(this, winMin, winMax, s, now, reduced, Collapsed);

        favAdd.DrawDropdown(model, s, "##silkfavdrop"u8);
        blockAdd.DrawDropdown(model, s, "##silkblkdrop"u8);
        menu.DrawWindow(this, s, now, reduced);
        cards.Draw(this, fg, s, now, reduced);

        TickDeferredOpen();
        TickDockDismiss();

        if (groupFront > 0)
            groupFront--;
    }

    private float Ease(ref Tween tween, float target, float seconds, UiCubicBezier curve)
    {
        tween.Go(target, now, reduced ? 0f : seconds, curve);
        return tween.Value(now);
    }

    private static uint U(Vector4 color) => SilkPalette.U32(color);

    private static uint U(Vector4 color, float alpha) => SilkPalette.U32(SilkPalette.Fade(color, alpha));

    private bool Button(ReadOnlySpan<byte> id, Vector2 min, Vector2 max, out bool hovered, out bool held)
    {
        ImGui.SetCursorScreenPos(min);
        var pressed = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        hovered = ImGui.IsItemHovered();
        held = ImGui.IsItemActive();
        return pressed;
    }

    private float DrawToolbar(float top)
    {
        var width = WidthCss;
        var min = P(14f, top);
        var max = P(width - 14f, top + 34f);
        var radius = 11f * s;

        dl.AddRectFilled(min, max, SilkMainDraw.Col(191, 211, 236, 0.05f), radius);

        var segments = EmoteActions.PenumbraReady ? 3 : 2;
        var column = (max.X - min.X) / segments;
        var showIcons = width > 450f;

        for (var i = 0; i < segments; i++)
        {
            var bMin = new Vector2(min.X + column * i, min.Y);
            var bMax = new Vector2(i == segments - 1 ? max.X : min.X + column * (i + 1), max.Y);
            ImGui.PushID(i);
            var pressed = Button("tb"u8, bMin, bMax, out var hovered, out var held);
            ImGui.PopID();

            var wash = Ease(ref toolbarHover[i], held ? 0.14f : hovered ? 0.09f : 0f, 0.25f, UiCubicBezier.Ease);

            if (wash > 0.001f)
            {
                var flags = segments == 1 ? ImDrawFlags.RoundCornersAll
                    : i == 0 ? ImDrawFlags.RoundCornersLeft
                    : i == segments - 1 ? ImDrawFlags.RoundCornersRight
                    : ImDrawFlags.RoundCornersNone;
                dl.AddRectFilled(bMin, bMax, SilkMainDraw.Col(191, 211, 236, wash), flags == ImDrawFlags.RoundCornersNone ? 0f : radius, flags);
            }

            if (i > 0)
                dl.AddRectFilled(new Vector2(bMin.X, bMin.Y + 8f * s), new Vector2(bMin.X + 1f * s, bMax.Y - 8f * s), SilkMainDraw.Col(191, 211, 236, 0.16f));

            var (label, icon) = i switch
            {
                0 => (L.Refresh.Text, SilkMainIcons.Refresh),
                1 => (L.SyncEveryone.Text, SilkMainIcons.Sync),
                _ => (L.CreateMod.Text, SilkMainIcons.Cube),
            };

            var iconW = showIcons ? 15f * s + 9f * s : 0f;
            var room = MathF.Max(0f, bMax.X - bMin.X - iconW - 24f * s);
            var textW = MathF.Min(SilkFonts.Measure(SilkFace.Ui600, 12.5f, label).X, room);
            var x = (bMin.X + bMax.X - textW - iconW) * 0.5f;
            var midY = (bMin.Y + bMax.Y) * 0.5f;

            if (showIcons)
            {
                var iconMin = new Vector2(x, midY - 7.5f * s);
                var start = SilkIcons.Draw(dl, icon, iconMin, 15f * s, U(SilkPalette.Ice));
                var centre = iconMin + new Vector2(7.5f * s);
                AnimateToolbarIcon(i, start, centre);
                x += iconW;
            }

            var lh = SilkFonts.LineHeight(SilkFace.Ui600, 12.5f);
            SilkFonts.Draw(dl, new Vector2(MathF.Round(x), MathF.Round(midY - lh * 0.5f)), U(SilkPalette.Ink), SilkFace.Ui600, 12.5f, label, 0f, room);

            if (hovered && SilkFonts.Truncates(SilkFace.Ui600, 12.5f, label, room))
                SilkTooltip.Hover(label, bMin, bMax);

            if (!pressed)
                continue;

            switch (i)
            {
                case 0:
                    spinAt = now;
                    EmoteActions.Refresh();
                    Wave?.Invoke(null, null);
                    break;
                case 1:
                    syncAt = now;

                    if (Configuration.SelfBypassMode == SelfBypassMode.DirectPlay)
                        menu.OpenSync(new Vector2(bMin.X, bMax.Y + 4f * s), now);
                    else
                        EmoteActions.Sync(true);

                    Wave?.Invoke(null, null);
                    break;
                default:
                    cubeAt = now;
                    EmoteActions.OpenCreateMod();
                    Wave?.Invoke(null, null);
                    break;
            }
        }

        SilkMainDraw.InsetRing(dl, min, max, radius, SilkMainDraw.Col(191, 211, 236, 0.16f));
        return top + 34f;
    }

    private static readonly float[] BounceAt = [0f, 0.25f, 0.5f, 0.75f, 1f];
    private static readonly float[] BounceY = [0f, -3f, 0f, -1.5f, 0f];
    private static readonly float[] BounceScale = [1f, 1.12f, 1f, 1.05f, 1f];

    private void AnimateToolbarIcon(int index, int start, Vector2 centre)
    {
        if (reduced)
            return;

        if (index == 0)
        {
            var t = (now - spinAt) / 0.8f;

            if (t is >= 0f and < 1f)
                SilkMainDraw.Transform(dl, start, centre, Vector2.One, SilkUi.EaseOut.Evaluate(t) * MathF.PI * 2f, Vector2.Zero);

            return;
        }

        var at = index == 1 ? syncAt : cubeAt;
        var p = (now - at) / 0.7f;

        if (p is < 0f or >= 1f)
            return;

        var e = UiCubicBezier.EaseOut.Evaluate(p);
        var y = Piecewise(e, BounceAt, BounceY);
        var scale = Piecewise(e, BounceAt, BounceScale);
        SilkMainDraw.Transform(dl, start, centre, new Vector2(scale), 0f, new Vector2(0f, y * s));
    }

    internal static float Piecewise(float p, ReadOnlySpan<float> at, ReadOnlySpan<float> values)
    {
        if (p <= at[0])
        {
            var slope = (values[1] - values[0]) / (at[1] - at[0]);
            return values[0] + (p - at[0]) * slope;
        }

        for (var i = 1; i < at.Length; i++)
        {
            if (p <= at[i])
                return values[i - 1] + (values[i] - values[i - 1]) * ((p - at[i - 1]) / (at[i] - at[i - 1]));
        }

        var last = at.Length - 1;
        var tail = (values[last] - values[last - 1]) / (at[last] - at[last - 1]);
        return values[last] + (p - at[last]) * tail;
    }

    private float DrawChecks(float top)
    {
        var width = WidthCss;
        var gap = width <= 540f ? 12f : 18f;
        var lh = SilkFonts.LineHeight(SilkFace.Ui500, 12.5f) / s;
        var rowH = MathF.Max(16f, lh);
        Span<float> widths = stackalloc float[3];
        Span<float> shown = stackalloc float[3];

        for (var i = 0; i < 3; i++)
        {
            widths[i] = 16f + 8f + SilkFonts.Measure(SilkFace.Ui500, 12.5f, CheckLabels.Source(i)).X / s;
            shown[i] = 16f + 8f + SilkFonts.Measure(SilkFace.Ui500, 12.5f, CheckLabels[i]).X / s;
        }

        var avail = width - 28f;
        var y = top + 12f;
        var index = 0;

        while (index < 3)
        {
            var lineW = widths[index];
            var end = index + 1;

            while (end < 3 && lineW + gap + widths[end] <= avail)
            {
                lineW += gap + widths[end];
                end++;
            }

            var shownW = gap * (end - index - 1);
            var labelsW = 0f;

            for (var i = index; i < end; i++)
            {
                shownW += shown[i];
                labelsW += shown[i] - 24f;
            }

            var squeeze = shownW <= avail || labelsW <= 0f ? 1f : MathF.Max(0f, labelsW - (shownW - avail)) / labelsW;
            lineW = shownW <= avail ? shownW : avail;
            var x = 14f + (avail - lineW) * 0.5f;

            for (var i = index; i < end; i++)
            {
                var w = 24f + (shown[i] - 24f) * squeeze;
                DrawCheck(i, x, y, rowH, w);
                x += w + gap;
            }

            index = end;
            y += rowH + (index < 3 ? 6f : 0f);
        }

        return y + 2f;
    }

    private void DrawCheck(int i, float x, float y, float rowH, float w)
    {
        var value = i switch
        {
            0 => Configuration.ShowAllEmotes,
            1 => Configuration.ShowInvalidEmotes,
            _ => Configuration.ShowEmoteIds,
        };

        ImGui.PushID(i + 10);
        var pressed = Button("ck"u8, P(x, y), P(x + w, y + rowH), out var hovered, out _);
        ImGui.PopID();

        if (pressed)
        {
            value = !value;

            switch (i)
            {
                case 0:
                    Configuration.ShowAllEmotes = value;
                    break;
                case 1:
                    Configuration.ShowInvalidEmotes = value;
                    break;
                default:
                    Configuration.ShowEmoteIds = value;
                    break;
            }

            model.Invalidate();
        }

        var on = Ease(ref checkOn[i], value ? 1f : 0f, 0.25f, SilkUi.EaseOut);
        var boxMin = P(x, y + (rowH - 16f) * 0.5f);
        var boxMax = boxMin + new Vector2(16f * s);
        var r = 5f * s;

        if (on > 0.001f)
        {
            SilkMainDraw.Shadow(dl, boxMin, boxMax, r, SilkMainDraw.Col(191, 211, 236, 0.8f * on), 12f * s, -2f * s);
            dl.AddRectFilled(boxMin, boxMax, SilkMainDraw.Col(191, 211, 236, on), r);
        }

        SilkMainDraw.InsetRing(dl, boxMin, boxMax, r, SilkMainDraw.Col(191, 211, 236, 0.16f * (1f - on)), 1.5f * s);

        if (on > 0.001f)
        {
            var size = 10f * s;
            var centre = (boxMin + boxMax) * 0.5f;
            var start = SilkIcons.Draw(dl, SilkMainIcons.Check, centre - new Vector2(size * 0.5f), size, SilkMainDraw.Col(7, 16, 30, on));
            var scale = 0.4f + 0.6f * on;
            SilkMainDraw.Transform(dl, start, centre, new Vector2(scale), 0f, Vector2.Zero);
        }

        var color = value || hovered ? SilkPalette.Ink : SilkPalette.Ink2;
        var lh = SilkFonts.LineHeight(SilkFace.Ui500, 12.5f);
        var textTop = winMin.Y + y * s + (rowH * s - lh) * 0.5f;
        var room = (w - 24f) * s + 0.5f;
        SilkFonts.Draw(dl, new Vector2(MathF.Round(winMin.X + (x + 24f) * s), MathF.Round(textTop)), U(color), SilkFace.Ui500, 12.5f, CheckLabels[i], 0f, room);

        if (hovered && SilkFonts.Truncates(SilkFace.Ui500, 12.5f, CheckLabels[i], room))
            SilkTooltip.Hover(CheckLabels[i], P(x, y), P(x + w, y + rowH));
    }

    private float DrawSearch(float top)
    {
        var width = WidthCss;
        var min = P(14f, top);
        var max = P(width - 14f, top + 32f);
        var r = 10f * s;
        var focus = Ease(ref searchFocus, searchActive ? 1f : 0f, 0.25f, UiCubicBezier.Ease);

        dl.AddRectFilled(min, max, SilkMainDraw.Col(0, 0, 0, 0.35f), r);

        if (focus > 0.001f)
            SilkMainDraw.OuterRing(dl, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.08f * focus), 4f * s);

        SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.LerpCol(SilkMainDraw.Col(191, 211, 236, 0.16f), SilkMainDraw.Col(191, 211, 236, 0.6f), focus));
        SilkIcons.Draw(dl, SilkMainIcons.Search, new Vector2(min.X + 12f * s, (min.Y + max.Y) * 0.5f - 7.5f * s), 15f * s, U(SilkPalette.Ink3));

        var inputLeft = min.X + (12f + 15f + 10f) * s;
        var inputRight = max.X - 12f * s;
        var lineBox = 13.5f * SilkUi.TextScale * 1.4f * s;
        var lineTop = (min.Y + max.Y) * 0.5f - lineBox * 0.5f;
        var textTop = lineTop + SilkFonts.TextTop(SilkFace.Ui400, 13.5f, 1.4f);

        ImGui.SetCursorScreenPos(new Vector2(inputLeft, MathF.Round(textTop)));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, SilkPalette.Ink);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, SilkPalette.Ink3);
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, SilkPalette.Alpha(SilkPalette.Ice, 0.3f));
        ImGui.SetNextItemWidth(inputRight - inputLeft);

        bool changed;

        using (SilkFonts.PushInput(SilkFace.Ui400, 13.5f))
            changed = ImGui.InputTextWithHint("##silksearch"u8, L.SearchEmotes.Text, ref search, 256);

        searchActive = ImGui.IsItemActive();
        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(2);

        if (changed)
            model.SetQuery(search);

        return top + 32f;
    }

    private float DrawTabs(float top)
    {
        var width = WidthCss;
        var left = 14f;
        var avail = width - 28f;
        Span<float> natural = stackalloc float[7];
        Span<float> noCount = stackalloc float[7];
        float totalNatural = 0f, totalNoCount = 0f;

        for (var i = 0; i < 7; i++)
        {
            var label = SilkFonts.Measure(SilkFace.Ui600, 13f, TabLabels[i]).X / s;
            var count = SilkFonts.Measure(SilkFace.Mono500, 10f, Count(model.Count((SilkTab)i))).X / s;
            var icon = i >= 5 ? 13f + 7f : 0f;
            noCount[i] = 9f + icon + label + 9f;
            natural[i] = noCount[i] + 7f + count;
            totalNatural += natural[i];
            totalNoCount += noCount[i];
        }

        tabMode = totalNatural <= avail + 1f ? 0 : totalNoCount <= avail + 1f ? 1 : 2;

        var used = 0f;

        for (var i = 0; i < 7; i++)
        {
            tabW[i] = tabMode == 0 ? natural[i] : tabMode == 1 ? noCount[i] : 37f;
            used += tabW[i];
        }

        var spacer = MathF.Max(0f, avail - used);
        var x = left;

        for (var i = 0; i < 7; i++)
        {
            if (i == 5)
                x += spacer;

            tabX[i] = x;
            x += tabW[i];
        }

        var bottomLine = P(left, top + 40f);
        var tabsMax = P(width - 14f, top + 41f);
        var current = (int)model.Tab;

        var targetBarX = tabX[current] + 8f;
        var targetBarW = tabW[current] - 16f;

        if (!barPlaced || reduced)
        {
            barX.Snap(targetBarX);
            barW.Snap(targetBarW);
            barPlaced = true;
        }
        else
        {
            barX.Go(targetBarX, now, 0.4f, SilkUi.EaseOut);
            barW.Go(targetBarW, now, 0.4f, SilkUi.EaseOut);
        }

        if (lastBarTab != model.Tab)
        {
            lastBarTab = model.Tab;
            barColor.Go(current == 5 ? 1f : current == 6 ? 2f : 0f, now, reduced ? 0f : 0.3f, UiCubicBezier.Ease);
        }

        var bx = barX.Value(now);
        var bw = barW.Value(now);

        var glowMin = P(bx - 8f, top);
        var glowMax = P(bx + bw + 8f, top + 40f);
        dl.PushClipRect(glowMin, glowMax, true);
        var glowRim = new Vector2((glowMax.X - glowMin.X) * 0.6f, (glowMax.Y - glowMin.Y));
        HalfEllipseGlow(new Vector2((glowMin.X + glowMax.X) * 0.5f, glowMax.Y), glowRim, SilkMainDraw.Col(191, 211, 236, 0.16f));
        dl.PopClipRect();

        for (var i = 0; i < 7; i++)
            DrawTab(i, top, current == i);

        dl.AddRectFilled(bottomLine, new Vector2(tabsMax.X, bottomLine.Y + 1f * s), SilkMainDraw.Col(191, 211, 236, 0.16f));

        var bar = BarColor(barColor.Value(now));
        var barMin = P(bx, top + 39f);
        var barMax = P(bx + bw, top + 41f);
        SilkMainDraw.Shadow(dl, barMin, barMax, 2f * s, SilkMainDraw.Col(bar, 0.25f), 18f * s, 0f, new Vector2(0f, -6f * s));
        SilkMainDraw.Shadow(dl, barMin, barMax, 2f * s, SilkMainDraw.Col(bar, 0.9f), 12f * s, 1f * s);
        dl.AddRectFilled(barMin, barMax, SilkMainDraw.Col(bar), 2f * s);

        return top + 41f;
    }

    private static Vector3 BarColor(float t)
    {
        var ice = new Vector3(191, 211, 236);
        var star = new Vector3(255, 211, 110);
        var bad = new Vector3(255, 111, 134);

        return t <= 1f ? SilkMainDraw.Lerp(ice, star, t) : SilkMainDraw.Lerp(star, bad, t - 1f);
    }

    private void HalfEllipseGlow(Vector2 centre, Vector2 radius, uint color)
    {
        Span<Vector2> rim = stackalloc Vector2[25];

        for (var i = 0; i < rim.Length; i++)
        {
            var a = MathF.PI + MathF.PI * i / (rim.Length - 1);
            rim[i] = centre + new Vector2(MathF.Cos(a) * radius.X, MathF.Sin(a) * radius.Y);
        }

        SilkMainDraw.Fan(dl, centre, rim, color, color & 0x00FFFFFFu, false);
    }

    private void DrawTab(int i, float top, bool on)
    {
        var min = P(tabX[i], top);
        var max = P(tabX[i] + tabW[i], top + 40f);
        ImGui.PushID(i + 20);
        var pressed = Button("tab"u8, min, max, out var hovered, out _);
        ImGui.PopID();

        var hover = Ease(ref tabHover[i], hovered ? 1f : 0f, 0.25f, UiCubicBezier.Ease);
        var active = Ease(ref tabOn[i], on ? 1f : 0f, 0.25f, UiCubicBezier.Ease);
        var baseColor = SilkPalette.Mix(SilkPalette.Ink3, SilkPalette.Ink2, hover);
        var color = SilkPalette.Mix(baseColor, SilkPalette.Ink, active);
        var iconColor = i == 5 ? SilkPalette.Mix(color, SilkPalette.Star, active) : i == 6 ? SilkPalette.Mix(color, SilkPalette.Bad, active) : color;
        var midY = (min.Y + max.Y) * 0.5f;
        var x = min.X + (tabMode == 2 ? 12f : 9f) * s;

        if (tabMode == 2 || i >= 5)
        {
            SilkIcons.Draw(dl, TabIcons[i], new Vector2(x, midY - 6.5f * s), 13f * s, U(iconColor));
            x += (13f + 7f) * s;
        }

        if (tabMode != 2)
        {
            var lh = SilkFonts.LineHeight(SilkFace.Ui600, 13f);
            var w = SilkFonts.Draw(dl, new Vector2(MathF.Round(x), MathF.Round(midY - lh * 0.5f)), U(color), SilkFace.Ui600, 13f, TabLabels[i]).X;

            if (w <= 0f)
                w = SilkFonts.Measure(SilkFace.Ui600, 13f, TabLabels[i]).X;

            x += w + 7f * s;

            if (tabMode == 0)
            {
                var countColor = SilkPalette.Mix(SilkPalette.Ink3, SilkPalette.Ice, active);
                var clh = SilkFonts.LineHeight(SilkFace.Mono500, 10f);
                SilkFonts.Draw(dl, new Vector2(MathF.Round(x), MathF.Round(midY - clh * 0.5f)), U(countColor), SilkFace.Mono500, 10f, Count(model.Count((SilkTab)i)));
            }
        }

        if (tabMode == 2 && hovered)
        {
            var tip = TabTip(i);
            SilkTooltip.Hover(tip, min, max);
        }

        if (pressed && model.Tab != (SilkTab)i)
        {
            model.SetTab((SilkTab)i);
            Wave?.Invoke(null, null);
        }
    }

    private string TabTip(int i)
    {
        var count = model.Count((SilkTab)i);

        if (tabTips[i] is { } cached && tabTipCounts[i] == count && tabTipRevisions[i] == NoireLanguages.Revision)
            return cached;

        tabTipCounts[i] = count;
        tabTipRevisions[i] = NoireLanguages.Revision;
        return tabTips[i] = L.TabTip.With("tab", TabLabels[i], "count", Count(count));
    }

    public void Dispose()
    {
        cat.Dispose();
        dock.Dispose();
        kofiIcon.Dispose();
        discordIcon.Dispose();
    }
}
