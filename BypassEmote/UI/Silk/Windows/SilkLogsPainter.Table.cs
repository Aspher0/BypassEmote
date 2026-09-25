using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.HistoryLogger;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace BypassEmote.UI.Silk.Windows;

internal sealed partial class SilkLogsPainter
{
    private static string EmptyText => L.NoEntryMatches.Text;
    private static string TimeHead => L.TimeHead.Text;
    private const string HeadSpace = " ";
    private static string LevelHead => L.LevelHead.Text;
    private static string CategoryHead => L.CategoryHead.Text;
    private static string MessageHead => L.MessageHead.Text;
    private static string SourceHead => L.SourceHead.Text;

    private static readonly Vector4 HeadBg = SilkPalette.Hex(0x0a0f1b);
    private static readonly Vector4 RowLine = SilkPalette.Rgb(191, 211, 236, 0.05f);
    private static readonly Vector4 RowHover = SilkPalette.Rgb(191, 211, 236, 0.035f);
    private static readonly Vector4 RowSelected = SilkPalette.Rgb(191, 211, 236, 0.09f);
    private static readonly Vector4 RowWarning = SilkPalette.Rgb(255, 190, 110, 0.035f);
    private static readonly Vector4 RowError = SilkPalette.Rgb(255, 111, 134, 0.045f);

    private sealed class LogRow
    {
        public HistoryLogEntry Entry = null!;
        public string Time = string.Empty;
        public int Level;
        public string Category = string.Empty;
        public string Source = string.Empty;
        public readonly List<string> Lines = [];
        public bool First;
        public int Line;
        public string Text = string.Empty;
        public float Y;
        public float Height;
    }

    private readonly SilkScrollPane tableScroll = new();
    private readonly List<LogRow> rows = [];
    private readonly List<LogRow> rowPool = [];
    private readonly List<string> lineBuffer = [];
    private readonly Dictionary<HistoryLogEntry, string> timeTexts = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<(HistoryLogEntry Entry, int Line)> selection = [];
    private readonly List<HistoryLogEntry> contextEntries = [];
    private readonly System.Text.StringBuilder copyBuilder = new();
    private int anchorRow = -1;
    private HistoryLogEntry? contextEntry;

    private float timeWidth;
    private float levelWidth;
    private float categoryWidth;
    private float sourceWidth;
    private float messageWidth;
    private float rowsHeight;

    private int builtRevision = -1;
    private int builtPage = -1;
    private int builtPerPage = -1;
    private bool builtEmpty;
    private bool builtSplit;
    private bool builtHideCategory;
    private bool builtHideSource;
    private float builtWidth = -1f;
    private float builtScale = -1f;
    private float builtTextScale = -1f;

    private void DrawTable(HistoryLogView current, Vector2 min, Vector2 max)
    {
        if (max.Y <= min.Y)
            return;

        var s = SilkUi.Scale;
        var radius = 12f * s;
        var headHeight = (21.5f * s) + SilkText.NaturalLine(10f, SilkWeight.Bold);

        Build(current, max.X - min.X);

        SilkPaint.Fill(min, max, SilkPalette.Fade(SilkPalette.Panel, SilkUi.Opacity), radius);

        var headMax = new Vector2(max.X, MathF.Min(max.Y, min.Y + headHeight));
        var bodyMin = new Vector2(min.X, headMax.Y);

        if (bodyMin.Y < max.Y)
        {
            tableScroll.Update("rowsbar", bodyMin, max, rowsHeight);
            SilkPaneKit.PushClip(bodyMin, max);
            DrawRows(current, bodyMin, max);
            SilkPaneKit.PopClip();
            tableScroll.DrawBar(bodyMin, max);
        }

        DrawContextMenu();

        SilkPaneKit.PushClip(min, headMax);
        DrawHead(current, min, headMax);
        SilkPaneKit.PopClip();

        SilkPaint.InsetRing(min, max, SilkPalette.Line, radius, s);
    }

