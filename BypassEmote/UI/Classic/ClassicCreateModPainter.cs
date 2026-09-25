using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI.Classic;

internal sealed class ClassicCreateModPainter
{
    private static readonly IdLabel CreateLabel = new(L.Create, "###BypassEmoteCreateModCreate");

    private NoireExcelPicker<Emote>? _source;
    private NoireExcelPicker<Emote>? _target;

    private NoireMultiCombo<string>? _races;

    private string _modName = string.Empty;
    private bool _enableOnCreation = true;
    private bool _highestPriority = false;

    private (uint Source, uint Target)? _racesFilledFor;

    private string _status = string.Empty;
    private bool _statusIsGood;

    public void ShowFor(Emote emote)
    {
        Picker(ref _source, "BypassEmoteCreateModSource", L.PickEmoteToPlay.Text).Select(emote.RowId);

        Show();
    }

    public void Show()
    {
        _status = string.Empty;
        ForgetPaths();
    }

    private int _opening;

    private void TakeOpening(CreateModWindow window)
    {
        if (_opening == window.Opening)
            return;

        _opening = window.Opening;

        if (window.OpenedFor is { } emote)
            ShowFor(emote);
        else
            Show();
    }

    internal void Draw(CreateModWindow window)
    {
        TakeOpening(window);

        if (Service.Penumbra is not { Available: true })
        {
            ImGui.TextColored(NoireTheme.Current.Resolve(ThemeColor.Danger),
                Service.Penumbra?.UnavailableReason is { Length: > 0 } reason ? reason : L.PenumbraNotRunning.Text);
            return;
        }

        ImGui.TextWrapped(L.ClassicCreateModIntro.Text);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var names = SettingsLayout.NameColumn(L.EmoteToPlay.Source, L.PlayedOver.Source, L.RacesCovered.Source, L.ModName.Source, L.EnableOnCreation.Source, L.HighestPriority.Source);
        var controls = MathF.Max(NoireUI.Scaled(240f), ImGui.GetContentRegionAvail().X - names - NoireUI.Scaled(40f));

        RefillRacesWhenThePairChanges();

        using (var rows = SettingsLayout.Rows("##BypassEmoteCreateModRows", names, controls))
        {
            if (rows)
            {
                SettingsLayout.Name(L.EmoteToPlay.Text);
                DrawPicker(ref _source, "BypassEmoteCreateModSource", L.PickEmoteToPlay.Text, controls);
                SettingsLayout.Help(L.EmoteToPlayHelp.Text);

                SettingsLayout.Name(L.PlayedOver.Text);
                DrawPicker(ref _target, "BypassEmoteCreateModTarget", L.PickEmoteToPlayOver.Text, controls);
                SettingsLayout.Help(L.PlayedOverHelp.Text);

                SettingsLayout.Name(L.RacesCovered.Text);
                DrawRaces(controls);
                SettingsLayout.Help(L.RacesCoveredHelp.Text);

                SettingsLayout.Name(L.ModName.Text);
                ImGui.InputTextWithHint("##BypassEmoteCreateModName", L.ModNamePlaceholder.Text, ref _modName,
                    PermanentModBuilder.MaxModNameLength);
                SettingsLayout.Help(L.ModNameHelp.Text);

                var enableOnCreation = _enableOnCreation;
                if (SettingsLayout.Check(L.EnableOnCreation.Text, ref enableOnCreation))
                    _enableOnCreation = enableOnCreation;

                SettingsLayout.Help(L.EnableOnCreationHelp.Text);

                var highestPriority = _highestPriority;
                if (SettingsLayout.Check(L.HighestPriority.Text, ref highestPriority))
                    _highestPriority = highestPriority;

                SettingsLayout.Help(L.ClassicHighestPriorityHelp.Text);
            }
        }

        DrawSourceAnimation();

        DrawMatchWarnings();

        DrawCoverageWarnings();

        ImGui.Spacing();
        DrawCreate();

        if (_status.Length == 0)
            return;

        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(
            NoireTheme.Current.Resolve(_statusIsGood ? ThemeColor.Success : ThemeColor.Danger), _status);
        ImGui.PopTextWrapPos();
    }

    private static readonly string[] AllRaceNames = [.. RaceGenderData.AllRaces.Select(race => race.Name)];

    private readonly Dictionary<string, RacePaths?> _pathsByRace = new(StringComparer.Ordinal);

