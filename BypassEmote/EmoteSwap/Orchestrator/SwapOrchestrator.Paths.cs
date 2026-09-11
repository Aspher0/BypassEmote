using BypassEmote.Enums;
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
        string? RequiredNamesPath = null, string? SourceFaceLibrary = null, bool WeaponMotion = false);

    internal static string? SelectRequestedPath(string relativePapPath, IReadOnlyList<string> fallbackSkeletons,
        Func<string, bool> modProvides, Func<string, bool> vanillaExists)
        => EmotePathHelper.FindExistingPath(relativePapPath, fallbackSkeletons, modProvides, vanillaExists);

    private List<VariantPair> PairVariants(EmoteAttributes source, EmoteAttributes target, string skeleton)
        => BuildPairs(source, target, EmotePathHelper.GetFallbackOrder(skeleton), ForeignModProvides, VanillaExists);

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

            pairs.Add(new VariantPair(sourcePath, targetPath,
                SourceFaceLibrary: source.FaceLibraryFor(sourceVariant.RelativePapPath),
                WeaponMotion: sourceVariant.WeaponMotion));
        }

        if (pairs.Count == 0)
            return pairs;

        AppendAdjustPair(source, target, fallbackOrder, modProvides, vanillaExists, pairs);
        AppendIntroPair(source, target, fallbackOrder, modProvides, vanillaExists, pairs);

        return pairs;
    }

    private static void AppendAdjustPair(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> fallbackOrder, Func<string, bool> modProvides, Func<string, bool> vanillaExists,
        List<VariantPair> pairs)
    {
        if (target.AdjustRelativePapPath is not { } targetAdjust)
            return;

        if (SelectRequestedPath(targetAdjust, fallbackOrder, static _ => false, vanillaExists) is not { } namesPath)
            return;

        string? sourcePath = null;
        string? sourceFaceLibrary = null;
        var sourceWeaponMotion = false;

        if (source.AdjustRelativePapPath is { } ownAdjust
            && SelectRequestedPath(ownAdjust, fallbackOrder, modProvides, vanillaExists) is { } ownAdjustPath)
        {
            sourcePath = ownAdjustPath;
            sourceFaceLibrary = source.FaceLibraryFor(ownAdjust);
        }
        else if (source.Variants.FirstOrDefault(variant => variant.Posture == PostureFlags.Mounted) is { } upperBody
            && SelectRequestedPath(upperBody.RelativePapPath, fallbackOrder, modProvides, vanillaExists) is { } upperBodyPath)
        {
            sourcePath = upperBodyPath;
            sourceFaceLibrary = source.FaceLibraryFor(upperBody.RelativePapPath);
            sourceWeaponMotion = upperBody.WeaponMotion;
        }

        if (sourcePath == null)
            return;

        if (SelectRequestedPath(targetAdjust, fallbackOrder, modProvides, vanillaExists) is { } targetPath)
            pairs.Add(new VariantPair(sourcePath, targetPath, namesPath, sourceFaceLibrary, sourceWeaponMotion));
    }

    private static void AppendIntroPair(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> fallbackOrder, Func<string, bool> modProvides, Func<string, bool> vanillaExists,
        List<VariantPair> pairs)
    {
        if (target.IntroRelativePapPath is not { } targetIntro)
            return;

        if (SelectRequestedPath(targetIntro, fallbackOrder, static _ => false, vanillaExists) is not { } introNamesPath)
            return;

        string? introSourcePath = null;
        string? introSourceFaceLibrary = null;
        var introSourceWeaponMotion = false;

        if (source.IntroRelativePapPath is { } ownIntro
            && SelectRequestedPath(ownIntro, fallbackOrder, modProvides, vanillaExists) is { } ownIntroPath)
        {
            introSourcePath = ownIntroPath;
            introSourceFaceLibrary = source.FaceLibraryFor(ownIntro);
            introSourceWeaponMotion = source.IntroIsWeaponMotion;
        }
        else if ((source.Variants.FirstOrDefault(variant => variant.Posture == PostureFlags.Standing)
                ?? source.Variants.FirstOrDefault()) is { } lentVariant
            && SelectRequestedPath(lentVariant.RelativePapPath, fallbackOrder, modProvides, vanillaExists) is { } lentPath)
        {
            introSourcePath = lentPath;
            introSourceFaceLibrary = source.FaceLibraryFor(lentVariant.RelativePapPath);
            introSourceWeaponMotion = lentVariant.WeaponMotion;
        }

        if (introSourcePath == null)
            return;

        if (SelectRequestedPath(targetIntro, fallbackOrder, modProvides, vanillaExists) is { } introTargetPath)
            pairs.Add(new VariantPair(introSourcePath, introTargetPath, introNamesPath, introSourceFaceLibrary,
                introSourceWeaponMotion));
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

    internal sealed record PlainRaceFiles(string Skeleton, bool SourceIsModded,
        IReadOnlyDictionary<string, byte[]> Files);

    // The retargeted paps a written-out mod is made of, keyed by the game path each one is served over. Null
    // when the two emotes share no posture, or when nothing could be retargeted.
    internal IReadOnlyDictionary<string, byte[]>? BuildPlainSwapFiles(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> skeletons, string? ownSkeleton = null)
    {
        var built = new List<PlainRaceFiles>(skeletons.Count);

        foreach (var skeleton in skeletons)
        {
            if (BuildPlainSwapFilesFor(source, target, skeleton) is { } race)
                built.Add(race);
        }

        var merged = MergePlainRaceFiles(built, ownSkeleton);

        return merged.Count > 0 ? merged : null;
    }

    internal static IReadOnlyDictionary<string, byte[]> MergePlainRaceFiles(IReadOnlyList<PlainRaceFiles> races,
        string? ownSkeleton)
    {
        var merged = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        var ordered = RaceCoveragePlanner.ByCoveragePreference(races,
            race => race.SourceIsModded,
            race => string.Equals(race.Skeleton, ownSkeleton, StringComparison.OrdinalIgnoreCase));

        foreach (var race in ordered)
        {
            foreach (var (gamePath, bytes) in race.Files)
                merged.TryAdd(gamePath, bytes);
        }

        return merged;
    }

    private PlainRaceFiles? BuildPlainSwapFilesFor(EmoteAttributes source, EmoteAttributes target, string skeleton)
    {
        var fallbackOrder = EmotePathHelper.GetFallbackOrder(skeleton);
        var pairs = PairVariants(source, target, skeleton);

        if (pairs.Count == 0)
            return null;

        var resolvedPairs = new List<ResolvedVariantPair>(pairs.Count);
        foreach (var pair in pairs)
            resolvedPairs.Add(new ResolvedVariantPair(pair, ResolveOutsideOwnLiveSwap(pair.SourceRequestedPath)));

        var grouped = BuildGroupedFiles(resolvedPairs, group => BuildGroupOutput(group, fallbackOrder));

        if (grouped.Main == null)
            return null;

        var modded = resolvedPairs.Any(pair => !string.Equals(pair.ResolvedSourcePath, pair.Pair.SourceRequestedPath,
            StringComparison.OrdinalIgnoreCase));

        return new PlainRaceFiles(skeleton, modded, grouped.Files);
    }

    internal bool ForeignModServes(string requestedPath) => ForeignModProvides(requestedPath);

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
            Log.Error(ex, $"Could not read /{source.Command}'s intro shape; treating it as lent.", LogPrefix);
            return false;
        }
    }

    internal static bool IsStaleVulnerableShape(EmotePlayType targetLoopKind, IntroKind targetIntro,
        bool sourceCarriesOwnDistinctIntroFile)
        => targetLoopKind == EmotePlayType.Looped
           && !(targetIntro == IntroKind.Pap && sourceCarriesOwnDistinctIntroFile);

    internal static bool OnDiskShapeMatches(SwapOptionEntry? kept)
    {
        if (kept == null)
            return false;

        return GamePathsOf(kept).Any();
    }

    internal static IEnumerable<string> GamePathsOf(SwapOptionEntry kept)
        => kept.FilesByRace.Values.SelectMany(files => files.Keys);

    internal IReadOnlyList<string> VanillaNamePathsFor(EmoteAttributes emote, IReadOnlyList<string> fallbackOrder)
    {
        var paths = new List<string>(emote.Variants.Count + 1);

        void Add(string relative)
        {
            if (SelectRequestedPath(relative, fallbackOrder, static _ => false, VanillaExists) is { } path
                && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        foreach (var variant in emote.Variants)
            Add(variant.RelativePapPath);

        if (emote.IntroRelativePapPath is { } intro)
            Add(intro);

        return paths;
    }

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
