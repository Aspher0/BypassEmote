using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed class SilkQuickAdd
{
    private const int MaxResults = 200;
    private const float ItemHeight = 32f;
    private const int VisibleItems = 10;

    private readonly NoireString placeholder;
    private readonly bool favourites;
    private readonly List<SilkEmoteEntry> results = new(MaxResults);
    private string text = string.Empty;
    private string builtFor = "\0";
    private int builtVersion = -1;
    private bool open;
    private bool focusedLast;
    private Vector2 fieldMin;
    private Vector2 fieldMax;
    private Vector2 dropMax;
    private int hoverIndex = -1;

    internal SilkQuickAdd(NoireString placeholder, bool favourites)
    {
        this.placeholder = placeholder;
        this.favourites = favourites;
    }

    internal bool Open => open;

    internal bool MouseInside(Vector2 point)
        => open && point.X >= fieldMin.X && point.Y >= fieldMin.Y && point.X < dropMax.X && point.Y < dropMax.Y;

    internal void DrawField(ImDrawListPtr dl, Vector2 min, Vector2 max, float s)
    {
        fieldMin = min;
        fieldMax = max;
        var r = 9f * s;
        dl.AddRectFilled(min, max, SilkMainDraw.Col(0, 0, 0, 0.25f), r);
        SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.16f));
        var icon = favourites ? SilkMainIcons.Star : SilkMainIcons.Ban;
        SilkIcons.Draw(dl, icon, new Vector2(min.X + 11f * s, (min.Y + max.Y) * 0.5f - 7f * s), 14f * s, SilkPalette.U32(SilkPalette.Ink3));

        var left = min.X + (11f + 14f + 9f) * s;
        var right = max.X - 11f * s;
        var lineBox = 13f * SilkUi.TextScale * 1.4f * s;
        var top = (min.Y + max.Y) * 0.5f - lineBox * 0.5f + SilkFonts.TextTop(SilkFace.Ui400, 13f, 1.4f);

        ImGui.SetCursorScreenPos(new Vector2(left, MathF.Round(top)));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, SilkPalette.Ink);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, SilkPalette.Ink3);
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, SilkPalette.Alpha(SilkPalette.Ice, 0.3f));
        ImGui.SetNextItemWidth(right - left);

        using (SilkFonts.PushInput(SilkFace.Ui400, 13f))
            ImGui.InputTextWithHint(favourites ? "##silkfavadd"u8 : "##silkblkadd"u8, placeholder.Text, ref text, 128);

        var focused = ImGui.IsItemActive();
        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(2);

        if (focused && !focusedLast)
            open = true;

        if (focused && ImGui.IsKeyPressed(ImGuiKey.Escape))
            open = false;

        focusedLast = focused;
    }

    internal void Close()
    {
        open = false;
        hoverIndex = -1;
    }

    private void Rebuild(SilkEmoteModel model)
    {
        if (builtFor == text && builtVersion == model.Version)
            return;

        builtFor = text;
        builtVersion = model.Version;
        results.Clear();
        var query = text.Trim().ToLowerInvariant();

        foreach (var entry in model.All)
        {
            if (query.Length > 0 && !entry.SearchKey.Contains(query, StringComparison.Ordinal))
                continue;

            results.Add(entry);

            if (results.Count >= MaxResults)
                break;
        }
    }

    internal void DrawDropdown(SilkEmoteModel model, float s, ReadOnlySpan<byte> windowId)
    {
        if (!open)
            return;

        Rebuild(model);

        var count = Math.Min(results.Count, VisibleItems);
        var rows = Math.Max(1, count);
        var height = (rows * ItemHeight + 8f) * s;
        var pos = new Vector2(fieldMin.X, fieldMax.Y + 4f * s);
        var width = fieldMax.X - fieldMin.X;
        dropMax = pos + new Vector2(width, height);

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(new Vector2(width, height));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f * s));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 6f * s);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, SilkPalette.Alpha(SilkPalette.Ice, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, SilkPalette.Alpha(SilkPalette.Ice, 0.25f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, SilkPalette.Alpha(SilkPalette.Ice, 0.3f));

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;

        var visible = ImGui.Begin(windowId, flags);
        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(4);

        if (visible)
        {
            NoireWindowChrome.KeepInFront();
            var dl = ImGui.GetWindowDrawList();
            var wMin = ImGui.GetWindowPos();
            var wMax = wMin + ImGui.GetWindowSize();
            dl.PushClipRectFullScreen();
            SilkMainDraw.Shadow(dl, wMin, wMax, 10f * s, SilkMainDraw.Col(0, 0, 0, 0.9f), 40f * s, -8f * s, new Vector2(0f, 16f * s));
            dl.PopClipRect();
            dl.AddRectFilled(wMin, wMax, SilkMainDraw.Hex(0x0b1120), 10f * s);
            SilkMainDraw.OuterRing(dl, wMin, wMax, 10f * s, SilkMainDraw.Col(191, 211, 236, 0.16f));

            if (results.Count == 0)
            {
                var lh = SilkFonts.LineHeight(SilkFace.Ui600, 12.5f);
                SilkFonts.DrawFitted(dl, new Vector2(wMin.X + 14f * s, MathF.Round(wMin.Y + 4f * s + (ItemHeight * s - lh) * 0.5f)),
                    SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui600, 12.5f, L.NoEmoteMatches.Text, wMax.X - wMin.X - 28f * s);
            }

            var picked = -1;
            var hovered = -1;
            var inner = ImGui.GetContentRegionAvail().X;

            for (var i = 0; i < results.Count; i++)
            {
                var entry = results[i];
                var rowMin = ImGui.GetCursorScreenPos();

                if (rowMin.Y > wMax.Y + ItemHeight * s || rowMin.Y + ItemHeight * s < wMin.Y)
                {
                    ImGui.Dummy(new Vector2(inner, ItemHeight * s));
                    continue;
                }

                ImGui.PushID((int)entry.RowId);

                if (ImGui.InvisibleButton("q"u8, new Vector2(inner, ItemHeight * s)))
                    picked = i;

                if (ImGui.IsItemHovered())
                    hovered = i;

                ImGui.PopID();

                var rowMax = rowMin + new Vector2(inner, ItemHeight * s);
                var marked = favourites ? model.IsFavourite(entry.RowId) : model.IsBlocked(entry.RowId);
                var hot = hovered == i;

                if (hot)
                    dl.AddRectFilled(rowMin, rowMax, SilkMainDraw.Col(191, 211, 236, 0.1f), 7f * s);

                var iconMin = new Vector2(rowMin.X + 10f * s, rowMin.Y + 6f * s);
                SilkGameIcon.Draw(dl, entry.IconId, iconMin, iconMin + new Vector2(20f * s), 5f * s);

                var markColor = favourites ? SilkPalette.Star : SilkPalette.Bad;
                var color = marked ? markColor : hot ? SilkPalette.Ink : SilkPalette.Ink2;
                var lh = SilkFonts.LineHeight(SilkFace.Ui600, 12.5f);
                SilkFonts.Draw(dl, new Vector2(MathF.Round(rowMin.X + 38f * s), MathF.Round(rowMin.Y + (ItemHeight * s - lh) * 0.5f)),
                    SilkPalette.U32(color), SilkFace.Ui600, 12.5f, entry.Name, 0f, inner - 130f * s);

                if (marked)
                {
                    var note = favourites ? L.QuickMarkedFavourite.Text : L.QuickMarkedBlocked.Text;
                    var nw = SilkFonts.Measure(SilkFace.Ui500, 10.5f, note).X;
                    var nlh = SilkFonts.LineHeight(SilkFace.Ui500, 10.5f);
                    SilkFonts.Draw(dl, new Vector2(MathF.Round(rowMax.X - 10f * s - nw), MathF.Round(rowMin.Y + (ItemHeight * s - nlh) * 0.5f)),
                        SilkPalette.U32(SilkPalette.Ink3), SilkFace.Ui500, 10.5f, note);
                }
            }

            hoverIndex = hovered;

            if (picked >= 0)
            {
                var entry = results[picked];

                if (favourites)
                    model.ToggleFavourite(entry.RowId);
                else
                    model.ToggleBlocked(entry.RowId);

                text = string.Empty;
                open = false;
            }

            var windowHovered = ImGui.IsWindowHovered();

            if (!focusedLast && !windowHovered && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)))
                open = false;
        }

        ImGui.End();
    }
}
