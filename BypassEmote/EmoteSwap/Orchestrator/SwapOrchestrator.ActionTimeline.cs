using BypassEmote.Enums;
using BypassEmote.Models;
using NoireLib;
using NoireLib.Animations.PapFormat;
using NoireLib.Animations.PapFormat.Tmb;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal readonly record struct ActionTimelineFile(string GamePath, byte[] Bytes);

    private static readonly ConcurrentDictionary<string, ActionTimelineServing.ServedActionTimeline?>
        ServedActionTimelines = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>?> VanillaTimelineNames =
        new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>?> SourcePapNamesByPath =
        new(StringComparer.Ordinal);

    internal static Func<ResolvedVariantPair, byte[], ActionTimelineFile?>? ActionTimelinesFor(EmoteAttributes source,
        EmoteAttributes target)
    {
        if (source.LoopKind != EmotePlayType.OneShot || target.LoopKind != EmotePlayType.Looped)
            return ActionTimelineFor;

        Log.Debug($"Tmb not served: /{source.Command} is one-shot, /{target.Command} loops", LogPrefix);

        return null;
    }

    internal static ActionTimelineFile? ActionTimelineFor(ResolvedVariantPair pair, byte[] servedPap)
    {
        if (pair.Pair.LentSource || pair.ResolvedSourceTimeline is not { } resolvedTimeline)
            return null;

        if (ActionTimelinePathFor(pair.Pair.SourceRequestedPath) is not { } sourceTimeline
            || string.Equals(resolvedTimeline, sourceTimeline, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (ActionTimelinePathFor(pair.Pair.TargetRequestedPath) is not { } targetTimeline)
            return null;

        if (SourcePapNames(pair.Pair.SourceRequestedPath, pair.ResolvedSourcePath) is not { Count: > 0 } sourceNames)
            return null;

        var key = $"{resolvedTimeline}|{StampFor(sourceTimeline, resolvedTimeline)}|{targetTimeline}"
            + $"|{string.Join(",", sourceNames)}";

        if (ServedActionTimelines.GetOrAdd(key,
                _ => BuildServedActionTimeline(resolvedTimeline, sourceNames, targetTimeline)) is not { } served)
        {
            return null;
        }

        var papNames = PapAnimationNames.Read(servedPap);

        if (served.BodyNames.Any(name => !papNames.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            Log.Debug($"Tmb not served on '{targetTimeline}': '{pair.Pair.TargetRequestedPath}' has no "
                + $"{string.Join(", ", served.BodyNames)}", LogPrefix);

            return null;
        }

        return new ActionTimelineFile(targetTimeline, served.Bytes);
    }

    private static IReadOnlyList<string>? SourcePapNames(string requestedPath, string resolvedPath)
        => SourcePapNamesByPath.GetOrAdd($"{resolvedPath}|{StampFor(requestedPath, resolvedPath)}",
            _ => ReadPap(requestedPath, resolvedPath) is { } bytes ? PapAnimationNames.Read(bytes) : null);

    private static ActionTimelineServing.ServedActionTimeline? BuildServedActionTimeline(string resolvedTimeline,
        IReadOnlyList<string> sourceNames, string targetTimeline)
    {
        byte[] sourceBytes;

        try
        {
            sourceBytes = File.ReadAllBytes(resolvedTimeline);
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to read tmb '{resolvedTimeline}': {ex.Message}", LogPrefix);
            return null;
        }

        if (VanillaNamesAskedBy(targetTimeline) is not { Count: > 0 } targetNames)
        {
            Log.Debug($"Tmb not served: '{targetTimeline}' has no animation", LogPrefix);
            return null;
        }

        try
        {
            return ActionTimelineServing.Serve(sourceBytes, sourceNames, targetNames, resolvedTimeline, targetTimeline);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to serve tmb '{resolvedTimeline}' on '{targetTimeline}'", LogPrefix);

            return null;
        }
    }

    private static IReadOnlyList<string>? VanillaNamesAskedBy(string timelinePath)
        => VanillaTimelineNames.GetOrAdd(timelinePath, static path =>
            NoireService.DataManager.FileExists(path) && NoireService.DataManager.GetFile(path)?.Data is { } vanilla
                ? TmbAnimationRenamer.ReadAnimationReferences(vanilla)
                : null);
}
