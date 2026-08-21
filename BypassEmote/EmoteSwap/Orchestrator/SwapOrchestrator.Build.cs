using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;

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
        SwapModManager.SwapFilePlan Plan, bool ComposeUniqueNames, bool PublishInternalNames, string? SourceModName,
        SwapTimings Timings, bool ExecuteAfterApply = true);

    private sealed record SwapBuildOutcome(IReadOnlyDictionary<string, byte[]> Files,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FilesByRace,
        long ElapsedAtRetarget, long ElapsedAtPrepare,
        bool FadeProtectedIntro, bool ClampedIntro, bool UniqueNamesApplied,
        IReadOnlyDictionary<string, string>? UniqueNameByKey, bool InternalUniqueNamesApplied,
        IReadOnlyList<string>? InternalNames);

    private const string BackgroundOperationName = "Emote Swap byte pipeline";

    private void StartBackgroundBuild(SwapBuildRequest request)
    {
        _ = AsyncHelper.RunBackgroundThenFrameworkSafeAsync(
            () => BuildSwapFilesOrNull(request),
            outcome => FinishSwapOnFrameworkThread(request, outcome),
            ex => NoireLogger.LogDebug(
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
            NoireLogger.LogError(ex, $"Building the swap of /{request.Source.Command} onto /{request.Target.Command} failed.", LogPrefix);
            return null;
        }
    }

    private SwapBuildOutcome? BuildSwapFiles(SwapBuildRequest request)
    {
        var retargeted = new Dictionary<string, GroupOutput?>(StringComparer.Ordinal);
        var allFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var gamePathsByRace = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        GroupedSwapFiles? drawnFiles = null;
        IReadOnlyList<ResolvedVariantPair> drawnPairs = [];

        foreach (var race in request.Races)
        {
            var grouped = BuildGroupedFiles(race.Pairs,
                RetargetingOncePerInput(retargeted, race.FallbackOrder, request.ComposeUniqueNames),
                request.PublishInternalNames);

            var isDrawnBody = race.Race == request.Skeleton;

            if (grouped.Main == null)
            {
                if (isDrawnBody)
                {
                    NoireLogger.LogError($"No variant of /{request.Source.Command} could be retargeted onto /{request.Target.Command}.", LogPrefix);
                    return null;
                }

                NoireLogger.LogDebug($"Nothing retargeted for {race.Race}; that body is left out of this swap.", LogPrefix);
                continue;
            }

            if (isDrawnBody)
            {
                drawnFiles = grouped;
                drawnPairs = race.Pairs;
            }

            foreach (var (gamePath, bytes) in grouped.Files)
                allFiles.TryAdd(gamePath, bytes);

            gamePathsByRace[race.Race] = [.. grouped.Files.Keys];
        }

        var elapsedAtRetarget = request.Timings.Clock.ElapsedMilliseconds;

        if (drawnFiles is not { } drawn)
        {
            NoireLogger.LogError($"The swap of /{request.Source.Command} covers no body to play it on.", LogPrefix);
            return null;
        }

        if (_swapMods.PrepareFiles(request.Plan, allFiles) is not { } prepared)
            return null;

        return new SwapBuildOutcome(allFiles, FilesByRace(gamePathsByRace, prepared.RedirectedPaths),
            elapsedAtRetarget, request.Timings.Clock.ElapsedMilliseconds,
            FadeProtectedIntro: OutputFadeProtected(drawnPairs, drawn),
            ClampedIntro: drawn.ClampedIntro,
            UniqueNamesApplied: drawn.UniqueNames,
            UniqueNameByKey: drawn.UniqueNameByKey,
            InternalUniqueNamesApplied: drawn.InternalUniqueNames,
            InternalNames: drawn.InternalNames);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FilesByRace(
        IReadOnlyDictionary<string, IReadOnlyList<string>> gamePathsByRace,
        IReadOnlyDictionary<string, string> redirectedPaths)
    {
        var byRace = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            gamePathsByRace.Count, StringComparer.Ordinal);

        foreach (var (race, gamePaths) in gamePathsByRace)
        {
            var redirects = new Dictionary<string, string>(gamePaths.Count, StringComparer.Ordinal);

            foreach (var gamePath in gamePaths)
            {
                if (redirectedPaths.TryGetValue(gamePath, out var relativePath))
                    redirects[gamePath] = relativePath;
            }

            byRace[race] = redirects;
        }

        return byRace;
    }

    private void FinishSwapOnFrameworkThread(SwapBuildRequest request, SwapBuildOutcome? outcome)
    {
        try
        {
            FinishSwapCore(request, outcome);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Finishing the swap of /{request.Source.Command} failed.", LogPrefix);
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
            ReportFailure(request, PenumbraUnavailableMessage);
            return;
        }

        var newEntry = EntryFor(request, built);
        var atEntry = request.Timings.Clock.ElapsedMilliseconds;

        if (!_swapMods.AddAndSelect(newEntry, built.Files, request.Skeleton))
        {
            _generations.Relinquish(request.Generation);
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
            NoireLogger.LogDebug(
                $"The swap of /{request.Source.Command} onto /{request.Target.Command} now serves {request.Skeleton}"
                + $" ({timings.AtApply}ms).", LogPrefix);

            return;
        }

        ExecuteSwapTail(request.Source, request.Target, request.Generation, timings);
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

        return new SwapOptionEntry(request.ContentKey, groupName, optionName, request.Source.RowId,
            request.Target.RowId, IsIdlePoseSwap: false, built.FilesByRace,
            UniqueNameByKey: built.UniqueNameByKey,
            InternalNames: built.InternalNames,
            FadeProtectedIntro: built.FadeProtectedIntro,
            ClampedIntro: built.ClampedIntro,
            UniqueNames: built.UniqueNamesApplied,
            InternalUniqueNames: built.InternalUniqueNamesApplied,
            RulesStamp: SwapRulesStamp.Current(),
            SourceKey: request.SourceKey);
    }

    private void RefuseBackgroundReturn(SwapBuildRequest request, BackgroundVerdict verdict)
    {
        if (ShouldRelinquishClaim(verdict))
            _generations.Relinquish(request.Generation);

        NoireLogger.LogDebug(
            $"{BackgroundRefusalDetail(verdict)} (/{request.Source.Command} onto /{request.Target.Command}).", LogPrefix);

        if (ShouldWarnOnRefusal(verdict))
            ReportFailure(request, GenericFailureMessage);
    }

    private static void ReportFailure(SwapBuildRequest request, string message)
    {
        if (request.ExecuteAfterApply)
            FeedbackHelper.Error(message);
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