    private void DrawHead(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;

        SilkPaint.Fill(min, max, HeadBg, 12f * s, RectCorners.Top);
        SilkPaint.Fill(new Vector2(min.X, max.Y - s), max, SilkPalette.Line2, 0f);

        var x = min.X;
        var timeMin = new Vector2(x, min.Y);
        var timeMax = new Vector2(x + timeWidth, max.Y);
        var clicked = SilkPaneKit.Hit("sort", timeMin, timeMax, out var hovered, out _);

        if (clicked)
        {
            current.SortDescending = !current.SortDescending;
            tableScroll.Reset();
        }

        var headColor = hovered ? SilkPalette.Ink2 : SilkPalette.Ink3;
        var textTop = SilkText.GlyphTop(min.Y, max.Y - min.Y, 10f, SilkWeight.Bold);
        var drawn = SilkText.Draw(new Vector2(x + (12f * s), textTop), SilkFonts.Upper(TimeHead), 10f, SilkWeight.Bold, headColor, 1f);

        DrawSortArrow(new Vector2(x + (12f * s) + drawn.X + SilkText.Width(HeadSpace, 10f, SilkWeight.Bold, 1f), textTop), current.SortDescending, headColor);
        x += timeWidth;

        SilkText.Draw(new Vector2(x + (12f * s), textTop), SilkFonts.Upper(LevelHead), 10f, SilkWeight.Bold, SilkPalette.Ink3, 1f);
        x += levelWidth;

        if (!HistoryLoggerConfig.HideCategoryColumn)
        {
            SilkText.Draw(new Vector2(x + (12f * s), textTop), SilkFonts.Upper(CategoryHead), 10f, SilkWeight.Bold, SilkPalette.Ink3, 1f);
            x += categoryWidth;
        }

        SilkText.Draw(new Vector2(x + (12f * s), textTop), SilkFonts.Upper(MessageHead), 10f, SilkWeight.Bold, SilkPalette.Ink3, 1f);
        x += messageWidth;

        if (!HistoryLoggerConfig.HideSourceColumn)
            SilkText.Draw(new Vector2(x + (12f * s), textTop), SilkFonts.Upper(SourceHead), 10f, SilkWeight.Bold, SilkPalette.Ink3, 1f);
    }

    private static void DrawSortArrow(Vector2 min, bool descending, Vector4 color)
    {
        var s = SilkUi.Scale;
        var height = 9f * s;
        var x = min.X + (2.5f * s);
        var top = min.Y + (2f * s);
        var bottom = top + height;
        var headY = descending ? bottom : top;
        var direction = descending ? -1f : 1f;

        SilkPaint.Line(new Vector2(x, top), new Vector2(x, bottom), color, s);
        SilkPaint.Line(new Vector2(x, headY), new Vector2(x - (2.5f * s), headY + (direction * 2.5f * s)), color, s);
        SilkPaint.Line(new Vector2(x, headY), new Vector2(x + (2.5f * s), headY + (direction * 2.5f * s)), color, s);
    }

