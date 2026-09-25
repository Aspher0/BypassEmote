using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib;
using NoireLib.HistoryLogger;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Windows;

internal sealed partial class SilkLogsPainter
{
    private enum Dropdown
    {
        None,
        Categories,
        Options,
        Add,
    }

    private static string SearchHint => L.LogsSearch.Text;
    private static string AllCategories => L.AllCategories.Text;
    private static string OptionsText => L.Options.Text;
    private static string AddText => L.Add.Text;
    private static string AddEntryText => L.AddEntry.Text;
    private static string CategoryHint => L.CategoryHint.Text;
    private static string MessageHint => L.MessageHint.Text;
    private static string RefreshText => L.Refresh.Text;
    private static string ExportText => L.Export.Text;
    private static string HoldText => L.HoldToClear.Text;
    private static string ExportTip => L.ExportTip.Text;
    private static string HoldTipMemory => L.HoldTipMemory.Text;
    private static string HoldTipDatabase => L.HoldTipDatabase.Text;
    private static string PersistText => L.PersistToDatabase.Text;
    private static string ColorsText => L.ShowColors.Text;
    private static string SplitText => L.SplitLines.Text;
    private static string HideCategoryText => L.HideCategory.Text;
    private static string HideSourceText => L.HideSource.Text;
    private const string CategoriesPopup = "##silklogs.cat";
    private const string OptionsPopup = "##silklogs.opt";
    private const string AddPopup = "##silklogs.add";

    private static readonly HistoryLogLevel[] Levels = Enum.GetValues<HistoryLogLevel>();
    private static readonly TextList LevelNames = new(L.LevelTrace, L.LevelDebug, L.LevelInfo, L.LevelWarning, L.LevelError, L.LevelCritical);
    private static readonly Vector4[] LevelColors =
    [
        SilkPalette.Hex(0x7d8799),
        SilkPalette.Hex(0x9aa8c2),
        SilkPalette.Hex(0x8fc4ff),
        SilkPalette.Hex(0xffbe6e),
        SilkPalette.Hex(0xff6f86),
        SilkPalette.Hex(0xe36bff),
    ];

    private static readonly int[] PageSizes = [25, 50, 100];
    private static readonly string[] PageSizeTexts = ["25", "50", "100"];
    private static readonly Vector4 ChipOffBg = SilkPalette.Rgb(0, 0, 0, 0.2f);
    private static readonly Vector4 HoverBg = SilkPalette.Rgb(191, 211, 236, 0.07f);
    private static readonly Vector4 MenuShadow = SilkPalette.Rgb(0, 0, 0, 0.95f);

    private readonly Dictionary<int, string> numberTexts = new();
    private readonly Vector2[] barMin = new Vector2[5];
    private readonly Vector2[] barMax = new Vector2[5];

    private HistoryLogView? view;
    private NoireHistoryLogger? viewLogger;
    private string search = string.Empty;
    private string newCategory = "General";
    private string newMessage = string.Empty;
    private Dropdown open;
    private bool pressedWhileOpen;
    private Vector2 menuMin;
    private Vector2 menuMax;
    private Vector2 windowRight;
    private bool searchActive;

    private float holdProgress;
    private bool holdLatched;
    private float releaseFrom;
    private float releaseAt = -10f;
    private float exportAt = -10f;
    private float refreshAt = -10f;

    private static readonly SilkIconShape ExportTray = SilkIcons.Stroked(16f, 1.6f, true, "M3 10v3h10v-3");
    private static readonly SilkIconShape ExportArrow = SilkIcons.Stroked(16f, 1.6f, true, "M8 2v8M5 5l3-3 3 3");

    private float RefreshSpin()
    {
        var since = SilkUi.Time - refreshAt;

        if (since < 0f || since >= 0.6f || SilkUi.ReducedMotion)
            return 0f;

        return SilkUi.EaseOut.Evaluate(since / 0.6f) * MathF.Tau;
    }

    private float ExportLift()
    {
        var since = SilkUi.Time - exportAt;

        if (since < 0f || since >= 0.45f || SilkUi.ReducedMotion)
            return 0f;

        var t = since / 0.45f;
        return 3f * MathF.Sin(t * MathF.PI);
    }

    private string categoryLabel = AllCategories;
    private int categoryLabelRevision = -1;
    private int categoryLabelCount = -1;
    private string? categoryLabelFirst;

    private string countText = string.Empty;
    private string pageText = string.Empty;
    private int textRevision = -1;
    private int textPage = -1;
    private int textPerPage = -1;
    private bool textEmpty;
    private int textLanguage = -1;

    private readonly BeLogsWindow window;
    private readonly string animKey;

    internal SilkLogsPainter(BeLogsWindow window)
    {
        this.window = window;
        animKey = "SilkWindow." + window.Id;
    }

