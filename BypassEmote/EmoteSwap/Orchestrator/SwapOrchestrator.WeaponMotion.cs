using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal static string MovedToFolder(string relativePapPath, string motionFolder)
    {
        var slash = relativePapPath.IndexOf('/');

        return slash < 0 ? relativePapPath : motionFolder + relativePapPath[slash..];
    }

    private static string KeyTailOf(string relativePapPath)
    {
        var slash = relativePapPath.IndexOf('/');

        return slash < 0 ? relativePapPath : relativePapPath[(slash + 1)..];
    }

    private static bool IsReferenceMotionPath(string relativePapPath)
        => relativePapPath.StartsWith(EmoteAttributeCatalog.ReferenceMotionFolder + "/", StringComparison.Ordinal);

    internal static EmoteAttributes WithMotionFolder(EmoteAttributes source, string motionFolder)
    {
        if (!source.HasWeaponMotionVariants()
            || string.Equals(motionFolder, EmoteAttributeCatalog.ReferenceMotionFolder, StringComparison.Ordinal))
        {
            return source;
        }

        var variants = source.Variants
            .Select(variant => variant.WeaponMotion
                ? variant with { RelativePapPath = MovedToFolder(variant.RelativePapPath, motionFolder) }
                : variant)
            .ToList();

        var intro = source.IntroIsWeaponMotion && source.IntroRelativePapPath is { } introPath
            ? MovedToFolder(introPath, motionFolder)
            : source.IntroRelativePapPath;

        return source with
        {
            Variants = variants,
            IntroRelativePapPath = intro,
            FaceLibraries = MovedFaceLibraries(source.FaceLibraries, motionFolder),
        };
    }

    private static IReadOnlyDictionary<string, string>? MovedFaceLibraries(
        IReadOnlyDictionary<string, string>? faceLibraries, string motionFolder)
    {
        if (faceLibraries == null || faceLibraries.Count == 0)
            return faceLibraries;

        var moved = new Dictionary<string, string>(faceLibraries.Count, StringComparer.Ordinal);

        foreach (var (relativePapPath, faceLibrary) in faceLibraries)
        {
            var key = IsReferenceMotionPath(relativePapPath)
                ? MovedToFolder(relativePapPath, motionFolder)
                : relativePapPath;

            moved[key] = faceLibrary;
        }

        return moved;
    }

    internal static IReadOnlyList<string> WeaponMotionKeyTails(EmoteAttributes source)
    {
        var tails = new List<string>();

        foreach (var variant in source.Variants)
        {
            if (variant.WeaponMotion && !tails.Contains(KeyTailOf(variant.RelativePapPath), StringComparer.Ordinal))
                tails.Add(KeyTailOf(variant.RelativePapPath));
        }

        if (source.IntroIsWeaponMotion && source.IntroRelativePapPath is { } introPath
            && !tails.Contains(KeyTailOf(introPath), StringComparer.Ordinal))
        {
            tails.Add(KeyTailOf(introPath));
        }

        return tails;
    }

    private string MotionFolderFor(EmoteAttributes source, ICharacter localPlayer, IReadOnlyList<string> fallbackOrder)
    {
        if (!source.HasWeaponMotionVariants())
            return EmoteAttributeCatalog.ReferenceMotionFolder;

        var tails = WeaponMotionKeyTails(source);
        var own = ReadWeaponMotionFolder(localPlayer);

        foreach (var candidate in WeaponMotionFolders.LadderFor(own))
        {
            if (!tails.All(tail => SelectRequestedPath(candidate + "/" + tail, fallbackOrder,
                    ForeignModProvides, VanillaExists) != null))
            {
                continue;
            }

            NoireLogger.LogWarning($"/{source.Command}: weapon motion '{own ?? "<unreadable>"}' "
                + $"served from '{candidate}'.", LogPrefix);

            return candidate;
        }

        NoireLogger.LogWarning($"/{source.Command}: weapon motion '{own ?? "<unreadable>"}' has no folder holding "
            + $"[{string.Join(", ", tails)}]; falling back to '{EmoteAttributeCatalog.ReferenceMotionFolder}'.",
            LogPrefix);

        return EmoteAttributeCatalog.ReferenceMotionFolder;
    }

    private static string? ReadWeaponMotionFolder(ICharacter localPlayer)
    {
        try
        {
            return CharacterHelper.GetWeaponMotionFolder(localPlayer);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not read the player's weapon motion; the ladder starts at its foot.",
                LogPrefix);

            return null;
        }
    }
}
