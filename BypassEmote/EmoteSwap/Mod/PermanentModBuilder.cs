using BypassEmote.IPC;
using BypassEmote.Localization;
using BypassEmote.Models;
using NoireLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class PermanentModBuilder
{
    private const string LogPrefix = "[PermanentModBuilder] ";
    private const string HumanPathPrefix = "chara/human/";
    private const string HumanPathMiddle = "/animation/a0001/";
    internal const int MaxModNameLength = 64;

    internal sealed record Outcome(bool Created, string Message);

    internal static Outcome Create(EmoteAttributes source, EmoteAttributes target, IReadOnlyList<string> skeletons,
        string modName, bool enable, bool highestPriority)
    {
        try
        {
            return CreateCore(source, target, skeletons, modName, enable, highestPriority);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Creating a mod for /{source.Command} over /{target.Command} failed.", LogPrefix);
            return new Outcome(false, L.BuildFailed.Text);
        }
    }

    private static Outcome CreateCore(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> skeletons, string modName, bool enable, bool highestPriority)
    {
        if (skeletons.Count == 0)
            return new Outcome(false, L.BuildPickRace.Text);

        if (Service.Penumbra is not { Available: true } penumbra)
            return new Outcome(false, Service.Penumbra?.UnavailableReason ?? L.PenumbraNotRunning.Text);

        var name = CleanName(modName);
        if (name.Length == 0)
            return new Outcome(false, L.BuildNameFirst.Text);

        if (penumbra.GetModRootDirectory() is not { Length: > 0 } modRoot)
            return new Outcome(false, L.BuildNoModFolder.Text);

        if (Service.Orchestrator is not { } orchestrator)
            return new Outcome(false, L.BuildNoEngine.Text);

        var directoryName = DirectoryNameFor(name);
        var modDirectory = Path.Combine(modRoot, directoryName);

        if (Directory.Exists(modDirectory))
            return new Outcome(false, L.BuildFolderTaken.With("folder", directoryName));

        var ownSkeleton = NoireService.ObjectTable.LocalPlayer is { } player
            ? SwapOrchestrator.SkeletonFor(player)
            : null;

        if (orchestrator.BuildPlainSwapFiles(source, target, skeletons, ownSkeleton) is not { Count: > 0 } files)
        {
            return new Outcome(false, L.BuildNoPosture.With("source", source.Command, "target", target.Command));
        }

        var redirects = new Dictionary<string, string>(files.Count);
        foreach (var gamePath in files.Keys)
            redirects[gamePath] = RelativeFileFor(gamePath);

        var assigned = penumbra.GetPlayerCollection();

        var priority = assigned is { } priorityCollection && highestPriority
            ? PriorityOver(penumbra, priorityCollection.Id, [.. files.Keys])
            : 0;

        if (!WriteMod(modDirectory, name, source, target, files, redirects))
            return new Outcome(false, L.BuildNotWritten.Text);

        if (!penumbra.AddMod(directoryName))
        {
            return new Outcome(false, L.BuildNotLoaded.With("mod", name, "folder", directoryName));
        }

        if (assigned is { } collectionToPrioritiseIn)
            penumbra.TrySetModPriority(collectionToPrioritiseIn.Id, directoryName, priority);

        if (!enable)
            return new Outcome(true, L.BuildCreatedOff.With("mod", name));

        if (assigned is not { } collection)
            return new Outcome(true, L.BuildCreatedNoCollection.With("mod", name));

        if (!penumbra.TrySetModEnabled(collection.Id, directoryName, true))
            return new Outcome(true, L.BuildCreatedNotEnabled.With("mod", name, "collection", collection.Name));

        return new Outcome(true, L.BuildCreatedOn.With("mod", name, "collection", collection.Name));
    }

    private static int PriorityOver(IPCCaller_Penumbra penumbra, Guid collectionId,
        IReadOnlyCollection<string> gamePaths)
    {
        var states = penumbra.GetAllModStates(collectionId);
        var modRoot = penumbra.GetModRootDirectory();

        return SwapOrchestrator.ComputeAppliedPriority(gamePaths,
            penumbra.ResolvePlayerPath,
            resolved => Service.SwapMods?.IsOwnPath(resolved) == true,
            resolved => SwapModManager.ModDirectoryFromDiskPath(resolved, modRoot) is { } directory
                     && states != null && states.TryGetValue(directory, out var state)
                ? state.Priority
                : null);
    }

    private static bool WriteMod(string modDirectory, string name, EmoteAttributes source, EmoteAttributes target,
        IReadOnlyDictionary<string, byte[]> files, IReadOnlyDictionary<string, string> redirects)
    {
        try
        {
            foreach (var (gamePath, bytes) in files)
            {
                var file = Path.Combine(modDirectory, redirects[gamePath].Replace('/', Path.DirectorySeparatorChar));

                if (Path.GetDirectoryName(file) is { Length: > 0 } folder)
                    Directory.CreateDirectory(folder);

                File.WriteAllBytes(file, bytes);
            }

            var meta = new ModMeta(name, Service.PenumbraModAuthor, DescriptionFor(source, target),
                Service.PenumbraModVersion, Service.PenumbraModWebsite);

            ModStore.WriteMeta(modDirectory, meta, redirects);

            Log.Debug($"Wrote '{name}' to '{modDirectory}': /{source.Command} over /{target.Command}, "
                + $"{files.Count} file(s).", LogPrefix);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to write the mod under '{modDirectory}'.", LogPrefix);
            return false;
        }
    }

    internal static string RelativeFileFor(string gamePath)
    {
        var normalized = gamePath.Replace('\\', '/');

        if (normalized.StartsWith(HumanPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var afterPrefix = normalized[HumanPathPrefix.Length..];
            var middle = afterPrefix.IndexOf(HumanPathMiddle, StringComparison.OrdinalIgnoreCase);

            if (middle > 0)
            {
                var skeleton = afterPrefix[..middle];
                var rest = afterPrefix[(middle + HumanPathMiddle.Length)..];

                if (rest.Length > 0)
                    return $"files/{skeleton}/{rest}";
            }
        }

        return $"files/{normalized.TrimStart('/')}";
    }

    internal static string DescriptionFor(EmoteAttributes source, EmoteAttributes target)
        => $"Plays /{source.Command} whenever /{target.Command} is used. Made with Bypass Emote.";

    internal static string CleanName(string? modName)
    {
        if (string.IsNullOrWhiteSpace(modName))
            return string.Empty;

        var collapsed = string.Join(' ', modName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Length <= MaxModNameLength ? collapsed : collapsed[..MaxModNameLength].TrimEnd();
    }

    internal static string DirectoryNameFor(string name)
    {
        var kept = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character) || character is ' ' or '-' or '_')
                kept.Append(character);
        }

        var cleaned = string.Join('_', kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        while (cleaned.StartsWith('_'))
            cleaned = cleaned[1..];

        return cleaned.Length == 0 ? "BypassEmoteMod" : cleaned;
    }
}
