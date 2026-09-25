using BypassEmote.EmoteSwap;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

public sealed class SilkDock : IDisposable
{
    private const float Width = 300f;
    private const float Gap = 12f;

    private static readonly float[] BackStops = [0f, 1f];
    private static readonly uint[] BackColors = [SilkMainDraw.Hex(0x070b16), SilkMainDraw.Hex(0x04060c)];
    private static readonly float[] SheenStops = [0f, 0.42f, 1f];
    private static readonly uint[] SheenColors =
    [
        SilkMainDraw.Col(255, 255, 255, 0.28f), SilkMainDraw.Col(255, 255, 255, 0f), SilkMainDraw.Col(255, 255, 255, 0f),
    ];
    private static readonly uint[] PlayColors = new uint[2];
    private static readonly TextList MatchTips = new(
        L.LoopExact, L.LoopLenient, L.SoundExact, L.SoundLenient,
        L.TurnExact, L.TurnLenient);

    private SilkEmoteEntry? entry;
    private bool open;
    private float openedAt = -10f;
    private float popAt = -10f;
    private Tween offset;
    private Tween notchY;
    private bool placeFirst;
    private bool offsetSet;
    private bool notchSet;
    private bool wasFocused;
    private bool right = true;
    private Vector2 windowMin;
    private Vector2 windowMax;

    private SilkSwapPlan? plan;
    private SilkEmoteEntry? planFor;

    private readonly List<string> nameLines = new();
    private string nameFor = string.Empty;
    private float nameWidth;
    private float nameEm;
    private bool poseThrough;
    private string poseCode = string.Empty;
    private int poseCodeIndex = -1;
    private int textRevision = -1;
    private readonly List<string> reasonLines = new();
    private readonly List<List<string>> detailLines = new();
    private SilkSwapPlan? reasonFor;
    private float reasonWidth;
    private float reasonEm;
    private bool reasonHover;
    private Vector2 reasonAnchorMin;
    private Vector2 reasonAnchorMax;
    private string throughFor = string.Empty;
    private string through = string.Empty;

    private Tween closeHover;
    private Tween playLift;
    private readonly Tween[] buttonHover = new Tween[8];

    internal bool IsOpen => open;

    internal SilkEmoteEntry? Entry => entry;

    internal Func<ImDrawListPtr, Vector2, Vector2, float, bool>? PaintBackdrop { get; set; }

    internal Func<float>? Opacity { get; set; }

    internal Func<bool>? ClickThrough { get; set; }

    internal bool MouseInside(Vector2 position)
        => open && position.X >= windowMin.X && position.Y >= windowMin.Y && position.X < windowMax.X && position.Y < windowMax.Y;

    internal void Open(SilkEmoteEntry target, float now, bool reduced)
    {
        var changed = !ReferenceEquals(entry, target);
        var wasOpen = open;
        entry = target;
        open = true;

        if (changed && !reduced)
            popAt = now;

        if (!wasOpen)
            openedAt = reduced ? -10f : now;

        if (changed || !wasOpen)
            placeFirst = true;

        if (changed)
            planFor = null;
    }

    internal void Close()
    {
        open = false;
        entry = null;
        offsetSet = false;
        notchSet = false;
    }

    private float planAt = -10f;
    private long planInputs = long.MinValue;

    private static long Inputs(SilkEmoteModel model)
    {
        long hash = model.Version;
        hash = hash * 31 + (int)Configuration.SelfBypassMode;
        hash = hash * 31 + (int)Configuration.LoopMatching;
        hash = hash * 31 + (int)Configuration.TurnMatching;
        hash = hash * 31 + (int)Configuration.SoundMatching;
        hash = hash * 31 + (int)Configuration.ModdedTargets;
        hash = hash * 31 + (int)Configuration.IdlePoseLoops;
        hash = hash * 31 + (int)Configuration.CachedDispatch;
        hash = hash * 31 + (int)Configuration.DispatchFidelity;
        hash = hash * 31 + Configuration.MaxTargetsPerRank;
        hash = hash * 31 + (SwapLayers.SwapOwnedEmotes ? 1 : 0);
        hash = hash * 31 + (Service.Penumbra is { Available: true } ? 1 : 0);
        hash = hash * 31 + (Service.Catalog is { Ready: true } ? 1 : 0);

        foreach (var blocked in Configuration.BlockedTargetEmotesEmoteSwap)
            hash = hash * 31 + blocked;

        foreach (var over in Configuration.EmoteOverrides)
        {
            hash = hash * 31 + over.SourceEmote;
            hash = hash * 31 + over.Targets.Count;
            hash = hash * 31 + (over.LimitedToTargets ? 1 : 0);

            foreach (var target in over.Targets)
                hash = hash * 31 + target;
        }

        if (NoireService.ObjectTable.LocalPlayer is { } player)
            hash = hash * 31 + (int)EmoteHelper.ConditionOf(player);

        return hash;
    }

    private void RefreshPlan(SilkEmoteModel model)
    {
        var inputs = Inputs(model);
        var stale = SilkUi.Time - planAt > 1f;

        if (!stale && ReferenceEquals(planFor, entry) && planInputs == inputs)
            return;

        planAt = SilkUi.Time;
        planFor = entry;
        planInputs = inputs;
        plan = entry != null && ShowsSwap(entry) ? Service.Orchestrator?.SilkPreviewTarget(entry.RowId) : null;
    }

    private static bool ShowsSwap(SilkEmoteEntry e)
        => EmoteActions.InEmoteSwap && Configuration.EnablePreviewPopup && !e.Invalid;

