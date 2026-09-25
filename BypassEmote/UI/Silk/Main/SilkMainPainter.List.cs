using BypassEmote.EmoteSwap;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal enum RowZone
{
    None,
    Row,
    Favourite,
    Block,
    Info,
}

internal sealed partial class SilkMainPainter
{
    private readonly record struct RowMetrics(
        float Height, float Rounding, float FavX, float BlockX, float MarkY, float MarkSize, float MarkIcon,
        float IconX, float IconY, float IconSize, float IconRounding, float BadgeOuter, float BadgeInner, float BadgeCheck,
        float NameX, float NamePx, float IdPx, float CommandPx, float InfoRight, float InfoY, float InfoSize, float InfoHitRight, float InfoHitY);

    private static readonly RowMetrics NormalRow = new(40f, 10f, 6f, 36f, 9f, 22f, 13f, 68f, 6f, 28f, 7f, 8f, 6f, 10f, 104f, 13.5f, 10.5f, 11.5f, 28f, 13f, 14f, 32f, 9f);
    private static readonly RowMetrics CompactRow = new(29f, 7f, 4f, 28f, 4.5f, 20f, 12f, 52f, 2f, 25f, 6f, 7f, 5.2f, 8.5f, 85f, 13.5f, 10.5f, 11.5f, 26f, 7.5f, 14f, 30f, 4.5f);

    private static RowMetrics Row => Configuration.CompactEmoteList ? CompactRow : NormalRow;

    private static float RowHeight => Row.Height;

    internal static float RowMiddle => Row.Height * 0.5f;
    private const float ListPad = 6f;
    private const float DoubleClickSeconds = 0.2f;

    private SilkEmoteEntry? pendingOpen;
    private SilkEmoteEntry? lastClicked;
    private float lastClickedAt;
    private float pendingOpenAt;

    private static readonly float ItalicShear = MathF.Tan(12f * MathF.PI / 180f);

    private uint hoverRow;
    private Tween hoverRowT;
    private uint fadeRow;
    private Tween fadeRowT;
    private uint markRow;
    private RowZone markZone;
    private Tween markT;
    private uint selectedRow;
    private Tween selectT;
    private uint previousSelected;
    private Tween previousSelectT;
    private uint sweepRow;
    private float sweepAt = -10f;
    private RowZone pressZone;
    private float lastScroll;
    private float listScroll;
    private bool listHovered;
    private bool scrollDragging;
    private float scrollDragStart;
    private float scrollDragOrigin;
    private Vector2 rowLeftTop;
    private Vector2 listScreenMin;
    private Vector2 listScreenMax;

    internal uint SelectedRow => selectedRow;

    internal Vector2 ListScreenMin => listScreenMin;

    internal Vector2 ListScreenMax => listScreenMax;

    internal bool TryRowTop(uint rowId, out float screenTop)
    {
        screenTop = 0f;
        var view = model.View;

        for (var i = 0; i < view.Count; i++)
        {
            if (view[i].RowId != rowId)
                continue;

            screenTop = rowLeftTop.Y + i * RowHeight * s;
            return true;
        }

        return false;
    }

    private void DrawListBox(float top, float bottom)
    {
        var width = WidthCss;
        var min = P(14f, top);
        var max = P(width - 14f, bottom);
        var r = 12f * s;
        dl.AddRectFilled(min, max, SilkMainDraw.Col(9, 13, 22, 0.66f), r, ImDrawFlags.RoundCornersBottom);
        dl.AddRect(min + new Vector2(0.5f * s), max - new Vector2(0.5f * s), SilkMainDraw.Col(191, 211, 236, 0.09f),
            r - 0.5f * s, ImDrawFlags.RoundCornersBottom, 1f * s);

        var y = top;
        var quick = model.Tab switch
        {
            SilkTab.Favourites => favAdd,
            SilkTab.Blocked => blockAdd,
            _ => null,
        };

        if (quick != null)
        {
            quick.DrawField(dl, P(24f, top + 6f), P(width - 24f, top + 34f), s);
            y = top + 38f;
        }

        if (model.Tab != SilkTab.Favourites)
            favAdd.Close();

        if (model.Tab != SilkTab.Blocked)
            blockAdd.Close();

        var footer = width > 540f ? FooterHeight() : 0f;
        DrawRows(P(14f, y), P(width - 14f, bottom - footer));

        if (footer > 0f)
            DrawFooter(bottom - footer, width);
    }

