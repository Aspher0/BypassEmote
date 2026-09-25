using BypassEmote.EmoteSwap;
using BypassEmote.Localization;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace BypassEmote.UI.Silk.Settings;

internal sealed partial class SilkSettingsPainter
{
    private static string OverridesInfo => L.SilkOverridesIntro.Text;

    private static string LimitedName => L.SilkLimited.Text;

    private static string LimitedHelp => L.LimitedHelp.Text;

    private static string SourceAdderLabel => L.SilkOverrideEmote.Text;
    private static string TargetAdderLabel => L.SilkAddTarget.Text;
    private static string LockedColumnTitle => L.SilkLockedEmote.Text;
    private static string EmptyColumnTitle => L.SilkPickLeft.Text;
    private static string RemoveOverrideTooltip => L.RemoveOverride.Text;
    private static string RemoveTargetTooltip => L.SilkRemove.Text;

    private static readonly StringBuilder TooltipBuilder = new();

    private readonly SilkScrollArea sourceScroll = new();
    private readonly SilkScrollArea targetScroll = new();
    private readonly SilkEmotePicker sourcePicker = new("##silkoverridesource", L.SearchEmotes);
    private readonly SilkEmotePicker targetPicker = new("##silkoverridetarget", L.SearchEmotes);
    private readonly Dictionary<uint, SwapAdvice.Severity?> severities = new();
    private readonly Dictionary<uint, string> tooltips = new();
    private readonly List<string> countLabels = new();

    private uint removeHeld;
    private uint selectedSource;
    private string? playsThroughTitle;
    private uint playsThroughFor;
    private int playsThroughRevision = -1;
    private int severitiesVersion = -1;
    private uint severitiesFor;
    private int dragIndex = -1;
    private float dragOffset;
    private bool dragging;

    private static List<EmoteOverride> Overrides => Configuration.EmoteOverrides;

    private bool OverlayOpen => sourcePicker.IsOpen || targetPicker.IsOpen;

    private void ShowOverrideFor(uint sourceRowId)
    {
        if (sourceRowId == 0)
            return;

        var overrides = Overrides;

        if (Find(overrides, sourceRowId) == null)
        {
            overrides.Add(new EmoteOverride { SourceEmote = sourceRowId });
            Configuration.Instance.RequestSave();
        }

        selectedSource = sourceRowId;
    }

    private void CloseOverlays()
    {
        dragging = false;
        dragIndex = -1;
    }

    private static EmoteOverride? Find(List<EmoteOverride> overrides, uint sourceRowId)
    {
        foreach (var entry in overrides)
        {
            if (entry.SourceEmote == sourceRowId)
                return entry;
        }

        return null;
    }

    private float DrawOverrides(Vector2 origin, float width, float available)
    {
        var scale = SilkUi.Scale;
        var overrides = Overrides;
        var y = origin.Y;
        var infoHeight = SilkText.ParagraphHeight(OverridesInfo, 13f, SilkWeight.Regular, width - (28f * scale), 1.4f) + (24f * scale);

        y += 14f * scale;
        SilkPaint.Fill(new Vector2(origin.X, y), new Vector2(origin.X + width, y + infoHeight), SilkPalette.InfoBg, 12f * scale);
        SilkPaint.InsetRing(new Vector2(origin.X, y), new Vector2(origin.X + width, y + infoHeight), SilkPalette.InfoRing, 12f * scale, scale);
        SilkText.DrawParagraph(new Vector2(origin.X + (14f * scale), y + (12f * scale)), OverridesInfo, 13f, SilkWeight.Regular, SilkPalette.Ink2, width - (28f * scale), 1.4f);
        y += infoHeight;

        y += 14f * scale;

        var columnsHeight = MathF.Max(260f * scale, available - (y - origin.Y) - (4f * scale));
        var gap = 12f * scale;
        var leftWidth = MathF.Min(MathF.Max(200f * scale, width * 0.32f), width - gap - (200f * scale));
        var leftMin = new Vector2(origin.X, y);
        var leftMax = new Vector2(origin.X + leftWidth, y + columnsHeight);
        var rightMin = new Vector2(leftMax.X + gap, y);
        var rightMax = new Vector2(origin.X + width, y + columnsHeight);

        var configured = Find(overrides, selectedSource);

        DrawSourceColumn(leftMin, leftMax, overrides);
        DrawTargetColumn(rightMin, rightMax, configured);

        return columnsHeight + (y - origin.Y);
    }