    private void DrawRows(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        var top = min.Y - tableScroll.Offset;

        if (rows.Count == 0)
        {
            var height = (80f * s) + SilkText.LineBox(12f, 1.45f);
            SilkText.DrawInBox(new Vector2(min.X, top), new Vector2(max.X, top + height), EmptyText, 12f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.Center);
            return;
        }

        var hoveredRow = -1;
        var clicked = SilkPaneKit.Hit("rows", min, max, out var bodyHovered, out _);
        var mouse = ImGui.GetMousePos();

        if (bodyHovered)
        {
            var local = mouse.Y - top;

            for (var i = 0; i < rows.Count; i++)
            {
                if (local >= rows[i].Y && local < rows[i].Y + rows[i].Height)
                {
                    hoveredRow = i;
                    break;
                }
            }
        }

        if (clicked && hoveredRow >= 0)
            SelectRow(hoveredRow);

        if (bodyHovered && hoveredRow >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            var key = Key(rows[hoveredRow]);

            if (!selection.Contains(key))
            {
                selection.Clear();
                selection.Add(key);
                anchorRow = hoveredRow;
            }

            contextEntry = rows[hoveredRow].Entry;
            OpenContextMenu(mouse);
        }

        var messageLine = SilkText.LineBox(12f, 1.45f);
        var pillHeight = SilkText.NaturalLine(10f, SilkWeight.Bold) + (4f * s);
        var hideCategory = HistoryLoggerConfig.HideCategoryColumn;
        var hideSource = HistoryLoggerConfig.HideSourceColumn;
        var colors = HistoryLoggerConfig.ShowLevelBackgroundColors;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowMin = new Vector2(min.X, top + row.Y);
            var rowMax = new Vector2(max.X, rowMin.Y + row.Height);

            if (rowMax.Y < min.Y || rowMin.Y > max.Y)
                continue;

            if (colors && row.Level >= (int)HistoryLogLevel.Warning)
                SilkPaint.Fill(rowMin, rowMax, row.Level == (int)HistoryLogLevel.Warning ? RowWarning : RowError, 0f);

            if (selection.Contains(Key(row)))
                SilkPaint.Fill(rowMin, rowMax, RowSelected, 0f);
            else if (hoveredRow == i)
                SilkPaint.Fill(rowMin, rowMax, RowHover, 0f);

            SilkPaint.Fill(new Vector2(rowMin.X, rowMax.Y - s), rowMax, RowLine, 0f);

            var x = rowMin.X;
            var textTop = rowMin.Y + (8.5f * s);

            if (row.First)
                SilkText.Draw(new Vector2(x + (12f * s), textTop), row.Time, 11f, SilkWeight.Medium, SilkPalette.Ink3, 0f, true);

            x += timeWidth;

            if (row.First)
                DrawLevelPill(new Vector2(x + (12f * s), rowMin.Y + (9.5f * s)), pillHeight, row.Level);

            x += levelWidth;

            if (!hideCategory)
            {
                if (row.First)
                    SilkText.Draw(new Vector2(x + (12f * s), textTop), row.Category, 11f, SilkWeight.SemiBold, SilkPalette.Ink2, 0f, false, categoryWidth - (24f * s));

                x += categoryWidth;
            }

            for (var l = 0; l < row.Lines.Count; l++)
                SilkText.Draw(new Vector2(x + (12f * s), textTop + (l * messageLine)), row.Lines[l], 12f, SilkWeight.Medium, SilkPalette.Ink);

            x += messageWidth;

            if (!hideSource && row.First)
                SilkText.Draw(new Vector2(x + (12f * s), textTop), row.Source, 11f, SilkWeight.Regular, SilkPalette.Ink3, 0f, true, sourceWidth - (24f * s));
        }
    }

    private static void DrawLevelPill(Vector2 min, float height, int level)
    {
        var s = SilkUi.Scale;
        var color = LevelColors[level];
        var text = LevelNames[level];
        var width = (24f * s) + SilkText.Width(text, 10f, SilkWeight.Bold, 0.4f);
        var max = min + new Vector2(width, height);

        SilkPaint.Fill(min, max, SilkPalette.Alpha(color, 0.13f), height * 0.5f);

        var dot = 5f * s;
        var dotMin = new Vector2(min.X + (7f * s), MathF.Round(((min.Y + max.Y) * 0.5f) - (dot * 0.5f)));
        SilkPaint.Fill(dotMin, dotMin + new Vector2(dot, dot), color, dot * 0.5f);

        SilkText.Draw(new Vector2(min.X + (17f * s), min.Y + (2f * s)), text, 10f, SilkWeight.Bold, color, 0.4f);
    }

    private void Build(HistoryLogView current, float width)
    {
        var s = SilkUi.Scale;
        var textScale = SilkUi.TextScale;
        var rounded = MathF.Round(width);
        var empty = NoLevels;
        var split = HistoryLoggerConfig.SelectLinesSeparately;
        var hideCategory = HistoryLoggerConfig.HideCategoryColumn;
        var hideSource = HistoryLoggerConfig.HideSourceColumn;

        if (builtRevision == current.Revision && builtPage == current.Page && builtPerPage == current.ItemsPerPage
            && builtEmpty == empty && builtSplit == split && builtHideCategory == hideCategory && builtHideSource == hideSource
            && builtWidth == rounded && builtScale == s && builtTextScale == textScale)
            return;

        builtRevision = current.Revision;
        builtPage = current.Page;
        builtPerPage = current.ItemsPerPage;
        builtEmpty = empty;
        builtSplit = split;
        builtHideCategory = hideCategory;
        builtHideSource = hideSource;
        builtWidth = rounded;
        builtScale = s;
        builtTextScale = textScale;

        foreach (var row in rows)
        {
            row.Lines.Clear();
            rowPool.Add(row);
        }

        rows.Clear();

        if (!empty)
        {
            foreach (var entry in current.PageEntries)
            {
                var message = entry.Message ?? string.Empty;

                if (split)
                {
                    var first = true;
                    var any = false;
                    var lineIndex = 0;

                    foreach (var part in message.Split('\n'))
                    {
                        var line = part.Trim('\r');

                        if (line.Length == 0)
                            continue;

                        any = true;
                        rows.Add(NewRow(entry, first, line, lineIndex++));
                        first = false;
                    }

                    if (!any)
                        rows.Add(NewRow(entry, true, string.Empty, 0));
                }
                else
                {
                    rows.Add(NewRow(entry, true, message, -1));
                }
            }
        }

        timeWidth = ColumnWidth(SilkText.Width(SilkFonts.Upper(TimeHead), 10f, SilkWeight.Bold, 1f)
            + SilkText.Width(HeadSpace, 10f, SilkWeight.Bold, 1f) + (6f * s));
        levelWidth = ColumnWidth(SilkText.Width(SilkFonts.Upper(LevelHead), 10f, SilkWeight.Bold, 1f));
        categoryWidth = ColumnWidth(SilkText.Width(SilkFonts.Upper(CategoryHead), 10f, SilkWeight.Bold, 1f));
        sourceWidth = ColumnWidth(SilkText.Width(SilkFonts.Upper(SourceHead), 10f, SilkWeight.Bold, 1f));

        foreach (var row in rows)
        {
            if (!row.First)
                continue;

            timeWidth = MathF.Max(timeWidth, ColumnWidth(SilkText.Width(row.Time, 11f, SilkWeight.Medium, 0f, true)));
            levelWidth = MathF.Max(levelWidth, ColumnWidth((24f * s) + SilkText.Width(LevelNames[row.Level], 10f, SilkWeight.Bold, 0.4f)));

            if (!hideCategory)
                categoryWidth = MathF.Max(categoryWidth, ColumnWidth(SilkText.Width(row.Category, 11f, SilkWeight.SemiBold)));

            if (!hideSource)
                sourceWidth = MathF.Max(sourceWidth, ColumnWidth(SilkText.Width(row.Source, 11f, SilkWeight.Regular, 0f, true)));
        }

        if (hideCategory)
            categoryWidth = 0f;

        if (hideSource)
            sourceWidth = 0f;

        var headMessage = ColumnWidth(SilkText.Width(SilkFonts.Upper(MessageHead), 10f, SilkWeight.Bold, 1f));
        messageWidth = MathF.Max(headMessage, width - timeWidth - levelWidth - categoryWidth - sourceWidth);

        var messageLine = SilkText.LineBox(12f, 1.45f);
        var pillHeight = SilkText.NaturalLine(10f, SilkWeight.Bold) + (5f * s);
        var wrapWidth = MathF.Max(20f * s, messageWidth - (24f * s));
        var y = 0f;

        foreach (var row in rows)
        {
            SilkText.Wrap(row.Lines.Count > 0 ? row.Lines[0] : string.Empty, 12f, SilkWeight.Medium, wrapWidth, lineBuffer);
            row.Lines.Clear();

            foreach (var line in lineBuffer)
                row.Lines.Add(line);

            row.Y = y;
            row.Height = (17f * s) + MathF.Max(row.Lines.Count * messageLine, pillHeight);
            y += row.Height;
        }

        rowsHeight = rows.Count == 0 ? (80f * s) + messageLine : y;
    }

    private static float ColumnWidth(float content) => content + (24f * SilkUi.Scale);

    private LogRow NewRow(HistoryLogEntry entry, bool first, string message, int line)
    {
        LogRow row;

        if (rowPool.Count > 0)
        {
            row = rowPool[^1];
            rowPool.RemoveAt(rowPool.Count - 1);
        }
        else
        {
            row = new LogRow();
        }

        row.Entry = entry;
        row.First = first;
        row.Level = (int)entry.Level;
        row.Category = entry.Category ?? string.Empty;
        row.Source = entry.Source ?? string.Empty;
        row.Time = TimeText(entry);
        row.Line = line;
        row.Text = message;
        row.Lines.Clear();
        row.Lines.Add(message);
        return row;
    }

    private static (HistoryLogEntry Entry, int Line) Key(LogRow row) => (row.Entry, row.Line);

    private void ClearSelection()
    {
        selection.Clear();
        anchorRow = -1;
    }

    private void SelectRow(int index)
    {
        var io = ImGui.GetIO();
        var key = Key(rows[index]);

        if (io.KeyShift && anchorRow >= 0 && anchorRow < rows.Count)
        {
            if (!io.KeyCtrl)
                selection.Clear();

            var start = Math.Min(anchorRow, index);
            var end = Math.Max(anchorRow, index);

            for (var i = start; i <= end; i++)
                selection.Add(Key(rows[i]));
        }
        else if (io.KeyCtrl)
        {
            if (!selection.Add(key))
                selection.Remove(key);
        }
        else if (selection.Contains(key) && selection.Count == 1)
        {
            selection.Clear();
        }
        else
        {
            selection.Clear();
            selection.Add(key);
        }

        anchorRow = index;
    }

    private void CollectSelectedEntries()
    {
        contextEntries.Clear();

        foreach (var row in rows)
        {
            if (selection.Contains(Key(row)) && (contextEntries.Count == 0 || !ReferenceEquals(contextEntries[^1], row.Entry)))
                contextEntries.Add(row.Entry);
        }

        if (contextEntries.Count == 0 && contextEntry != null)
            contextEntries.Add(contextEntry);
    }

    private const int MenuDelete = 1;
    private const int MenuCopy = 2;
    private const int MenuCopyMessages = 3;

    private static readonly SilkIconShape CopyIcon = SilkIcons.Stroked(16f, 1.5f, true, "M5.5 5.5h7v8h-7z", "M3.5 10.5v-8h7");

    private readonly SilkMenu contextMenu = new("##silklogsmenu");

    private void OpenContextMenu(Vector2 at)
    {
        CollectSelectedEntries();

        var split = HistoryLoggerConfig.SelectLinesSeparately;
        var count = contextEntries.Count;

        contextMenu.Clear();

        if (viewLogger?.CanUserDeleteEntries == true)
        {
            contextMenu.Add(MenuDelete, count > 1 ? L.Count(L.DeleteSelected, count) : L.DeleteEntry.Text, SilkIcons.Get(SilkIcon.Trash), true, L.HoldCtrlToDelete.Text);
            contextMenu.Separator();
        }

        string copyLabel;

        if (split)
            copyLabel = count > 1 ? L.Count(L.CopyLinesMultiple, count) : selection.Count <= 1 ? L.CopyLine.Text : L.CopyLines.Text;
        else
            copyLabel = count > 1 ? L.Count(L.CopyEntries, count) : L.CopyEntry.Text;

        contextMenu.Add(MenuCopy, copyLabel, CopyIcon);

        if (!split || SelectionTouchesFirstLine())
            contextMenu.Add(MenuCopyMessages, count > 1 ? L.Count(L.CopyMessages, count) : L.CopyMessage.Text, SilkIcons.Get(SilkIcon.Logs));

        contextMenu.Open(at);
    }

    private void DrawContextMenu()
    {
        var picked = contextMenu.Draw();

        if (picked < 0)
            return;

        CollectSelectedEntries();

        switch (picked)
        {
            case MenuDelete when viewLogger != null:
                foreach (var entry in contextEntries)
                {
                    viewLogger.RemoveEntry(entry);
                    selection.RemoveWhere(key => ReferenceEquals(key.Entry, entry));
                }

                contextEntry = null;
                break;

            case MenuCopy:
                ImGui.SetClipboardText(NoireHistoryLogger.FormatEntries(contextEntries));
                break;

            case MenuCopyMessages:
                copyBuilder.Clear();

                foreach (var entry in contextEntries)
                {
                    if (copyBuilder.Length > 0)
                        copyBuilder.Append('\n');

                    copyBuilder.Append(entry.Message);
                }

                ImGui.SetClipboardText(copyBuilder.ToString());
                break;
        }
    }

    private bool SelectionTouchesFirstLine()
    {
        foreach (var key in selection)
        {
            if (key.Line <= 0)
                return true;
        }

        return false;
    }

    private string TimeText(HistoryLogEntry entry)
    {
        if (timeTexts.TryGetValue(entry, out var text))
            return text;

        text = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        if (timeTexts.Count > 4000)
            timeTexts.Clear();

        timeTexts[entry] = text;
        return text;
    }
}