    private float FooterHeight()
    {
        var kbd = SilkFonts.LineHeight(SilkFace.Mono500, 10f) / s + 2f;
        var text = SilkFonts.LineHeight(SilkFace.Ui500, 11f) / s;
        return 1f + 8f + MathF.Max(kbd, text) + 9f;
    }

    private static readonly TextList HintKeysPreview = new(L.HintClick, L.HintDoubleClick, L.HintRightClick, L.HintDrag);
    private static readonly TextList HintTextsPreview = new(L.HintDetails, L.HintPlay, L.HintMore, L.HintHotbar);
    private static readonly TextList HintKeysPlain = new(L.HintClick, L.HintRightClick, L.HintDrag);
    private static readonly TextList HintTextsPlain = new(L.HintPlaySelf, L.HintMore, L.HintHotbar);

    internal static (string[] Keys, string[] Texts) Hints()
        => Configuration.EnablePreviewPopup ? (HintKeysPreview.Array, HintTextsPreview.Array) : (HintKeysPlain.Array, HintTextsPlain.Array);

    private void DrawFooter(float top, float width)
    {
        var lineMin = P(14f, top);
        dl.AddRectFilled(lineMin, P(width - 14f, top + 1f), SilkMainDraw.Col(191, 211, 236, 0.09f));

        var (keys, texts) = Hints();
        var kbdH = SilkFonts.LineHeight(SilkFace.Mono500, 10f) + 2f * s;
        var textH = SilkFonts.LineHeight(SilkFace.Ui500, 11f);
        var spanH = MathF.Max(kbdH, textH);
        var total = 0f;

        for (var i = 0; i < keys.Length; i++)
        {
            total += SpanWidth(keys[i], texts[i], 10f, 11f, 6f, 6f);

            if (i > 0)
                total += 16f * s;
        }

        var x = winMin.X + (width * s - total) * 0.5f;
        var spanTop = winMin.Y + (top + 1f + 8f) * s;
        DrawHintRow(dl, keys, texts, x, spanTop, spanH, 10f, 11f, 6f, 6f, 5f, 16f * s, float.MaxValue, out _);
    }

    private float SpanWidth(string key, string text, float kbdPx, float textPx, float kbdPad, float gap)
        => SilkFonts.Measure(SilkFace.Mono500, kbdPx, key).X + kbdPad * 2f * s + gap * s + SilkFonts.Measure(SilkFace.Ui500, textPx, text).X;

    internal float DrawHintRow(ImDrawListPtr list, string[] keys, string[] texts, float x, float top, float spanH, float kbdPx,
        float textPx, float kbdPad, float gap, float radius, float spanGap, float maxRight, out int lines, float rowGap = 0f, bool draw = true)
    {
        var startX = x;
        lines = 1;
        var kbdLh = SilkFonts.LineHeight(SilkFace.Mono500, kbdPx);
        var kbdH = kbdLh + 2f * s;
        var textLh = SilkFonts.LineHeight(SilkFace.Ui500, textPx);

        for (var i = 0; i < keys.Length; i++)
        {
            var keyW = SilkFonts.Measure(SilkFace.Mono500, kbdPx, keys[i]).X;
            var textW = SilkFonts.Measure(SilkFace.Ui500, textPx, texts[i]).X;
            var spanW = keyW + kbdPad * 2f * s + gap * s + textW;

            if (i > 0 && x + spanW > maxRight)
            {
                x = startX;
                top += spanH + rowGap;
                lines++;
            }

            var kbdMin = new Vector2(MathF.Round(x), MathF.Round(top + (spanH - kbdH) * 0.5f));
            var kbdMax = kbdMin + new Vector2(keyW + kbdPad * 2f * s, kbdH);
            if (draw)
            {
                SilkMainDraw.InsetRing(list, kbdMin, kbdMax, radius * s, SilkMainDraw.Col(191, 211, 236, 0.16f));
                SilkFonts.Draw(list, new Vector2(kbdMin.X + kbdPad * s, kbdMin.Y + 1f * s), SilkPalette.U32(SilkPalette.Ink2), SilkFace.Mono500, kbdPx, keys[i]);
                SilkFonts.Draw(list, new Vector2(MathF.Round(kbdMax.X + gap * s), MathF.Round(top + (spanH - textLh) * 0.5f)),
                    SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui500, textPx, texts[i]);
            }

            x += spanW + spanGap;
        }

        return top + spanH;
    }