    private void DrawSourceColumn(Vector2 min, Vector2 max, List<EmoteOverride> overrides)
    {
        var scale = SilkUi.Scale;
        var pad = 6f * scale;

        SilkControls.Card(min, max);

        var headerHeight = (16f * scale) + SilkText.NaturalLine(10.5f, SilkWeight.Bold);
        var headerLeft = min.X + (8f * scale) + pad;
        SilkText.DrawInBox(new Vector2(headerLeft, min.Y + pad), new Vector2(max.X - pad - (8f * scale), min.Y + pad + headerHeight),
            SilkFonts.Upper(LockedColumnTitle), 10.5f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Start, 1f);

        var adderHeight = 34f * scale;
        var listMin = new Vector2(min.X + pad, min.Y + pad + headerHeight);
        var listMax = new Vector2(max.X - pad, max.Y - pad - adderHeight - (6f * scale));
        var rowHeight = 40f * scale;
        var offset = sourceScroll.Begin(listMin, listMax, !OverlayOpen);

        ImGui.PushClipRect(listMin, listMax, true);

        EmoteOverride? removing = null;

        try
        {
            var y = listMin.Y - offset;

            for (var i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                var rowMin = new Vector2(listMin.X, y);
                var rowMax = new Vector2(listMax.X, y + rowHeight);
                var selected = entry.SourceEmote == selectedSource;

                ImGui.PushID((int)entry.SourceEmote);

                var hovered = SilkSettingsKit.Hovered(rowMin, rowMax) || removeHeld == entry.SourceEmote;
                var right = rowMax.X - (8f * scale);
                var removeSize = 18f * scale;
                var removeMin = new Vector2(right - removeSize, MathF.Round(((rowMin.Y + rowMax.Y) * 0.5f) - (removeSize * 0.5f)));
                var removeMax = removeMin + new Vector2(removeSize, removeSize);
                var removeHovered = false;

                if (hovered)
                {
                    if (SilkSettingsKit.Hit("remove", removeMin, removeMax, out removeHovered))
                        removing = entry;

                    if (ImGui.IsItemActive())
                        removeHeld = entry.SourceEmote;
                    else if (removeHeld == entry.SourceEmote)
                        removeHeld = 0;
                }

                if (SilkSettingsKit.Hit("row", rowMin, rowMax, out _) && !removeHovered)
                    selectedSource = entry.SourceEmote;

                var radius = 9f * scale;

                if (selected)
                {
                    SilkPaint.Fill(rowMin, rowMax, SilkPalette.IceWash10, radius);
                    SilkPaint.InsetRing(rowMin, rowMax, SilkPalette.IceRing30, radius, scale);
                }
                else if (hovered)
                {
                    SilkPaint.Fill(rowMin, rowMax, SilkPalette.Rgb(191, 211, 236, 0.05f), radius);
                }

                var icon = 26f * scale;
                var iconMin = new Vector2(rowMin.X + (8f * scale), MathF.Round(((rowMin.Y + rowMax.Y) * 0.5f) - (icon * 0.5f)));
                var iconId = SilkEmoteIndex.Get(entry.SourceEmote)?.IconId ?? 0u;
                SilkSettingsKit.Glyph(iconMin, iconMin + new Vector2(icon, icon), iconId, 7f * scale);

                var countText = CountLabel(entry.Targets.Count);
                var countWidth = SilkText.Width(countText, 10.5f, SilkWeight.Medium, 0f, true);

                if (hovered)
                {
                    SilkIcons.DrawCentered(SilkIcon.Close, (removeMin + removeMax) * 0.5f, 10f * scale, removeHovered ? SilkPalette.Bad : SilkPalette.Ink3);

                    if (removeHovered)
                        SilkTooltip.Hover(RemoveOverrideTooltip, removeMin, removeMax);

                    right -= removeSize + (6f * scale);
                }
                else
                {
                    SilkText.DrawInBox(new Vector2(right - countWidth, rowMin.Y), new Vector2(right, rowMax.Y), countText, 10.5f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.Start, 0f, true);
                    right -= countWidth + (6f * scale);
                }

                var name = SwapOrchestrator.NameOf(entry.SourceEmote);
                SilkText.DrawInBox(new Vector2(iconMin.X + icon + (10f * scale), rowMin.Y), new Vector2(right, rowMax.Y), name, 13f, SilkWeight.Regular,
                    selected || hovered ? SilkPalette.Ink : SilkPalette.Ink2, UiAlign.Start, 0f, false, 1.4f);

                ImGui.PopID();
                y += rowHeight;
            }

            sourceScroll.End(listMin, listMax, y + offset - listMin.Y);
        }
        finally
        {
            ImGui.PopClipRect();
        }

        if (removing != null)
        {
            overrides.Remove(removing);
            Configuration.Instance.RequestSave();

            if (selectedSource == removing.SourceEmote)
                selectedSource = 0;
        }

        var adderMin = new Vector2(min.X + pad, max.Y - pad - adderHeight);
        var adderMax = new Vector2(max.X - pad, max.Y - pad);

        if (Adder(SourceAdderLabel, adderMin, adderMax, "##silksourceadder"))
        {
            sourcePicker.Filter = SilkEmoteIndex.Swappable;
            sourcePicker.Marked = rowId => Find(Overrides, rowId) != null;
            sourcePicker.MarkedNote = L.OverriddenMark.Text;
            sourcePicker.Open();
        }

        if (sourcePicker.Draw(adderMin, adderMax, adderMax.X - adderMin.X, true) is { } picked)
        {
            ShowOverrideFor(picked);
            severitiesVersion = -1;
        }
    }

