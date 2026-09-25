using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI.Classic;

internal static class OverridesTab
{
    private const float IconSize = 18f;
    private const float RemoveButtonSize = 18f;

    private static uint _selectedSource;

    private static readonly IdLabel LimitedLabel = new(L.ClassicLimited, "###BypassEmoteOverrideLimited");

    private static EmoteQuickAdd? _sourceAdd;
    private static EmoteQuickAdd? _targetAdd;

    private static NoireReorderableList<uint>? _targetList;
    private static uint _targetListBoundTo;

    private static readonly Dictionary<uint, UiImageSource> Icons = new();

    private const double AdviceLifetime = 0.5;

    private static readonly string RemoveIcon = FontAwesomeIcon.Times.ToIconString();
    private static readonly Dictionary<uint, SourceRowTexts> SourceRows = new();
    private static readonly Dictionary<uint, string> Names = new();
    private static int namesRevision = -1;
    private static readonly Dictionary<(uint Source, uint Target, bool WithMods), (double At, IReadOnlyList<SwapAdvice.Line> Lines)> Advice = new();
    private static EmoteOverride? _markedFor;

    private sealed class SourceRowTexts
    {
        public string RemoveId = string.Empty;
        public string Label = string.Empty;
        public bool Empty;
        public int Revision = -1;
    }

    internal static void ShowFor(uint sourceRowId)
    {
        if (sourceRowId == 0)
            return;

        var overrides = Configuration.EmoteOverrides;

        if (Find(overrides, sourceRowId) == null)
            overrides.Add(new EmoteOverride { SourceEmote = sourceRowId });

        _selectedSource = sourceRowId;
    }

    private static EmoteOverride? Find(List<EmoteOverride> overrides, uint sourceRowId)
    {
        for (var i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].SourceEmote == sourceRowId)
                return overrides[i];
        }

        return null;
    }

    private static string CachedName(uint emoteRowId)
    {
        if (namesRevision != NoireLanguages.Revision)
        {
            namesRevision = NoireLanguages.Revision;
            Names.Clear();
            SourceRows.Clear();
        }

        if (!Names.TryGetValue(emoteRowId, out var name))
            Names[emoteRowId] = name = SwapOrchestrator.NameOf(emoteRowId);

        return name;
    }

    private static SourceRowTexts TextsFor(EmoteOverride entry)
    {
        var name = CachedName(entry.SourceEmote);

        if (!SourceRows.TryGetValue(entry.SourceEmote, out var texts))
        {
            texts = new SourceRowTexts { RemoveId = RemoveIcon + "##BypassEmoteOverrideDrop" + entry.SourceEmote };
            SourceRows[entry.SourceEmote] = texts;
        }

        var empty = entry.Targets.Count == 0;

        if (texts.Revision != NoireLanguages.Revision || texts.Empty != empty)
        {
            texts.Revision = NoireLanguages.Revision;
            texts.Empty = empty;
            texts.Label = (empty ? L.ClassicEmptyOverride.With("name", name) : name) + "##BypassEmoteOverrideSource" + entry.SourceEmote;
        }

        return texts;
    }

    internal static void Draw()
    {
        var overrides = Configuration.EmoteOverrides;

        ImGui.TextColoredWrapped(NoireTheme.Current.Resolve(ThemeColor.Info),
            L.ClassicOverridesIntro.Text);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Service.Catalog is not { Ready: true })
            ImGui.TextDisabled(L.CatalogBuilding.Text);

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

        var picker = _sourceAdd ??= new EmoteQuickAdd("BypassEmoteOverrideSourceAdd", L.ClassicOverrideEmote)
        {
            Marked = rowId => Configuration.EmoteOverrides.Any(entry => entry.SourceEmote == rowId),
            MarkedColor = NoireTheme.Current.Resolve(ThemeColor.Accent),
            MarkedNote = L.OverriddenMark,
        };

        if (picker.Draw(width) is not { } picked)
            return;

        if (Find(overrides, picked) == null)
            overrides.Add(new EmoteOverride { SourceEmote = picked });

        _selectedSource = picked;
    }

    private static void DrawSourceRows(List<EmoteOverride> overrides)
    {
        if (overrides.Count == 0)
        {
            ImGui.TextDisabled(L.ClassicNoOverride.Text);
            return;
        }

        EmoteOverride? removing = null;

        foreach (var entry in overrides)
        {
            var top = ImGui.GetCursorPosY();
            var texts = TextsFor(entry);

            if (RemoveButton(texts.RemoveId, L.RemoveOverride.Text))
                removing = entry;

            ImGui.SameLine(0f, NoireUI.Scaled(4f));
            ImGui.SetCursorPosY(top);

            DrawIcon(entry.SourceEmote);

            if (ImGui.Selectable(texts.Label, _selectedSource == entry.SourceEmote))
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
        var configured = Find(overrides, _selectedSource);
        var height = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing();

        using (var child = ImRaii.Child("##BypassEmoteOverrideTargets", new Vector2(0f, height), true))
        {
            if (child)
            {
                if (configured == null)
                    ImGui.TextDisabled(L.ClassicPickLeft.Text);
                else
                    DrawTargetPane(configured);
            }
        }

        if (configured == null)
            return;

        var picker = _targetAdd ??= new EmoteQuickAdd("BypassEmoteOverrideTargetAdd", L.ClassicAddTarget)
        {
            MarkedColor = NoireTheme.Current.Resolve(ThemeColor.Accent),
            MarkedNote = L.AlreadyTargetMark,
        };

        if (!ReferenceEquals(_markedFor, configured))
        {
            _markedFor = configured;
            picker.Marked = rowId => configured.Targets.Contains(rowId);
        }

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

        if (ImGui.Checkbox(LimitedLabel.Text, ref limited))
            configured.LimitedToTargets = limited;

        ImGui.SameLine();

        SettingsLayout.Marker(L.LimitedHelp.Text);
    }

    private static NoireReorderableList<uint> BoundList(EmoteOverride configured)
    {
        _targetList ??= new NoireReorderableList<uint>("BypassEmoteOverrideTargetList")
        {
            AllowDelete = true,
            RowHeight = NoireUI.Scaled(IconSize + 4f),
            EmptyText = L.ClassicNoTarget.Text,
            Label = CachedName,
            Renderer = DrawTargetRow,
        };

        _targetList.EmptyText = L.ClassicNoTarget.Text;

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
        => SwapAdviceView.DrawTooltip(CachedName(targetRowId), AdviceFor(targetRowId, withMods: true));

    private static IReadOnlyList<SwapAdvice.Line> AdviceFor(uint targetRowId, bool withMods)
    {
        var key = (_selectedSource, targetRowId, withMods);
        var now = ImGui.GetTime();

        if (Advice.TryGetValue(key, out var held) && now - held.At < AdviceLifetime)
            return held.Lines;

        var lines = ComputeAdvice(targetRowId, withMods);
        Advice[key] = (now, lines);
        return lines;
    }

    private static IReadOnlyList<SwapAdvice.Line> ComputeAdvice(uint targetRowId, bool withMods)
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

        return SwapAdvice.ForOverride(source, CachedName(source.RowId), target, CachedName(targetRowId), facts);
    }

    private static bool RemoveButton(string id, string tooltip)
    {
        var size = NoireUI.Scaled(RemoveButtonSize);
        bool pressed;

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero))
            pressed = ImGui.Button(id, new Vector2(size, size));

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
