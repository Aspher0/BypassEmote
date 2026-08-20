using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Animations.PapFormat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal sealed record GroupOutput(byte[] Bytes, bool ClampedIntro,
        IReadOnlyList<string>? ExtraOutputPaths = null,
        IReadOnlyDictionary<string, string>? UniqueNameByKey = null,
        bool UniqueNamesApplied = false);

    internal sealed record GroupedSwapFiles(IReadOnlyDictionary<string, byte[]> Files, ResolvedVariantPair? Main,
        bool ClampedIntro, bool UniqueNames = false,
        IReadOnlyDictionary<string, string>? UniqueNameByKey = null,
        bool InternalUniqueNames = false,
        IReadOnlyList<string>? InternalNames = null);

    internal static GroupedSwapFiles BuildGroupedFiles(IReadOnlyList<ResolvedVariantPair> pairs,
        Func<IReadOnlyList<ResolvedVariantPair>, GroupOutput?> retargetGroup, bool publishInternalNames = true)
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
        var uniqueNames = true;
        var uniqueNameByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var internalNames = new List<string>();
        var internalNamesSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var members in groupsInOrder)
        {
            if (retargetGroup(members) is not { } output)
                continue;

            foreach (var member in members)
                files[member.Pair.TargetRequestedPath] = output.Bytes;

            if (publishInternalNames)
            {
                foreach (var internalName in PapAnimationNames.Read(output.Bytes))
                {
                    if (internalNamesSeen.Add(internalName))
                        internalNames.Add(internalName);
                }
            }

            foreach (var composedPath in output.ExtraOutputPaths ?? [])
                files[composedPath] = output.Bytes;

            foreach (var (timelineKey, uniqueName) in output.UniqueNameByKey ?? EmptyUniqueNames)
                uniqueNameByKey[timelineKey] = uniqueName;

            main ??= members[0];
            clampedIntro |= output.ClampedIntro;
            uniqueNames &= output.UniqueNamesApplied;
        }

        return new GroupedSwapFiles(files, main, clampedIntro,
            UniqueNames: main != null && uniqueNames,
            UniqueNameByKey: uniqueNameByKey.Count > 0 ? uniqueNameByKey : null,
            InternalUniqueNames: main != null && internalNames.Count > 0,
            InternalNames: internalNames.Count > 0 ? internalNames : null);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyUniqueNames = new Dictionary<string, string>();

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
        IReadOnlyList<string>? requiredNamesOverride = null)
    {
        if (ReadPap(pair.SourceRequestedPath, resolvedSourcePath) is not { } sourceBytes)
        {
            NoireLogger.LogDebug($"No readable source pap for '{pair.SourceRequestedPath}' (resolved to '{resolvedSourcePath}').", LogPrefix);
            return null;
        }

        var requiredNames = requiredNamesOverride;
        if (requiredNames == null)
        {
            var namesPath = pair.RequiredNamesPath ?? pair.TargetRequestedPath;
            if (ReadVanillaPap(namesPath) is not { } targetVanillaBytes)
            {
                NoireLogger.LogDebug($"No vanilla target pap at '{namesPath}' to read required names from.", LogPrefix);
                return null;
            }

            requiredNames = PapAnimationNames.Read(targetVanillaBytes);
        }

        if (requiredNames.Count == 0)
        {
            NoireLogger.LogDebug($"No animation names to retarget '{pair.SourceRequestedPath}' onto '{pair.TargetRequestedPath}' with.", LogPrefix);
            return null;
        }

        byte[] retargeted;
        try
        {
            retargeted = PapRetargeter.Retarget(sourceBytes, requiredNames, removeAnimationLock: true, out _);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Retargeting '{pair.SourceRequestedPath}' onto '{pair.TargetRequestedPath}' produced an unusable pap; variant skipped.", LogPrefix);
            return null;
        }

        return ApplyFaceLibrary(retargeted, pair.SourceFaceLibrary, pair.TargetRequestedPath, PapFaceLibrary.Inject);
    }

    private static GroupOutput? BuildGroupOutput(IReadOnlyList<ResolvedVariantPair> group,
        IReadOnlyList<string> fallbackOrder, bool composeUniqueNames)
    {
        if (group.Count == 1)
            return BuildRetargetedPap(group[0].Pair, group[0].ResolvedSourcePath) is { } bytes
                ? WithUniqueNames(new GroupOutput(bytes, ClampedIntro: false), group, fallbackOrder, composeUniqueNames)
                : null;

        return BuildSharedGroupPap(group, fallbackOrder, composeUniqueNames);
    }

    internal static GroupOutput WithUniqueNames(GroupOutput output, IReadOnlyList<ResolvedVariantPair> group,
        IReadOnlyList<string> fallbackOrder, bool composeUniqueNames = true)
    {
        if (!composeUniqueNames)
            return output;

        var contentTag = UniqueNamePlanner.ContentTagFor(output.Bytes);
        var uniqueNameByKey = new Dictionary<string, string>(group.Count, StringComparer.Ordinal);
        var composedPaths = new List<string>();
        var allCovered = group.Count > 0;

        foreach (var member in group)
            allCovered &= TryPlanMemberName(member.Pair.TargetRequestedPath, member.Pair.SourceRequestedPath,
                fallbackOrder, contentTag, uniqueNameByKey, composedPaths);

        return output with
        {
            ExtraOutputPaths = composedPaths.Count > 0 ? composedPaths : null,
            UniqueNameByKey = uniqueNameByKey.Count > 0 ? uniqueNameByKey : null,
            UniqueNamesApplied = allCovered,
        };
    }

    private static bool TryPlanMemberName(string targetRequestedPath, string sourceRequestedPath,
        IReadOnlyList<string> fallbackOrder, string contentTag, Dictionary<string, string> uniqueNameByKey,
        List<string> composedPaths)
    {
        if (UniqueNamePlanner.TimelineKeyFromPapPath(targetRequestedPath) is not { } timelineKey)
        {
            NoireLogger.LogDebug($"No timeline key readable off '{targetRequestedPath}'; leaving it vanilla.", LogPrefix);
            return false;
        }

        if (UniqueNamePlanner.UniqueNameFor(timelineKey, contentTag) is not { } uniqueName)
        {
            NoireLogger.LogDebug($"No unique name fits for '{timelineKey}'; leaving it vanilla.", LogPrefix);
            return false;
        }

        if (UniqueNamePlanner.ComposedPapPath(targetRequestedPath, timelineKey, uniqueName) is not { } composedPath)
        {
            NoireLogger.LogDebug($"No composable pap path for '{timelineKey}' under '{targetRequestedPath}'; leaving it vanilla.", LogPrefix);
            return false;
        }

        uniqueNameByKey[timelineKey] = uniqueName;

        foreach (var chainPath in UniqueNamePlanner.ComposedPapPathsForChain(composedPath, fallbackOrder))
        {
            if (!composedPaths.Contains(chainPath))
                composedPaths.Add(chainPath);
        }

        return true;
    }

    private static GroupOutput? BuildSharedGroupPap(IReadOnlyList<ResolvedVariantPair> group,
        IReadOnlyList<string> fallbackOrder, bool composeUniqueNames)
    {
        var lead = group[0];

        if (ReadPap(lead.Pair.SourceRequestedPath, lead.ResolvedSourcePath) is not { } sourceBytes)
        {
            NoireLogger.LogDebug($"No readable source pap for group led by '{lead.Pair.SourceRequestedPath}' (resolved to '{lead.ResolvedSourcePath}').", LogPrefix);
            return null;
        }

        var union = UnionRequiredNames(group, ReadVanillaNamesForNamesPath);
        if (union.Names.Count == 0)
        {
            NoireLogger.LogDebug($"No animation names to retarget the group led by '{lead.Pair.SourceRequestedPath}' onto.", LogPrefix);
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
            NoireLogger.LogError(ex, $"Retargeting the group led by '{lead.Pair.SourceRequestedPath}' produced an unusable pap; group skipped.", LogPrefix);
            return null;
        }

        NoireLogger.LogWarning(
            $"Served '{lead.Pair.TargetRequestedPath}' from '{lead.ResolvedSourcePath}': "
            + $"{FootstepEntryCount(retargeted)} footstep entr(y/ies), {clampedNames.Count} name(s) clamped.",
            LogPrefix);

        return ApplyFaceLibrary(retargeted, lead.Pair.SourceFaceLibrary, lead.Pair.TargetRequestedPath, PapFaceLibrary.Inject) is { } withFace
            ? WithUniqueNames(new GroupOutput(withFace, ClampedIntro: clampedNames.Count != 0), group, fallbackOrder,
                composeUniqueNames)
            : null;
    }

    private static int FootstepEntryCount(byte[] papBytes)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(papBytes));
            var pap = new PapFile(reader);

            return pap.Animations.Sum(animation =>
                animation.Tmb?.AllEntries.Count(entry => entry.Magic == "C042") ?? 0);
        }
        catch
        {
            return -1;
        }
    }

    private static IReadOnlyList<string>? ReadVanillaNamesForNamesPath(string namesPath)
        => ReadVanillaPap(namesPath) is { } vanillaBytes ? PapAnimationNames.Read(vanillaBytes) : null;

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
        if (sourceFaceLibrary is not { } faceLibrary)
            return retargetedBytes;

        try
        {
            return inject(retargetedBytes, faceLibrary);
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Injecting face library '{faceLibrary}' into the pap for '{targetRequestedPath}' failed ({ex.Message}); variant skipped.", LogPrefix);
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
            NoireLogger.LogError(ex, $"Could not read the modded pap at '{resolvedPath}'.", LogPrefix);
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
            NoireLogger.LogDebug($"Could not stamp '{resolvedPath}' ({ex.Message}); treating this swap as never reusable.", LogPrefix);
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
                NoireLogger.LogDebug("No vanilla pap was available to warm the byte pipeline with; skipping.", LogPrefix);
                return;
            }

            var warmUpClock = Stopwatch.StartNew();

            var names = PapAnimationNames.Read(papBytes);

            if (names.Count == 0)
            {
                NoireLogger.LogDebug("The warm-up pap declares no animation names; skipping.", LogPrefix);
                return;
            }

            var retargeted = PapRetargeter.Retarget(papBytes, names, removeAnimationLock: true, out _);
            var injected = PapFaceLibrary.Inject(retargeted, WarmUpFaceLibrary);
            var derivedName = SwapModManager.DeriveFileName(injected);

            NoireLogger.LogDebug(
                $"Warmed the byte pipeline in {warmUpClock.ElapsedMilliseconds}ms ({injected.Length} bytes, {derivedName}).",
                LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Warming the byte pipeline failed ({ex.Message}); the first swap pays the JIT instead.", LogPrefix);
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
