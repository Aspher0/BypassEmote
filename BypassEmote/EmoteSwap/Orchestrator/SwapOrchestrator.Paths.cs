using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal readonly record struct ResolvedVariantPair(VariantPair Pair, string ResolvedSourcePath);

    // One source pap -> target pap redirect. RequiredNamesPath names the vanilla pap the animation names
    // must come from.
    internal readonly record struct VariantPair(string SourceRequestedPath, string TargetRequestedPath,
        string? RequiredNamesPath = null, string? SourceFaceLibrary = null);

    internal static string? SelectRequestedPath(string relativePapPath, IReadOnlyList<string> fallbackSkeletons,
        Func<string, bool> modProvides, Func<string, bool> vanillaExists)
        => EmotePathHelper.FindExistingPath(relativePapPath, fallbackSkeletons, modProvides, vanillaExists);

    private List<VariantPair> PairVariants(EmoteAttributes source, EmoteAttributes target, string skeleton)
        => BuildPairs(source, target, EmotePathHelper.GetFallbackOrder(skeleton), ForeignModProvides, VanillaExists);

    // Every posture variant the two emotes share, paired source path -> target path, plus the target's intro
    // channel when it has a usable intro pap. Required names come from the chain's first vanilla copy, since a
    // mod would hand back its own. Order matters: the caller stamps identity off the first pair.
    internal static List<VariantPair> BuildPairs(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> fallbackOrder, Func<string, bool> modProvides, Func<string, bool> vanillaExists)
    {
        var pairs = new List<VariantPair>(source.Variants.Count);

        foreach (var sourceVariant in source.Variants)
        {
            if (target.Variants.FirstOrDefault(variant => variant.Posture == sourceVariant.Posture) is not { } targetVariant)
                continue;

            if (SelectRequestedPath(sourceVariant.RelativePapPath, fallbackOrder, modProvides, vanillaExists) is not { } sourcePath)
                continue;

            if (SelectRequestedPath(targetVariant.RelativePapPath, fallbackOrder, modProvides, vanillaExists) is not { } targetPath)
                continue;

            pairs.Add(new VariantPair(sourcePath, targetPath, SourceFaceLibrary: source.FaceLibraryFor(sourceVariant.RelativePapPath)));
        }

        // Alone, an intro pair is a half-swap: the target's intro redirected while its loop plays raw.
        if (pairs.Count == 0 || target.IntroRelativePapPath is not { } targetIntro)
            return pairs;

        // Names must come from a vanilla copy of the target's intro, so the mod probe is off here.
        if (SelectRequestedPath(targetIntro, fallbackOrder, static _ => false, vanillaExists) is not { } introNamesPath)
            return pairs;

        string? introSourcePath = null;
        string? introSourceFaceLibrary = null;

        if (source.IntroRelativePapPath is { } ownIntro
            && SelectRequestedPath(ownIntro, fallbackOrder, modProvides, vanillaExists) is { } ownIntroPath)
        {
            introSourcePath = ownIntroPath;
            introSourceFaceLibrary = source.FaceLibraryFor(ownIntro);
        }
        else if ((source.Variants.FirstOrDefault(variant => variant.Posture == PostureFlags.Standing)
                ?? source.Variants.FirstOrDefault()) is { } lentVariant
            && SelectRequestedPath(lentVariant.RelativePapPath, fallbackOrder, modProvides, vanillaExists) is { } lentPath)
        {
            introSourcePath = lentPath;
            introSourceFaceLibrary = source.FaceLibraryFor(lentVariant.RelativePapPath);
        }

        if (introSourcePath == null)
            return pairs;

        if (SelectRequestedPath(targetIntro, fallbackOrder, modProvides, vanillaExists) is { } introTargetPath)
            pairs.Add(new VariantPair(introSourcePath, introTargetPath, introNamesPath, introSourceFaceLibrary));

        return pairs;
    }

    internal static string PathSignatureFor(IEnumerable<VariantPair> pairs)
        => string.Join("|", pairs.Select(pair => $"{pair.SourceRequestedPath}>{pair.TargetRequestedPath}"));

    internal sealed record RaceBuildInput(string Race, IReadOnlyList<string> FallbackOrder,
        IReadOnlyList<ResolvedVariantPair> Pairs, RaceSourceInput Source);

    internal IReadOnlyList<RaceBuildInput> RaceInputsFor(EmoteAttributes source, EmoteAttributes target,
        string drawnSkeleton)
    {
        var modProvides = Memoized(ForeignModProvides);
        var vanillaExists = Memoized(VanillaExists);
        var resolve = Memoized(ResolveOutsideOwnMod);

        var inputs = new List<RaceBuildInput>();

        foreach (var race in RaceOrderFrom(drawnSkeleton))
        {
            var fallbackOrder = EmotePathHelper.GetFallbackOrder(race);
            var pairs = BuildPairs(source, target, fallbackOrder, modProvides, vanillaExists);

            if (pairs.Count == 0)
                continue;

            var resolved = pairs
                .Select(pair => new ResolvedVariantPair(pair, resolve(pair.SourceRequestedPath)))
                .ToList();

            var main = resolved[0];

            inputs.Add(new RaceBuildInput(race, fallbackOrder, resolved,
                new RaceSourceInput(race, main.ResolvedSourcePath,
                    StampFor(main.Pair.SourceRequestedPath, main.ResolvedSourcePath), PathSignatureFor(pairs))));
        }

        return inputs;
    }

    private static List<string> RaceOrderFrom(string drawnSkeleton)
    {
        var order = new List<string> { drawnSkeleton };

        order.AddRange(EmotePathHelper.AllHumanSkeletons.Where(
            race => !string.Equals(race, drawnSkeleton, StringComparison.OrdinalIgnoreCase)));

        return order;
    }

    private static Func<string, T> Memoized<T>(Func<string, T> probe)
    {
        var known = new Dictionary<string, T>(StringComparer.Ordinal);

        return path => known.TryGetValue(path, out var answer) ? answer : known[path] = probe(path);
    }

    internal RacePaths? PathsFor(EmoteAttributes source, EmoteAttributes target, string skeleton)
    {
        var pairs = PairVariants(source, target, skeleton);

        if (pairs.Count == 0)
            return null;

        return new RacePaths(skeleton,
            pairs.Select(pair => pair.SourceRequestedPath).Distinct(StringComparer.Ordinal).ToList(),
            pairs.Select(pair => pair.TargetRequestedPath).Distinct(StringComparer.Ordinal).ToList());
    }

    // The retargeted paps a written-out mod is made of, keyed by the game path each one is served over. Null
    // when the two emotes share no posture, or when nothing could be retargeted.
    internal IReadOnlyDictionary<string, byte[]>? BuildPlainSwapFiles(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> skeletons)
    {
        var merged = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var skeleton in skeletons)
        {
            if (BuildPlainSwapFilesFor(source, target, skeleton) is not { } files)
                continue;

            foreach (var (gamePath, bytes) in files)
            {
                if (!merged.ContainsKey(gamePath))
                    merged[gamePath] = bytes;
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    private IReadOnlyDictionary<string, byte[]>? BuildPlainSwapFilesFor(EmoteAttributes source, EmoteAttributes target,
        string skeleton)
    {
        var fallbackOrder = EmotePathHelper.GetFallbackOrder(skeleton);
        var pairs = PairVariants(source, target, skeleton);

        if (pairs.Count == 0)
            return null;

        var resolvedPairs = new List<ResolvedVariantPair>(pairs.Count);
        foreach (var pair in pairs)
            resolvedPairs.Add(new ResolvedVariantPair(pair, ResolveOutsideOwnLiveSwap(pair.SourceRequestedPath)));

        var grouped = BuildGroupedFiles(resolvedPairs,
            group => BuildGroupOutput(group, fallbackOrder, composeUniqueNames: false), publishInternalNames: false);

        return grouped.Main == null ? null : grouped.Files;
    }

    // Whether a mod other than our own serves this path. Our own generated mod must not count, or an earlier
    // swap keeps a previous body's chain step alive. Reads only, unlike ResolveOutsideOwnMod.
    private bool ForeignModProvides(string requestedPath)
    {
        var resolved = _penumbra.ResolvePlayerPath(requestedPath);
        return resolved != requestedPath && !_swapMods.IsOwnPath(resolved);
    }

    private string ResolveOutsideOwnMod(string requestedPath)
        => ResolveOutsideOwnModCore(requestedPath, _penumbra.ResolvePlayerPath, _swapMods.IsOwnPath, _swapMods.DeselectAll);

    internal static string ResolveOutsideOwnModCore(string requestedPath, Func<string, string> resolve,
        Func<string, bool> isOwnPath, Action deactivate)
    {
        var resolved = resolve(requestedPath);

        if (!isOwnPath(resolved))
            return resolved;

        deactivate();
        return resolve(requestedPath);
    }

    private string ResolveOutsideOwnLiveSwap(string requestedPath)
    {
        var resolved = _penumbra.ResolvePlayerPath(requestedPath);
        return _swapMods.IsOwnPath(resolved) ? requestedPath : resolved;
    }

    internal static int ComputeAppliedPriority(IReadOnlyCollection<string> requestedPaths, Func<string, string> resolve,
        Func<string, bool> isOwnPath, Func<string, int?> priorityOfWinningMod)
    {
        int? maxWinningPriority = null;

        foreach (var path in requestedPaths)
        {
            var resolved = resolve(path);
            if (resolved == path || isOwnPath(resolved))
                continue;

            var priority = priorityOfWinningMod(resolved) ?? 0;
            if (maxWinningPriority is not { } max || priority > max)
                maxWinningPriority = priority;
        }

        return maxWinningPriority is { } winner ? Math.Max(0, winner + 1) : 0;
    }

    private bool SourceCarriesOwnDistinctIntroFile(EmoteAttributes source, IReadOnlyList<string> fallbackOrder)
    {
        try
        {
            if (source.Intro != IntroKind.Pap || source.IntroRelativePapPath is not { } ownIntro)
                return false;

            if (SelectRequestedPath(ownIntro, fallbackOrder, ForeignModProvides, VanillaExists) is not { } introPath)
                return false;

            var loopVariant = source.Variants.FirstOrDefault(variant => variant.Posture == PostureFlags.Standing)
                ?? source.Variants.FirstOrDefault();

            if (loopVariant == null
                || SelectRequestedPath(loopVariant.RelativePapPath, fallbackOrder, ForeignModProvides, VanillaExists) is not { } loopPath)
            {
                return true;
            }

            return !string.Equals(_penumbra.ResolvePlayerPath(introPath), _penumbra.ResolvePlayerPath(loopPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not read /{source.Command}'s intro shape; treating it as lent.", LogPrefix);
            return false;
        }
    }

    internal static bool IsStaleVulnerableShape(EmotePlayType targetLoopKind, IntroKind targetIntro,
        bool sourceCarriesOwnDistinctIntroFile)
        => targetLoopKind == EmotePlayType.Looped
           && !(targetIntro == IntroKind.Pap && sourceCarriesOwnDistinctIntroFile);

    internal static bool OnDiskShapeMatches(SwapOptionEntry? kept, bool composeUniqueNames)
        => (kept != null && GamePathsOf(kept).Any(UniqueNamePlanner.IsComposedPapPath)) == composeUniqueNames;

    internal static IEnumerable<string> GamePathsOf(SwapOptionEntry kept)
        => kept.FilesByRace.Values.SelectMany(files => files.Keys);

    internal static string SkeletonFor(ICharacter character)
        => CharacterHelper.ResolveSkeletonId(character);

    internal static CharacterHelper.DrawnBody? DrawnBodyFor(ICharacter character)
        => CharacterHelper.GetDrawnBody(character);

    internal static string? DrawnSkeletonFor(ICharacter character)
        => CharacterHelper.GetDrawnSkeletonId(character);

    internal static PostureFlags PostureForCondition(EmoteCondition condition)
        => ActionTimelineSlots.PostureForCondition(condition);

    internal static PostureFlags PostureFromMode(CharacterModes mode, byte modeParam)
        => ActionTimelineSlots.PostureForMode(mode, modeParam);

    private static string? PapPathForTimeline(ushort timelineId)
        => ActionTimelineHelper.GetRelativePapPath(timelineId);
}