    private string AnimationKey => animKey;

    private Vector2 WindowMax => window.WindowMax;

    private HistoryLogView? View()
    {
        var logger = NoireLibMain.GetModule<NoireHistoryLogger>();

        if (logger == null)
            return null;

        if (view == null || !ReferenceEquals(viewLogger, logger))
        {
            view = new HistoryLogView(logger) { ItemsPerPage = 50 };
            ResetLevels(view);
            viewLogger = logger;
        }

        return view;
    }

    private static void ResetLevels(HistoryLogView target)
    {
        foreach (var level in Levels)
            target.SetLevelSelected(level, level >= HistoryLogLevel.Info);
    }

    private bool NoLevels => view == null || view.SelectedLevels.Count == 0;

    internal void DrawBody(Vector2 min, Vector2 max)
    {
        var current = View();

        if (current == null)
            return;

        windowRight = new Vector2(WindowMax.X, WindowMax.Y);

        var s = SilkUi.Scale;
        var footTop = max.Y - (54f * s);
        var tableTop = DrawBar(current, min, max);

        DrawTable(current, new Vector2(min.X + (16f * s), tableTop), new Vector2(max.X - (16f * s), MathF.Max(tableTop, footTop)));
        DrawFooter(current, new Vector2(min.X, footTop), max);
        DrawMenus(current);
    }

    internal void DrawOverlay(Vector2 min, Vector2 max)
    {
        if (open == Dropdown.None)
            return;

        var s = SilkUi.Scale;
        SilkPaint.BoxShadow(menuMin, menuMax, new Vector2(0f, 20f * s), 40f * s, -10f * s, MenuShadow, 11f * s);
    }

    private float DrawBar(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        var left = min.X + (16f * s);
        var avail = (max.X - (16f * s)) - left;
        var gap = 8f * s;

        UpdateCategoryLabel(current);

        Span<float> widths = stackalloc float[5];
        widths[0] = 220f * s;
        widths[1] = ChipsWidth(current);
        widths[2] = DropWidth(categoryLabel, true);
        widths[3] = DropWidth(OptionsText, true);
        widths[4] = DropWidth(AddText, false);

        Span<float> english = stackalloc float[5];
        english[0] = widths[0];
        english[1] = ChipsWidth(current, true);
        english[2] = DropWidth(ReferenceEquals(categoryLabel, AllCategories) ? L.AllCategories.Source : categoryLabel, true);
        english[3] = DropWidth(L.Options.Source, true);
        english[4] = DropWidth(L.Add.Source, false);

        Span<int> lineOf = stackalloc int[5];
        Span<float> lineUsed = stackalloc float[5];
        var line = 0;
        var count = 0;

        for (var i = 0; i < 5; i++)
        {
            var need = count == 0 ? english[i] : lineUsed[line] + gap + english[i];

            if (count > 0 && (need > avail || i == 3))
            {
                line++;
                lineUsed[line] = english[i];
                count = 1;
            }
            else
            {
                lineUsed[line] = need;
                count++;
            }

            lineOf[i] = line;
        }

        FitLines(widths, lineOf, lineUsed, line, avail, gap);

        var y = min.Y + (4f * s);

        for (var l = 0; l <= line; l++)
        {
            var hasSearch = lineOf[0] == l;
            var height = (hasSearch ? 36f : 30f) * s;
            var x = left;

            for (var i = 0; i < 5; i++)
            {
                if (lineOf[i] != l)
                    continue;

                var width = widths[i];

                if (i == 0)
                    width += MathF.Max(0f, avail - lineUsed[l]);

                var itemHeight = (i == 0 ? 36f : 30f) * s;
                barMin[i] = new Vector2(x, y + ((height - itemHeight) * 0.5f));
                barMax[i] = barMin[i] + new Vector2(width, itemHeight);
                x += width + gap;
            }

            y += height + (l < line ? gap : 0f);
        }

        DrawSearch(current, barMin[0], barMax[0]);
        DrawChips(current, barMin[1], barMax[1]);
        DrawDrop(Dropdown.Categories, CategoriesPopup, barMin[2], barMax[2], SilkIcon.Tag, categoryLabel, true);
        DrawDrop(Dropdown.Options, OptionsPopup, barMin[3], barMax[3], SilkIcon.Sliders, OptionsText, true);
        DrawDrop(Dropdown.Add, AddPopup, barMin[4], barMax[4], SilkIcon.Plus, AddText, false);

        return y + (10f * s);
    }

