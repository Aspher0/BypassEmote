using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private sealed record SwapBuildRequest(EmoteAttributes Source, EmoteAttributes Target, int Generation,
        IReadOnlyList<ResolvedVariantPair> Pairs, IReadOnlyList<string> FallbackOrder, string Skeleton,
        string PathSignature, SwapModManager.SwapFilePlan Plan, bool ComposeUniqueNames,
        bool PublishInternalNames, SwapTimings Timings, bool ExecuteAfterApply = true);

    private sealed record SwapBuildOutcome(string MainResolvedSourcePath, long MainStampTicks,
        SwapModManager.PreparedSwapFiles Prepared, long ElapsedAtRetarget, long ElapsedAtPrepare,
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
        var grouped = BuildGroupedFiles(request.Pairs,
            group => BuildGroupOutput(group, request.FallbackOrder, request.ComposeUniqueNames),
            request.PublishInternalNames);

        var elapsedAtRetarget = request.Timings.Clock.ElapsedMilliseconds;

        if (grouped.Main is not { } main)
        {
            NoireLogger.LogError($"No variant of /{request.Source.Command} could be retargeted onto /{request.Target.Command}.", LogPrefix);
            return null;
        }

        if (_swapMods.PrepareFiles(request.Plan, grouped.Files) is not { } prepared)
            return null;

        return new SwapBuildOutcome(main.ResolvedSourcePath,
            StampFor(main.Pair.SourceRequestedPath, main.ResolvedSourcePath), prepared, elapsedAtRetarget,
            request.Timings.Clock.ElapsedMilliseconds,
            FadeProtectedIntro: OutputFadeProtected(request.Pairs, grouped),
            ClampedIntro: grouped.ClampedIntro,
            UniqueNamesApplied: grouped.UniqueNames,
            UniqueNameByKey: grouped.UniqueNameByKey,
            InternalUniqueNamesApplied: grouped.InternalUniqueNames,
            InternalNames: grouped.InternalNames);
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
            FeedbackHelper.Error(GenericFailureMessage);
        }
    }

    private void FinishSwapCore(SwapBuildRequest request, SwapBuildOutcome? outcome)
    {
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

        if (_penumbra.GetPlayerCollection() is not { } collection)
        {
            _generations.Relinquish(request.Generation);
            FeedbackHelper.Error(PenumbraUnavailableMessage);
            return;
        }

        var reused = OnDiskShapeMatches(_swapMods.Current, request.ComposeUniqueNames)
            && _swapMods.CanReuse(request.Source.RowId, request.Target.RowId, built.MainResolvedSourcePath,
                built.MainStampTicks, collection.Id, request.Skeleton)
            && _swapMods.Reactivate();

        if (!reused)
        {
            var appliedPriority = ComputeAppliedPriorityForFreshApply(
                new HashSet<string>(built.Prepared.RedirectedPaths.Keys, StringComparer.Ordinal), collection.Id,
                out var competingMods);

            var manifest = new SwapManifest(SwapModManager.CurrentManifestSchemaVersion, request.Source.RowId,
                request.Target.RowId, built.MainResolvedSourcePath, built.MainStampTicks, collection.Id,
                RedirectsComputedByApply, appliedPriority, (int)built.Prepared.Flavor,
                FadeProtectedIntro: built.FadeProtectedIntro,
                ClampedIntro: built.ClampedIntro,
                UniqueNames: built.UniqueNamesApplied,
                UniqueNameByKey: built.UniqueNameByKey,
                InternalUniqueNames: built.InternalUniqueNamesApplied,
                InternalNames: built.InternalNames,
                Skeleton: request.Skeleton,
                PathSignature: request.PathSignature,
                CompetingMods: competingMods);

            if (!_swapMods.Register(manifest, built.Prepared))
            {
                _generations.Relinquish(request.Generation);
                FeedbackHelper.Error(GenericFailureMessage);
                return;
            }
        }

        var timings = request.Timings with
        {
            AtRetarget = built.ElapsedAtRetarget,
            AtPrepare = built.ElapsedAtPrepare,
            AtApply = request.Timings.Clock.ElapsedMilliseconds,
        };

        if (!request.ExecuteAfterApply)
        {
            NoireLogger.LogWarning(
                $"The swap of /{request.Source.Command} onto /{request.Target.Command} now serves {request.Skeleton}"
                + $" ({timings.AtApply}ms).", LogPrefix);

            return;
        }

        ExecuteSwapTail(request.Source, request.Target, request.Generation, timings);
    }

    private void RefuseBackgroundReturn(SwapBuildRequest request, BackgroundVerdict verdict)
    {
        if (ShouldRelinquishClaim(verdict))
            _generations.Relinquish(request.Generation);

        NoireLogger.LogDebug(
            $"{BackgroundRefusalDetail(verdict)} (/{request.Source.Command} onto /{request.Target.Command}).", LogPrefix);

        if (ShouldWarnOnRefusal(verdict))
            FeedbackHelper.Error(GenericFailureMessage);
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
