using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private static string DisplayNameFor(EmoteAttributes emote)
    {
        var name = EmoteHelper.GetEmoteById(emote.RowId) is { } row ? CommonHelper.GetEmoteName(row) : string.Empty;

        return string.IsNullOrWhiteSpace(name) ? emote.Command : name;
    }

    private static string? CommandFor(EmoteAttributes emote)
    {
        if (!string.IsNullOrWhiteSpace(emote.Command))
            return WithSlash(emote.Command);

        if (EmoteHelper.GetEmoteById(emote.RowId)?.TextCommand.ValueNullable is not { } textCommand)
            return null;

        foreach (var candidate in new[]
                 {
                     textCommand.Command, textCommand.ShortCommand, textCommand.Alias, textCommand.ShortAlias,
                 })
        {
            var text = candidate.ExtractText();

            if (!string.IsNullOrWhiteSpace(text))
                return WithSlash(text);
        }

        return null;
    }

    private static string WithSlash(string command)
        => command.StartsWith('/') ? command : $"/{command}";

    private sealed record SwapBuildRequest(EmoteAttributes Source, EmoteAttributes Target, int Generation,
        IReadOnlyList<RaceBuildInput> Races, string Skeleton, string ContentKey, string SourceKey,
        SwapModManager.SwapFilePlan Plan, string? SourceModName,
        SwapTimings Timings, bool ExecuteAfterApply = true, bool? HoldOffHand = null, SwapTrace? Trace = null);

    private sealed record SwapBuildOutcome(IReadOnlyDictionary<string, byte[]> Files,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FilesByRace,
        long ElapsedAtRetarget, long ElapsedAtPrepare,
        bool FadeProtectedIntro, bool ClampedIntro,
        Dictionary<string, GroupOutput?> Retargeted);

    private const string BackgroundOperationName = "Emote Swap byte pipeline";

    private const string CoverageOperationName = "Emote Swap other bodies";

    private void StartBackgroundBuild(SwapBuildRequest request)
    {
        _ = AsyncHelper.RunBackgroundThenFrameworkSafeAsync(
            () => BuildSwapFilesOrNull(request),
            outcome => FinishSwapOnFrameworkThread(request, outcome),
            ex => Log.Debug(
                $"Could not hand a finished swap build back to the framework thread ({ex.Message}); dropping it.", LogPrefix),
            BackgroundOperationName);
    }

    private SwapBuildOutcome? BuildSwapFilesOrNull(SwapBuildRequest request)
    {
        try
        {
            return BuildSwapFiles(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Building the swap of /{request.Source.Command} onto /{request.Target.Command} failed.", LogPrefix);
            return null;
        }
    }

    private SwapBuildOutcome? BuildSwapFiles(SwapBuildRequest request)
    {
        var retargeted = new Dictionary<string, GroupOutput?>(StringComparer.Ordinal);

        if (request.Races.FirstOrDefault(race => race.Race == request.Skeleton) is not { } drawnRace)
        {
            Log.Error($"The swap of /{request.Source.Command} covers no body to play it on.", LogPrefix);
            return null;
        }

        var drawn = BuildGroupedFiles(drawnRace.Pairs,
            RetargetingOncePerInput(retargeted, drawnRace.FallbackOrder, request.HoldOffHand));

        var elapsedAtRetarget = request.Timings.Clock.ElapsedMilliseconds;

        if (drawn.Main == null)
        {
            Log.Error($"No variant of /{request.Source.Command} could be retargeted onto /{request.Target.Command}.", LogPrefix);
            return null;
        }

        var assembled = AssembleRaceFiles(
            new Dictionary<string, GroupedSwapFiles>(StringComparer.Ordinal) { [drawnRace.Race] = drawn });

        if (_swapMods.PrepareFiles(request.Plan, assembled.WriteSet) == null)
            return null;

        return new SwapBuildOutcome(assembled.WriteSet, assembled.FilesByRace,
            elapsedAtRetarget, request.Timings.Clock.ElapsedMilliseconds,
            FadeProtectedIntro: OutputFadeProtected(drawnRace.Pairs, drawn),
            ClampedIntro: drawn.ClampedIntro,
            retargeted);
    }

    private void StartCoverageBuild(SwapBuildRequest request, Dictionary<string, GroupOutput?> retargeted)
    {
        if (request.Races.All(race => race.Race == request.Skeleton))
            return;

        _ = AsyncHelper.RunBackgroundThenFrameworkSafeAsync(
            () => BuildCoverageOrNull(request, retargeted),
            coverage =>
            {
                if (coverage == null || _disposed)
                    return;

                if (!_swapMods.AddCoverage(request.ContentKey, request.Skeleton, coverage))
                {
                    Log.Debug($"The other bodies of /{request.Source.Command} onto /{request.Target.Command} were "
                        + "not kept: the swap or the drawn body changed while they were built.", LogPrefix);
                }
            },
            ex => Log.Debug(
                $"Could not hand the other bodies of a swap back to the framework thread ({ex.Message}); dropping them.",
                LogPrefix),
            CoverageOperationName);
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? BuildCoverageOrNull(
        SwapBuildRequest request, Dictionary<string, GroupOutput?> retargeted)
    {
        try
        {
            var clock = Stopwatch.StartNew();
            var byRace = new Dictionary<string, GroupedSwapFiles>(StringComparer.Ordinal);

            foreach (var race in request.Races)
            {
                if (race.Race == request.Skeleton)
                    continue;

                var grouped = BuildGroupedFiles(race.Pairs,
                    RetargetingOncePerInput(retargeted, race.FallbackOrder, request.HoldOffHand));

                if (grouped.Main == null)
                {
                    Log.Debug($"Nothing retargeted for {race.Race}; that body is left out of this swap.", LogPrefix);
                    continue;
                }

                byRace[race.Race] = grouped;
            }

            if (byRace.Count == 0)
                return null;

            var assembled = AssembleRaceFiles(byRace);

            if (_swapMods.PrepareFiles(request.Plan, assembled.WriteSet) == null)
                return null;

            Log.Debug($"Built the other {byRace.Count} bod(y/ies) of /{request.Source.Command} onto "
                + $"/{request.Target.Command} in {clock.ElapsedMilliseconds}ms.", LogPrefix);

            return assembled.FilesByRace;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Building the other bodies of /{request.Source.Command} onto /{request.Target.Command} failed.",
                LogPrefix);

            return null;
        }
    }

    internal sealed record AssembledRaceFiles(
        IReadOnlyDictionary<string, byte[]> WriteSet,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FilesByRace);

    internal static AssembledRaceFiles AssembleRaceFiles(IReadOnlyDictionary<string, GroupedSwapFiles> byRace)
    {
        var writeSet = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var filesByRace = new Dictionary<string, IReadOnlyDictionary<string, string>>(byRace.Count, StringComparer.Ordinal);
        var fileNames = new Dictionary<byte[], string>(ReferenceEqualityComparer.Instance);

        foreach (var (race, grouped) in byRace)
        {
            var redirects = new Dictionary<string, string>(grouped.Files.Count, StringComparer.Ordinal);

            foreach (var (gamePath, bytes) in grouped.Files)
            {
                if (!fileNames.TryGetValue(bytes, out var fileName))
                    fileNames[bytes] = fileName = SwapModManager.DeriveFileName(bytes, string.Empty);

                var relativePath = SwapModManager.RedirectedPathValue(fileName + SwapModManager.FileExtensionFor(gamePath));

                redirects[gamePath] = relativePath;
                writeSet[relativePath] = bytes;
            }

            filesByRace[race] = redirects;
        }

        return new AssembledRaceFiles(writeSet, filesByRace);
    }

    private void FinishSwapOnFrameworkThread(SwapBuildRequest request, SwapBuildOutcome? outcome)
    {
        try
        {
            FinishSwapCore(request, outcome);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Finishing the swap of /{request.Source.Command} failed.", LogPrefix);
            ReportFailure(request, GenericFailureMessage);
        }
    }

    private void FinishSwapCore(SwapBuildRequest request, SwapBuildOutcome? outcome)
    {
        var atFrame = request.Timings.Clock.ElapsedMilliseconds;

        var verdict = ClassifyBackgroundReturn(
            disposed: _disposed,
            generationCurrent: _generations.IsCurrent(request.Generation),
            inEmoteSwapMode: Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap,
            hasLocalPlayer: NoireService.ObjectTable.LocalPlayer != null,
            buildSucceeded: outcome != null);

        if (verdict != BackgroundVerdict.Proceed)
        {
            RefuseBackgroundReturn(request, verdict);
            return;
        }

        var built = outcome!;

        if (_penumbra.GetPlayerCollection() == null)
        {
            _generations.Relinquish(request.Generation);
            ReportFailure(request, _penumbra.Available ? NoCharacterMessage : PenumbraUnavailableMessage);
            return;
        }

        var newEntry = EntryFor(request, built);
        var atEntry = request.Timings.Clock.ElapsedMilliseconds;

        if (!_swapMods.AddAndSelect(newEntry, built.Files, request.Skeleton))
        {
            _generations.Relinquish(request.Generation);
            LogSwapNotPlayed(request.Source, request.Target, request.Trace, "the generated mod could not be applied");
            ReportFailure(request, GenericFailureMessage);
            return;
        }

        var timings = request.Timings with
        {
            AtRetarget = built.ElapsedAtRetarget,
            AtPrepare = built.ElapsedAtPrepare,
            AtApply = request.Timings.Clock.ElapsedMilliseconds,
            AtFrame = atFrame,
            AtEntry = atEntry,
        };

        if (!request.ExecuteAfterApply)
        {
            Log.Debug(
                $"The swap of /{request.Source.Command} onto /{request.Target.Command} now serves {request.Skeleton}"
                + $" ({timings.AtApply}ms).", LogPrefix);

            StartCoverageBuild(request, built.Retargeted);
            return;
        }

        ExecuteSwapTail(request.Source, request.Target, request.Generation, timings, request.Trace);

        StartCoverageBuild(request, built.Retargeted);
    }

    private SwapOptionEntry EntryFor(SwapBuildRequest request, SwapBuildOutcome built)
    {
        var kept = _swapMods.KeptWithKey(request.ContentKey);

        var groupName = kept?.GroupName
            ?? _swapMods.GroupNameForTarget(request.Target.RowId)
            ?? OptionNaming.GroupNameFor(DisplayNameFor(request.Target), CommandFor(request.Target), request.Target.RowId,
                _swapMods.TakenGroupNames());

        var optionName = kept?.OptionName
            ?? OptionNaming.OptionNameFor(DisplayNameFor(request.Source), request.SourceModName,
                _swapMods.TakenOptionNames(groupName));

        var drawnPairs = request.Races.FirstOrDefault(race => race.Race == request.Skeleton)?.Pairs ?? [];

        return new SwapOptionEntry(request.ContentKey, groupName, optionName, request.Source.RowId,
            request.Target.RowId, IsIdlePoseSwap: false, built.FilesByRace,
            FadeProtectedIntro: built.FadeProtectedIntro,
            ClampedIntro: built.ClampedIntro,
            RulesStamp: SwapRulesStamp.Current(),
            SourceKey: request.SourceKey,
            SourceServedBy: SourceServedByFor(request.Target, drawnPairs));
    }

    private void RefuseBackgroundReturn(SwapBuildRequest request, BackgroundVerdict verdict)
    {
        if (ShouldRelinquishClaim(verdict))
            _generations.Relinquish(request.Generation);

        Log.Debug(
            $"{BackgroundRefusalDetail(verdict)} (/{request.Source.Command} onto /{request.Target.Command}).", LogPrefix);

        if (ShouldWarnOnRefusal(verdict))
        {
            LogSwapNotPlayed(request.Source, request.Target, request.Trace, "the swap files could not be built");
            ReportFailure(request, GenericFailureMessage);
        }
    }

    private static void ReportFailure(SwapBuildRequest request, string message)
    {
        if (request.ExecuteAfterApply)
            LogHelper.Error(message);
    }

    internal enum BackgroundVerdict
    {
        Proceed,

        Disposed,

        Superseded,

        ModeLeft,

        PlayerGone,

        BuildFailed,
    }

    internal static BackgroundVerdict ClassifyBackgroundReturn(bool disposed, bool generationCurrent,
        bool inEmoteSwapMode, bool hasLocalPlayer, bool buildSucceeded)
    {
        if (disposed)
            return BackgroundVerdict.Disposed;

        if (!generationCurrent)
            return BackgroundVerdict.Superseded;

        if (!inEmoteSwapMode)
            return BackgroundVerdict.ModeLeft;

        if (!hasLocalPlayer)
            return BackgroundVerdict.PlayerGone;

        return buildSucceeded ? BackgroundVerdict.Proceed : BackgroundVerdict.BuildFailed;
    }

    internal static bool ShouldRelinquishClaim(BackgroundVerdict verdict)
        => verdict is BackgroundVerdict.ModeLeft or BackgroundVerdict.PlayerGone or BackgroundVerdict.BuildFailed;

    internal static bool ShouldWarnOnRefusal(BackgroundVerdict verdict)
        => verdict == BackgroundVerdict.BuildFailed;

    internal static string BackgroundRefusalDetail(BackgroundVerdict verdict) => verdict switch
    {
        BackgroundVerdict.Disposed => "A swap finished building while the plugin was unloading; it was dropped",
        BackgroundVerdict.Superseded => "A superseded swap finished building; a newer swap owns the mod",
        BackgroundVerdict.ModeLeft => "A swap finished building after the player left Emote Swap mode; it was dropped",
        BackgroundVerdict.PlayerGone => "A swap finished building with no local player left to play it; it was dropped",
        BackgroundVerdict.BuildFailed => "A swap could not be built; nothing was applied",
        _ => "A swap was dropped",
    };
}