    private void DrawTargetColumn(Vector2 min, Vector2 max, EmoteOverride? configured)
    {
        var scale = SilkUi.Scale;
        var pad = 6f * scale;

        SilkControls.Card(min, max);

        var headerHeight = (16f * scale) + SilkText.NaturalLine(10.5f, SilkWeight.Bold);
        var title = configured == null ? EmptyColumnTitle : PlaysThroughTitle(configured.SourceEmote);
        var titleX = min.X + (8f * scale) + pad;
        var limitedRoom = SilkText.Width(L.SilkLimited.Source, 12.5f, SilkWeight.Medium);
        var checkWidth = SilkControls.CheckboxWidth(LimitedName, limitedRoom);
        var helpSize = SilkControls.HelpSize * scale;
        var checkPos = new Vector2(max.X - pad - (8f * scale) - helpSize - (6f * scale) - checkWidth, MathF.Round(min.Y + pad + ((headerHeight - (16f * scale)) * 0.5f)));

        var titleRoom = MathF.Max(1f, (configured == null ? max.X - pad - (8f * scale) : checkPos.X - (10f * scale)) - titleX);
        SilkText.DrawInBox(new Vector2(titleX, min.Y + pad), new Vector2(titleX + titleRoom, min.Y + pad + headerHeight),
            SilkFonts.Upper(title), 10.5f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Start, 1f);

        if (configured != null)
        {
            var limited = configured.LimitedToTargets;

            if (SilkControls.Checkbox("##silklimited", LimitedName, ref limited, checkPos, limitedRoom))
            {
                configured.LimitedToTargets = limited;
                Configuration.Instance.RequestSave();
            }

            SilkControls.HelpMark("##silklimitedhelp", LimitedHelp, new Vector2(max.X - pad - (8f * scale) - (helpSize * 0.5f), min.Y + pad + (headerHeight * 0.5f)));
        }

        var adderHeight = 34f * scale;
        var listMin = new Vector2(min.X + pad, min.Y + pad + headerHeight);
        var listMax = new Vector2(max.X - pad, max.Y - pad - adderHeight - (6f * scale));

        if (configured == null)
        {
            targetScroll.Reset();
            return;
        }

        RefreshSeverities(configured);

        var rowHeight = 44f * scale;
        var step = rowHeight + (5f * scale);
        var offset = targetScroll.Begin(listMin, listMax, !OverlayOpen);
        var targets = configured.Targets;
        var removeIndex = -1;

        ImGui.PushClipRect(listMin, listMax, true);

        try
        {
            var mouse = ImGui.GetMousePos();
            var y = listMin.Y - offset;

            for (var i = 0; i < targets.Count; i++)
            {
                var rowMin = new Vector2(listMin.X, y + (i * step));
                var rowMax = new Vector2(listMax.X, rowMin.Y + rowHeight);
                var dragged = dragging && dragIndex == i;

                if (dragged)
                {
                    var wanted = Math.Clamp((int)MathF.Round((mouse.Y - dragOffset - (y + (0.5f * scale))) / step), 0, targets.Count - 1);

                    if (wanted != i)
                    {
                        var moved = targets[i];
                        targets.RemoveAt(i);
                        targets.Insert(wanted, moved);
                        dragIndex = wanted;
                        Configuration.Instance.RequestSave();
                    }
                }

                if (DrawTargetRow(configured, targets[i], i, rowMin, rowMax, dragged))
                    removeIndex = i;
            }

            if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                dragging = false;
                dragIndex = -1;
            }

            targetScroll.End(listMin, listMax, (targets.Count * step) + (6f * scale));
        }
        finally
        {
            ImGui.PopClipRect();
        }

        if (removeIndex >= 0)
        {
            targets.RemoveAt(removeIndex);
            Configuration.Instance.RequestSave();
            severitiesVersion = -1;
        }