    internal void DrawWindow(SilkMainPainter view, Vector2 mainMin, Vector2 mainMax, float s, float now, bool reduced, bool hidden)
    {
        if (!open || entry == null)
            return;

        if (!Configuration.EnablePreviewPopup)
        {
            view.CloseDock();
            return;
        }

        if (hidden)
            return;

        using (NoireUI.Profiler.Measure("Silk.Dock.Plan"))
            RefreshPlan(view.Model);

        float height;

        using (NoireUI.Profiler.Measure("Silk.Dock.Measure"))
            height = Layout(view, default, Vector2.Zero, s, now, false);

        var mainH = mainMax.Y - mainMin.Y;
        height = MathF.Min(height, mainH);

        var viewport = ImGui.GetMainViewport();
        var vMin = viewport.Pos;
        var vMax = viewport.Pos + viewport.Size;
        var w = Width * s;
        var gap = Gap * s;
        right = vMax.X - mainMax.X >= w + gap + 8f * s || mainMin.X - vMin.X < w + gap + 8f * s;

        var hasRow = view.TryRowTop(entry.RowId, out var rowTop);
        var cy = hasRow ? rowTop + SilkMainPainter.RowMiddle * s : mainMin.Y + height * 0.5f;
        var lowest = MathF.Max(view.ListScreenMin.Y + 16f * s, view.ListScreenMax.Y - 16f * s);
        cy = Math.Clamp(cy, view.ListScreenMin.Y + 16f * s, lowest);

        if (!offsetSet || placeFirst)
        {
            var want = (hasRow ? rowTop + SilkMainPainter.RowMiddle * s : mainMin.Y + mainH * 0.5f) - mainMin.Y - height * 0.4f;
            var target = Math.Clamp(want, 0f, MathF.Max(0f, mainH - height)) / s;

            if (offsetSet && !reduced)
                offset.Go(target, now, 0.35f, SilkUi.EaseOut);
            else
                offset.Snap(target);

            offsetSet = true;
            placeFirst = false;
        }

        var top = mainMin.Y + Math.Clamp(offset.Value(now) * s, 0f, MathF.Max(0f, mainH - height));
        var left = right ? mainMax.X + gap : mainMin.X - gap - w;
        var ny = Math.Clamp(cy - top, 26f * s, MathF.Max(26f * s, height - 26f * s)) / s;

        if (!notchSet || reduced)
        {
            notchY.Snap(ny);
            notchSet = true;
        }
        else
        {
            notchY.Go(ny, now, 0.35f, SilkUi.EaseOut);
        }

        var enter = reduced ? 1f : Math.Clamp((now - openedAt) / 0.35f, 0f, 1f);
        var eased = SilkUi.EaseOut.Evaluate(enter);
        var dx = (right ? -10f : 10f) * (1f - eased) * s;
        var notchSide = 16f * s;
        var pos = new Vector2(MathF.Round(right ? left - notchSide : left), MathF.Round(top));
        var size = new Vector2(w + notchSide, height);
        windowMin = pos;
        windowMax = pos + size;

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoScrollWithMouse;

        var visible = ImGui.Begin("##silkdock"u8, flags);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(4);

        if (visible)
        {
            var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

            if (focused && !wasFocused)
                view.RequestGroupFront();

            wasFocused = focused;

            if (SilkUi.Settings.AlwaysOnTop)
                NoireWindowChrome.KeepInFront();
            else if ((view.GroupFrontPending || focused || view.MainFocused) && !SilkMainPainter.PopupBlocking())
                ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());

            var dl = ImGui.GetWindowDrawList();
            var start = dl.VtxBuffer.Size;
            var min = new Vector2(MathF.Round(left), MathF.Round(top));

            using (NoireUI.Profiler.Measure("Silk.Dock.Frame"))
                DrawFrame(dl, min, new Vector2(w, height), s, notchY.Value(now) * s);

            using (NoireUI.Profiler.Measure("Silk.Dock.Content"))
                Layout(view, dl, min, s, now, true);

            TickGroupDrag();

            using (NoireUI.Profiler.Measure("Silk.Dock.Post"))
            {
                if (dx != 0f)
                    SilkMainDraw.Transform(dl, start, Vector2.Zero, Vector2.One, 0f, new Vector2(dx, 0f));

                SilkMainDraw.MultiplyAlpha(dl, start, eased);
            }

            if (ClickThrough?.Invoke() == true)
                SilkPaint.DashedRing(min + new Vector2(1f), min + new Vector2(w, height) - new Vector2(1f), 15f * s,
                    SilkPalette.ClickThroughOutline, 4f * s, 4f * s);
        }

