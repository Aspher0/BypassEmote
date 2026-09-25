using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.UI.Silk.Main;
using BypassEmote.UI.Silk.Settings;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Windows;

internal sealed class SilkHotbarPainter
{
    private static string DragHint => L.SilkDragHint.Text;
    private static string StandardLabel => L.Standard.Text;
    private static string CrossLabel => L.Cross.Text;
    private static string LeftTrigger => L.LeftTrigger.Text;
    private static string RightTrigger => L.RightTrigger.Text;
    private static string CancelLabel => L.Cancel.Text;
    private static string AssignLabel => L.Assign.Text;
    private static string WhyPickSlot => L.PickASlot.Text;

    private static readonly string[] StandardBars = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];
    private static readonly string[] CrossBars = ["1", "2", "3", "4", "5", "6", "7", "8"];

    private readonly SlotInfo[] slots = new SlotInfo[16];
    private readonly Settings.SilkScrollArea scroll = new();
    private readonly string[] slotNumbers = BuildSlotNumbers();

    private int bar;
    private int slot = -1;
    private float readAt = -10f;
    private int readBar = -1;
    private string? stateText;
    private int stateSlot = -1;
    private int stateBar = -1;
    private bool stateOccupied;
    private int stateRevision = -1;

    private readonly HotbarWindow window;
    private readonly string animKey;

    internal SilkHotbarPainter(HotbarWindow window)
    {
        this.window = window;
        animKey = "SilkWindow." + window.Id;
    }

    public Emote? Emote
    {
        get => window.Emote;
        private set => window.Emote = value;
    }

    private int opening;

    private void TakeOpening()
    {
        if (opening == window.Opening)
            return;

        opening = window.Opening;
        slot = -1;
        stateSlot = -2;
        bar = Math.Clamp(Configuration.AssignModalHotbar, 0, AddonHelper.HotbarCount - 1);
        readBar = -1;
        scroll.Reset();
    }

    private bool IsOpen
    {
        get => window.IsOpen;
        set => window.IsOpen = value;
    }

    private struct SlotInfo
    {
        public bool Empty;
        public uint IconId;
        public string Name;
        public byte[]? NameBytes;
    }

    internal void DrawBody(Vector2 min, Vector2 max)
    {
        TakeOpening();

        var scale = SilkUi.Scale;
        var entry = Emote is { } emote ? SilkEmoteIndex.Get(emote.RowId) : null;
        var footHeight = (27f * scale) + (SilkControls.ButtonHeight * scale);
        var bodyMax = new Vector2(max.X, max.Y - footHeight);
        var origin = new Vector2(min.X + (16f * scale), min.Y + (4f * scale));
        var width = max.X - min.X - (32f * scale);

        ImGui.PushClipRect(min, bodyMax, true);

        try
        {
            ReadSlots();

            var offset = scroll.Begin(min, bodyMax, true);
            var top = origin.Y - offset;
            var y = top + (6f * scale);
            y += DrawWho(new Vector2(origin.X, y), width, entry);

            y += SilkSettingsKit.Section(L.HotbarLabel.Text, new Vector2(origin.X, y), width);
            y += DrawBars(new Vector2(origin.X, y), width);

            y += 14f * scale;
            y += DrawSlots(new Vector2(origin.X, y), width, entry);

            y += 12f * scale;
            y += DrawState(new Vector2(origin.X, y), width, entry);

            y += 10f * scale;
            SilkIcons.Draw(SilkIcon.Hand, new Vector2(origin.X, MathF.Round(y + (1f * scale))), 14f * scale, SilkPalette.Ink3);
            y += DrawDragHint(new Vector2(origin.X + (22f * scale), y), width - (22f * scale));

            scroll.End(min, bodyMax, y - top + (4f * scale) + (10f * scale));
        }
        finally
        {
            ImGui.PopClipRect();
        }

        DrawFoot(new Vector2(min.X, bodyMax.Y), max);
    }

    private float DrawWho(Vector2 origin, float width, SilkEmoteEntry? entry)
    {
        var scale = SilkUi.Scale;
        var height = 68f * scale;
        var max = origin + new Vector2(width, height);
        var radius = 12f * scale;
        var vivid = entry == null ? SilkPalette.Ice : SilkSettingsKit.Vivid(entry.IconId);

        SilkPaint.Fill(origin, max, SilkPalette.Panel, radius);
        SilkPaint.AngleGradient(origin, max, 135f, SilkPalette.Alpha(vivid, 0.16f), SilkPalette.Alpha(vivid, 0f), radius);
        SilkPaint.InsetRing(origin, max, SilkPalette.Alpha(vivid, 0.4f), radius, scale);

        if (entry == null)
            return height;

        var icon = 44f * scale;
        var iconMin = new Vector2(origin.X + (12f * scale), MathF.Round(((origin.Y + max.Y) * 0.5f) - (icon * 0.5f)));
        SilkSettingsKit.Glyph(iconMin, iconMin + new Vector2(icon, icon), entry.IconId, 10f * scale);

        var nameLine = SilkText.NaturalLine(15f, SilkWeight.ExtraBold);
        var codeLine = SilkText.NaturalLine(11f, SilkWeight.Regular, true);
        var top = MathF.Round(((origin.Y + max.Y) * 0.5f) - ((nameLine + codeLine) * 0.5f));
        var textLeft = iconMin.X + icon + (12f * scale);
        var textWidth = MathF.Max(1f, max.X - (12f * scale) - textLeft);

        SilkText.Draw(new Vector2(textLeft, top), entry.Name, 15f, SilkWeight.ExtraBold, SilkPalette.Ink, 0f, false, textWidth);
        SilkText.Draw(new Vector2(textLeft, top + nameLine), entry.CommandsJoined, 11f, SilkWeight.Regular, SilkPalette.Ink3, 0f, true, textWidth);

        return height;
    }

    private float DrawBars(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var labelWidth = MathF.Max(SilkText.Width(SilkFonts.Upper(StandardLabel), 10f, SilkWeight.Bold, 1f), SilkText.Width(SilkFonts.Upper(CrossLabel), 10f, SilkWeight.Bold, 1f)) + (12f * scale);
        var rowHeight = SilkControls.SegmentedHeight * scale;
        var gap = 8f * scale;
        var barsWidth = width - labelWidth;

        SilkText.DrawInBox(new Vector2(origin.X, origin.Y), new Vector2(origin.X + labelWidth, origin.Y + rowHeight), SilkFonts.Upper(StandardLabel), 10f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Start, 1f);

        var standard = bar < AddonHelper.StandardHotbarCount ? bar : -1;

        if (SilkControls.Segmented("##silkbarstd", ref standard, StandardBars, new Vector2(origin.X + labelWidth, origin.Y), barsWidth) && standard >= 0)
            SetBar(standard);

        var second = origin.Y + rowHeight + gap;
        SilkText.DrawInBox(new Vector2(origin.X, second), new Vector2(origin.X + labelWidth, second + rowHeight), SilkFonts.Upper(CrossLabel), 10f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Start, 1f);

        var cross = bar >= 10 ? bar - 10 : -1;

        if (SilkControls.Segmented("##silkbarxhb", ref cross, CrossBars, new Vector2(origin.X + labelWidth, second), barsWidth) && cross >= 0)
            SetBar(cross + 10);

        return (rowHeight * 2f) + gap;
    }

    private void SetBar(int value)
    {
        if (bar == value)
            return;

        bar = value;
        slot = -1;
        readBar = -1;
        scroll.Reset();
        stateSlot = -2;
        Configuration.AssignModalHotbar = value;
    }

    private float DrawSlots(Vector2 origin, float width, SilkEmoteEntry? entry)
    {
        var scale = SilkUi.Scale;
        var cross = bar >= 10;
        var height = cross ? 159f * scale : 0f;
        var slotSize = 42f * scale;
        var gap = 5f * scale;

        if (!cross)
        {
            var columns = Math.Max(1, Math.Min(12, (int)((width - (24f * scale) + gap) / (slotSize + gap))));
            var rows = (12 + columns - 1) / columns;
            height = (rows * slotSize) + ((rows - 1) * gap) + (32f * scale);
        }

        var max = origin + new Vector2(width, height);
        SilkPaint.Fill(origin, max, SilkPalette.Rgb(0, 0, 0, 0.3f), 12f * scale);
        SilkPaint.InsetRing(origin, max, SilkPalette.Line, 12f * scale, scale);

        if (cross)
            DrawCrossSlots(origin, max, entry);
        else
            DrawStandardSlots(origin, max, entry, slotSize, gap);

        return height;
    }

    private void DrawStandardSlots(Vector2 min, Vector2 max, SilkEmoteEntry? entry, float slotSize, float gap)
    {
        var scale = SilkUi.Scale;
        var available = max.X - min.X - (24f * scale);
        var columns = Math.Max(1, Math.Min(12, (int)((available + gap) / (slotSize + gap))));
        var rows = (12 + columns - 1) / columns;
        var top = min.Y + (16f * scale);

        for (var i = 0; i < 12; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var count = Math.Min(columns, 12 - (row * columns));
            var rowWidth = (count * slotSize) + ((count - 1) * gap);
            var left = MathF.Round(((min.X + max.X) * 0.5f) - (rowWidth * 0.5f));
            var slotMin = new Vector2(left + (column * (slotSize + gap)), top + (row * (slotSize + gap)));
            DrawSlot(i, slotMin, slotSize, entry);
        }

        _ = rows;
    }

    private void DrawCrossSlots(Vector2 min, Vector2 max, SilkEmoteEntry? entry)
    {
        var scale = SilkUi.Scale;
        var slotSize = 42f * scale;
        var diamondWidth = slotSize * 3f;
        var diamondHeight = (30f * scale * 3f) + slotSize;
        var setGap = 14f * scale;
        var groupGap = 26f * scale;
        var setWidth = (diamondWidth * 2f) + setGap;
        var totalWidth = (setWidth * 2f) + groupGap;
        var left = MathF.Round(((min.X + max.X) * 0.5f) - (totalWidth * 0.5f));
        var top = min.Y + (14f * scale);

        Diamond(left, top, 0, slotSize, entry);
        Diamond(left + diamondWidth + setGap, top, 4, slotSize, entry);
        Diamond(left + setWidth + groupGap, top, 8, slotSize, entry);
        Diamond(left + setWidth + groupGap + diamondWidth + setGap, top, 12, slotSize, entry);

        var labelTop = top + diamondHeight + (6f * scale);
        var labelLine = 13f * scale;

        SilkText.DrawInBox(new Vector2(left, labelTop), new Vector2(left + setWidth, labelTop + labelLine), LeftTrigger, 9.5f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Center, 1f);
        SilkText.DrawInBox(new Vector2(left + setWidth + groupGap, labelTop), new Vector2(left + (setWidth * 2f) + groupGap, labelTop + labelLine), RightTrigger, 9.5f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Center, 1f);
    }

    private void Diamond(float left, float top, int first, float slotSize, SilkEmoteEntry? entry)
    {
        var scale = SilkUi.Scale;
        var rowHeight = 30f * scale;

        DrawSlot(first, new Vector2(left + slotSize, top), slotSize, entry);
        DrawSlot(first + 1, new Vector2(left, top + rowHeight - (6f * scale)), slotSize, entry);
        DrawSlot(first + 2, new Vector2(left + (slotSize * 2f), top + rowHeight - (6f * scale)), slotSize, entry);
        DrawSlot(first + 3, new Vector2(left + slotSize, top + (rowHeight * 2f) + (6f * scale)), slotSize, entry);
    }

    private void DrawSlot(int index, Vector2 min, float size, SilkEmoteEntry? entry)
    {
        var scale = SilkUi.Scale;
        var max = min + new Vector2(size, size);
        var info = slots[index];
        var selected = slot == index;
        var radius = 9f * scale;

        ImGui.PushID(index);
        var clicked = SilkSettingsKit.Hit("slot", min, max, out var hovered);
        ImGui.PopID();

        if (clicked)
        {
            slot = index;
            stateSlot = -2;
        }

        var lift = hovered && !selected ? scale : 0f;
        min.Y -= lift;
        max.Y -= lift;

        if (info.Empty)
        {
            SilkPaint.Fill(min, max, SilkPalette.Rgb(191, 211, 236, 0.03f), radius);

            var stripe = SilkPalette.Rgb(191, 211, 236, 0.03f);
            var drawList = NoireShapes.DrawList;

            if (!drawList.IsNull)
            {
                drawList.PushClipRect(min, max, true);

                for (var x = min.X - size; x < max.X + size; x += 14.142f * scale)
                    SilkPaint.Line(new Vector2(x, max.Y), new Vector2(x + size, min.Y), stripe, 5f * scale);

                drawList.PopClipRect();
            }

            SilkPaint.InsetRing(min, max, SilkPalette.Rgb(191, 211, 236, 0.16f), radius, scale);
        }
        else
        {
            SilkPaint.Fill(min, max, SilkPalette.Rgb(191, 211, 236, 0.04f), radius);
            SilkPaint.InsetRing(min, max, SilkPalette.Line, radius, scale);

            var icon = 36f * scale;
            var iconMin = new Vector2(MathF.Round(((min.X + max.X) * 0.5f) - (icon * 0.5f)), MathF.Round(((min.Y + max.Y) * 0.5f) - (icon * 0.5f)));
            SilkSettingsKit.Glyph(iconMin, iconMin + new Vector2(icon, icon), info.IconId, 7f * scale);
        }

        SilkText.Draw(new Vector2(min.X + (3f * scale), min.Y + (1f * scale)), slotNumbers[index], 8.5f, SilkWeight.SemiBold, SilkPalette.Ink3, 0f, true);

        if (entry != null && (hovered || selected))
        {
            var ghost = 36f * scale;
            var ghostMin = new Vector2(min.X + (3f * scale), min.Y + (3f * scale));
            SilkSettingsKit.Glyph(ghostMin, ghostMin + new Vector2(ghost, ghost), entry.IconId, 7f * scale, selected ? 1f : 0.55f);
        }

        if (selected)
        {
            var vivid = entry == null ? SilkPalette.Ice : SilkSettingsKit.Vivid(entry.IconId);
            SilkPaint.Glow(min, max, 18f * scale, -4f * scale, vivid, radius);
            SilkPaint.InsetRing(min, max, vivid, radius, 2f * scale);

            if (!info.Empty)
            {
                var dot = new Vector2(max.X - (3f * scale), min.Y + (3f * scale));
                SilkPaint.Circle(dot, 7f * scale, SilkPalette.Popup);
                SilkPaint.Circle(dot, 5f * scale, SilkPalette.Warn);
            }
        }

        if (hovered)
            SilkTooltip.Hover(SlotTooltip(index), min, max);
    }

    private static float DrawDragHint(Vector2 topLeft, float width)
    {
        const float LineHeight = 18f / 11.5f;

        if (SilkText.Width(DragHint, 11.5f, SilkWeight.Medium) <= width)
        {
            SilkText.DrawInBox(topLeft, topLeft + new Vector2(width, 18f * SilkUi.Scale), DragHint, 11.5f, SilkWeight.Medium, SilkPalette.Ink3);
            return 18f * SilkUi.Scale;
        }

        return SilkText.DrawParagraph(topLeft, DragHint, 11.5f, SilkWeight.Medium, SilkPalette.Ink3, width, LineHeight);
    }

    private float DrawState(Vector2 origin, float width, SilkEmoteEntry? entry)
    {
        var scale = SilkUi.Scale;
        var info = slot >= 0 ? slots[slot] : default;
        var occupied = slot >= 0 && !info.Empty;
        var warn = occupied;
        var text = StateText(entry);

        var textLeftOffset = (12f + 16f + 10f) * scale + (occupied ? (24f + 10f) * scale : 0f);
        var textWidth = width - textLeftOffset - (12f * scale);

        var lines = SilkText.Lines(text, 12.5f, SilkWeight.Medium, textWidth).Count;
        var height = lines <= 1 ? 48f * scale : MathF.Ceiling((lines * SilkText.LineBox(12.5f, 1.5f)) + (28f * scale));
        var max = origin + new Vector2(width, height);
        var radius = 10f * scale;

        SilkPaint.Fill(origin, max, warn ? SilkPalette.WarnBg : SilkPalette.Rgb(191, 211, 236, 0.05f), radius);
        SilkPaint.InsetRing(origin, max, warn ? SilkPalette.Rgb(255, 190, 110, 0.25f) : SilkPalette.Line, radius, scale);

        var icon = 16f * scale;
        var iconMin = new Vector2(origin.X + (12f * scale), MathF.Round(((origin.Y + max.Y) * 0.5f) - (icon * 0.5f)));
        var glyph = slot < 0 ? SilkIcon.Hand : occupied ? SilkIcon.Warn : SilkIcon.Ok;
        var color = slot < 0 ? SilkPalette.Ink3 : occupied ? SilkPalette.WarnText : SilkPalette.Ok;

        SilkIcons.Draw(glyph, iconMin, icon, color);

        var textLeft = iconMin.X + icon + (10f * scale);

        if (occupied)
        {
            var thumb = 24f * scale;
            var thumbMin = new Vector2(textLeft, MathF.Round(((origin.Y + max.Y) * 0.5f) - (thumb * 0.5f)));
            SilkSettingsKit.Glyph(thumbMin, thumbMin + new Vector2(thumb, thumb), info.IconId, 6f * scale);
            textLeft = thumbMin.X + thumb + (10f * scale);
        }

        var textColor = slot < 0 ? SilkPalette.Ink3 : occupied ? SilkPalette.WarnText : SilkPalette.Ink2;

        if (lines <= 1)
            SilkText.DrawInBox(new Vector2(textLeft, origin.Y), new Vector2(max.X - (12f * scale), max.Y), text, 12.5f, SilkWeight.Medium, textColor, UiAlign.Start, 0f, false, 1.5f);
        else
            SilkText.DrawParagraph(new Vector2(textLeft, origin.Y + (14f * scale)), text, 12.5f, SilkWeight.Medium, textColor, max.X - (12f * scale) - textLeft, 1.5f);

        return height;
    }

    private void DrawFoot(Vector2 min, Vector2 max)
    {
        var scale = SilkUi.Scale;

        SilkPaint.Fill(min, max, SilkPalette.Rgb(0, 0, 0, 0.18f), 0f);
        SilkPaint.Fill(min, new Vector2(max.X, min.Y + scale), SilkPalette.Line, 0f);

        var assignWidth = SilkControls.ButtonWidth(AssignLabel);
        var cancelWidth = SilkControls.ButtonWidth(CancelLabel);
        var assignMin = new Vector2(max.X - (16f * scale) - assignWidth, min.Y + (13f * scale));
        var cancelMin = new Vector2(assignMin.X - (10f * scale) - cancelWidth, assignMin.Y);

        if (slot < 0)
            SilkText.DrawInBox(new Vector2(min.X + (16f * scale), min.Y), new Vector2(cancelMin.X - (10f * scale), max.Y - (2f * scale)), WhyPickSlot, 12f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.Start, 0f, false, 0f);

        if (SilkControls.Button(CancelLabel, cancelMin))
            IsOpen = false;

        if (SilkControls.Button(AssignLabel, assignMin, SilkButtonKind.Primary, null, slot < 0))
            Assign();
    }

    private void Assign()
    {
        if (slot < 0 || Emote is not { } emote)
            return;

        AddonHelper.SetHotbarSlot(Math.Clamp(bar, 0, AddonHelper.HotbarCount - 1), slot, RaptureHotbarModule.HotbarSlotType.Emote, emote.RowId);
        readBar = -1;
        scroll.Reset();
        stateSlot = -2;
        IsOpen = false;
    }

    private unsafe void ReadSlots()
    {
        var now = SilkUi.Time;

        if (readBar == bar && now - readAt < 0.5f)
            return;

        readBar = bar;
        readAt = now;

        var count = AddonHelper.HotbarSlotCount(bar);

        for (var i = 0; i < slots.Length; i++)
        {
            if (i >= count)
            {
                slots[i] = new SlotInfo { Empty = true, Name = string.Empty };
                continue;
            }

            var raw = AddonHelper.GetHotbarSlot(bar, i);
            var empty = raw == null || raw->IsEmpty;
            var iconId = 0u;
            var bytes = ReadOnlySpan<byte>.Empty;

            if (!empty)
            {
                iconId = raw->IconId;

                if (iconId == 0)
                    iconId = (uint)Math.Max(0, raw->GetIconIdForSlot(raw->CommandType, raw->CommandId));

                bytes = raw->PopUpHelp.AsSpan();
            }

            if (slots[i].Empty == empty && slots[i].IconId == iconId && bytes.SequenceEqual(slots[i].NameBytes))
                continue;

            var name = empty ? string.Empty : raw->PopUpHelp.ToString();
            slots[i] = new SlotInfo { Empty = empty, IconId = iconId, Name = name, NameBytes = bytes.ToArray() };

            if (i == slot)
                stateSlot = -2;
        }

        if (slot >= count)
            slot = -1;
    }

    private string SlotTooltip(int index)
    {
        var info = slots[index];

        return info.Empty
            ? L.SlotEmpty.With("slot", slotNumbers[index])
            : L.SlotHolds.With("slot", slotNumbers[index], "name", info.Name);
    }

    private string StateText(SilkEmoteEntry? entry)
    {
        var info = slot >= 0 ? slots[slot] : default;

        if (stateText != null && stateSlot == slot && stateBar == bar && stateOccupied == (slot >= 0 && !info.Empty)
            && stateRevision == NoireLanguages.Revision)
            return stateText;

        stateSlot = slot;
        stateBar = bar;
        stateOccupied = slot >= 0 && !info.Empty;
        stateRevision = NoireLanguages.Revision;

        if (slot < 0)
        {
            stateText = L.PickASlotFor.With("emote", entry?.Name ?? L.ThisEmote.Text);
        }
        else
        {
            var cross = bar >= 10;
            var number = slotNumbers[cross ? bar - 10 : bar];
            var slotNumber = slotNumbers[slot];

            stateText = info.Empty
                ? (cross ? L.CrossSlotEmpty : L.HotbarSlotEmpty).With("bar", number, "slot", slotNumber)
                : L.Fill(cross ? L.CrossSlotHolds : L.HotbarSlotHolds, "bar", number, "slot", slotNumber, "name", info.Name);
        }

        return stateText;
    }

    private static string[] BuildSlotNumbers()
    {
        var numbers = new string[16];

        for (var i = 0; i < numbers.Length; i++)
            numbers[i] = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return numbers;
    }
}
