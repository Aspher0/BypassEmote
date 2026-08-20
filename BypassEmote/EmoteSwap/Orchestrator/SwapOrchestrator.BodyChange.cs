using BypassEmote.Models;
using NoireLib;
using NoireLib.Animations.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    public void CorrectForDrawnSkeleton(string skeleton)
    {
        try
        {
            CorrectForDrawnSkeletonCore(skeleton);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not correct the swap for {skeleton}.", LogPrefix);
        }
    }

    private void CorrectForDrawnSkeletonCore(string skeleton)
    {
        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap || !_catalog.Ready)
            return;

        if (_swapMods.Current is not { } current)
            return;

        if (SignatureUnder(current, skeleton) is { } signature && signature == current.PathSignature)
        {
            NoireLogger.LogDebug(
                $"{skeleton} asks for the same paths as {current.Skeleton}, so the swap is left exactly as it is.", LogPrefix);

            _swapMods.RecordServedSkeleton(skeleton);
            return;
        }

        if (_swapMods.TurnedOffSinceLastOn)
        {
            NoireLogger.LogDebug($"The drawn body became {skeleton} while the swap was off; the next press builds for it.", LogPrefix);
            return;
        }

        if (current.IsIdlePoseSwap)
        {
            EndForBodyChange($"the drawn body became {skeleton} under an idle-pose swap");
            return;
        }

        if (_catalog.Get(current.SourceEmote) is not { } source || _catalog.Get(current.TargetEmote) is not { } target)
        {
            EndForBodyChange($"emote {current.SourceEmote} or {current.TargetEmote} could not be read back for {skeleton}");
            return;
        }

        var pairs = PairVariants(source, target, skeleton);

        if (pairs.Count == 0)
        {
            EndForBodyChange($"/{source.Command} and /{target.Command} share no usable variant on {skeleton}");
            return;
        }

        var resolvedPairs = new List<ResolvedVariantPair>(pairs.Count);
        foreach (var pair in pairs)
            resolvedPairs.Add(new ResolvedVariantPair(pair, ResolveOutsideOwnMod(pair.SourceRequestedPath)));

        NoireLogger.LogWarning(
            $"The drawn body became {skeleton}, which /{source.Command} onto /{target.Command} was not built for; "
            + "rewriting the mod for it without touching the emote.", LogPrefix);

        var composeUniqueNames = current.RedirectedPaths.Keys.Any(UniqueNamePlanner.IsComposedPapPath);

        var request = new SwapBuildRequest(source, target, _generations.TakeOwnership(), resolvedPairs,
            EmotePathHelper.GetFallbackOrder(skeleton), skeleton, PathSignatureFor(pairs),
            _swapMods.BeginPrepare(), composeUniqueNames, PublishInternalNames: true,
            new SwapTimings(Stopwatch.StartNew(), AtMatch: 0, AtPair: 0, AtRetarget: 0, AtPrepare: 0, AtApply: 0),
            ExecuteAfterApply: false);

        StartBackgroundBuild(request);
    }

    private string? SignatureUnder(SwapManifest current, string skeleton)
    {
        if (current.PathSignature == null || current.IsIdlePoseSwap)
            return null;

        if (_catalog.Get(current.SourceEmote) is not { } source || _catalog.Get(current.TargetEmote) is not { } target)
            return null;

        return PathSignatureFor(PairVariants(source, target, skeleton));
    }

    private void EndForBodyChange(string reason)
    {
        NoireLogger.LogDebug($"Ending the swap: {reason}.", LogPrefix);

        _endWatcher.StopWatching();
        _swapMods.Deactivate();
    }
}
