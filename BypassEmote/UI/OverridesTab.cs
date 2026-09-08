using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI;

internal static class OverridesTab
{
    private const float IconSize = 18f;
    private const float RemoveButtonSize = 18f;

    private static uint _selectedSource;

    private static EmoteQuickAdd? _sourceAdd;
    private static EmoteQuickAdd? _targetAdd;

    private static NoireReorderableList<uint>? _targetList;
    private static uint _targetListBoundTo;

    private static readonly Dictionary<uint, UiImageSource> Icons = new();

    internal static void Draw()
    {
        var overrides = Configuration.EmoteOverrides;

        ImGui.TextColoredWrapped(NoireTheme.Current.Resolve(ThemeColor.Info),
            "This tab allows you to make sure a locked emote will always use one or multiple specific target emotes.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Service.Catalog is not { Ready: true })
            ImGui.TextDisabled("The emote catalog is still building.");

        var listWidth = MathF.Max(NoireUI.Scaled(150f), ImGui.GetContentRegionAvail().X * 0.36f);

        using (ImRaii.Group())
            DrawSources(overrides, listWidth);

        ImGui.SameLine();

        using (ImRaii.Group())
            DrawTargets(overrides);
    }

    private static void DrawSources(List<EmoteOverride> overrides, float width)
    {
        var height = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing();

        using (var child = ImRaii.Child("##BypassEmoteOverrideSources", new Vector2(width, height), true))
        {
            if (child)
                DrawSourceRows(overrides);
        }

        var picker = _sourceAdd ??= new EmoteQuickAdd("BypassEmoteOverrideSourceAdd", "Override an emote...")
        {
            Marked = rowId => Configuration.EmoteOverrides.Any(entry => entry.SourceEmote == rowId),
            MarkedColor = NoireTheme.Current.Resolve(ThemeColor.Accent),
            MarkedNote = "(overridden)",
        };

        if (picker.Draw(width) is not { } picked)
            return;

        if (overrides.FirstOrDefault(entry => entry.SourceEmote == picked) == null)
            overrides.Add(new EmoteOverride { SourceEmote = picked });

        _selectedSource = picked;
    }

    private static void DrawSourceRows(List<EmoteOverride> overrides)
    {
        if (overrides.Count == 0)
        {
            ImGui.TextDisabled("No override yet.");
            return;
        }

        EmoteOverride? removing = null;

        foreach (var entry in overrides)
        {
            var top = ImGui.GetCursorPosY();

            if (RemoveButton($"##BypassEmoteOverrideDrop{entry.SourceEmote}", "Remove this override"))
                removing = entry;

            ImGui.SameLine(0f, NoireUI.Scaled(4f));
            ImGui.SetCursorPosY(top);

            DrawIcon(entry.SourceEmote);

            var label = SwapOrchestrator.NameOf(entry.SourceEmote);

            if (entry.Targets.Count == 0)
                label += "  (empty)";

            if (ImGui.Selectable($"{label}##BypassEmoteOverrideSource{entry.SourceEmote}",
                _selectedSource == entry.SourceEmote))
            {
                _selectedSource = entry.SourceEmote;
            }
        }

        if (removing == null)
            return;

        overrides.Remove(removing);

        if (_selectedSource == removing.SourceEmote)
            _selectedSource = 0;
    }

    private static void DrawTargets(List<EmoteOverride> overrides)
    {
        var configured = overrides.FirstOrDefault(entry => entry.SourceEmote == _selectedSource);
        var height = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing();

        using (var child = ImRaii.Child("##BypassEmoteOverrideTargets", new Vector2(0f, height), true))
        {
            if (child)
            {
                if (configured == null)
                    ImGui.TextDisabled("Pick an emote on the left.");
                else
                    DrawTargetPane(configured);
            }
        }

        if (configured == null)
            return;

        var picker = _targetAdd ??= new EmoteQuickAdd("BypassEmoteOverrideTargetAdd", "Add a target emote...")
        {
            MarkedColor = NoireTheme.Current.Resolve(ThemeColor.Accent),
            MarkedNote = "(already a target)",
        };

        picker.Marked = rowId => configured.Targets.Contains(rowId);

        if (picker.Draw(ImGui.GetContentRegionAvail().X) is not { } picked)
            return;

        if (!configured.Targets.Remove(picked))
            configured.Targets.Add(picked);
    }

    private static void DrawTargetPane(EmoteOverride configured)
    {
        DrawLimitedToggle(configured);

        ImGui.Separator();

        BoundList(configured).Draw();
    }

    private static void DrawLimitedToggle(EmoteOverride configured)
    {
        var limited = configured.LimitedToTargets;

        if (ImGui.Checkbox("Limited to selected targets only", ref limited))
            configured.LimitedToTargets = limited;

        ImGui.SameLine();

        SettingsLayout.Marker("On, this emote lands on one of these targets or it does not play at all."
            + "\nOff, the usual configuration takes over whenever none of them can be used as targets during a swap.");
    }

    private static NoireReorderableList<uint> BoundList(EmoteOverride configured)
    {
        _targetList ??= new NoireReorderableList<uint>("BypassEmoteOverrideTargetList")
        {
            AllowDelete = true,
            RowHeight = NoireUI.Scaled(IconSize + 4f),
            EmptyText = "No target yet.",
            Label = SwapOrchestrator.NameOf,
            Renderer = DrawTargetRow,
        };

        if (_targetListBoundTo != configured.SourceEmote || !ReferenceEquals(_targetList.Items, configured.Targets))
        {
            _targetList.Items = configured.Targets;
            _targetListBoundTo = configured.SourceEmote;
        }

        return _targetList;
    }

    private static void DrawTargetRow(UiReorderRowDraw<uint> row)
    {
        var origin = ImGui.GetCursorScreenPos();
        var lines = AdviceFor(row.Item, withMods: false);
        var severity = lines.Count == 0 ? null : (SwapAdvice.Severity?)SwapAdvice.WorstOf(lines);

        DrawIcon(row.Item);

        using (ImRaii.PushColor(ImGuiCol.Text, severity is { } worst ? SwapAdviceView.ColorFor(worst) : default,
            severity != null))
            ImGui.TextUnformatted(row.Label);

        if (lines.Count == 0 || !ImGui.IsWindowHovered() || !ImGui.IsMouseHoveringRect(origin, origin + row.Size))
            return;

        DrawAdviceTooltip(row.Item);
    }

    private static void DrawAdviceTooltip(uint targetRowId)
        => SwapAdviceView.DrawTooltip(SwapOrchestrator.NameOf(targetRowId), AdviceFor(targetRowId, withMods: true));

    private static IReadOnlyList<SwapAdvice.Line> AdviceFor(uint targetRowId, bool withMods)
    {
        if (Service.Catalog is not { Ready: true } catalog
            || catalog.Get(_selectedSource) is not { } source
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

        return SwapAdvice.ForOverride(source, SwapOrchestrator.NameOf(source.RowId), target,
            SwapOrchestrator.NameOf(targetRowId), facts);
    }

    private static bool RemoveButton(string id, string tooltip)
    {
        var size = NoireUI.Scaled(RemoveButtonSize);
        bool pressed;

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero))
            pressed = ImGui.Button(FontAwesomeIcon.Times.ToIconString() + id, new Vector2(size, size));

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(tooltip);
        }

        return pressed;
    }

    private static void DrawIcon(uint emoteRowId)
    {
        var size = NoireUI.Scaled(IconSize);
        var top = ImGui.GetCursorPosY();

        if (IconFor(emoteRowId)?.GetWrap() is { } wrap)
            ImGui.Image(wrap.Handle, new Vector2(size, size));
        else
            ImGui.Dummy(new Vector2(size, size));

        ImGui.SameLine(0f, NoireUI.Scaled(6f));
        ImGui.SetCursorPosY(top + MathF.Max(0f, (size - ImGui.GetTextLineHeight()) * 0.5f));
    }

    private static UiImageSource? IconFor(uint emoteRowId)
    {
        if (Icons.TryGetValue(emoteRowId, out var cached))
            return cached;

        if (EmoteHelper.GetEmoteById(emoteRowId) is not { } emote)
            return null;

        var source = UiImageSource.FromGameIcon(CommonHelper.GetEmoteIcon(emote));
        Icons[emoteRowId] = source;
        return source;
    }
}