    private readonly Dictionary<string, bool> _moddedByRace = new(StringComparer.Ordinal);

    private static string SkeletonOf(string raceName)
    => RaceGenderData.AllRaces.First(race => race.Name == raceName).Id;

    private RacePaths? PathsFor(string raceName)
    {
        if (_pathsByRace.TryGetValue(raceName, out var known))
            return known;

        RacePaths? paths = null;

        if (Service.Orchestrator is { } orchestrator
            && Service.Catalog is { Ready: true } catalog
            && _source?.SelectedRowId is { } sourceRowId
            && _target?.SelectedRowId is { } targetRowId
            && catalog.Get(sourceRowId) is { } source
            && catalog.Get(targetRowId) is { } target)
        {
            paths = orchestrator.PathsFor(source, target, SkeletonOf(raceName));
        }

        _pathsByRace[raceName] = paths;
        return paths;
    }

    private void ForgetPaths()
    {
        _pathsByRace.Clear();
        _moddedByRace.Clear();
        _racesFilledFor = null;
    }

    private bool ModdedFor(string raceName)
    {
        if (_moddedByRace.TryGetValue(raceName, out var known))
            return known;

        var modded = Service.Orchestrator is { } orchestrator
            && PathsFor(raceName) is { } paths
            && paths.SourcePaths.Any(orchestrator.ForeignModServes);

        _moddedByRace[raceName] = modded;
        return modded;
    }

    private void RefillRacesWhenThePairChanges()
    {
        var pair = _source?.SelectedRowId is { } source && _target?.SelectedRowId is { } target
            ? ((uint, uint)?)(source, target)
            : null;

        if (pair == _racesFilledFor)
            return;

        _pathsByRace.Clear();
        _moddedByRace.Clear();
        _racesFilledFor = pair;

        var picker = Races();
        var available = AllRaceNames.Where(race => PathsFor(race) != null).ToList();

        picker.SetItems(available);
        picker.SetSelection(available);
    }

    private NoireMultiCombo<string> Races()
        => _races ??= new NoireMultiCombo<string>("BypassEmoteCreateModRaces", AllRaceNames)
        {
            PreviewPlaceholder = L.NoRaceCovered.Text,
            FilterHint = L.SearchRaces.Text,
            VisibleItemCount = 12,
        };

    private void DrawRaces(float width)
    {
        var picker = Races();

        picker.Width = width;
        picker.PreviewPlaceholder = L.NoRaceCovered.Text;
        picker.FilterHint = L.SearchRaces.Text;
        picker.Draw();
    }

    private void DrawSourceAnimation()
    {
        if (_source?.SelectedRowId is not { } rowId
            || Service.Catalog?.Get(rowId) is not { } source
            || Service.Orchestrator is not { } orchestrator
            || NoireService.ObjectTable.LocalPlayer is not { } player)
        {
            return;
        }

        ImGui.Spacing();

        if (orchestrator.ModServingAnimation(source, SwapOrchestrator.SkeletonFor(player)) is { } modName)
            ImGui.TextColored(NoireTheme.Current.Resolve(ThemeColor.Accent), L.ModdedAnimation.With("mod", modName));
        else
            ImGui.TextDisabled(L.VanillaAnimation.Text);
    }

    private void DrawMatchWarnings()
    {
        if (Service.Catalog is not { Ready: true } catalog
            || _source?.SelectedRowId is not { } sourceRowId
            || _target?.SelectedRowId is not { } targetRowId
            || sourceRowId == targetRowId
            || catalog.Get(sourceRowId) is not { } source
            || catalog.Get(targetRowId) is not { } target)
        {
            return;
        }

        var sourceName = SwapOrchestrator.NameOf(sourceRowId);
        var targetName = SwapOrchestrator.NameOf(targetRowId);

        var lines = new List<SwapAdvice.Line>();

        if (!EmoteHelper.IsEmoteUnlocked(targetRowId))
        {
            lines.Add(new SwapAdvice.Line(SwapAdvice.Severity.Warning, L.NotUnlockedTarget.With("target", targetName)));
        }

        if (ModOnTheTarget(target) is { Length: > 0 } modName)
        {
            lines.Add(new SwapAdvice.Line(SwapAdvice.Severity.Warning, L.ModChangesTarget.With("mod", modName, "target", targetName)));
        }

        lines.AddRange(SwapAdvice.Behaviour(source, sourceName, target, targetName));

        if (lines.Count == 0)
            return;

        ImGui.Spacing();
        SwapAdviceView.DrawLines(lines, 0f);
    }

