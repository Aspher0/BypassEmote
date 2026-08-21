using BypassEmote.IPC;
using BypassEmote.Models;
using NoireLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class PermanentModBuilder
{
    private const string LogPrefix = "[PermanentModBuilder] ";
    private const string FilesSubfolderName = "files";
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
            NoireLogger.LogError(ex, $"Creating a mod for /{source.Command} over /{target.Command} failed.", LogPrefix);
            return new Outcome(false, "Something went wrong. Nothing was created; the log has the details.");
        }
    }

    private static Outcome CreateCore(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<string> skeletons, string modName, bool enable, bool highestPriority)
    {
        if (skeletons.Count == 0)
            return new Outcome(false, "Pick at least one race for the mod to cover.");

        if (Service.Penumbra is not { Available: true } penumbra)
            return new Outcome(false, "Penumbra not available.");

        var name = CleanName(modName);
        if (name.Length == 0)
            return new Outcome(false, "Give the mod a name first.");

        if (penumbra.GetModRootDirectory() is not { Length: > 0 } modRoot)
            return new Outcome(false, "Penumbra's mod folder could not be read.");

        if (Service.Orchestrator is not { } orchestrator)
            return new Outcome(false, "The swap engine is not running.");

        var directoryName = DirectoryNameFor(name);
        var modDirectory = Path.Combine(modRoot, directoryName);

        if (Directory.Exists(modDirectory))
            return new Outcome(false, $"Penumbra already holds a mod folder called '{directoryName}'. Pick another name.");

        if (orchestrator.BuildPlainSwapFiles(source, target, skeletons) is not { Count: > 0 } files)
        {
            return new Outcome(false, $"/{source.Command} cannot be played over /{target.Command}: "
                + "they share no posture to move the animation onto.");
        }

        var redirects = new Dictionary<string, string>(files.Count);
        foreach (var gamePath in files.Keys)
            redirects[gamePath] = RelativeFileFor(gamePath);

        if (!WriteMod(modDirectory, name, source, target, files, redirects))
            return new Outcome(false, "The mod's files could not be written. The log has the details.");

        if (!penumbra.AddMod(directoryName))
        {
            return new Outcome(false, $"'{name}' was written to '{directoryName}' but Penumbra would not take it. "
                + "Rediscovering mods in Penumbra should pick it up.");
        }

        var assigned = penumbra.GetPlayerCollection();

        if (assigned is { } ranked)
            penumbra.TrySetModPriority(ranked.Id, directoryName, PriorityFor(penumbra, ranked.Id, highestPriority));

        if (!enable)
            return new Outcome(true, $"'{name}' was created. Enable it in Penumbra when you want it.");

        if (assigned is not { } collection)
            return new Outcome(true, $"'{name}' was created, but no collection is assigned to your character, so it is off.");

        if (!penumbra.TrySetModEnabled(collection.Id, directoryName, true))
            return new Outcome(true, $"'{name}' was created, but Penumbra would not switch it on in {collection.Name}.");

        return new Outcome(true, $"'{name}' was created and switched on in {collection.Name}.");
    }

    private static int PriorityFor(IPCCaller_Penumbra penumbra, Guid collectionId, bool highestPriority)
    {
        if (!highestPriority || penumbra.GetAllModStates(collectionId) is not { Count: > 0 } states)
            return 0;

        return Math.Max(0, states.Values.Max(state => state.Priority) + 1);
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

            SimpleV3ModWriter.Write(modDirectory, meta, redirects);

            NoireLogger.LogDebug($"Wrote '{name}' to '{modDirectory}': /{source.Command} over /{target.Command}, "
                + $"{files.Count} file(s).", LogPrefix);

            return true;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the mod under '{modDirectory}'.", LogPrefix);
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
                    return $"{FilesSubfolderName}/{skeleton}/{rest}";
            }
        }

        // Anything not shaped like a human animation path keeps its own shape under the same folder.
        return $"{FilesSubfolderName}/{normalized.TrimStart('/')}";
    }

    internal static string DescriptionFor(EmoteAttributes source, EmoteAttributes target)
        => $"Plays /{source.Command} whenever /{target.Command} is used. Made with BypassEmote.";

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
