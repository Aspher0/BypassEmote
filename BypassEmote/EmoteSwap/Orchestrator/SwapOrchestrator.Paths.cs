using BypassEmote.Enums;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal readonly record struct ResolvedVariantPair(VariantPair Pair, string ResolvedSourcePath,
        string? ResolvedSourceTimeline = null);

    private const string AnimationFolder = "/animation/a0001/";

    internal static string? ActionTimelinePathFor(string papPath)
    {
        var start = papPath.IndexOf(AnimationFolder, StringComparison.OrdinalIgnoreCase);
        var relative = start < 0 ? papPath : papPath[(start + AnimationFolder.Length)..];
        var slash = relative.IndexOf('/');

        if (slash < 0 || !relative.EndsWith(".pap", StringComparison.OrdinalIgnoreCase))
            return null;

        return $"chara/action/{relative[(slash + 1)..^4]}.tmb";
    }

    internal readonly record struct VariantPair(string SourceRequestedPath, string TargetRequestedPath,
        string? RequiredNamesPath = null, string? SourceFaceLibrary = null, bool WeaponMotion = false,
        bool LentSource = false);

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
        var lent = false;

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
            lent = true;
        }

        if (sourcePath == null)
            return;

        if (SelectRequestedPath(targetAdjust, fallbackOrder, modProvides, vanillaExists) is { } targetPath)
            pairs.Add(new VariantPair(sourcePath, targetPath, namesPath, sourceFaceLibrary, sourceWeaponMotion, lent));
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
        var introLent = false;

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
            introLent = true;
        }

        if (introSourcePath == null)
            return;

        if (SelectRequestedPath(targetIntro, fallbackOrder, modProvides, vanillaExists) is { } introTargetPath)
            pairs.Add(new VariantPair(introSourcePath, introTargetPath, introNamesPath, introSourceFaceLibrary,
                introSourceWeaponMotion, introLent));
    }

    internal static string PathSignatureFor(IEnumerable<VariantPair> pairs)
        => string.Join("|", pairs.Select(pair => $"{pair.SourceRequestedPath}>{pair.TargetRequestedPath}"));

    internal sealed record RaceBuildInput(string Race, IReadOnlyList<string> FallbackOrder,
        IReadOnlyList<ResolvedVariantPair> Pairs, RaceSourceInput Source);

    internal IReadOnlyList<RaceBuildInput> RaceInputsFor(EmoteAttributes source, EmoteAttributes target,
        string drawnSkeleton)
    {
        var races = RaceOrderFrom(drawnSkeleton);
        var (modProvides, resolve) = BatchedResolvers([source, target], races);
        var vanillaExists = Memoized(VanillaExists);

        var inputs = new List<RaceBuildInput>();

        foreach (var race in races)
        {
            var fallbackOrder = EmotePathHelper.GetFallbackOrder(race);
            var pairs = BuildPairs(source, target, fallbackOrder, modProvides, vanillaExists);

            if (pairs.Count == 0)
                continue;

            var resolved = pairs
                .Select(pair => new ResolvedVariantPair(pair, resolve(pair.SourceRequestedPath),
                    ActionTimelinePathFor(pair.SourceRequestedPath) is { } timeline ? resolve(timeline) : null))
                .ToList();

            var main = resolved[0];

            inputs.Add(new RaceBuildInput(race, fallbackOrder, resolved,
                new RaceSourceInput(race, main.ResolvedSourcePath,
                    StampFor(main.Pair.SourceRequestedPath, main.ResolvedSourcePath),
                    PathSignatureFor(pairs) + TimelineSignatureFor(resolved))));
        }

        return inputs;
    }

    internal static string TimelineSignatureFor(IEnumerable<ResolvedVariantPair> pairs)
        => string.Concat(pairs
            .Where(pair => pair.ResolvedSourceTimeline is { } resolved
                && ActionTimelinePathFor(pair.Pair.SourceRequestedPath) is { } requested
                && !string.Equals(resolved, requested, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.ResolvedSourceTimeline!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(resolved => $"|tmb={resolved}:{StampFor(string.Empty, resolved)}"));

    private static List<string> RaceOrderFrom(string drawnSkeleton)
    {
        var order = new List<string> { drawnSkeleton };

        order.AddRange(EmotePathHelper.AllHumanSkeletons.Where(
            race => !string.Equals(race, drawnSkeleton, StringComparison.OrdinalIgnoreCase)));

        return order;
    }

    private (Func<string, bool> ModProvides, Func<string, string> Resolve) BatchedResolvers(
        IReadOnlyList<EmoteAttributes> emotes, IReadOnlyList<string> races)
    {
        var relativePaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var emote in emotes)
        {
            foreach (var variant in emote.Variants)
                relativePaths.Add(variant.RelativePapPath);

            if (emote.IntroRelativePapPath is { } intro)
                relativePaths.Add(intro);

            if (emote.AdjustRelativePapPath is { } adjust)
                relativePaths.Add(adjust);
        }

        var paths = races
            .SelectMany(race => EmotePathHelper.GetFallbackOrder(race))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(step => relativePaths.Select(relative => EmotePathHelper.GetSkeletonPath(step, relative)))
            .Concat(relativePaths.Select(ActionTimelinePathFor).OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (_penumbra.ResolvePlayerPaths(paths) is not { } resolved)
            return (Memoized(ForeignModProvides), Memoized(ResolveOutsideOwnMod));

        var resolvedByPath = new Dictionary<string, string>(paths.Count, StringComparer.Ordinal);

        for (var index = 0; index < paths.Count; index++)
            resolvedByPath[paths[index]] = resolved[index];

        bool ModProvides(string path)
            => resolvedByPath.TryGetValue(path, out var served)
                ? served != path && !_swapMods.IsOwnPath(served)
                : ForeignModProvides(path);

        string Resolve(string path)
            => resolvedByPath.TryGetValue(path, out var served) && (served == path || !_swapMods.IsOwnPath(served))
                ? served
                : ResolveOutsideOwnMod(path);

        return (Memoized(ModProvides), Memoized(Resolve));
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
        var pairs = PairVariants(source, target, skeleton);

        if (pairs.Count == 0)
            return null;

        var resolvedPairs = new List<ResolvedVariantPair>(pairs.Count);
        foreach (var pair in pairs)
            resolvedPairs.Add(new ResolvedVariantPair(pair, ResolveOutsideOwnLiveSwap(pair.SourceRequestedPath),
                ActionTimelinePathFor(pair.SourceRequestedPath) is { } timeline ? ResolveOutsideOwnLiveSwap(timeline) : null));

        var grouped = BuildGroupedFiles(resolvedPairs, group => BuildGroupOutput(group),
            ActionTimelinesFor(source, target));

        if (grouped.Main == null)
            return null;

        var modded = resolvedPairs.Any(pair => !string.Equals(pair.ResolvedSourcePath, pair.Pair.SourceRequestedPath,
            StringComparison.OrdinalIgnoreCase));

        return new PlainRaceFiles(skeleton, modded, grouped.Files);
    }

    internal bool ForeignModServes(string requestedPath) => ForeignModProvides(requestedPath);

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
            Log.Error(ex, $"Could not read /{source.Command}'s intro shape. Treated as lent.", LogPrefix);
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

    internal static string SkeletonFor(ICharacter character)
        => CharacterHelper.ResolveSkeletonId(character);

    internal static CharacterHelper.DrawnBody? DrawnBodyFor(ICharacter character)
        => CharacterHelper.GetDrawnBody(character);

    internal static PostureFlags PostureForCondition(EmoteCondition condition)
        => ActionTimelineSlots.PostureForCondition(condition);

    internal static PostureFlags PostureFromMode(CharacterModes mode, byte modeParam)
        => ActionTimelineSlots.PostureForMode(mode, modeParam);

    private static string? PapPathForTimeline(ushort timelineId)
        => ActionTimelineHelper.GetRelativePapPath(timelineId);
}
