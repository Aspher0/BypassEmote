using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Animations.PapFormat;
using NoireLib.Animations.PapFormat.Tmb;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal sealed record GroupOutput(byte[] Bytes, bool ClampedIntro);

    internal sealed record GroupedSwapFiles(IReadOnlyDictionary<string, byte[]> Files, ResolvedVariantPair? Main,
        bool ClampedIntro);

    internal static GroupedSwapFiles BuildGroupedFiles(IReadOnlyList<ResolvedVariantPair> pairs,
        Func<IReadOnlyList<ResolvedVariantPair>, GroupOutput?> retargetGroup,
        Func<ResolvedVariantPair, byte[], ActionTimelineFile?>? actionTimeline = null)
    {
        var groupsBySource = new Dictionary<string, List<ResolvedVariantPair>>(StringComparer.OrdinalIgnoreCase);
        var groupsInOrder = new List<List<ResolvedVariantPair>>();

        foreach (var pair in pairs)
        {
            if (!groupsBySource.TryGetValue(pair.ResolvedSourcePath, out var members))
            {
                members = [];
                groupsBySource[pair.ResolvedSourcePath] = members;
                groupsInOrder.Add(members);
            }

            members.Add(pair);
        }

        var files = new Dictionary<string, byte[]>(pairs.Count);
        ResolvedVariantPair? main = null;
        var clampedIntro = false;

        foreach (var members in groupsInOrder)
        {
            if (retargetGroup(members) is not { } output)
                continue;

            foreach (var member in members)
            {
                files[member.Pair.TargetRequestedPath] = output.Bytes;

                if (actionTimeline?.Invoke(member, output.Bytes) is { } timeline)
                    files.TryAdd(timeline.GamePath, timeline.Bytes);
            }

            main ??= members[0];
            clampedIntro |= output.ClampedIntro;
        }

        return new GroupedSwapFiles(files, main, clampedIntro);
    }

    internal static bool OutputFadeProtected(IReadOnlyList<ResolvedVariantPair> pairs, GroupedSwapFiles grouped)
    {
        if (grouped.ClampedIntro)
            return false;

        foreach (var pair in pairs)
        {
            if (pair.Pair.RequiredNamesPath != null && grouped.Files.ContainsKey(pair.Pair.TargetRequestedPath))
                return true;
        }

        return false;
    }

    private static byte[]? BuildRetargetedPap(VariantPair pair, string resolvedSourcePath,
        IReadOnlyList<string>? requiredNamesOverride = null, bool? holdOffHand = null,
        string? resolvedSourceTimeline = null)
    {
        if (ReadPap(pair.SourceRequestedPath, resolvedSourcePath) is not { } sourceBytes)
        {
            Log.Debug($"No readable source pap for '{pair.SourceRequestedPath}' (resolved to '{resolvedSourcePath}').", LogPrefix);
            return null;
        }

        if (ServedByAMod(pair, resolvedSourcePath)
            && AnimationsAskedFor(pair.SourceRequestedPath, resolvedSourceTimeline) is { } askedFor
            && NothingPlaysFrom(PapAnimationNames.Read(sourceBytes), askedFor))
        {
            Log.Debug($"Pap served unchanged on '{pair.TargetRequestedPath}': '{resolvedSourcePath}' has none of "
                + $"{string.Join(", ", askedFor)}", LogPrefix);

            return sourceBytes;
        }

        var requiredNames = requiredNamesOverride;
        if (requiredNames == null)
        {
            var namesPath = pair.RequiredNamesPath ?? pair.TargetRequestedPath;
            if (ReadVanillaNamesForNamesPath(namesPath) is not { } targetNames)
            {
                Log.Debug($"No vanilla target pap at '{namesPath}' to read required names from.", LogPrefix);
                return null;
            }

            requiredNames = targetNames;
        }

        if (requiredNames.Count == 0)
        {
            Log.Debug($"No animation names to retarget '{pair.SourceRequestedPath}' onto '{pair.TargetRequestedPath}' with.", LogPrefix);
            return null;
        }

        byte[] retargeted;
        try
        {
            retargeted = PapRetargeter.Retarget(sourceBytes, requiredNames, removeAnimationLock: true, out _);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Retargeting '{pair.SourceRequestedPath}' onto '{pair.TargetRequestedPath}' produced an unusable pap. Variant skipped.", LogPrefix);
            return null;
        }

        return ApplyWeaponHold(
            ApplyFaceLibrary(retargeted, pair.SourceFaceLibrary, pair.TargetRequestedPath, PapFaceLibrary.Inject),
            pair, holdOffHand, ServedByAMod(pair, resolvedSourcePath));
    }

    internal static bool ServedByAMod(VariantPair pair, string resolvedSourcePath)
        => !string.Equals(resolvedSourcePath, pair.SourceRequestedPath, StringComparison.Ordinal);

    private static readonly IReadOnlySet<string> AnimationClipMagic =
        new HashSet<string>(StringComparer.Ordinal) { "C009", "C010" };

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>?> VanillaAskedByTimeline =
        new(StringComparer.Ordinal);

    internal static bool NothingPlaysFrom(IReadOnlyList<string> papNames, IReadOnlyList<string> askedFor)
        => !papNames.Any(name => askedFor.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string>? AnimationsAskedFor(string sourceRequestedPath, string? resolvedTimeline)
    {
        if (resolvedTimeline == null || ActionTimelinePathFor(sourceRequestedPath) is not { } requestedTimeline)
            return null;

        if (resolvedTimeline == requestedTimeline)
        {
            return VanillaAskedByTimeline.GetOrAdd(requestedTimeline, static path =>
                NoireService.DataManager.FileExists(path) && NoireService.DataManager.GetFile(path)?.Data is { } vanilla
                    ? AnimationsAskedBy(vanilla)
                    : null);
        }

        try
        {
            return AnimationsAskedBy(File.ReadAllBytes(resolvedTimeline));
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to read tmb '{resolvedTimeline}': {ex.Message}", LogPrefix);
            return null;
        }
    }

    private static IReadOnlyList<string>? AnimationsAskedBy(byte[] timelineBytes)
    {
        var names = TmbEntryScanner.ScanTmb(timelineBytes, AnimationClipMagic)
            .Select(entry => entry.Path)
            .OfType<string>()
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return names.Count > 0 ? names : null;
    }

    private static byte[] HoldWeapons(byte[] papBytes, bool offHand)
        => PapWeaponHold.Apply(papBytes, offHand, SwapLayers.WeaponStowAtEnd, SwapLayers.WeaponTravelAnimation);

    internal static byte[]? ApplyWeaponHold(byte[]? papBytes, VariantPair pair, bool? holdOffHand, bool sourceIsModded,
        Func<byte[], bool, byte[]>? hold = null)
    {
        if (papBytes == null || holdOffHand is not { } offHand || !pair.WeaponMotion)
            return papBytes;

        if (sourceIsModded)
        {
            Log.Debug($"'{pair.SourceRequestedPath}' comes from a mod. Weapon hold left to its own timeline.", LogPrefix);

            return papBytes;
        }

        try
        {
            var held = (hold ?? HoldWeapons)(papBytes, offHand);

            var statements = EntryCount(held, WeaponPositionMagic);

            Log.Debug($"Weapons put in hand for '{pair.TargetRequestedPath}': {statements} "
                + $"statement(s), {(offHand ? "two weapons" : "one weapon")}.", LogPrefix);

            if (statements == 0)
            {
                Log.Warning($"'{pair.TargetRequestedPath}' came back with no weapon statement. "
                    + "Weapons stay where the game last put them.", LogPrefix);
            }

            return held;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not put the weapons in hand for '{pair.TargetRequestedPath}'. "
                + "Swap served without them.", LogPrefix);

            return papBytes;
        }
    }

    private const string WeaponPositionMagic = "C014";

    private static int EntryCount(byte[] papBytes, string magic)
    {
        try
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal) { magic };

            return TmbEntryScanner.ScanPap(papBytes, wanted).Count;
        }
        catch
        {
            return -1;
        }
    }

    internal static Func<IReadOnlyList<ResolvedVariantPair>, GroupOutput?> RetargetingOncePerInput(
        Dictionary<string, GroupOutput?> built, bool? holdOffHand = null)
        => group =>
        {
            var key = GroupInputKey(group, ReadVanillaNamesForNamesPath);

            if (!built.TryGetValue(key, out var bare))
            {
                bare = BuildGroupOutput(group, holdOffHand);
                built[key] = bare;
            }

            return bare is { } output ? output : null;
        };

    internal static string GroupInputKey(IReadOnlyList<ResolvedVariantPair> group,
        Func<string, IReadOnlyList<string>?>? namesOf = null)
        => string.Join("|", group.Select(member =>
        {
            var namesPath = member.Pair.RequiredNamesPath ?? member.Pair.TargetRequestedPath;
            var names = namesOf?.Invoke(namesPath) is { Count: > 0 } read ? string.Join(",", read) : namesPath;

            return $"{member.ResolvedSourcePath}{(ServedByAMod(member.Pair, member.ResolvedSourcePath) ? " (mod)" : string.Empty)}"
                + $">{(member.Pair.RequiredNamesPath != null ? "lent " : string.Empty)}{names}"
                + $">{member.Pair.SourceFaceLibrary}{(member.Pair.WeaponMotion ? " (weapon)" : string.Empty)}";
        }));

    private static GroupOutput? BuildGroupOutput(IReadOnlyList<ResolvedVariantPair> group, bool? holdOffHand = null)
    {
        if (group.Count == 1)
            return BuildRetargetedPap(group[0].Pair, group[0].ResolvedSourcePath, holdOffHand: holdOffHand,
                resolvedSourceTimeline: group[0].ResolvedSourceTimeline) is { } bytes
                ? new GroupOutput(bytes, ClampedIntro: false)
                : null;

        return BuildSharedGroupPap(group, holdOffHand);
    }

    private static GroupOutput? BuildSharedGroupPap(IReadOnlyList<ResolvedVariantPair> group, bool? holdOffHand = null)
    {
        var lead = group[0];

        if (ReadPap(lead.Pair.SourceRequestedPath, lead.ResolvedSourcePath) is not { } sourceBytes)
        {
            Log.Debug($"No readable source pap for group led by '{lead.Pair.SourceRequestedPath}' (resolved to '{lead.ResolvedSourcePath}').", LogPrefix);
            return null;
        }

        var sourceNames = PapAnimationNames.Read(sourceBytes);

        if (group.All(member => ServedByAMod(member.Pair, member.ResolvedSourcePath)
                && AnimationsAskedFor(member.Pair.SourceRequestedPath, member.ResolvedSourceTimeline) is { } askedFor
                && NothingPlaysFrom(sourceNames, askedFor)))
        {
            Log.Debug($"Pap served unchanged on '{lead.Pair.TargetRequestedPath}': '{lead.ResolvedSourcePath}' has "
                + "none of the animations its tmbs ask for", LogPrefix);

            return new GroupOutput(sourceBytes, ClampedIntro: false);
        }

        var union = UnionRequiredNames(group, ReadVanillaNamesForNamesPath);
        if (union.Names.Count == 0)
        {
            Log.Debug($"No animation names to retarget the group led by '{lead.Pair.SourceRequestedPath}' onto.", LogPrefix);
            return null;
        }

        byte[] retargeted;
        var clampedNames = new List<string>();

        try
        {
            retargeted = PapRetargeter.RetargetToNames(sourceBytes, union.Names, removeAnimationLock: true, out _,
                oneFrameWhenLentNames: union.OneFrameWhenLentNames, clampedNames: clampedNames);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Retargeting the group led by '{lead.Pair.SourceRequestedPath}' produced an unusable pap. Group skipped.", LogPrefix);
            return null;
        }

        Log.Debug(
            $"Served '{lead.Pair.TargetRequestedPath}' from '{lead.ResolvedSourcePath}': "
            + $"{FootstepEntryCount(retargeted)} footstep entr(y/ies), {clampedNames.Count} name(s) clamped.",
            LogPrefix);

        return ApplyWeaponHold(ApplyFaceLibrary(retargeted, lead.Pair.SourceFaceLibrary, lead.Pair.TargetRequestedPath, PapFaceLibrary.Inject), lead.Pair, holdOffHand, ServedByAMod(lead.Pair, lead.ResolvedSourcePath)) is { } withFace
            ? new GroupOutput(withFace, ClampedIntro: clampedNames.Count != 0)
            : null;
    }

    private static readonly IReadOnlySet<string> FootstepMagic = new HashSet<string>(StringComparer.Ordinal) { "C042" };

    private static readonly IReadOnlySet<string> FaceLibraryMagic = new HashSet<string>(StringComparer.Ordinal) { "TMPP" };

    private static int FootstepEntryCount(byte[] papBytes)
        => TmbEntryScanner.ScanPap(papBytes, FootstepMagic).Count;

    internal static bool EveryTimelineDeclaresAFaceLibrary(byte[] papBytes)
    {
        if (papBytes.Length < 26 || BinaryPrimitives.ReadInt32LittleEndian(papBytes) != 0x20706170)
            return false;

        var timelines = BinaryPrimitives.ReadInt16LittleEndian(papBytes.AsSpan(8, 2));

        return timelines > 0 && TmbEntryScanner.ScanPap(papBytes, FaceLibraryMagic).Count >= timelines;
    }

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>?> VanillaNamesByPath =
        new(StringComparer.Ordinal);

    private static IReadOnlyList<string>? ReadVanillaNamesForNamesPath(string namesPath)
        => VanillaNamesByPath.GetOrAdd(namesPath,
            static path => ReadVanillaPap(path) is { } vanillaBytes ? PapAnimationNames.Read(vanillaBytes) : null);

    internal sealed record UnionedNames(List<string> Names, HashSet<string> OneFrameWhenLentNames);

    internal static UnionedNames UnionRequiredNames(IReadOnlyList<ResolvedVariantPair> group,
        Func<string, IReadOnlyList<string>?> readVanillaNames)
    {
        var union = new List<string>();
        var oneFrameWhenLent = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in group)
        {
            var isIntroMember = member.Pair.RequiredNamesPath != null;

            var namesPath = member.Pair.RequiredNamesPath ?? member.Pair.TargetRequestedPath;

            if (readVanillaNames(namesPath) is not { } names)
                continue;

            foreach (var name in names)
            {
                if (!seen.Add(name))
                    continue;

                union.Add(name);

                if (isIntroMember)
                    oneFrameWhenLent.Add(name);
            }
        }

        return new UnionedNames(union, oneFrameWhenLent);
    }

    internal static byte[]? ApplyFaceLibrary(byte[] retargetedBytes, string? sourceFaceLibrary,
        string targetRequestedPath, Func<byte[], string, byte[]> inject)
    {
        if (sourceFaceLibrary is not { } faceLibrary || EveryTimelineDeclaresAFaceLibrary(retargetedBytes))
            return retargetedBytes;

        try
        {
            return inject(retargetedBytes, faceLibrary);
        }
        catch (Exception ex)
        {
            Log.Debug($"Injecting face library '{faceLibrary}' into the pap for '{targetRequestedPath}' failed ({ex.Message}). Variant skipped.", LogPrefix);
            return null;
        }
    }

    private static byte[]? ReadPap(string requestedPath, string resolvedPath)
    {
        if (resolvedPath == requestedPath)
            return ReadVanillaPap(requestedPath);

        try
        {
            return File.ReadAllBytes(resolvedPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not read the modded pap at '{resolvedPath}'.", LogPrefix);
            return null;
        }
    }

    private static byte[]? ReadVanillaPap(string gamePath)
    {
        if (!NoireService.DataManager.FileExists(gamePath))
            return null;

        var data = NoireService.DataManager.GetFile(gamePath)?.Data;
        return data is { Length: > 0 } ? data : null;
    }

    private static long StampFor(string requestedPath, string resolvedPath)
    {
        if (resolvedPath == requestedPath)
            return 0;

        try
        {
            return File.GetLastWriteTimeUtc(resolvedPath).Ticks;
        }
        catch (Exception ex)
        {
            Log.Debug($"Could not stamp '{resolvedPath}' ({ex.Message}). This swap will not be reused.", LogPrefix);
            return DateTime.UtcNow.Ticks;
        }
    }

    private static bool VanillaExists(string gamePath) => NoireService.DataManager.FileExists(gamePath);

    private static readonly string[] WarmUpCandidateRelativePaths =
    [
        IdlePoseData.ResidentIdleRelativePapPath,
        "bt_common/emote/sit.pap",
        "bt_common/emote/pose01_loop.pap",
    ];

    private const string WarmUpSkeleton = "c0101";

    private const string WarmUpFaceLibrary = "chara/human/c0101/animation/f0001/nonresident/warmup.tmb";

    internal static void WarmUpBytePipeline()
    {
        try
        {
            var papBytes = ReadFirstWarmUpPap();

            if (papBytes == null)
            {
                Log.Debug("No vanilla pap to warm the byte pipeline with. Skipped.", LogPrefix);
                return;
            }

            var warmUpClock = Stopwatch.StartNew();

            var names = PapAnimationNames.Read(papBytes);

            if (names.Count == 0)
            {
                Log.Debug("The warm-up pap declares no animation names. Skipped.", LogPrefix);
                return;
            }

            var retargeted = PapRetargeter.Retarget(papBytes, names, removeAnimationLock: true, out _);
            var injected = PapFaceLibrary.Inject(retargeted, WarmUpFaceLibrary);
            var derivedName = SwapModManager.DeriveFileName(injected);

            Log.Debug(
                $"Warmed the byte pipeline in {warmUpClock.ElapsedMilliseconds}ms ({injected.Length} bytes, {derivedName}).",
                LogPrefix);
        }
        catch (Exception ex)
        {
            Log.Debug($"Warming the byte pipeline failed ({ex.Message}).", LogPrefix);
        }
    }

    private static byte[]? ReadFirstWarmUpPap()
    {
        foreach (var relativePath in WarmUpCandidateRelativePaths)
        {
            if (ReadVanillaPap(EmotePathHelper.GetSkeletonPath(WarmUpSkeleton, relativePath)) is { } bytes)
                return bytes;
        }

        return null;
    }
}