        ImGui.End();
        DrawReasonCard(s);
    }

    internal Action<Vector2>? MoveGroup;

    private bool groupDragging;
    private Vector2 groupDragLast;

    private void TickGroupDrag()
    {
        var mouse = ImGui.GetMousePos();

        if (groupDragging)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                groupDragging = false;
                return;
            }

            var delta = mouse - groupDragLast;
            groupDragLast = mouse;

            if (delta != Vector2.Zero)
                MoveGroup?.Invoke(delta);

            return;
        }

        if (MoveGroup != null && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsWindowHovered()
            && MouseInside(mouse) && !ImGui.IsAnyItemHovered() && !ImGui.IsAnyItemActive())
        {
            groupDragging = true;
            groupDragLast = mouse;
        }
    }

    private void DrawFrame(ImDrawListPtr dl, Vector2 min, Vector2 size, float s, float ny)
    {
        var max = min + size;
        var r = 16f * s;
        var op = Opacity?.Invoke() ?? 1f;
        var color = entry!.Color;

        var notchMin = right
            ? new Vector2(min.X - 14f * s, min.Y + ny - 18f * s)
            : new Vector2(max.X - 2f * s, min.Y + ny - 18f * s);
        var notchMax = notchMin + new Vector2(16f, 36f) * s;
        var notchFlags = right ? ImDrawFlags.RoundCornersLeft : ImDrawFlags.RoundCornersRight;
        dl.AddRectFilled(notchMin, notchMax, SilkMainDraw.Col(7, 11, 20, op), 9f * s, notchFlags);
        dl.AddRect(notchMin - new Vector2(0.5f * s), notchMax + new Vector2(0.5f * s), SilkMainDraw.Col(191, 211, 236, 0.14f),
            9.5f * s, notchFlags, 1f * s);
        var barMin = right
            ? new Vector2(notchMin.X + 5f * s, notchMin.Y + 10f * s)
            : new Vector2(notchMax.X - 8f * s, notchMin.Y + 10f * s);
        var barMax = barMin + new Vector2(3f, 16f) * s;
        SilkMainDraw.Shadow(dl, barMin, barMax, 2f * s, SilkMainDraw.Col(color), 8f * s);
        dl.AddRectFilled(barMin, barMax, SilkMainDraw.Col(color), 2f * s);

        dl.AddRectFilled(min, max, SilkMainDraw.Col(7, 11, 20, op), r);

        if (PaintBackdrop == null || !PaintBackdrop(dl, min, max, r))
        {
            var start = dl.VtxBuffer.Size;
            dl.AddRectFilled(min, max, SilkMainDraw.Col(255, 255, 255, op), r);
            SilkMainDraw.ShadeVertical(dl, start, min.Y, max.Y, BackStops, BackColors);
        }

        SilkMainDraw.OuterRing(dl, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.12f));
    }

    private float Layout(SilkMainPainter view, ImDrawListPtr dl, Vector2 min, float s, float now, bool draw)
    {
        var e = entry!;
        var color = e.Color;
        var w = Width * s;
        var cx = min.X + w * 0.5f;
        var ct = ClickThrough?.Invoke() ?? false;
        var y = min.Y;

        if (draw)
            DrawClose(view, dl, min, w, s, now);

        y += 22f * s;

        if (draw)
            DrawBig(dl, e, new Vector2(cx - 38f * s, y), s, now);

        y += 76f * s + 12f * s;

        EnsureName(e, s);
        var nameLine = 22f * SilkUi.TextScale * s;
        var nameLh = SilkFonts.LineHeight(SilkFace.Ui800, 20f);

        for (var i = 0; i < nameLines.Count; i++)
        {
            if (draw)
            {
                var lw = SilkFonts.Measure(SilkFace.Ui800, 20f, nameLines[i], -0.4f).X;
                SilkFonts.Draw(dl, new Vector2(MathF.Round(cx - lw * 0.5f), MathF.Round(y + (nameLine - nameLh) * 0.5f)),
                    SilkPalette.U32(SilkPalette.Ink), SilkFace.Ui800, 20f, nameLines[i], -0.4f);
            }

            y += nameLine;
        }

        y += 8f * s;
        y = Chips(dl, e, cx, y, w - 36f * s, s, color, draw);
        y += 9f * s;
        y = Meta(dl, e, cx, y, w - 36f * s, s, color, draw);
        y += 12f * s;

        if (plan != null && ShowsSwap(e))
        {
            y += 4f * s;
            y = Via(dl, view, e, plan, min.X + 12f * s, y, w - 24f * s, s, now, color, draw);
            y += 10f * s;
        }

        return Actions(dl, view, e, min.X, y, w, s, now, color, draw, ct) - min.Y;
    }

    private void EnsureName(SilkEmoteEntry e, float s)
    {
        var width = (Width - 36f) * s;

        if (ReferenceEquals(nameFor, e.Name) && MathF.Abs(nameWidth - width) < 0.5f && MathF.Abs(nameEm - SilkFonts.Em(1f)) < 0.001f)
            return;

        nameFor = e.Name;
        nameWidth = width;
        nameEm = SilkFonts.Em(1f);
        SilkText.Wrap(e.Name, 20f, SilkWeight.ExtraBold, width, nameLines);

        if (nameLines.Count == 0)
            nameLines.Add(e.Name);
    }

    private void DrawClose(SilkMainPainter view, ImDrawListPtr dl, Vector2 min, float w, float s, float now)
    {
        var bMin = new Vector2(min.X + w - 38f * s, min.Y + 10f * s);
        var bMax = bMin + new Vector2(28f * s);
        ImGui.SetCursorScreenPos(bMin);
        var clicked = ImGui.InvisibleButton("##dockclose"u8, bMax - bMin);
        var hot = ImGui.IsItemHovered();
        closeHover.Go(hot ? 1f : 0f, now, SilkUi.ReducedMotion ? 0f : 0.2f, UiCubicBezier.Ease);
        var h = closeHover.Value(now);

        if (h > 0.001f)
            dl.AddRectFilled(bMin, bMax, SilkMainDraw.Col(255, 107, 125, 0.14f * h), 8f * s);

        var color = SilkPalette.Mix(SilkPalette.Ink3, SilkPalette.CloseHoverText, h);
        SilkIcons.Draw(dl, SilkMainIcons.Close, bMin + new Vector2(7f * s), 14f * s, SilkPalette.U32(color));

        if (hot)
            SilkTooltip.Hover(CloseTip, bMin, bMax);

        if (clicked)
            view.CloseDock();
    }

    private static string CloseTip => L.Close.Text;
    private static string PreviewLabel => L.PreviewPopup.Text;
    private static string PreviewTip => L.PreviewPopupTip.Text;
    private static string BlockTip => L.NeverTarget.Text;
    private static string ForceTip => L.OnlyEmoteSwapSwaps.Text;

    private void DrawBig(ImDrawListPtr dl, SilkEmoteEntry e, Vector2 min, float s, float now)
    {
        var size = 76f * s;
        var max = min + new Vector2(size);
        var r = 19f * s;
        var start = dl.VtxBuffer.Size;
        var centre = (min + max) * 0.5f;

        SilkMainDraw.Shadow(dl, min, max, r, SilkMainDraw.Col(0, 0, 0, 0.9f), 18f * s, -10f * s, new Vector2(0f, 8f * s));
        SilkGameIcon.Draw(dl, e.IconId, min, max, r);

        var sheen = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, SilkMainDraw.Col(255, 255, 255), r);
        var angle = 155f * MathF.PI / 180f;
        var dir = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var half = (MathF.Abs(size * dir.X) + MathF.Abs(size * dir.Y)) * 0.5f;
        SilkMainDraw.Shade(dl, sheen, centre - dir * half, centre + dir * half, SheenStops, SheenColors);
        SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(255, 255, 255, 0.14f));

        var t = (now - popAt) / 0.5f;

        if (t is >= 0f and < 1f && !SilkUi.ReducedMotion)
        {
            var eased = SilkUi.EaseOut.Evaluate(t);
            SilkMainDraw.Transform(dl, start, centre, new Vector2(0.86f + 0.14f * eased), 0f, Vector2.Zero);
            SilkMainDraw.MultiplyAlpha(dl, start, 0.4f + 0.6f * eased);
        }
    }

    private static float Chips(ImDrawListPtr dl, SilkEmoteEntry e, float cx, float top, float maxW, float s, Vector3 color, bool draw)
    {
        var commands = e.Commands;
        var count = Math.Max(1, commands.Length);
        var chipH = SilkFonts.LineHeight(SilkFace.Mono500, 11f) + 6f * s;
        var gap = 4f * s;
        var index = 0;
        var y = top;

        while (index < count)
        {
            var lineW = 0f;
            var end = index;

            while (end < count)
            {
                var cw = ChipWidth(commands.Length == 0 ? L.NoTextCommand.Text : commands[end], s);

                if (end > index && lineW + gap + cw > maxW)
                    break;

                lineW += (end > index ? gap : 0f) + cw;
                end++;
            }

            var x = cx - lineW * 0.5f;

            for (var i = index; i < end; i++)
            {
                var text = commands.Length == 0 ? L.NoTextCommand.Text : commands[i];
                var cw = ChipWidth(text, s);

                if (draw)
                {
                    var cMin = new Vector2(MathF.Round(x), MathF.Round(y));
                    var cMax = cMin + new Vector2(cw, chipH);
                    dl.AddRectFilled(cMin, cMax, SilkMainDraw.Col(0, 0, 0, 0.35f), 6f * s);
                    SilkMainDraw.InsetRing(dl, cMin, cMax, 6f * s,
                        i == 0 ? SilkMainDraw.Col(color, 0.4f) : SilkMainDraw.Col(191, 211, 236, 0.09f));
                    SilkFonts.Draw(dl, cMin + new Vector2(7f * s, 3f * s), SilkPalette.U32(i == 0 ? SilkPalette.Ink : SilkPalette.Ink2),
                        SilkFace.Mono500, 11f, text);
                }

                x += cw + gap;
            }

            index = end;
            y += chipH + (index < count ? gap : 0f);
        }

        return y;
    }

    private static float ChipWidth(string text, float s) => SilkFonts.Measure(SilkFace.Mono500, 11f, text).X + 14f * s;

    private static float Meta(ImDrawListPtr dl, SilkEmoteEntry e, float cx, float top, float room, float s, Vector3 color, bool draw)
    {
        var chipText = e.Owned ? L.Owned.Text : L.Locked.Text;
        var chipTextW = SilkFonts.Measure(SilkFace.Ui500, 11f, chipText).X;
        var lh = SilkFonts.LineHeight(SilkFace.Ui500, 11f);
        var rowH = lh + 4f * s;
        var patch = e.Patch;
        var ids = Configuration.ShowEmoteIds;
        var gap = 6f * s;
        var separator = gap + 3f * s + gap;
        var categoryW = SilkFonts.Measure(SilkFace.Ui500, 11f, e.CategoryName).X;
        var patchW = patch != null ? SilkFonts.Measure(SilkFace.Ui500, 11f, e.PatchLabel).X : 0f;
        var idW = ids ? SilkFonts.Measure(SilkFace.Ui500, 11f, e.IdLabel).X : 0f;
        var fixedW = 16f * s + separator + (patch != null ? separator : 0f) + (ids ? separator : 0f);
        var textsW = chipTextW + categoryW + patchW + idW;

        if (fixedW + textsW > room && textsW > 0f)
        {
            var share = MathF.Max(0f, room - fixedW) / textsW;
            chipTextW *= share;
            categoryW *= share;
            patchW *= share;
            idW *= share;
        }

        var chipW = chipTextW + 16f * s;
        var total = fixedW + chipTextW + categoryW + patchW + idW;

        if (!draw)
            return top + rowH;

        var x = MathF.Round(cx - total * 0.5f);
        var cMin = new Vector2(x, MathF.Round(top));
        var cMax = cMin + new Vector2(chipW, rowH);
        var ok = new Vector3(114, 224, 180);
        var chipColor = e.Owned ? ok : color;
        dl.AddRectFilled(cMin, cMax, SilkMainDraw.Col(chipColor, e.Owned ? 0.1f : 0.14f), rowH * 0.5f);
        SilkMainDraw.InsetRing(dl, cMin, cMax, rowH * 0.5f, SilkMainDraw.Col(chipColor, e.Owned ? 0.28f : 0.3f));
        SilkFonts.DrawFitted(dl, cMin + new Vector2(8f * s, 2f * s), SilkMainDraw.Col(chipColor), SilkFace.Ui500, 11f, chipText, chipTextW);

        x += chipW;
        var ink3 = SilkPalette.U32(SilkPalette.Ink3);
        var textTop = MathF.Round(top + (rowH - lh) * 0.5f);

        x = Dot(dl, x, top, rowH, gap, s);
        SilkFonts.DrawFitted(dl, new Vector2(x, textTop), ink3, SilkFace.Ui500, 11f, e.CategoryName, categoryW);
        x += categoryW;

        if (patch != null)
        {
            x = Dot(dl, x, top, rowH, gap, s);
            SilkFonts.DrawFitted(dl, new Vector2(x, textTop), ink3, SilkFace.Ui500, 11f, e.PatchLabel, patchW);
            x += patchW;
        }

        if (ids)
        {
            x = Dot(dl, x, top, rowH, gap, s);
            SilkFonts.DrawFitted(dl, new Vector2(x, textTop), ink3, SilkFace.Ui500, 11f, e.IdLabel, idW);
        }

        return top + rowH;
    }

    private static float Dot(ImDrawListPtr dl, float x, float top, float rowH, float gap, float s)
    {
        x += gap;
        dl.AddCircleFilled(new Vector2(x + 1.5f * s, top + rowH * 0.5f), 1.5f * s, SilkPalette.U32(SilkPalette.Ink3), 8);
        return MathF.Round(x + 3f * s + gap);
    }

    private void EnsureReason(SilkSwapPlan swap, float width, float s)
    {
        if (ReferenceEquals(reasonFor, swap) && MathF.Abs(reasonWidth - width) < 0.5f
            && MathF.Abs(reasonEm - SilkFonts.Em(1f)) < 0.001f)
        {
            return;
        }

        reasonFor = swap;
        reasonWidth = width;
        reasonEm = SilkFonts.Em(1f);
        SilkText.Wrap(swap.Headline, 12f, SilkWeight.Medium, width, reasonLines);

        while (detailLines.Count < swap.Details.Length)
            detailLines.Add(new List<string>());

        while (detailLines.Count > swap.Details.Length)
            detailLines.RemoveAt(detailLines.Count - 1);

        for (var i = 0; i < swap.Details.Length; i++)
            SilkText.Wrap(swap.Details[i], 11.5f, SilkWeight.Medium, (SilkRowCards.Width - SilkRowCards.Pad * 2f - 12f) * s, detailLines[i]);
    }

    private float Via(ImDrawListPtr dl, SilkMainPainter view, SilkEmoteEntry e, SilkSwapPlan swap, float x, float top, float w,
        float s, float now, Vector3 color, bool draw)
    {
        var headLh = SilkFonts.LineHeight(SilkFace.Ui700, 9.5f);
        var bLh = SilkFonts.LineHeight(SilkFace.Ui600, 12.5f);
        var codeLine = SilkFonts.LineBox(13f, 1.4f);
        var rowH = MathF.Max(30f * s, bLh + codeLine);
        var inner = w - 24f * s;
        var reasonLine = SilkFonts.LineBox(12f, 1.35f);
        float body;

        if (swap.Kind == SilkPreviewKind.None)
        {
            EnsureReason(swap, inner, s);
            body = MathF.Max(1, reasonLines.Count) * reasonLine;
        }
        else if (swap.Kind == SilkPreviewKind.IdlePose)
        {
            body = rowH;
        }
        else
        {
            body = rowH + 10f * s + 22f * s;
        }

        var height = 11f * s + headLh + 9f * s + body + 11f * s;

        if (!draw)
            return top + height;

        var min = new Vector2(x, top);
        var max = new Vector2(x + w, top + height);
        dl.AddRectFilled(min, max, SilkMainDraw.Col(0, 0, 0, 0.28f), 11f * s);
        SilkMainDraw.InsetRing(dl, min, max, 11f * s, SilkMainDraw.Col(191, 211, 236, 0.09f));

        var ix = x + 12f * s;
        var iy = top + 11f * s;
        var tag = swap.FromOverride ? L.OverrideTag.Text : swap.Kind == SilkPreviewKind.IdlePose ? L.IdlePoseTag.Text : L.EmoteSwap.Text;
        var tagW = SilkFonts.FittedWidth(SilkFace.Mono500, 10f, tag, (w - 24f * s) * 0.45f);
        var tagLh = SilkFonts.LineHeight(SilkFace.Mono500, 10f);
        var tagX = x + w - 12f * s - tagW;
        var headRoom = tagX - (swap.Kind == SilkPreviewKind.None && swap.Details.Length > 0 ? 28f * s : 10f * s) - ix;

        SilkFonts.DrawFitted(dl, new Vector2(ix, iy), SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui700, 9.5f, SilkFonts.Upper(L.SwapPreview.Text), headRoom, 1f);
        SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(tagX), MathF.Round(iy + (headLh - tagLh) * 0.5f)),
            SilkPalette.U32(SilkPalette.Ink3), SilkFace.Mono500, 10f, tag, tagW);

        if (swap.Kind == SilkPreviewKind.None && swap.Details.Length > 0)
        {
            var infoMin = new Vector2(MathF.Round(tagX - 20f * s), MathF.Round(iy + (headLh - 13f * s) * 0.5f));
            var infoMax = infoMin + new Vector2(13f * s);
            var hot = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(infoMin - new Vector2(4f * s), infoMax + new Vector2(4f * s));
            reasonHover = hot;
            reasonAnchorMin = infoMin;
            reasonAnchorMax = infoMax;
            SilkIcons.Draw(dl, SilkMainIcons.Info, infoMin, 13f * s,
                SilkPalette.U32(hot ? SilkPalette.Ink : SilkPalette.Ink3));
        }

        iy += headLh + 9f * s;

        if (swap.Kind == SilkPreviewKind.None)
        {
            for (var i = 0; i < reasonLines.Count; i++)
            {
                var lineTop = iy + i * reasonLine + (reasonLine - SilkFonts.LineHeight(SilkFace.Ui500, 12f)) * 0.5f;
                SilkFonts.Draw(dl, new Vector2(MathF.Round(ix), MathF.Round(lineTop)), SilkPalette.U32(SilkPalette.Ink2),
                    SilkFace.Ui500, 12f, reasonLines[i]);
            }

            return top + height;
        }

        var imgTop = iy + (rowH - 30f * s) * 0.5f;
        var ring = SilkMainDraw.Col(191, 211, 236, 0.16f);
        var aMin = new Vector2(ix, imgTop);
        SilkGameIcon.Draw(dl, e.IconId, aMin, aMin + new Vector2(30f * s), 8f * s);
        SilkMainDraw.OuterRing(dl, aMin, aMin + new Vector2(30f * s), 8f * s, ring);
        HoverTip(aMin, aMin + new Vector2(30f * s), e.Name);
        ix += 30f * s + 9f * s;
        SilkIcons.Draw(dl, SilkMainIcons.ArrowThin, new Vector2(ix, iy + (rowH - 16f * s) * 0.5f), 16f * s, SilkMainDraw.Col(color));
        ix += 16f * s + 9f * s;

        var pose = swap.Kind == SilkPreviewKind.IdlePose;
        var target = pose ? ChangePose(view) : view.Model.Get(swap.Target!.RowId);
        var bMin = new Vector2(ix, imgTop);

        if (target != null)
        {
            SilkGameIcon.Draw(dl, target.IconId, bMin, bMin + new Vector2(30f * s), 8f * s);
            HoverTip(bMin, bMin + new Vector2(30f * s), target.Name);
        }

        SilkMainDraw.OuterRing(dl, bMin, bMin + new Vector2(30f * s), 8f * s, ring);
        ix += 30f * s + 9f * s;

        var targetName = pose ? L.YourIdlePoseLower.Text : target?.Name ?? swap.Target!.Command;

        if (!ReferenceEquals(throughFor, targetName) || poseThrough != pose || textRevision != NoireLanguages.Revision)
        {
            throughFor = targetName;
            poseThrough = pose;
            textRevision = NoireLanguages.Revision;
            poseCodeIndex = -1;
            through = pose ? L.YourIdlePose.Text : L.PlaysThrough.With("emote", targetName);
        }

        var textRight = x + w - 12f * s;
        SilkFonts.Draw(dl, new Vector2(MathF.Round(ix), MathF.Round(iy)), SilkPalette.U32(SilkPalette.Ink), SilkFace.Ui600, 12.5f,
            through, 0f, textRight - ix);

        string code;

        if (pose)
        {
            if (poseCodeIndex != swap.PoseIndex)
            {
                poseCodeIndex = swap.PoseIndex;
                poseCode = L.PoseNumber.With("pose", swap.PoseIndex.ToString());
            }

            code = poseCode;
        }
        else
        {
            code = target is { Commands.Length: > 0 } ? target.Commands[0] : "/" + swap.Target!.Command;
        }

        var codeLh = SilkFonts.LineHeight(SilkFace.Mono400, 10.5f);
        SilkFonts.Draw(dl, new Vector2(MathF.Round(ix), MathF.Round(iy + bLh + (codeLine - codeLh) * 0.5f)),
            SilkPalette.U32(SilkPalette.Ink3), SilkFace.Mono400, 10.5f, code, 0f, textRight - ix);
        iy += rowH + 10f * s;

        if (pose)
            return top + height;

        var cell = (inner - 10f * s) / 3f;

        for (var i = 0; i < 3; i++)
        {
            var exact = i switch
            {
                0 => swap.Target!.LoopKind == swap.Source!.LoopKind,
                1 => swap.Target!.Sound == swap.Source!.Sound,
                _ => swap.Target!.Turn == swap.Source!.Turn,
            };
            var label = i == 0 ? L.Loop.Text : i == 1 ? L.Sound.Text : L.Turn.Text;
            var cMin = new Vector2(x + 12f * s + i * (cell + 5f * s), iy);
            var cMax = cMin + new Vector2(cell, 22f * s);
            dl.AddRectFilled(cMin, cMax, SilkMainDraw.Col(191, 211, 236, 0.05f), 6f * s);
            var lw = SilkFonts.FittedWidth(SilkFace.Ui600, 10.5f, label, cell - 22f * s);
            var contentW = 10f * s + lw;
            var lx = (cMin.X + cMax.X - contentW) * 0.5f;
            var dot = exact ? new Vector3(114, 224, 180) : new Vector3(255, 190, 110);
            var dotCentre = new Vector2(lx + 2.5f * s, (cMin.Y + cMax.Y) * 0.5f);
            dl.AddCircleFilled(dotCentre, 5f * s, SilkMainDraw.Col(dot, 0.25f), 16);
            dl.AddCircleFilled(dotCentre, 2.5f * s, SilkMainDraw.Col(dot), 12);
            var llh = SilkFonts.LineHeight(SilkFace.Ui600, 10.5f);
            SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(lx + 10f * s), MathF.Round((cMin.Y + cMax.Y - llh) * 0.5f)),
                SilkPalette.U32(SilkPalette.Ink2), SilkFace.Ui600, 10.5f, label, lw);
            HoverTip(cMin, cMax, MatchTips[i * 2 + (exact ? 0 : 1)]);
        }

        return top + height;
    }

    private void DrawReasonCard(float s)
    {
        if (!reasonHover || plan is not { Details.Length: > 0 } swap)
            return;

        var fg = ImGui.GetForegroundDrawList();
        var pad = SilkRowCards.Pad * s;
        var width = SilkRowCards.Width * s;
        var line = SilkFonts.LineBox(11.5f, 1.45f);
        var height = pad * 2f;

        for (var i = 0; i < detailLines.Count; i++)
            height += detailLines[i].Count * line + (i > 0 ? 4f * s : 0f);

        var viewport = ImGui.GetMainViewport();
        var vMin = viewport.Pos;
        var vMax = viewport.Pos + viewport.Size;
        var x = reasonAnchorMax.X + 10f * s;

        if (x + width > vMax.X - 8f * s)
            x = reasonAnchorMin.X - width - 10f * s;

        x = MathF.Max(vMin.X + 8f * s, x);
        var y = Math.Clamp((reasonAnchorMin.Y + reasonAnchorMax.Y) * 0.5f - height * 0.5f, vMin.Y + 8f * s,
            MathF.Max(vMin.Y + 8f * s, vMax.Y - height - 8f * s));

        var min = new Vector2(MathF.Round(x), MathF.Round(y));
        var max = min + new Vector2(width, height);
        SilkMainDraw.Shadow(fg, min, max, 12f * s, SilkMainDraw.Col(0, 0, 0, 0.95f), 50f * s, -12f * s, new Vector2(0f, 20f * s));
        fg.AddRectFilled(min, max, SilkMainDraw.Col(10, 15, 26, 0.98f), 12f * s);
        SilkMainDraw.OuterRing(fg, min, max, 12f * s, SilkMainDraw.Col(191, 211, 236, 0.16f));

        var top = min.Y + pad;

        for (var i = 0; i < detailLines.Count; i++)
        {
            if (i > 0)
                top += 4f * s;

            for (var k = 0; k < detailLines[i].Count; k++)
            {
                var textTop = top + k * line + (line - SilkFonts.LineHeight(SilkFace.Ui500, 11.5f)) * 0.5f;
                SilkFonts.Draw(fg, new Vector2(MathF.Round(min.X + pad), MathF.Round(textTop)),
                    SilkPalette.U32(k == 0 ? SilkPalette.Ink2 : SilkPalette.Ink3), SilkFace.Ui500, 11.5f, detailLines[i][k]);
            }

            top += detailLines[i].Count * line;
        }
    }

    private static uint changePoseRow;

    private static SilkEmoteEntry? ChangePose(SilkMainPainter view)
    {
        if (changePoseRow == 0)
            changePoseRow = EmoteHelper.GetEmoteByCommand("/cpose") is { } emote ? emote.RowId : uint.MaxValue;

        return changePoseRow == uint.MaxValue ? null : view.Model.Get(changePoseRow);
    }

    private static void HoverTip(Vector2 min, Vector2 max, string text)
    {
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(min, max))
            SilkTooltip.Hover(text, min, max);
    }

    private float Actions(ImDrawListPtr dl, SilkMainPainter view, SilkEmoteEntry e, float left, float top, float w, float s,
        float now, Vector3 color, bool draw, bool ct)
    {
        var x = left + 12f * s;
        var inner = w - 24f * s;
        var lblH = SilkFonts.LineHeight(SilkFace.Ui700, 9.5f);
        var assignable = e.Assignable;
        var force = EmoteActions.CanForceSwap;
        var hasSecond = assignable || force;
        var height = 1f * s + 10f * s + 40f * s + 7f * s + 30f * s + (hasSecond ? 7f * s + 30f * s : 0f)
            + 7f * s + lblH + 7f * s + 30f * s + 9f * s + SilkControls.SwitchHeight * s + 12f * s;

        if (!draw)
            return top + height;

        var min = new Vector2(left, top);
        var max = new Vector2(left + w, top + height);
        dl.AddRectFilled(min, max, SilkMainDraw.Col(0, 0, 0, 0.2f), 16f * s, ImDrawFlags.RoundCornersBottom);
        dl.AddRectFilled(min, new Vector2(max.X, min.Y + 1f * s), SilkMainDraw.Col(191, 211, 236, 0.09f));

        var y = top + 1f * s + 10f * s;

        if (PlayButton(dl, new Vector2(x, y), new Vector2(x + inner, y + 40f * s), s, now, color) && !ct)
            view.Play(e);

        y += 40f * s + 7f * s;
        var half = (inner - 6f * s) * 0.5f;
        var fav = view.Model.IsFavourite(e.RowId);
        var blk = view.Model.IsBlocked(e.RowId);

        if (Ghost(dl, 0, new Vector2(x, y), new Vector2(x + half, y + 30f * s), fav ? L.Favourited.Text : L.Favourite.Text,
                SilkMainIcons.Star, fav ? 1 : 0, s, now) && !ct)
        {
            view.Model.ToggleFavourite(e.RowId);
        }

        var blkMin = new Vector2(x + half + 6f * s, y);
        var blkMax = new Vector2(x + inner, y + 30f * s);

        if (Ghost(dl, 1, blkMin, blkMax, blk ? L.BlockedAsTarget.Text : L.BlockAsTarget.Text, SilkMainIcons.BlockDock, blk ? 2 : 0, s, now) && !ct)
            view.Model.ToggleBlocked(e.RowId);

        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(blkMin, blkMax))
            SilkTooltip.Hover(BlockTip, blkMin, blkMax);

        y += 30f * s;

        if (hasSecond)
        {
            y += 7f * s;
            var both = assignable && force;
            var firstMax = both ? new Vector2(x + half, y + 30f * s) : new Vector2(x + inner, y + 30f * s);

            if (force)
            {
                var disabled = !EmoteActions.InEmoteSwap;

                if (Ghost(dl, 2, new Vector2(x, y), firstMax, L.ForceSwap.Text, SilkMainIcons.Swap, disabled ? -1 : 0, s, now) && !ct && !disabled)
                    EmoteActions.ForceSwap(e.Row);

                if (disabled && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(x, y), firstMax))
                    SilkTooltip.Hover(ForceTip, new Vector2(x, y), firstMax);
            }

            if (assignable)
            {
                var hMin = both ? new Vector2(x + half + 6f * s, y) : new Vector2(x, y);

                if (Ghost(dl, 3, hMin, new Vector2(x + inner, y + 30f * s), L.HotbarButton.Text, SilkMainIcons.Hotbar, 0, s, now) && !ct)
                    EmoteActions.OpenHotbar(e.Row);
            }

            y += 30f * s;
        }

        y += 7f * s;
        SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(x + 2f * s), MathF.Round(y)), SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui700, 9.5f,
            SilkFonts.Upper(L.PlayOnCompanion.Text), inner - 4f * s, 1f);
        y += lblH + 7f * s;

        var third = (inner - 12f * s) / 3f;

        for (var i = 0; i < 3; i++)
        {
            var cMin = new Vector2(x + i * (third + 6f * s), y);
            var label = i == 0 ? L.Minion.Text : i == 1 ? L.Pet.Text : L.Chocobo.Text;

            if (Ghost(dl, 4 + i, cMin, cMin + new Vector2(third, 30f * s), label, null, 0, s, now) && !ct)
                EmoteActions.PlayOn((Companion)i, e.Row);
        }

        y += 30f * s + 9f * s;
        var switchH = SilkControls.SwitchHeight * s;
        var labelLh = SilkFonts.LineHeight(SilkFace.Ui600, 11.5f);
        var preview = Configuration.EnablePreviewPopup;
        var infoSize = 14f * s;
        var infoMin = new Vector2(x + inner - infoSize, MathF.Round(y + (switchH - infoSize) * 0.5f));
        var switchX = infoMin.X - 8f * s - SilkControls.SwitchWidth * s;

        SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(x + 2f * s), MathF.Round(y + (switchH - labelLh) * 0.5f)),
            SilkPalette.U32(SilkPalette.Ink2), SilkFace.Ui600, 11.5f, PreviewLabel, switchX - 10f * s - (x + 2f * s));

        var infoMax = infoMin + new Vector2(infoSize, infoSize);

        if (SilkControls.Switch("##silkdockpreview", ref preview, new Vector2(switchX, y)) && !ct)
        {
            Configuration.EnablePreviewPopup = preview;
        }

        var infoHovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(infoMin - new Vector2(3f * s), infoMax + new Vector2(3f * s));
        SilkIcons.Draw(dl, SilkMainIcons.Info, infoMin, infoSize, SilkPalette.U32(infoHovered ? SilkPalette.Ink2 : SilkPalette.Ink3));

        if (infoHovered)
            SilkTooltip.Hover(PreviewTip, infoMin, infoMax, true);

        return top + height;
    }

    private bool PlayButton(ImDrawListPtr dl, Vector2 min, Vector2 max, float s, float now, Vector3 color)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton("##dockplay"u8, max - min);
        var hot = ImGui.IsItemHovered();
        playLift.Go(hot ? -1f : 0f, now, SilkUi.ReducedMotion ? 0f : 0.2f, SilkUi.EaseOut);
        var lift = new Vector2(0f, playLift.Value(now) * s);
        var start = dl.VtxBuffer.Size;
        var r = 11f * s;

        SilkMainDraw.Shadow(dl, min, max, r, SilkMainDraw.Col(0, 0, 0, 0.6f), 8f * s, -4f * s, new Vector2(0f, 3f * s));
        var fill = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, SilkMainDraw.Col(255, 255, 255), r);
        PlayColors[0] = SilkMainDraw.Col(color);
        PlayColors[1] = SilkMainDraw.Col(color, 0.82f);
        SilkMainDraw.ShadeVertical(dl, fill, min.Y, max.Y, BackStops, PlayColors);
        SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(255, 255, 255, 0.18f));

        var luminance = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;
        var onColor = luminance > 150f ? SilkMainDraw.Hex(0x07101e) : SilkMainDraw.Hex(0xffffff);
        var label = L.PlayEmote.Text;
        var small = L.OrDoubleClick.Text;
        var labelW = SilkFonts.Measure(SilkFace.Ui800, 13.5f, label).X;
        var smallW = SilkFonts.Measure(SilkFace.Ui600, 10f, small).X;
        var room = max.X - min.X - 24f * s - 13f * s - 9f * s;

        if (labelW + 9f * s + smallW > room)
        {
            var left = room - labelW - 9f * s;

            if (left >= 40f * s)
            {
                smallW = left;
            }
            else
            {
                small = string.Empty;
                smallW = 0f;
                labelW = MathF.Min(labelW, room);
            }
        }

        var total = 13f * s + 9f * s + labelW + (smallW > 0f ? 9f * s + smallW : 0f);
        var x = (min.X + max.X - total) * 0.5f;
        var midY = (min.Y + max.Y) * 0.5f;
        var lh = SilkFonts.LineHeight(SilkFace.Ui800, 13.5f);

        SilkIcons.Draw(dl, SilkMainIcons.Play, new Vector2(MathF.Round(x), MathF.Round(midY - 6.5f * s)), 13f * s, onColor);
        x += 22f * s;
        SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(x), MathF.Round(midY - lh * 0.5f)), onColor, SilkFace.Ui800, 13.5f, label, labelW);
        x += labelW + 9f * s;
        var baseline = midY - lh * 0.5f + SilkFonts.Ascent(SilkFace.Ui800, 13.5f);

        if (smallW > 0f)
        {
            SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(x), MathF.Round(baseline - SilkFonts.Ascent(SilkFace.Ui600, 10f))),
                SilkMainDraw.WithAlpha(onColor, 0.65f), SilkFace.Ui600, 10f, small, smallW);
        }

        if (lift.Y != 0f)
            SilkMainDraw.Transform(dl, start, Vector2.Zero, Vector2.One, 0f, lift);

        return clicked;
    }

    private bool Ghost(ImDrawListPtr dl, int index, Vector2 min, Vector2 max, string label, SilkIconShape? icon, int state,
        float s, float now)
    {
        ImGui.SetCursorScreenPos(min);
        ImGui.PushID(index + 200);
        var clicked = ImGui.InvisibleButton("g"u8, max - min);
        var hot = ImGui.IsItemHovered() && state >= 0;
        ImGui.PopID();

        buttonHover[index].Go(hot ? 1f : 0f, now, SilkUi.ReducedMotion ? 0f : 0.2f, UiCubicBezier.Ease);
        var h = buttonHover[index].Value(now);
        var r = 8f * s;
        Vector4 text;

        switch (state)
        {
            case 1:
                dl.AddRectFilled(min, max, SilkMainDraw.Col(255, 211, 110, 0.07f), r);
                SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(255, 211, 110, 0.35f));
                text = SilkPalette.Star;
                break;
            case 2:
                dl.AddRectFilled(min, max, SilkMainDraw.Col(255, 111, 134, 0.07f), r);
                SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(255, 111, 134, 0.35f));
                text = SilkPalette.Bad;
                break;
            default:
                dl.AddRectFilled(min, max, SilkMainDraw.Col(191, 211, 236, 0.05f + 0.04f * h), r);
                SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.09f));
                text = state < 0 ? SilkPalette.Fade(SilkPalette.Ink3, 0.7f) : SilkPalette.Mix(SilkPalette.Ink2, SilkPalette.Ink, h);
                break;
        }

        var iconW = icon != null ? 18f * s : 0f;
        var labelW = SilkFonts.FittedWidth(SilkFace.Ui600, 11.5f, label, max.X - min.X - 16f * s - iconW);
        var x = (min.X + max.X - labelW - iconW) * 0.5f;
        var midY = (min.Y + max.Y) * 0.5f;

        if (icon != null)
        {
            SilkIcons.Draw(dl, icon, new Vector2(MathF.Round(x), MathF.Round(midY - 6f * s)), 12f * s, SilkPalette.U32(text));
            x += iconW;
        }

        var lh = SilkFonts.LineHeight(SilkFace.Ui600, 11.5f);
        SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(x), MathF.Round(midY - lh * 0.5f)), SilkPalette.U32(text), SilkFace.Ui600, 11.5f, label, labelW);
        return clicked;
    }

    public void Dispose()
    {
    }
}