    private void DrawRows(Vector2 min, Vector2 max)
    {
        listScreenMin = min;
        listScreenMax = max;
        var size = max - min;

        if (size.X < 1f || size.Y < 1f)
            return;

        ImGui.PushClipRect(min, max, true);
        DrawRowsInside(min, size);
        ImGui.PopClipRect();
    }

    private void DrawRowsInside(Vector2 min, Vector2 size)
    {
        var cdl = ImGui.GetWindowDrawList();
        var view = model.View;
        var count = view.Count;
        var rowH = RowHeight * s;
        var pad = ListPad * s;
        var contentH = count == 0 ? size.Y : pad * 2f + count * rowH;
        var maxScroll = MathF.Max(0f, contentH - size.Y);

        if (ImGui.IsMouseHoveringRect(min, min + size) && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            var wheel = ImGui.GetIO().MouseWheel;

            if (wheel != 0f)
                listScroll -= wheel * rowH * 3f;
        }

        listScroll = Math.Clamp(listScroll, 0f, maxScroll);
        var scroll = listScroll;

        if (MathF.Abs(scroll - lastScroll) > 0.5f)
        {
            cards.Hide();
            lastScroll = scroll;
        }

        listHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
            && ImGui.IsMouseHoveringRect(min, min + size);

        if (maxScroll > 0f)
            DrawScrollbar(cdl, min, size, contentH, scroll, maxScroll);

        if (count == 0)
        {
            DrawEmpty(cdl, min, size);

            if (listHovered)
                Lean?.Invoke(null);

            cards.Leave();
            return;
        }

        var first = Math.Max(0, (int)MathF.Floor((scroll - pad) / rowH));
        var last = Math.Min(count - 1, (int)MathF.Ceiling((scroll + size.Y - pad) / rowH));
        var rowW = size.X - pad * 2f;
        rowLeftTop = new Vector2(min.X + pad, min.Y + pad - scroll);

        uint hoveredId = 0;
        var hoveredZone = RowZone.None;
        SilkEmoteEntry? hoveredEntry = null;
        Vector2 hoveredMin = default, hoveredMax = default, infoMin = default, infoMax = default;
        var mouse = ImGui.GetMousePos();

        for (var i = first; i <= last; i++)
        {
            var entry = view[i];
            var rMin = new Vector2(min.X + pad, min.Y + pad + i * rowH - scroll);
            var rMax = rMin + new Vector2(rowW, rowH);

            bool released, hovered, activated, active, rightClicked;

            using (SilkProfile.Detail("Silk.Main.RowInput"))
            {
                ImGui.SetCursorScreenPos(rMin);
                ImGui.PushID((int)entry.RowId);
                released = ImGui.InvisibleButton("row"u8, new Vector2(rowW, rowH));
                hovered = ImGui.IsItemHovered();
                activated = ImGui.IsItemActivated();
                active = ImGui.IsItemActive();
                rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);
                ImGui.PopID();
            }

            var metrics = Row;
            var favMin = rMin + new Vector2(metrics.FavX, metrics.MarkY) * s;
            var blkMin = rMin + new Vector2(metrics.BlockX, metrics.MarkY) * s;
            var mk = new Vector2(metrics.MarkSize * s);
            var infoTopLeft = new Vector2(rMax.X - metrics.InfoHitRight * s, rMin.Y + metrics.InfoHitY * s);
            var zone = RowZone.None;

            if (hovered)
            {
                zone = Inside(mouse, favMin, favMin + mk) ? RowZone.Favourite
                    : Inside(mouse, blkMin, blkMin + mk) ? RowZone.Block
                    : Inside(mouse, infoTopLeft, infoTopLeft + mk) ? RowZone.Info
                    : RowZone.Row;

                hoveredId = entry.RowId;
                hoveredZone = zone;
                hoveredEntry = entry;
                hoveredMin = rMin;
                hoveredMax = rMax;
                infoMin = infoTopLeft;
                infoMax = infoTopLeft + mk;
            }

            if (activated)
                pressZone = zone;

            if (active && pressZone == RowZone.Row && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && entry.Assignable)
            {
                HotbarDragDrop.BeginDrag(entry.Row);
                cards.Hide();
            }

            if (released && !HotbarDragDrop.IsDragging && zone == pressZone)
                RowClick(entry, zone, rMin, rMax);

            if (rightClicked)
            {
                cards.Hide();
                menu.OpenRow(entry, mouse, now);
            }

            using (SilkProfile.Detail("Silk.Main.Row"))
                DrawRow(cdl, entry, rMin, rMax, hovered, zone);
        }