        var adderMin = new Vector2(min.X + pad, max.Y - pad - adderHeight);
        var adderMax = new Vector2(max.X - pad, max.Y - pad);

        if (Adder(TargetAdderLabel, adderMin, adderMax, "##silktargetadder"))
        {
            targetPicker.Filter = SilkEmoteIndex.ValidTarget;
            targetPicker.Marked = rowId => configured.Targets.Contains(rowId);
            targetPicker.MarkedNote = L.AlreadyTargetMark.Text;
            targetPicker.Open();
        }

        if (targetPicker.Draw(adderMin, adderMax, adderMax.X - adderMin.X, true) is { } picked)
        {
            if (!configured.Targets.Remove(picked))
                configured.Targets.Add(picked);

            Configuration.Instance.RequestSave();
            severitiesVersion = -1;
        }
    }

    private bool DrawTargetRow(EmoteOverride configured, uint targetRowId, int index, Vector2 min, Vector2 max, bool dragged)
    {
        var scale = SilkUi.Scale;
        var radius = 9f * scale;

        SilkPaint.Fill(min, max, SilkPalette.Rgb(191, 211, 236, dragged ? 0.07f : 0.03f), radius);
        SilkPaint.InsetRing(min, max, dragged ? SilkPalette.IceRing30 : SilkPalette.Line, radius, scale);

        var gripMin = new Vector2(min.X + (6f * scale), min.Y);
        var gripMax = new Vector2(gripMin.X + (16f * scale), max.Y);

        ImGui.PushID((int)targetRowId);
        ImGui.SetCursorScreenPos(gripMin);
        ImGui.InvisibleButton("grip", gripMax - gripMin);
        var gripHovered = ImGui.IsItemHovered();

        if (ImGui.IsItemActive() && !dragging)
        {
            dragging = true;
            dragIndex = index;
            dragOffset = ImGui.GetMousePos().Y - min.Y;
        }

        if (gripHovered || (dragging && dragIndex == index))
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);

        SilkText.DrawInBox(gripMin, gripMax, "::", 13f, SilkWeight.Regular, SilkPalette.Ink3, UiAlign.Center, -2f, false, 1.4f);

        var rank = 20f * scale;
        var rankMin = new Vector2(gripMax.X + (10f * scale), MathF.Round(((min.Y + max.Y) * 0.5f) - (rank * 0.5f)));
        var rankMax = rankMin + new Vector2(rank, rank);
        SilkPaint.Fill(rankMin, rankMax, SilkPalette.Rgb(191, 211, 236, 0.14f), 6f * scale);
        SilkText.DrawInBox(rankMin, rankMax, CountLabel(index + 1), 10.5f, SilkWeight.SemiBold, SilkPalette.Ice, UiAlign.Center, 0f, true);

        var icon = 28f * scale;
        var iconMin = new Vector2(rankMax.X + (10f * scale), MathF.Round(((min.Y + max.Y) * 0.5f) - (icon * 0.5f)));
        var entry = SilkEmoteIndex.Get(targetRowId);
        SilkSettingsKit.Glyph(iconMin, iconMin + new Vector2(icon, icon), entry?.IconId ?? 0u, 7f * scale);

        var removeSize = 18f * scale;
        var removeMin = new Vector2(max.X - (10f * scale) - removeSize, MathF.Round(((min.Y + max.Y) * 0.5f) - (removeSize * 0.5f)));
        var removeMax = removeMin + new Vector2(removeSize, removeSize);
        var remove = SilkSettingsKit.Hit("remove", removeMin, removeMax, out var removeHovered);

        SilkIcons.DrawCentered(SilkIcon.Close, (removeMin + removeMax) * 0.5f, 11f * scale, removeHovered ? SilkPalette.Bad : SilkPalette.Ink3);

        if (removeHovered)
            SilkTooltip.Hover(RemoveTargetTooltip, removeMin, removeMax);

        var textLeft = iconMin.X + icon + (10f * scale);
        var textRight = removeMin.X - (10f * scale);
        var nameLine = SilkText.LineBox(13f, 1.4f);
        var codeLine = SilkText.NaturalLine(11f, SilkWeight.Regular, true);
        var block = nameLine + (2f * scale) + codeLine;
        var top = MathF.Round(((min.Y + max.Y) * 0.5f) - (block * 0.5f));
        var severity = severities.TryGetValue(targetRowId, out var known) ? known : null;
        var color = severity switch
        {
            SwapAdvice.Severity.Error => SilkPalette.Bad,
            SwapAdvice.Severity.Warning => SilkPalette.Warn,
            SwapAdvice.Severity.Note => SilkPalette.Ink2,
            _ => SilkPalette.Ink,
        };

        SilkText.Draw(new Vector2(textLeft, SilkText.GlyphTop(top, nameLine, 13f, SilkWeight.Bold, 1.4f)), SwapOrchestrator.NameOf(targetRowId), 13f, SilkWeight.Bold, color, 0f, false, MathF.Max(1f, textRight - textLeft));

        var command = entry != null && entry.Commands.Length > 0 ? entry.Commands[0] : string.Empty;

        if (command.Length > 0)
            SilkText.Draw(new Vector2(textLeft, top + nameLine + (2f * scale)), command, 11f, SilkWeight.Regular, SilkPalette.Ink3, 0f, true, MathF.Max(1f, textRight - textLeft));

        if (!removeHovered && SilkSettingsKit.Hovered(min, max) && Tooltip(configured, targetRowId) is { Length: > 0 } tooltip)
            SilkTooltip.Hover(tooltip, min, max);

        ImGui.PopID();
        return remove;
    }

    private bool Adder(string label, Vector2 min, Vector2 max, string id)
    {
        var scale = SilkUi.Scale;
        var clicked = SilkSettingsKit.Hit(id, min, max, out var hovered);

        SilkPaint.InsetRing(min, max, hovered ? SilkPalette.IceRing30 : SilkPalette.Line2, 9f * scale, scale);
        SilkText.DrawInBox(new Vector2(min.X + (10f * scale), min.Y), max, label, 13f, SilkWeight.Regular, hovered ? SilkPalette.Ink2 : SilkPalette.Ink3, UiAlign.Start, 0f, false, 1.4f);

        return clicked;
    }

    private string PlaysThroughTitle(uint sourceRowId)
    {
        if (playsThroughTitle != null && playsThroughFor == sourceRowId && playsThroughRevision == NoireLanguages.Revision)
            return playsThroughTitle;

        playsThroughFor = sourceRowId;
        playsThroughRevision = NoireLanguages.Revision;
        playsThroughTitle = L.SilkPlaysThrough.With("emote", SwapOrchestrator.NameOf(sourceRowId));
        return playsThroughTitle;
    }

    private string CountLabel(int count)
    {
        while (countLabels.Count <= count)
            countLabels.Add(countLabels.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return countLabels[count];
    }

    private void RefreshSeverities(EmoteOverride configured)
    {
        var version = configured.Targets.Count + (configured.LimitedToTargets ? 1 << 16 : 0);

        if (severitiesFor == configured.SourceEmote && severitiesVersion == version)
            return;

        severitiesFor = configured.SourceEmote;
        severitiesVersion = version;
        severities.Clear();
        tooltips.Clear();

        foreach (var target in configured.Targets)
        {
            var lines = AdviceFor(configured.SourceEmote, target, false);
            severities[target] = lines.Count == 0 ? null : SwapAdvice.WorstOf(lines);
        }
    }

    private string? Tooltip(EmoteOverride configured, uint targetRowId)
    {
        if (tooltips.TryGetValue(targetRowId, out var cached))
            return cached;

        var lines = AdviceFor(configured.SourceEmote, targetRowId, true);

        if (lines.Count == 0)
        {
            tooltips[targetRowId] = string.Empty;
            return null;
        }

        TooltipBuilder.Clear();
        TooltipBuilder.Append(SwapOrchestrator.NameOf(targetRowId));

        foreach (var line in lines)
            TooltipBuilder.Append('\n').Append("- ").Append(line.Text);

        var text = TooltipBuilder.ToString();
        tooltips[targetRowId] = text;
        return text;
    }

    private static IReadOnlyList<SwapAdvice.Line> AdviceFor(uint sourceRowId, uint targetRowId, bool withMods)
    {
        if (Service.Catalog is not { Ready: true } catalog
            || catalog.Get(sourceRowId) is not { } source
            || catalog.Get(targetRowId) is not { } target)
        {
            return [];
        }

        string? changedBy = null;

        if (withMods && Service.Orchestrator is { } orchestrator && NoireService.ObjectTable.LocalPlayer is { } player)
            changedBy = orchestrator.ModServingAnimation(target, SwapOrchestrator.SkeletonFor(player));

        var facts = new SwapAdvice.Facts(
            EmoteHelper.IsEmoteUnlocked(targetRowId),
            Configuration.BlockedTargetEmotesEmoteSwap.Contains(targetRowId),
            changedBy);

        return SwapAdvice.ForOverride(source, SwapOrchestrator.NameOf(source.RowId), target, SwapOrchestrator.NameOf(targetRowId), facts);
    }
}
