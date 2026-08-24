using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI;

/// <summary> Turns a pair of emotes into an simple Penumbra mod. </summary>
public sealed class CreateModWindow : Window, IDisposable
{
    private NoireExcelPicker<Emote>? _source;
    private NoireExcelPicker<Emote>? _target;

    private NoireMultiCombo<string>? _races;

    private string _modName = string.Empty;
    private bool _enableOnCreation = true;
    private bool _highestPriority = false;

    // The pair the race picker was last filled for, so it refills when either emote changes.
    private (uint Source, uint Target)? _racesFilledFor;

    private string _status = string.Empty;
    private bool _statusIsGood;

    public CreateModWindow() : base("Bypass Emote - Create a mod##BypassEmoteCreateMod")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    /// <summary> Opens the window with the emote already in the source slot (from the main UI). </summary>
    public void ShowFor(Emote emote)
    {
        Picker(ref _source, "BypassEmoteCreateModSource", "Pick the emote to play...").Select(emote.RowId);

        _status = string.Empty;
        IsOpen = true;
        BringToFront();
    }

    public void Show()
    {
        _status = string.Empty;
        IsOpen = true;
        BringToFront();
    }

    public override void Draw()
    {
        if (Service.Penumbra is not { Available: true })
        {
            ImGui.TextColored(NoireTheme.Current.Resolve(ThemeColor.Danger),
                Service.Penumbra?.UnavailableReason is { Length: > 0 } reason ? reason : "Penumbra is not running.");
            return;
        }

        ImGui.TextWrapped("This window allows you to create a permanent swap mod, and it stays unaffected by BypassEmote. You own it, and you manage it, like any other mod.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var names = SettingsLayout.NameColumn("Emote to play", "Played over", "Races covered", "Mod name", "Enable on creation", "Highest priority");
        var controls = MathF.Max(NoireUI.Scaled(240f), ImGui.GetContentRegionAvail().X - names - NoireUI.Scaled(40f));

        RefillRacesWhenThePairChanges();

        using (var rows = SettingsLayout.Rows("##BypassEmoteCreateModRows", names, controls))
        {
            if (rows)
            {
                SettingsLayout.Name("Emote to play");
                DrawPicker(ref _source, "BypassEmoteCreateModSource", "Pick the emote to play...", controls);
                SettingsLayout.Help("The animation the mod plays. The one you have not unlocked.");

                SettingsLayout.Name("Played over");
                DrawPicker(ref _target, "BypassEmoteCreateModTarget", "Pick the emote to play over...", controls);
                SettingsLayout.Help("The emote you will actually use in game. The one you have unlocked.");

                SettingsLayout.Name("Races covered");
                DrawRaces(controls);
                SettingsLayout.Help("Which bodies the mod is written for.");

                SettingsLayout.Name("Mod name");
                ImGui.InputTextWithHint("##BypassEmoteCreateModName", "My emote mod", ref _modName,
                    PermanentModBuilder.MaxModNameLength);
                SettingsLayout.Help("The name of the generated mod.");

                var enableOnCreation = _enableOnCreation;
                if (SettingsLayout.Check("Enable on creation", ref enableOnCreation))
                    _enableOnCreation = enableOnCreation;

                SettingsLayout.Help("Switches the mod on in your character's collection as soon as it exists.");

                var highestPriority = _highestPriority;
                if (SettingsLayout.Check("Highest priority", ref highestPriority))
                    _highestPriority = highestPriority;

                SettingsLayout.Help("Make it highest priority in your collection on creation.");
            }
        }

        DrawSourceAnimation();

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

    private void RefillRacesWhenThePairChanges()
    {
        _pathsByRace.Clear();

        var pair = _source?.SelectedRowId is { } source && _target?.SelectedRowId is { } target
            ? ((uint, uint)?)(source, target)
            : null;

        if (pair == _racesFilledFor)
            return;

        _racesFilledFor = pair;

        var picker = Races();
        var available = AllRaceNames.Where(race => PathsFor(race) != null).ToList();

        picker.SetItems(available);
        picker.SetSelection(available);
    }

    private NoireMultiCombo<string> Races()
        => _races ??= new NoireMultiCombo<string>("BypassEmoteCreateModRaces", AllRaceNames)
        {
            PreviewPlaceholder = "No race covered",
            FilterHint = "Search races...",
            VisibleItemCount = 12,
        };

    private void DrawRaces(float width)
    {
        var picker = Races();

        picker.Width = width;
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
            ImGui.TextColored(NoireTheme.Current.Resolve(ThemeColor.Accent), $"Modded animation: {modName}");
        else
            ImGui.TextDisabled("Vanilla animation");
    }

    private void DrawCoverageWarnings()
    {
        if (_racesFilledFor == null)
            return;

        var picked = Races().Selected.ToHashSet(StringComparer.Ordinal);

        if (picked.Count == 0)
            return;

        var plan = RaceCoveragePlanner.For(AllRaceNames, picked, PathsFor);

        if (plan.Shared.Count == 0 && plan.AlsoReached.Count == 0)
            return;

        var warning = NoireTheme.Current.Resolve(ThemeColor.Warning);

        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);

        foreach (var shared in plan.Shared)
        {
            ImGui.TextColored(warning, $"{string.Join(", ", shared.Losers)} read the same animation file as "
                + $"{shared.Winner}. {shared.Winner}'s version plays for all of them.");
        }

        if (plan.AlsoReached.Count > 0)
        {
            ImGui.TextColored(warning, "This also changes the emote for "
                + $"{string.Join(", ", plan.AlsoReached)}: they read a file the mod writes.");
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
            if (ImGui.Button("Create", new Vector2(-1f, ImGui.GetFrameHeight() * 1.4f)) && ready)
                Create(source!.Value, target!.Value, name, [.. races.Select(SkeletonOf)]);
        }

        if (sameEmote)
            ImGui.TextDisabled("An emote cannot be played over itself.");
        else if (source != null && target != null && races.Count == 0)
            ImGui.TextDisabled("Pick at least one race.");
        else if (!ready)
            ImGui.TextDisabled("Pick both emotes and name the mod.");
    }

    private void Create(uint sourceRowId, uint targetRowId, string name, IReadOnlyList<string> skeletons)
    {
        if (Service.Catalog is not { Ready: true } catalog)
        {
            Report(false, "Emote data is still loading. Try again in a moment.");
            return;
        }

        if (catalog.Get(sourceRowId) is not { } source || catalog.Get(targetRowId) is not { } target)
        {
            Report(false, "One of those emotes has no animation this can read.");
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
        resolved.Draw();
    }

    private static NoireExcelPicker<Emote> Picker(ref NoireExcelPicker<Emote>? picker, string id, string placeholder)
        => picker ??= new NoireExcelPicker<Emote>(id, CommonHelper.GetEmoteName)
        {
            Icon = CommonHelper.GetEmoteIcon,
            Include = emote => CommonHelper.GetEmotePlayType(emote) != EmotePlayType.DoNotPlay
                            && CommonHelper.IsEmoteDisplayable(emote),
            FilterHint = "Search emotes...",
            PreviewPlaceholder = placeholder,
        };

    public void Dispose() { }
}