    private static string? ModOnTheTarget(EmoteAttributes target)
        => Service.Orchestrator is { } orchestrator && NoireService.ObjectTable.LocalPlayer is { } player
            ? orchestrator.ModServingAnimation(target, SwapOrchestrator.SkeletonFor(player))
            : null;

    private void DrawCoverageWarnings()
    {
        if (_racesFilledFor == null)
            return;

        var picked = Races().Selected.ToHashSet(StringComparer.Ordinal);

        if (picked.Count == 0)
            return;

        var ownSkeleton = NoireService.ObjectTable.LocalPlayer is { } player
            ? SwapOrchestrator.SkeletonFor(player)
            : null;

        var plan = RaceCoveragePlanner.For(AllRaceNames, picked, PathsFor, ModdedFor,
            race => string.Equals(SkeletonOf(race), ownSkeleton, StringComparison.OrdinalIgnoreCase));

        if (plan.Shared.Count == 0 && plan.AlsoReached.Count == 0)
            return;

        var warning = NoireTheme.Current.Resolve(ThemeColor.Warning);

        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);

        foreach (var shared in plan.Shared)
        {
            ImGui.TextColored(warning, L.SharedAnimationFile.With("losers", string.Join(", ", shared.Losers), "winner", shared.Winner));
        }

        if (plan.AlsoReached.Count > 0)
        {
            ImGui.TextColored(warning, L.AlsoReached.With("races", string.Join(", ", plan.AlsoReached)));
        }

        ImGui.PopTextWrapPos();
    }

    private void DrawCreate()
    {
        var source = _source?.SelectedRowId;
        var target = _target?.SelectedRowId;
        var name = PermanentModBuilder.CleanName(_modName);

        var sameEmote = source is { } from && target is { } onto && from == onto;
        var races = Races().Selected;
        var ready = source != null && target != null && name.Length > 0 && !sameEmote && races.Count > 0;

        using (ImRaii.Disabled(!ready))
        {
            if (ImGui.Button(CreateLabel.Text, new Vector2(-1f, ImGui.GetFrameHeight() * 1.4f)) && ready)
                Create(source!.Value, target!.Value, name, [.. races.Select(SkeletonOf)]);
        }

        if (sameEmote)
            ImGui.TextDisabled(L.NotOverItself.Text);
        else if (source != null && target != null && races.Count == 0)
            ImGui.TextDisabled(L.PickRace.Text);
        else if (!ready)
            ImGui.TextDisabled(L.PickBoth.Text);
    }

    private void Create(uint sourceRowId, uint targetRowId, string name, IReadOnlyList<string> skeletons)
    {
        if (Service.Catalog is not { Ready: true } catalog)
        {
            Report(false, L.EmoteDataLoading.Text);
            return;
        }

        if (catalog.Get(sourceRowId) is not { } source || catalog.Get(targetRowId) is not { } target)
        {
            Report(false, L.NoReadableAnimation.Text);
            return;
        }

        var outcome = PermanentModBuilder.Create(source, target, skeletons, name, _enableOnCreation, _highestPriority);
        Report(outcome.Created, outcome.Message);
    }

    private void Report(bool good, string message)
    {
        _statusIsGood = good;
        _status = message;
    }

    private static void DrawPicker(ref NoireExcelPicker<Emote>? picker, string id, string placeholder, float width)
    {
        var resolved = Picker(ref picker, id, placeholder);

        resolved.Combo.Width = width;
        resolved.PreviewPlaceholder = placeholder;
        resolved.FilterHint = L.SearchEmotes.Text;
        resolved.Draw();
    }

    private static NoireExcelPicker<Emote> Picker(ref NoireExcelPicker<Emote>? picker, string id, string placeholder)
        => picker ??= new NoireExcelPicker<Emote>(id, CommonHelper.GetEmoteName)
        {
            Icon = CommonHelper.GetEmoteIcon,
            Include = emote => CommonHelper.GetEmotePlayType(emote) != EmotePlayType.DoNotPlay
                            && CommonHelper.IsEmoteDisplayable(emote),
            FilterHint = L.SearchEmotes.Text,
            PreviewPlaceholder = placeholder,
        };

}