    private void DrawSearch(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        var radius = 11f * s;
        var focus = SilkUi.Css(AnimationKey, "search", searchActive ? 1f : 0f, 0.25f);

        if (focus > 0f)
            SilkPaint.OuterRing(min, max, SilkPalette.Fade(SilkPalette.FocusHalo, focus), radius, 4f * s);

        SilkPaint.Fill(min, max, SilkPalette.Sunken, radius);
        SilkPaint.InsetRing(min, max, SilkPalette.Mix(SilkPalette.Line2, SilkPalette.IceRing60, focus), radius, s);

        var icon = 15f * s;
        SilkIcons.Draw(SilkIcon.Search, new Vector2(min.X + (12f * s), MathF.Round(((min.Y + max.Y) * 0.5f) - (icon * 0.5f))), icon, SilkPalette.Ink3);

        var inputLeft = min.X + (37f * s);
        var inputWidth = MathF.Max(1f, max.X - (12f * s) - inputLeft);

        if (Input("##silklogs.search", SearchHint, ref search, 200, new Vector2(inputLeft, min.Y), inputWidth, max.Y - min.Y, SilkFace.Ui400, 13.5f, out searchActive))
        {
            current.SearchText = search;
            OnFiltersChanged();
        }
    }

    private static bool Input(string id, string hint, ref string text, int maxLength, Vector2 boxMin, float width, float boxHeight, SilkFace face, float cssPx, out bool active)
    {
        using var font = SilkFonts.Push(face, cssPx);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, SilkPalette.Ink);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, SilkPalette.Ink3);
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, SilkPalette.IceRing30);

        var frame = ImGui.GetFrameHeight();
        ImGui.SetCursorScreenPos(new Vector2(boxMin.X, MathF.Round(boxMin.Y + ((boxHeight - frame) * 0.5f))));
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.InputTextWithHint(id, hint, ref text, maxLength);
        active = ImGui.IsItemActive();

        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(2);
        return changed;
    }

    private float ChipsWidth(HistoryLogView current, bool english = false)
    {
        var s = SilkUi.Scale;
        var total = 0f;

        for (var i = 0; i < Levels.Length; i++)
            total += ChipWidth(current, i, english) + (i > 0 ? 4f * s : 0f);

        return total;
    }

    private float ChipWidth(HistoryLogView current, int index, bool english = false)
    {
        var s = SilkUi.Scale;
        var number = PaneNumber(current.CountOf(Levels[index]));
        var name = english ? LevelNames.Source(index) : LevelNames[index];
        return (10f + 7f + 6f + 6f + 10f) * s + SilkText.Width(name, 11.5f, SilkWeight.SemiBold) + SilkText.Width(number, 10f, SilkWeight.Medium, 0f, true);
    }

    private static void FitLines(Span<float> widths, ReadOnlySpan<int> lineOf, Span<float> lineUsed, int lines, float avail, float gap)
    {
        var s = SilkUi.Scale;

        for (var l = 0; l <= lines; l++)
        {
            var used = 0f;
            var count = 0;

            for (var i = 0; i < widths.Length; i++)
            {
                if (lineOf[i] != l)
                    continue;

                used += widths[i] + (count > 0 ? gap : 0f);
                count++;
            }

            var excess = used - avail;

            if (excess > 0f && lineOf[0] == l)
            {
                var give = MathF.Min(excess, MathF.Max(0f, widths[0] - (140f * s)));
                widths[0] -= give;
                excess -= give;
            }

            if (excess > 0f)
            {
                var shrinkable = 0f;

                for (var i = 2; i < widths.Length; i++)
                {
                    if (lineOf[i] == l)
                        shrinkable += MathF.Max(0f, widths[i] - (60f * s));
                }

                for (var i = 2; i < widths.Length && shrinkable > 0f; i++)
                {
                    if (lineOf[i] == l)
                        widths[i] -= excess * (MathF.Max(0f, widths[i] - (60f * s)) / shrinkable);
                }
            }

            lineUsed[l] = MathF.Min(used, avail);
        }
    }

    private void DrawChips(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        var x = min.X;

        for (var i = 0; i < Levels.Length; i++)
        {
            var width = ChipWidth(current, i);
            var chipMin = new Vector2(x, min.Y);
            var chipMax = new Vector2(x + width, max.Y);
            var level = Levels[i];
            var on = current.IsLevelSelected(level);
            var color = LevelColors[i];

            ImGui.PushID(i);

            if (SilkPaneKit.Hit("lv", chipMin, chipMax, out _, out _))
            {
                current.SetLevelSelected(level, !on);
                OnFiltersChanged();
                on = !on;
            }

            ImGui.PopID();

            var radius = 8f * s;

            if (on)
            {
                SilkPaint.Fill(chipMin, chipMax, SilkPalette.Alpha(color, 0.10f), radius);
                SilkPaint.InsetRing(chipMin, chipMax, SilkPalette.Alpha(color, 0.55f), radius, s);
            }
            else
            {
                SilkPaint.Fill(chipMin, chipMax, ChipOffBg, radius);
                SilkPaint.InsetRing(chipMin, chipMax, SilkPalette.Line, radius, s);
            }

            var dot = 7f * s;
            var dotMin = new Vector2(chipMin.X + (10f * s), MathF.Round(((chipMin.Y + chipMax.Y) * 0.5f) - (dot * 0.5f)));
            var dotMax = dotMin + new Vector2(dot, dot);

            if (on)
                SilkPaint.Glow(dotMin, dotMax, 8f * s, 0f, color, dot * 0.5f);

            SilkPaint.Fill(dotMin, dotMax, on ? color : SilkPalette.Alpha(color, 0.35f), dot * 0.5f);

            var textColor = on ? SilkPalette.Ink : SilkPalette.Ink3;
            var textX = chipMin.X + (23f * s);
            var drawn = SilkText.DrawInBox(new Vector2(textX, chipMin.Y), new Vector2(chipMax.X, chipMax.Y), LevelNames[i], 11.5f, SilkWeight.SemiBold, textColor);
            var number = PaneNumber(current.CountOf(level));
            SilkText.DrawInBox(new Vector2(textX + drawn.X + (6f * s), chipMin.Y), chipMax, number, 10f, SilkWeight.Medium, SilkPalette.Fade(textColor, 0.7f), UiAlign.Start, 0f, true);

            x += width + (4f * s);
        }
    }

    private float DropWidth(string text, bool chevron)
    {
        var s = SilkUi.Scale;
        var width = (10f + 12f + 7f + 10f) * s + SilkText.Width(text, 11.5f, SilkWeight.SemiBold);

        if (chevron)
            width += (7f + 12f) * s;

        return width;
    }

    private void DrawDrop(Dropdown kind, string popup, Vector2 min, Vector2 max, SilkIcon icon, string text, bool chevron)
    {
        var s = SilkUi.Scale;

        ImGui.PushID((int)kind);
        var clicked = SilkPaneKit.Hit("dd", min, max, out var hovered, out _);

        if (ImGui.IsItemActivated())
            pressedWhileOpen = open == kind;

        ImGui.PopID();

        if (clicked)
        {
            if (pressedWhileOpen || open == kind)
            {
                open = Dropdown.None;
            }
            else
            {
                open = kind;
                ImGui.OpenPopup(popup);
            }

            pressedWhileOpen = false;
        }

        var radius = 8f * s;
        var color = hovered ? SilkPalette.Ink : SilkPalette.Ink2;
        SilkPaint.InsetRing(min, max, SilkPalette.Line2, radius, s);

        var glyph = 12f * s;
        var centreY = (min.Y + max.Y) * 0.5f;
        SilkIcons.Draw(icon, new Vector2(min.X + (10f * s), MathF.Round(centreY - (glyph * 0.5f))), glyph, color);
        var textMax = new Vector2(max.X - ((chevron ? 28f : 9f) * s), max.Y);
        SilkText.DrawInBox(new Vector2(min.X + (29f * s), min.Y), textMax, text, 11.5f, SilkWeight.SemiBold, color);

        if (hovered && SilkText.Width(text, 11.5f, SilkWeight.SemiBold) > textMax.X - min.X - (29f * s) + 0.5f)
            SilkTooltip.Hover(text, min, max);

        if (chevron)
            SilkIcons.Draw(SilkIcon.Chevron, new Vector2(max.X - (22f * s), MathF.Round(centreY - (glyph * 0.5f))), glyph, color);

        if (open == kind)
            PlaceMenu(kind, min, max);
    }

    private void PlaceMenu(Dropdown kind, Vector2 anchorMin, Vector2 anchorMax)
    {
        var s = SilkUi.Scale;
        var size = new Vector2(220f * s, MenuHeight(kind));

        if (kind == Dropdown.Categories && view != null)
        {
            foreach (var category in view.Categories)
                size.X = MathF.Max(size.X, ((12f + 31f + 8f) * s) + SilkText.Width(category, 12f));
        }

        var x = anchorMin.X;

        if (x + size.X > windowRight.X - (8f * s))
            x = anchorMax.X - size.X;

        menuMin = new Vector2(x, anchorMin.Y + (36f * s));
        menuMax = menuMin + size;
    }

    private const int MaxVisibleCategories = 10;

    private readonly Settings.SilkScrollArea categoryScroll = new();

    private float MenuHeight(Dropdown kind)
    {
        var s = SilkUi.Scale;

        return kind switch
        {
            Dropdown.Categories => (12f + 30f + 9f) * s + (Math.Min(view?.Categories.Count ?? 0, MaxVisibleCategories) * 30f * s),
            Dropdown.Options => (12f * s) + (PersistShown ? 39f * s : 0f) + (120f * s),
            Dropdown.Add => 136f * s,
            _ => 0f,
        };
    }

    private bool PersistShown => viewLogger?.AllowUserTogglePersistence == true;

    private void DrawMenus(HistoryLogView current)
    {
        DrawMenu(current, Dropdown.Categories, CategoriesPopup);
        DrawMenu(current, Dropdown.Options, OptionsPopup);
        DrawMenu(current, Dropdown.Add, AddPopup);
    }

    private void DrawMenu(HistoryLogView current, Dropdown kind, string popup)
    {
        var s = SilkUi.Scale;
        var pad = 2f * s;

        if (open == kind)
        {
            ImGui.SetNextWindowPos(menuMin - new Vector2(pad, pad));
            ImGui.SetNextWindowSize((menuMax - menuMin) + new Vector2(pad * 2f, pad * 2f));
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 0f);

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse;

        var visible = ImGui.BeginPopup(popup, flags);
        ImGui.PopStyleVar(3);

        if (!visible)
        {
            if (open == kind)
                open = Dropdown.None;

            return;
        }

        if (open != kind)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        var radius = 11f * s;
        SilkPaint.Fill(menuMin, menuMax, SilkPalette.Popup, radius);
        SilkPaint.OuterRing(menuMin, menuMax, SilkPalette.Line2, radius, s);

        var inner = new Vector2(menuMin.X + (6f * s), menuMin.Y + (6f * s));
        var width = (menuMax.X - menuMin.X) - (12f * s);

        switch (kind)
        {
            case Dropdown.Categories:
                DrawCategoryMenu(current, inner, width);
                break;
            case Dropdown.Options:
                DrawOptionsMenu(inner, width);
                break;
            case Dropdown.Add:
                DrawAddMenu(inner, width);
                break;
        }

        ImGui.EndPopup();
    }

    private void DrawCategoryMenu(HistoryLogView current, Vector2 origin, float width)
    {
        var s = SilkUi.Scale;
        var y = origin.Y;

        if (Check("all", new Vector2(origin.X, y), width, AllCategories, current.SelectedCategories.Count == 0))
        {
            current.ClearCategoryFilter();
            OnFiltersChanged();
        }

        y += 30f * s;
        SilkPaint.Fill(new Vector2(origin.X + (4f * s), y + (4f * s)), new Vector2(origin.X + width - (4f * s), y + (5f * s)), SilkPalette.Line, 0f);
        y += 9f * s;

        var categories = current.Categories;
        var listMin = new Vector2(origin.X, y);
        var listMax = new Vector2(origin.X + width, menuMax.Y - (6f * s));
        var offset = categoryScroll.Begin(listMin, listMax, true);

        ImGui.PushClipRect(listMin, listMax, true);
        y -= offset;

        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            ImGui.PushID(i);

            if (Check("c", new Vector2(origin.X, y), width, category, current.IsCategorySelected(category)))
            {
                current.ToggleCategory(category);
                OnFiltersChanged();
            }

            ImGui.PopID();
            y += 30f * s;
        }

        ImGui.PopClipRect();
        categoryScroll.End(listMin, listMax, categories.Count * 30f * s);
    }

    private void DrawOptionsMenu(Vector2 origin, float width)
    {
        var s = SilkUi.Scale;
        var y = origin.Y;
        var logger = viewLogger;

        if (PersistShown && logger != null)
        {
            if (Check("persist", new Vector2(origin.X, y), width, PersistText, logger.PersistLogs))
                logger.SetPersistLogs(!logger.PersistLogs, true);

            y += 30f * s;
            SilkPaint.Fill(new Vector2(origin.X + (4f * s), y + (4f * s)), new Vector2(origin.X + width - (4f * s), y + (5f * s)), SilkPalette.Line, 0f);
            y += 9f * s;
        }

        if (Check("colors", new Vector2(origin.X, y), width, ColorsText, HistoryLoggerConfig.ShowLevelBackgroundColors))
            HistoryLoggerConfig.ShowLevelBackgroundColors = !HistoryLoggerConfig.ShowLevelBackgroundColors;

        y += 30f * s;

        if (Check("split", new Vector2(origin.X, y), width, SplitText, HistoryLoggerConfig.SelectLinesSeparately))
            HistoryLoggerConfig.SelectLinesSeparately = !HistoryLoggerConfig.SelectLinesSeparately;

        y += 30f * s;

        if (Check("nocat", new Vector2(origin.X, y), width, HideCategoryText, HistoryLoggerConfig.HideCategoryColumn))
            HistoryLoggerConfig.HideCategoryColumn = !HistoryLoggerConfig.HideCategoryColumn;

        y += 30f * s;

        if (Check("nosrc", new Vector2(origin.X, y), width, HideSourceText, HistoryLoggerConfig.HideSourceColumn))
            HistoryLoggerConfig.HideSourceColumn = !HistoryLoggerConfig.HideSourceColumn;
    }

    private void DrawAddMenu(Vector2 origin, float width)
    {
        var s = SilkUi.Scale;
        var x = origin.X + (6f * s);
        var y = origin.Y + (6f * s);
        var inner = width - (12f * s);

        Field("##silklogs.newcat", CategoryHint, ref newCategory, new Vector2(x, y), inner, 60, out _);
        y += 38f * s;

        var submitted = Field("##silklogs.newmsg", MessageHint, ref newMessage, new Vector2(x, y), inner, 300, out var enter);
        y += 38f * s;

        var add = SilkPaneKit.PrimaryButton("addentry", new Vector2(x, y), new Vector2(x + inner, y + (36f * s)), AddEntryText, true);

        if ((add || (submitted && enter)) && viewLogger != null && !string.IsNullOrWhiteSpace(newMessage))
        {
            viewLogger.AddEntry(newMessage.Trim(), string.IsNullOrWhiteSpace(newCategory) ? "General" : newCategory.Trim(), HistoryLogLevel.Info, "Manual");
            newMessage = string.Empty;
            open = Dropdown.None;
            ImGui.CloseCurrentPopup();
        }
    }

    private bool Field(string id, string hint, ref string text, Vector2 min, float width, int maxLength, out bool enter)
    {
        var s = SilkUi.Scale;
        var max = min + new Vector2(width, 32f * s);
        var radius = 10f * s;

        SilkPaint.Fill(min, max, SilkPalette.Sunken, radius);
        SilkPaint.InsetRing(min, max, SilkPalette.Line2, radius, s);

        var changed = Input(id, hint, ref text, maxLength, new Vector2(min.X + (12f * s), min.Y), width - (24f * s), 32f * s, SilkFace.Ui500, 13f, out var active);
        enter = active && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));

        if (active)
        {
            SilkPaint.OuterRing(min, max, SilkPalette.FocusHalo, radius, 4f * s);
            SilkPaint.InsetRing(min, max, SilkPalette.IceRing60, radius, s);
        }

        return changed || enter;
    }

    private static bool Check(string id, Vector2 min, float width, string text, bool on)
    {
        var s = SilkUi.Scale;
        var max = min + new Vector2(width, 30f * s);
        var clicked = SilkPaneKit.Hit(id, min, max, out var hovered, out _);

        if (hovered)
            SilkPaint.Fill(min, max, HoverBg, 7f * s);

        var box = 14f * s;
        var boxMin = new Vector2(min.X + (8f * s), MathF.Round(((min.Y + max.Y) * 0.5f) - (box * 0.5f)));
        var boxMax = boxMin + new Vector2(box, box);

        if (on)
        {
            SilkPaint.Fill(boxMin, boxMax, SilkPalette.Ice, 4f * s);
            SilkPaint.Line(boxMin + new Vector2(4f * s, 7f * s), boxMin + new Vector2(6.2f * s, 9.2f * s), SilkPalette.PriText, 1.6f * s);
            SilkPaint.Line(boxMin + new Vector2(6.2f * s, 9.2f * s), boxMin + new Vector2(10.2f * s, 5.2f * s), SilkPalette.PriText, 1.6f * s);
        }
        else
        {
            SilkPaint.InsetRing(boxMin, boxMax, SilkPalette.Line2, 4f * s, 1.5f * s);
        }

        SilkText.DrawInBox(new Vector2(min.X + (31f * s), min.Y), max, text, 12f, SilkWeight.Medium, hovered ? SilkPalette.Ink : SilkPalette.Ink2);
        return clicked;
    }

    private void DrawFooter(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        var top = min.Y + (10f * s);
        var height = 30f * s;
        var gap = 10f * s;
        var left = min.X + (16f * s);
        var right = max.X - (16f * s);

        UpdateTexts(current);

        var icon = SilkPaneKit.Icon(SilkIcon.Refresh);
        var refreshWidth = SilkPaneKit.ButtonWidth(RefreshText, icon, 16f, 13f, 14f, 8f);
        var exportWidth = SilkPaneKit.ButtonWidth(ExportText, SilkPaneKit.Icon(SilkIcon.Export), 16f, 13f, 14f, 8f);
        var holdWidth = SilkPaneKit.ButtonWidth(HoldText, SilkPaneKit.Icon(SilkIcon.Trash), 16f, 13f, 14f, 8f);
        var optWidth = 140f * s;
        var pageWidth = MathF.Max(74f * s, SilkText.Width(pageText, 12f, SilkWeight.SemiBold, 0f, true) + (16f * s));
        var pagerWidth = (4f * 28f * s) + (4f * 4f * s) + pageWidth;
        var countWidth = MathF.Max(0f, (right - left) - refreshWidth - exportWidth - holdWidth - optWidth - pagerWidth - (5f * gap));

        var x = left;
        SilkText.DrawInBox(new Vector2(x, top), new Vector2(x + countWidth, top + height), countText, 12f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.Start, 0f, false, 0f);
        x += countWidth + gap;

        if (SilkPaneKit.Button("refresh", new Vector2(x, top), new Vector2(x + refreshWidth, top + height), RefreshText, icon, true, 16f, 13f, 14f, 8f, 10f, null, 0f, 1f, RefreshSpin()))
        {
            refreshAt = SilkUi.Time;

            if (viewLogger?.PersistLogs == true)
                viewLogger.LoadEntriesFromDatabase(true);
        }

        x += refreshWidth + gap;

        if (SilkPaneKit.Button("export", new Vector2(x, top), new Vector2(x + exportWidth, top + height), ExportText, ExportTray, true, 16f, 13f, 14f, 8f, 10f, null, 0f, 1f, 0f, ExportArrow, ExportLift()))
        {
            exportAt = SilkUi.Time;
            DebugLogExporter.Export();
        }

        if (ImGui.IsItemHovered())
            SilkTooltip.Hover(ExportTip, new Vector2(x, top), new Vector2(x + exportWidth, top + height));

        x += exportWidth + gap;
        DrawHold(new Vector2(x, top), new Vector2(x + holdWidth, top + height));
        x += holdWidth + gap;

        DrawPageSizes(current, new Vector2(x, top), new Vector2(x + optWidth, top + height));
        x += optWidth + gap;

        DrawPager(current, new Vector2(x, top + s), pageWidth);
    }

    private void DrawHold(Vector2 min, Vector2 max)
    {
        var logger = viewLogger;
        var persisting = logger?.PersistLogs == true;
        var allowed = logger != null && (persisting ? logger.AllowUserClearDatabase : logger.AllowUserClearInMemory);
        var now = SilkUi.Time;
        var fill = holdProgress;

        if (fill <= 0f && releaseFrom > 0f)
        {
            var t = (now - releaseAt) / 0.2f;

            if (t >= 1f || SilkUi.ReducedMotion)
                releaseFrom = 0f;
            else
                fill = releaseFrom * (1f - UiCubicBezier.Ease.Evaluate(t));
        }

        SilkPaneKit.Button("hold", min, max, HoldText, SilkPaneKit.Icon(SilkIcon.Trash), allowed, 16f, 13f, 14f, 8f, 10f, SilkPalette.BadText, fill);

        var held = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered();

        if (hovered)
            SilkTooltip.Hover(persisting ? HoldTipDatabase : HoldTipMemory, min, max);

        if (held && allowed && !holdLatched)
        {
            holdProgress += ImGui.GetIO().DeltaTime / 1.2f;

            if (holdProgress >= 1f)
            {
                if (persisting)
                    logger!.ClearDatabaseEntries();
                else
                    logger!.ClearEntries();

                ClearSelection();
                holdProgress = 0f;
                holdLatched = true;
                releaseFrom = 0f;
            }
        }
        else
        {
            if (holdProgress > 0f)
            {
                releaseFrom = holdProgress;
                releaseAt = now;
                holdProgress = 0f;
            }

            if (!held)
                holdLatched = false;
        }
    }

    private void DrawPageSizes(HistoryLogView current, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        var radius = 9f * s;

        SilkPaint.Fill(min, max, SilkPalette.Sunken, radius);
        SilkPaint.InsetRing(min, max, SilkPalette.Line2, radius, s);

        var innerMin = min + new Vector2(2f * s, 2f * s);
        var slot = ((max.X - min.X) - (4f * s)) / PageSizes.Length;
        var height = (max.Y - min.Y) - (4f * s);
        var index = Array.IndexOf(PageSizes, current.ItemsPerPage);

        if (index >= 0)
        {
            var pillX = SilkUi.Ease(AnimationKey, "per.x", index * slot, 0.4f);
            var pillMin = new Vector2(innerMin.X + pillX, innerMin.Y);
            var pillMax = pillMin + new Vector2(slot, height);
            SilkPaint.Fill(pillMin, pillMax, SilkPalette.IceWash16, 7f * s);
            SilkPaint.InsetRing(pillMin, pillMax, SilkPalette.IceRing30, 7f * s, s);
        }

        for (var i = 0; i < PageSizes.Length; i++)
        {
            var bMin = new Vector2(innerMin.X + (i * slot), innerMin.Y);
            var bMax = bMin + new Vector2(slot, height);

            ImGui.PushID(i);

            if (SilkPaneKit.Hit("per", bMin, bMax, out _, out _) && current.ItemsPerPage != PageSizes[i])
            {
                current.ItemsPerPage = PageSizes[i];
                tableScroll.Reset();
            }

            ImGui.PopID();

            SilkText.DrawInBox(bMin, bMax, PageSizeTexts[i], 11.5f, SilkWeight.SemiBold, i == index ? SilkPalette.Ink : SilkPalette.Ink3, UiAlign.Center);
        }
    }

    private void DrawPager(HistoryLogView current, Vector2 min, float pageWidth)
    {
        var s = SilkUi.Scale;
        var size = 28f * s;
        var gap = 4f * s;
        var page = current.Page;
        var pages = current.PageCount;
        var x = min.X;

        if (PagerButton("first", new Vector2(x, min.Y), size, SilkIcon.First, page > 1))
            SetPage(current, 1);

        x += size + gap;

        if (PagerButton("prev", new Vector2(x, min.Y), size, SilkIcon.Prev, page > 1))
            SetPage(current, page - 1);

        x += size + gap;
        SilkText.DrawInBox(new Vector2(x, min.Y), new Vector2(x + pageWidth, min.Y + size), pageText, 12f, SilkWeight.SemiBold, SilkPalette.Ink, UiAlign.Center, 0f, true);
        x += pageWidth + gap;

        if (PagerButton("next", new Vector2(x, min.Y), size, SilkIcon.Next, page < pages))
            SetPage(current, page + 1);

        x += size + gap;

        if (PagerButton("last", new Vector2(x, min.Y), size, SilkIcon.Last, page < pages))
            SetPage(current, pages);
    }

    private void SetPage(HistoryLogView current, int page)
    {
        current.Page = page;
        tableScroll.Reset();
    }

    private static bool PagerButton(string id, Vector2 min, float size, SilkIcon icon, bool enabled)
    {
        var s = SilkUi.Scale;
        var max = min + new Vector2(size, size);
        var clicked = SilkPaneKit.Hit(id, min, max, out var hovered, out _) && enabled;
        var alpha = enabled ? 1f : 0.3f;
        var active = enabled && hovered;

        if (active)
            SilkPaint.Fill(min, max, HoverBg, 7f * s);

        SilkPaint.InsetRing(min, max, SilkPalette.Fade(SilkPalette.Line, alpha), 7f * s, s);

        var glyph = 12f * s;
        SilkIcons.Draw(icon, min + new Vector2((size - glyph) * 0.5f, (size - glyph) * 0.5f), glyph,
            SilkPalette.Fade(active ? SilkPalette.Ink : SilkPalette.Ink2, alpha * alpha));

        return clicked;
    }

    private void UpdateCategoryLabel(HistoryLogView current)
    {
        var selectedCategories = current.SelectedCategories;
        var count = selectedCategories.Count;
        string? first = null;

        if (count == 1)
        {
            foreach (var category in selectedCategories)
                first = category;
        }

        if (count == categoryLabelCount && ReferenceEquals(first, categoryLabelFirst) && categoryLabelRevision == NoireLanguages.Revision)
            return;

        categoryLabelRevision = NoireLanguages.Revision;
        categoryLabelCount = count;
        categoryLabelFirst = first;
        categoryLabel = count switch
        {
            0 => AllCategories,
            1 => first!,
            _ => L.Count(L.CategoriesCount, count),
        };
    }

    private void UpdateTexts(HistoryLogView current)
    {
        var revision = current.Revision;
        var page = current.Page;
        var perPage = current.ItemsPerPage;
        var empty = NoLevels;

        if (revision == textRevision && page == textPage && perPage == textPerPage && empty == textEmpty
            && textLanguage == NoireLanguages.Revision)
            return;

        textLanguage = NoireLanguages.Revision;
        textRevision = revision;
        textPage = page;
        textPerPage = perPage;
        textEmpty = empty;

        var matching = empty ? 0 : current.Entries.Count;
        var total = current.TotalCount;
        var pages = empty ? 1 : current.PageCount;
        var start = current.PageStartIndex;
        var shown = empty ? 0 : current.PageEntries.Count;

        countText = matching > 0
            ? L.Count(L.ShowingEntries, matching).Replace("{first}", (start + 1).ToString()).Replace("{last}", (start + shown).ToString())
                .Replace("{total}", total.ToString())
            : L.Count(L.NoEntries, total);
        pageText = $"{(empty ? 1 : page)} / {pages}";
    }

    private void OnFiltersChanged()
    {
        ClearSelection();
        tableScroll.Reset();
    }

    private string PaneNumber(int value) => SilkPaneKit.Cached(numberTexts, value);
}
