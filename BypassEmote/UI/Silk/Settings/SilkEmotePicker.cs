using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.UI.Silk.Main;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Settings;

internal static class SilkEmoteIndex
{
    private static SilkEmoteEntry[] entries = [];
    private static readonly Dictionary<uint, SilkEmoteEntry> ById = new();
    private static bool built;

    internal static IReadOnlyList<SilkEmoteEntry> Entries
    {
        get
        {
            Build();
            return entries;
        }
    }

    internal static SilkEmoteEntry? Get(uint rowId)
    {
        Build();
        return ById.TryGetValue(rowId, out var entry) ? entry : null;
    }

    internal static bool Swappable(SilkEmoteEntry entry)
        => !entry.Invalid && Service.Catalog?.Get(entry.RowId) is not { IsPoseFamily: true };

    internal static bool ValidTarget(SilkEmoteEntry entry) => !entry.Invalid;

    internal static void RefreshOwnership()
    {
        Build();

        foreach (var entry in entries)
            entry.Owned = EmoteHelper.IsEmoteUnlocked(entry.RowId);
    }

    private static void Build()
    {
        if (built)
            return;

        var sheet = ExcelSheetHelper.GetSheet<Emote>();

        if (sheet == null)
            return;

        var list = new List<SilkEmoteEntry>(sheet.Count);

        foreach (var row in sheet)
        {
            if (CommonHelper.GetEmotePlayType(row) == EmotePlayType.DoNotPlay)
                continue;

            var entry = new SilkEmoteEntry(row);
            list.Add(entry);
            ById[entry.RowId] = entry;
        }

        list.Sort(static (a, b) => a.RowId.CompareTo(b.RowId));
        entries = [.. list];
        built = true;
        RefreshOwnership();
    }
}

internal sealed class SilkEmotePicker
{
    private const float RowHeight = 36f;
    private const float SearchHeight = 34f;
    private const float ListHeight = 250f;
    private const int MaxResults = 400;

    private readonly string id;
    private readonly string searchId;
    private readonly string listId;
    private readonly NoireString placeholder;
    private readonly List<SilkEmoteEntry> results = new(MaxResults);

    private string text = string.Empty;
    private string builtFor = "\0";
    private bool builtOnce;
    private bool focusNext;

    internal SilkEmotePicker(string id, NoireString placeholder)
    {
        this.id = id;
        this.placeholder = placeholder;
        searchId = id + "search";
        listId = id + "list";
    }

    internal Func<SilkEmoteEntry, bool>? Filter { get; set; }

    internal Func<uint, bool>? Marked { get; set; }

    internal string MarkedNote { get; set; } = string.Empty;

    internal bool IsOpen => ImGui.IsPopupOpen(id);

    internal void Open()
    {
        text = string.Empty;
        builtFor = "\0";
        focusNext = true;
        SilkEmoteIndex.RefreshOwnership();
        ImGui.OpenPopup(id);
    }

    internal void Close()
    {
        if (IsOpen)
            ImGui.CloseCurrentPopup();
    }