        UpdateHover(hoveredId, hoveredZone);

        if (hoveredEntry != null)
        {
            Lean?.Invoke(SilkVivid.Rgba(hoveredEntry.Color));

            if (hoveredZone == RowZone.Info)
            {
                cards.Hover(hoveredEntry, infoMin, infoMax, now);
            }
            else
            {
                cards.Leave();
            }

            if (hoveredZone is RowZone.Favourite or RowZone.Block)
            {
                var isOn = hoveredZone == RowZone.Favourite ? model.IsFavourite(hoveredEntry.RowId) : model.IsBlocked(hoveredEntry.RowId);
                var tip = hoveredZone == RowZone.Favourite
                    ? isOn ? L.RemoveFavourite.Text : L.AddFavourite.Text
                    : isOn ? L.AllowTarget.Text : L.BlockTarget.Text;
                var zMin = hoveredMin + new Vector2(hoveredZone == RowZone.Favourite ? Row.FavX : Row.BlockX, Row.MarkY) * s;
                SilkTooltip.Hover(tip, zMin, zMin + new Vector2(Row.MarkSize * s));
            }
        }
        else
        {
            if (listHovered || hoverRow != 0)
                Lean?.Invoke(null);

            cards.Leave();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && listHovered)
            cards.Hide();
    }

    private static bool Inside(Vector2 p, Vector2 min, Vector2 max) => p.X >= min.X && p.Y >= min.Y && p.X < max.X && p.Y < max.Y;

    private void UpdateHover(uint id, RowZone zone)
    {
        if (id != hoverRow)
        {
            if (hoverRow != 0)
            {
                fadeRow = hoverRow;
                fadeRowT.Snap(hoverRowT.Value(now));
                fadeRowT.Go(0f, now, reduced ? 0f : 0.25f, UiCubicBezier.Ease);
            }

            hoverRow = id;
            hoverRowT.Snap(0f);
        }

        hoverRowT.Go(id != 0 ? 1f : 0f, now, reduced ? 0f : 0.25f, UiCubicBezier.Ease);

        var mkZone = zone is RowZone.Favourite or RowZone.Block ? zone : RowZone.None;

        if (id != markRow || mkZone != markZone)
        {
            markRow = id;
            markZone = mkZone;
            markT.Snap(0f);
        }

        markT.Go(mkZone != RowZone.None ? 1f : 0f, now, reduced ? 0f : 0.2f, UiCubicBezier.Ease);
    }

    private void RowClick(SilkEmoteEntry entry, RowZone zone, Vector2 rMin, Vector2 rMax)
    {
        switch (zone)
        {
            case RowZone.Favourite:
                model.ToggleFavourite(entry.RowId);
                break;
            case RowZone.Block:
                model.ToggleBlocked(entry.RowId);
                break;
            case RowZone.Info:
                break;
            case RowZone.Row:
                if (!Configuration.EnablePreviewPopup)
                {
                    Play(entry, rMin, rMax);
                    break;
                }

                var isDouble = ReferenceEquals(lastClicked, entry) && now - lastClickedAt <= DoubleClickSeconds;
                lastClicked = isDouble ? null : entry;
                lastClickedAt = now;

                if (isDouble)
                {
                    pendingOpen = null;
                    Play(entry, rMin, rMax);
                    break;
                }

                if (dock.IsOpen)
                {
                    pendingOpen = null;
                    OpenDock(entry);
                    break;
                }

                pendingOpen = entry;
                pendingOpenAt = now;
                break;
        }
    }

    internal void TickDeferredOpen()
    {
        if (pendingOpen is not { } waiting || now - pendingOpenAt <= DoubleClickSeconds)
            return;

        pendingOpen = null;
        OpenDock(waiting);
    }

    internal void CancelDeferredOpen() => pendingOpen = null;

    internal void OpenDock(SilkEmoteEntry entry)
    {
        if (selectedRow != entry.RowId)
        {
            previousSelected = selectedRow;
            previousSelectT.Snap(selectT.Value(now));
            previousSelectT.Go(0f, now, reduced ? 0f : 0.25f, UiCubicBezier.Ease);
            selectedRow = entry.RowId;
            selectT.Snap(0f);
            selectT.Go(1f, now, reduced ? 0f : 0.25f, UiCubicBezier.Ease);
        }

        dock.Open(entry, now, reduced);
    }

    internal void CloseDock()
    {
        if (selectedRow != 0)
        {
            previousSelected = selectedRow;
            previousSelectT.Snap(selectT.Value(now));
            previousSelectT.Go(0f, now, reduced ? 0f : 0.25f, UiCubicBezier.Ease);
        }

        selectedRow = 0;
        dock.Close();
    }

    internal void Play(SilkEmoteEntry entry, Vector2? rowMin = null, Vector2? rowMax = null)
    {
        var color = entry.Color;

        if (rowMin == null && TryRowTop(entry.RowId, out var top))
        {
            rowMin = new Vector2(rowLeftTop.X, top);
            rowMax = rowMin + new Vector2(listScreenMax.X - listScreenMin.X - ListPad * 2f * s, RowHeight * s);
        }

        var swapped = EmoteActions.WouldSwap(entry);
        var plan = swapped ? Service.Orchestrator?.SilkPreviewTarget(entry.RowId) : null;
        uint targetIcon = 0;

        if (plan is { Kind: SilkPreviewKind.Target, Target: { } chosen })
            targetIcon = model.Get(chosen.RowId)?.IconId ?? 0;
        else if (plan is { Kind: SilkPreviewKind.IdlePose } pose)
            targetIcon = model.Get(pose.PoseRowId)?.IconId ?? 0;

        var errorsBefore = BypassEmote.Helpers.LogHelper.ErrorCount;
        EmoteActions.PlaySelf(entry.Row);

        if (plan is { Kind: SilkPreviewKind.None } || BypassEmote.Helpers.LogHelper.ErrorCount != errorsBefore)
            return;

        if (!reduced && rowMin != null)
        {
            sweepRow = entry.RowId;
            sweepAt = now;
        }

        pill.Show(entry, swapped, targetIcon, color, now);
        Wave?.Invoke(rowMin is { } m ? m.X + 40f * s : null, SilkVivid.Rgba(color));
    }

    private void DrawScrollbar(ImDrawListPtr cdl, Vector2 min, Vector2 size, float contentH, float scroll, float maxScroll)
    {
        var trackH = size.Y;
        var thumbH = MathF.Max(24f * s, trackH * size.Y / contentH);
        var thumbY = min.Y + (trackH - thumbH) * (scroll / maxScroll);
        var w = 6f * s;
        var x = min.X + size.X - w - 2f * s;
        var thumbMin = new Vector2(x, thumbY);
        var thumbMax = new Vector2(x + w, thumbY + thumbH);

        ImGui.SetCursorScreenPos(new Vector2(x - 2f * s, min.Y));
        ImGui.InvisibleButton("##silkscroll"u8, new Vector2(w + 4f * s, size.Y));
        var hot = ImGui.IsItemHovered();

        if (ImGui.IsItemActivated())
        {
            var mouseY = ImGui.GetMousePos().Y;

            if (mouseY < thumbMin.Y || mouseY > thumbMax.Y)
                listScroll = Math.Clamp((mouseY - min.Y - thumbH * 0.5f) / (trackH - thumbH) * maxScroll, 0f, maxScroll);

            scrollDragging = true;
            scrollDragStart = mouseY;
            scrollDragOrigin = listScroll;
        }

        if (scrollDragging)
        {
            if (!ImGui.IsItemActive())
            {
                scrollDragging = false;
            }
            else
            {
                var delta = ImGui.GetMousePos().Y - scrollDragStart;
                listScroll = Math.Clamp(scrollDragOrigin + delta / MathF.Max(1f, trackH - thumbH) * maxScroll, 0f, maxScroll);
            }
        }

        if (listHovered || scrollDragging)
        {
            var alpha = scrollDragging ? 0.3f : hot ? 0.25f : 0.15f;
            cdl.AddRectFilled(thumbMin, thumbMax, SilkMainDraw.Col(191, 211, 236, alpha), w * 0.5f);
        }
    }

    private void DrawEmpty(ImDrawListPtr cdl, Vector2 min, Vector2 size)
    {
        string title, body;

        switch (model.Tab)
        {
            case SilkTab.Favourites when model.Count(SilkTab.Favourites) == 0:
                title = L.NoFavourited.Text;
                body = L.NoFavouritedHint.Text;
                break;
            case SilkTab.Blocked when model.Count(SilkTab.Blocked) == 0:
                title = L.NoBlockedEmote.Text;
                body = L.NoBlockedHint.Text;
                break;
            default:
                title = EmptyTitle();
                body = L.NothingMatchesHint.Text;
                break;
        }

        var top = min.Y + (ListPad + 60f) * s;
        var cx = min.X + size.X * 0.5f;
        var tw = SilkFonts.Measure(SilkFace.Ui700, 16f, title).X;
        SilkFonts.Draw(cdl, new Vector2(MathF.Round(cx - tw * 0.5f), MathF.Round(top)), SilkPalette.U32(SilkPalette.Ink), SilkFace.Ui700, 16f, title);
        top += SilkFonts.LineHeight(SilkFace.Ui700, 16f) + 4f * s;
        var bw = SilkFonts.Measure(SilkFace.Ui400, 13f, body).X;
        var bodyTop = top + SilkFonts.TextTop(SilkFace.Ui400, 13f, 1.4f);
        SilkFonts.Draw(cdl, new Vector2(MathF.Round(cx - bw * 0.5f), MathF.Round(bodyTop)), SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui400, 13f, body);
    }

    private string emptyTitle = string.Empty;
    private string emptyFor = "\0";
    private int emptyRevision = -1;

    private string EmptyTitle()
    {
        if (!ReferenceEquals(emptyFor, search) || emptyRevision != NoireLanguages.Revision)
        {
            emptyFor = search;
            emptyRevision = NoireLanguages.Revision;
            emptyTitle = L.NothingMatches.With("query", search.Trim());
        }

        return emptyTitle;
    }

    private void DrawRow(ImDrawListPtr cdl, SilkEmoteEntry entry, Vector2 rMin, Vector2 rMax, bool hovered, RowZone zone)
    {
        var color = entry.Color;
        var m = Row;
        var r = m.Rounding * s;

        var hover = entry.RowId == hoverRow ? hoverRowT.Value(now) : entry.RowId == fadeRow ? fadeRowT.Value(now) : 0f;
        var sel = entry.RowId == selectedRow ? selectT.Value(now) : entry.RowId == previousSelected ? previousSelectT.Value(now) : 0f;

        if (hover > 0.001f)
            cdl.AddRectFilled(rMin, rMax, SilkMainDraw.Col(191, 211, 236, 0.06f * hover * (1f - sel)), r);

        if (sel > 0.001f)
        {
            cdl.AddRectFilled(rMin, rMax, SilkMainDraw.Col(color, 0.13f * sel), r);
            SilkMainDraw.InsetRing(cdl, rMin, rMax, r, SilkMainDraw.Col(color, 0.38f * sel));
        }

        if (entry.RowId == sweepRow)
            DrawSweep(cdl, rMin, rMax, color);

        var favOn = model.IsFavourite(entry.RowId);
        var blkOn = model.IsBlocked(entry.RowId);
        DrawMark(cdl, entry, rMin + new Vector2(m.FavX, m.MarkY) * s, RowZone.Favourite, favOn);
        DrawMark(cdl, entry, rMin + new Vector2(m.BlockX, m.MarkY) * s, RowZone.Block, blkOn);

        var icMin = rMin + new Vector2(m.IconX, m.IconY) * s;
        var icMax = icMin + new Vector2(m.IconSize * s);

        if (!SilkGameIcon.Draw(cdl, entry.IconId, icMin, icMax, m.IconRounding * s))
            SilkGameIcon.Placeholder(cdl, icMin, icMax, m.IconRounding * s);

        if (entry.Owned)
        {
            var centre = icMax - new Vector2(2f * s);
            cdl.AddCircleFilled(centre, m.BadgeOuter * s, SilkMainDraw.Hex(0x0a0f18), 20);
            cdl.AddCircleFilled(centre, m.BadgeInner * s, SilkMainDraw.Hex(0x72e0b4), 20);
            SilkIcons.Draw(cdl, SilkMainIcons.OwnedCheck, centre - new Vector2(m.BadgeCheck * 0.5f * s), m.BadgeCheck * s, SilkMainDraw.Hex(0x062016));
        }

        var nmLeft = rMin.X + m.NameX * s;
        var nmRight = rMax.X - m.InfoHitRight * s - 8f * s;
        var nameFace = entry.Invalid ? SilkFace.Ui500 : SilkFace.Ui600;
        var nameLh = SilkFonts.LineHeight(nameFace, m.NamePx);
        var baseline = rMin.Y + (m.Height * s - nameLh) * 0.5f + SilkFonts.Ascent(nameFace, m.NamePx);
        var nmTop = baseline - SilkFonts.Ascent(nameFace, m.NamePx);

        cdl.PushClipRect(new Vector2(nmLeft, rMin.Y), new Vector2(nmRight, rMax.Y), true);
        var x = nmLeft;

        if (Configuration.ShowEmoteIds)
        {
            var idTop = baseline - SilkFonts.Ascent(SilkFace.Mono500, m.IdPx);
            x += SilkFonts.Draw(cdl, new Vector2(MathF.Round(x), MathF.Round(idTop)), SilkPalette.U32(SilkPalette.Ink3), SilkFace.Mono500, m.IdPx, entry.IdText).X + 8f * s;
        }

        var nameStart = cdl.VtxBuffer.Size;
        var nameColor = entry.Invalid ? SilkPalette.Ink3 : SilkPalette.Ink;
        var nameW = SilkFonts.Draw(cdl, new Vector2(MathF.Round(x), MathF.Round(nmTop)), SilkPalette.U32(nameColor), nameFace, m.NamePx, entry.Name).X;

        if (entry.Invalid)
            Shear(cdl, nameStart, baseline);

        x += nameW + 8f * s;

        if (entry.CommandsJoined.Length > 0 && x < nmRight)
        {
            var codeTop = baseline - SilkFonts.Ascent(SilkFace.Mono400, m.CommandPx);
            SilkFonts.Draw(cdl, new Vector2(MathF.Round(x), MathF.Round(codeTop)), SilkPalette.U32(SilkPalette.Ink3), SilkFace.Mono400, m.CommandPx,
                entry.CommandsJoined, 0f, nmRight - x);
        }

        cdl.PopClipRect();

        var infoColor = entry.RowId == hoverRow ? SilkPalette.Mix(SilkPalette.Ink3, SilkPalette.Ink2, hover) : SilkPalette.Ink3;
        SilkIcons.Draw(cdl, SilkMainIcons.Info, new Vector2(rMax.X - m.InfoRight * s, rMin.Y + m.InfoY * s), m.InfoSize * s, SilkPalette.U32(infoColor));
    }

    private static void Shear(ImDrawListPtr cdl, int start, float baseline)
    {
        var verts = cdl.VtxBuffer.AsSpan();

        for (var i = start; i < verts.Length; i++)
            verts[i].Pos.X += (baseline - verts[i].Pos.Y) * ItalicShear;
    }

    private void DrawMark(ImDrawListPtr cdl, SilkEmoteEntry entry, Vector2 min, RowZone which, bool on)
    {
        var m = Row;
        var max = min + new Vector2(m.MarkSize * s);
        var hot = entry.RowId == markRow && markZone == which ? markT.Value(now) : 0f;

        if (hot > 0.001f)
            cdl.AddRectFilled(min, max, SilkMainDraw.Col(191, 211, 236, 0.08f * hot), 6f * s);

        Vector4 color;

        if (on)
        {
            color = which == RowZone.Favourite ? SilkPalette.Star : SilkPalette.Bad;
        }
        else
        {
            color = SilkPalette.Mix(SilkPalette.Hex(0x3a4458), SilkPalette.Ink2, hot);
        }

        var iconMin = min + new Vector2((m.MarkSize - m.MarkIcon) * 0.5f * s);

        if (on && which == RowZone.Favourite)
            SilkMainDraw.RadialGlow(cdl, (min + max) * 0.5f, m.MarkSize * 0.45f * s, m.MarkSize * 0.45f * s, SilkMainDraw.Col(255, 211, 110, 0.35f), SilkMainDraw.Col(255, 211, 110, 0f));

        SilkIcons.Draw(cdl, which == RowZone.Favourite ? SilkMainIcons.Star : SilkMainIcons.Ban, iconMin, m.MarkIcon * s, SilkPalette.U32(color));
    }

    private void DrawSweep(ImDrawListPtr cdl, Vector2 rMin, Vector2 rMax, Vector3 color)
    {
        var t = (now - sweepAt) / 0.7f;

        if (t is < 0f or >= 1f || reduced)
            return;

        var w = rMax.X - rMin.X;
        var shift = -w + 2f * w * SilkUi.EaseOut.Evaluate(t);
        var a = rMin.X + shift + 0.3f * w;
        var m = rMin.X + shift + 0.5f * w;
        var b = rMin.X + shift + 0.7f * w;
        var clear = SilkMainDraw.Col(color, 0f);
        var peak = SilkMainDraw.Col(color, 0.45f);

        cdl.PushClipRect(rMin, rMax, true);
        cdl.AddRectFilledMultiColor(new Vector2(a, rMin.Y), new Vector2(m, rMax.Y), clear, peak, peak, clear);
        cdl.AddRectFilledMultiColor(new Vector2(m, rMin.Y), new Vector2(b, rMax.Y), peak, clear, clear, peak);
        cdl.PopClipRect();
    }
}
