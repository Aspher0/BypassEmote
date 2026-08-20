using NoireLib.Animations.PapFormat;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class UniqueNamePlanner
{
    internal const int MaxAnimationNameLength = PapAnimation.MaxNameLength;

    private const int HashHexLength = 10;
    private const int MinHashHexLength = 6;
    private const string UniqueMarker = "bp";
    private const string PapExtension = ".pap";
    private const string PapBaseMarker = "/bt_common/";
    private const string HumanPathPrefix = "chara/human/";

    internal static string ContentTagFor(byte[] sourceBytes)
        => Convert.ToHexString(SHA1.HashData(sourceBytes)).ToLowerInvariant();

    internal static string? UniqueNameFor(string vanillaName, string contentTag)
    {
        var slash = vanillaName.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : vanillaName[..(slash + 1)];

        var underscore = vanillaName.LastIndexOf('_');
        var suffix = underscore < 0 || underscore == vanillaName.Length - 1
            ? string.Empty
            : vanillaName[underscore..];

        var digest = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(vanillaName + "|" + contentTag)))
            .ToLowerInvariant();

        for (var hexLength = HashHexLength; hexLength >= MinHashHexLength; hexLength--)
        {
            var candidate = directory + UniqueMarker + digest[..hexLength] + suffix;
            if (candidate.Length <= MaxAnimationNameLength)
                return candidate;
        }

        return null;
    }

    internal static string? TimelineKeyFromPapPath(string papPath)
    {
        if (!papPath.EndsWith(PapExtension, StringComparison.Ordinal))
            return null;

        var marker = papPath.LastIndexOf(PapBaseMarker, StringComparison.Ordinal);
        if (marker < 0)
            return null;

        var start = marker + PapBaseMarker.Length;
        var key = papPath[start..^PapExtension.Length];
        return key.Length > 0 ? key : null;
    }

    internal static bool IsComposedPapPath(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath) || !gamePath.EndsWith(PapExtension, StringComparison.Ordinal))
            return false;

        var slash = gamePath.LastIndexOf('/');
        var name = slash < 0 ? gamePath : gamePath[(slash + 1)..];
        if (!name.StartsWith(UniqueMarker, StringComparison.Ordinal))
            return false;

        var hex = 0;
        while (UniqueMarker.Length + hex < name.Length && Uri.IsHexDigit(name[UniqueMarker.Length + hex]))
            hex++;

        return hex >= MinHashHexLength;
    }

    internal static string? ComposedPapPath(string memberTargetPath, string vanillaName, string uniqueName)
    {
        var trailing = vanillaName + PapExtension;
        if (memberTargetPath.EndsWith(trailing, StringComparison.Ordinal))
            return memberTargetPath[..^trailing.Length] + uniqueName + PapExtension;

        var marker = memberTargetPath.LastIndexOf(PapBaseMarker, StringComparison.Ordinal);
        if (marker < 0 || !memberTargetPath.EndsWith(PapExtension, StringComparison.Ordinal))
            return null;

        return memberTargetPath[..(marker + PapBaseMarker.Length)] + uniqueName + PapExtension;
    }

    internal static IReadOnlyList<string> ComposedPapPathsForChain(string composedPath,
        IReadOnlyList<string> fallbackOrder)
    {
        if (SkeletonEnd(composedPath) is not { } skeletonEnd)
            return [composedPath];

        var tail = composedPath[skeletonEnd..];
        var paths = new List<string>(fallbackOrder.Count + 1);

        foreach (var skeleton in fallbackOrder)
        {
            var candidate = HumanPathPrefix + skeleton + tail;
            if (!paths.Contains(candidate))
                paths.Add(candidate);
        }

        if (!paths.Contains(composedPath))
            paths.Add(composedPath);

        return paths;
    }

    private static int? SkeletonEnd(string gamePath)
    {
        if (!gamePath.StartsWith(HumanPathPrefix, StringComparison.Ordinal))
            return null;

        var end = gamePath.IndexOf('/', HumanPathPrefix.Length);
        return end > HumanPathPrefix.Length ? end : null;
    }
}