    internal uint? Draw(Vector2 anchorMin, Vector2 anchorMax, float width, bool above)
    {
        if (!ImGui.IsPopupOpen(id))
            return null;

        Rebuild();

        var scale = SilkUi.Scale;
        var pad = 6f * scale;
        var rows = Math.Max(1, Math.Min(results.Count, 7));
        var listHeight = MathF.Min(ListHeight * scale, rows * RowHeight * scale);
        var height = (pad * 2f) + (SearchHeight * scale) + pad + listHeight;
        var top = above ? anchorMin.Y - (6f * scale) - height : anchorMax.Y + (6f * scale);

        ImGui.SetNextWindowPos(new Vector2(anchorMin.X, top), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 12f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 6f * scale);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, SilkPalette.Rgb(191, 211, 236, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, SilkPalette.Rgb(191, 211, 236, 0.25f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, SilkPalette.Rgb(191, 211, 236, 0.3f));

        var open = ImGui.BeginPopup(id, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);

        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(5);

        if (!open)
            return null;

        uint? picked = null;

        try
        {
            if (SilkUi.Settings.AlwaysOnTop)
                NoireWindowChrome.KeepInFront();

            var min = ImGui.GetWindowPos();
            var max = min + new Vector2(width, height);

            SilkPaint.PushClipExpanded(min, max, 60f * scale);
            SilkPaint.BoxShadow(min, max, new Vector2(0f, 24f * scale), 50f * scale, -10f * scale, SilkPalette.PopupShadow, 12f * scale);
            SilkPaint.OuterRing(min, max, SilkPalette.Line2, 12f * scale, scale);
            SilkPaint.Fill(min, max, SilkPalette.Popup, 12f * scale);
            SilkPaint.PopClip();

            if (focusNext)
            {
                focusNext = false;
                ImGui.SetKeyboardFocusHere();
            }

            var field = text;

            if (SilkControls.Search(searchId, ref field, min + new Vector2(pad, pad), width - (pad * 2f), placeholder.Text, SearchHeight, 64))
                text = field;

            picked = DrawList(new Vector2(min.X + pad, min.Y + (pad * 2f) + (SearchHeight * scale)), width - (pad * 2f), listHeight);

            if (ImGui.IsKeyPressed(ImGuiKey.Escape) || picked != null)
                ImGui.CloseCurrentPopup();
        }
        finally
        {
            ImGui.EndPopup();
        }

        return picked;
    }

    private uint? DrawList(Vector2 pos, float width, float height)
    {
        var scale = SilkUi.Scale;
        var rowHeight = RowHeight * scale;

        ImGui.SetCursorScreenPos(pos);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var visible = ImGui.BeginChild(listId, new Vector2(width, height), false, ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleColor();

        uint? picked = null;

        if (visible)
        {
            var origin = ImGui.GetCursorScreenPos();

            if (results.Count == 0)
                SilkText.DrawInBox(origin, origin + new Vector2(width, rowHeight), L.NoResult.Text, 12.5f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.Center);

            var scroll = ImGui.GetScrollY();
            var first = Math.Max(0, (int)(scroll / rowHeight) - 1);
            var last = Math.Min(results.Count, first + (int)(height / rowHeight) + 3);

            for (var i = first; i < last; i++)
            {
                var entry = results[i];
                var min = new Vector2(origin.X, origin.Y + (i * rowHeight));
                var max = new Vector2(origin.X + width - (8f * scale), min.Y + rowHeight);

                if (SilkSettingsKit.Hit(entry.IdText, min, max, out var hovered))
                    picked = entry.RowId;

                var marked = Marked?.Invoke(entry.RowId) == true;

                if (hovered)
                    SilkPaint.Fill(min, max, SilkPalette.IceWash10, 8f * scale);

                var icon = 24f * scale;
                var iconMin = new Vector2(min.X + (8f * scale), MathF.Round(((min.Y + max.Y) * 0.5f) - (icon * 0.5f)));
                SilkSettingsKit.Glyph(iconMin, iconMin + new Vector2(icon, icon), entry.IconId, 6f * scale);

                var textX = iconMin.X + icon + (10f * scale);
                var command = entry.Commands.Length > 0 ? entry.Commands[0] : string.Empty;
                var commandWidth = command.Length > 0 ? SilkText.Width(command, 10.5f, SilkWeight.Regular, 0f, true) : 0f;
                var right = max.X - (8f * scale) - commandWidth;

                if (command.Length > 0)
                    SilkText.DrawInBox(new Vector2(right, min.Y), max, command, 10.5f, SilkWeight.Regular, SilkPalette.Ink3, UiAlign.Start, 0f, true);

                var note = marked && MarkedNote.Length > 0 ? MarkedNote : entry.Owned ? L.OwnedMark.Text : null;
                var noteColor = marked && MarkedNote.Length > 0 ? SilkPalette.Ice : SilkPalette.Ok;
                var noteWidth = note == null ? 0f : SilkText.Width(note, 9.5f, SilkWeight.SemiBold) + (8f * scale);
                var nameRoom = MathF.Max(20f * scale, right - (8f * scale) - noteWidth - textX);
                var nameWidth = MathF.Min(nameRoom, SilkText.Width(entry.Name, 13f, SilkWeight.Medium));

                if (note != null)
                    SilkText.DrawInBox(new Vector2(textX + nameWidth + (8f * scale), min.Y), new Vector2(right - (8f * scale), max.Y), note, 9.5f, SilkWeight.SemiBold, noteColor, UiAlign.Start);

                SilkText.DrawInBox(new Vector2(textX, min.Y), new Vector2(textX + nameRoom, max.Y), entry.Name, 13f, SilkWeight.Medium,
                    marked ? SilkPalette.Ice : hovered ? SilkPalette.Ink : SilkPalette.Ink2, UiAlign.Start, 0f, false, 0f);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + (results.Count * rowHeight)));
            ImGui.Dummy(new Vector2(1f, 1f));
        }

        ImGui.EndChild();
        return picked;
    }

    private void Rebuild()
    {
        if (builtOnce && string.Equals(builtFor, text, StringComparison.Ordinal))
            return;

        builtFor = text;
        builtOnce = true;
        results.Clear();

        var query = text.Trim().ToLowerInvariant();

        foreach (var entry in SilkEmoteIndex.Entries)
        {
            if (Filter != null && !Filter(entry))
                continue;

            if (query.Length > 0 && !entry.SearchKey.Contains(query, StringComparison.Ordinal))
                continue;

            results.Add(entry);

            if (results.Count >= MaxResults)
                break;
        }
    }
}
