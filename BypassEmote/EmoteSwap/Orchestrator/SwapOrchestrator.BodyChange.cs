using BypassEmote.Models;
using NoireLib;
using System;
using System.Diagnostics;

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

        if (_swapMods.Registry.Skeleton == skeleton)
            return;

        var plan = SkeletonRewritePlanner.For(_swapMods.Registry, skeleton);

        NoireLogger.LogDebug(
            $"The drawn body became {skeleton}; {plan.Rewrites.Count} option(s) are rewritten for it.", LogPrefix);

        _swapMods.RewriteForSkeleton(plan, skeleton);

        if (plan.UncoveredKeys.Count == 0)
            return;

        NoireLogger.LogDebug(
            $"{plan.UncoveredKeys.Count} kept swap(s) hold no file for {skeleton}; the live ones are built again.",
            LogPrefix);

        foreach (var key in plan.UncoveredKeys)
            RebuildUncovered(key, skeleton);
    }

    private void RebuildUncovered(string contentKey, string skeleton)
    {
        if (_swapMods.KeptWithKey(contentKey) is not { SelectedByUs: true, IsIdlePoseSwap: false } kept)
            return;

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
            return;

        if (_catalog.Get(kept.SourceEmote) is not { } source || _catalog.Get(kept.TargetEmote) is not { } target)
            return;

        source = WithConditionVariant(source, DirectPlayPlanner.PlayableAsFor(
            DirectPlayPlanner.ReadState(localPlayer).Condition));

        var raceInputs = RaceInputsFor(source, target, skeleton);

        if (raceInputs.Count == 0 || raceInputs[0].Race != skeleton)
        {
            NoireLogger.LogDebug(
                $"/{source.Command} and /{target.Command} share no usable posture variant on {skeleton}, "
                + $"so '{kept.OptionName}' is left as it was.", LogPrefix);

            return;
        }

        NoireLogger.LogDebug($"'{kept.OptionName}' is on, so it is built again for {skeleton}.", LogPrefix);

        const bool publishInternalNames = true;

        StartBackgroundBuild(new SwapBuildRequest(source, target, _generations.TakeOwnership(), raceInputs,
            skeleton, kept.ContentKey, kept.SourceKey ?? SourceKeyFor(source, raceInputs),
            _swapMods.BeginPrepare(), ComposeUniqueNamesFor(target, out _),
            publishInternalNames, ModServingAnimation(source, skeleton),
            new SwapTimings(Stopwatch.StartNew(), AtMatch: 0, AtPair: 0, AtRetarget: 0, AtPrepare: 0, AtApply: 0),
            ExecuteAfterApply: false));
    }
}
