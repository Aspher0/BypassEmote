using Newtonsoft.Json.Linq;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

internal sealed record GroupOnDisk(ModGroup Group, int Index, IReadOnlyList<string> Files);

internal static class ModStore
{
    private const string LogPrefix = "[ModStore] ";

    private static readonly IReadOnlyList<string> NoFiles = [];

    internal static bool WriteMeta(int layout, string modDirectory, ModMeta meta,
        IReadOnlyDictionary<string, string> gamePathToRelativeFile)
    {
        if (layout < ModLayout.V4)
            return SimpleV3ModWriter.Write(modDirectory, meta, gamePathToRelativeFile);

        var root = MergedMeta(modDirectory, meta);
        root[ModLayout.DefaultPropertyName] = SimpleV3ModWriter.BuildDefaultObject(gamePathToRelativeFile);

        var wrote = ModLayout.WriteMeta(modDirectory, root);

        ModLayout.RemoveV3Files(modDirectory);

        return wrote;
    }

    internal static bool WriteMetaMissing(int layout, string modDirectory, ModMeta meta)
    {
        if (layout < ModLayout.V4)
            return SimpleV3ModWriter.WriteMissing(modDirectory, meta);

        var root = ModLayout.ReadMeta(modDirectory) ?? SimpleV3ModWriter.BuildMetaObject(meta);
        root["FileVersion"] = ModLayout.V4;

        AbsorbV3Files(modDirectory, root);

        root[ModLayout.DefaultPropertyName] ??= SimpleV3ModWriter.BuildDefaultObject(new Dictionary<string, string>());

        var wrote = ModLayout.WriteMeta(modDirectory, root);

        ModLayout.RemoveV3Files(modDirectory);

        return wrote;
    }

    internal static Dictionary<string, GroupOnDisk> ReadGroups(int layout, string modDirectory)
        => layout < ModLayout.V4 ? ReadGroupFiles(modDirectory) : ReadInlineGroups(modDirectory);

    internal static bool WriteGroup(int layout, string modDirectory, ModGroup group, int index,
        IReadOnlyList<string>? knownFiles)
    {
        return layout < ModLayout.V4
            ? WriteGroupFile(modDirectory, group, index, knownFiles)
            : WriteInlineGroup(modDirectory, group, index);
    }

    internal static void DropEmptyGroups(int layout, string modDirectory)
    {
        var groups = ReadGroups(layout, modDirectory);

        if (layout < ModLayout.V4)
        {
            foreach (var onDisk in groups.Values.Where(onDisk => ModGroupFile.IsEmpty(onDisk.Group)))
            {
                foreach (var path in onDisk.Files)
                    ModLayout.Delete(path);
            }

            return;
        }

        var empty = groups.Values.Where(onDisk => ModGroupFile.IsEmpty(onDisk.Group))
            .Select(onDisk => onDisk.Group.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (empty.Count == 0)
            return;

        try
        {
            if (ModLayout.ReadMeta(modDirectory) is not { } root || root[ModLayout.GroupsPropertyName] is not JArray array)
                return;

            var kept = new JArray(array.OfType<JObject>()
                .Where(entry => !empty.Contains(entry["Name"]?.Value<string>() ?? string.Empty)));

            root[ModLayout.GroupsPropertyName] = kept;

            ModLayout.WriteMeta(modDirectory, root);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not drop the emptied groups of '{modDirectory}'.", LogPrefix);
        }
    }

    private static JObject MergedMeta(string modDirectory, ModMeta meta)
    {
        var root = ModLayout.ReadMeta(modDirectory) ?? new JObject();
        var built = SimpleV3ModWriter.BuildMetaObject(meta);

        foreach (var property in built.Properties())
            root[property.Name] = property.Value;

        root["FileVersion"] = ModLayout.V4;

        AbsorbV3Files(modDirectory, root);

        return root;
    }

    private static void AbsorbV3Files(string modDirectory, JObject root)
    {
        try
        {
            if (root[ModLayout.DefaultPropertyName] == null
                && ReadJsonObject(Path.Combine(modDirectory, ModLayout.DefaultFileName)) is { } container)
            {
                root[ModLayout.DefaultPropertyName] = container;
            }

            if (root[ModLayout.GroupsPropertyName] is JArray held && held.Count > 0)
                return;

            var files = ModLayout.GroupFiles(modDirectory).OrderBy(IndexInFileName).ToList();

            if (files.Count == 0)
                return;

            var array = new JArray();

            foreach (var path in files)
            {
                if (ReadJsonObject(path) is { } group)
                    array.Add(group);
            }

            if (array.Count > 0)
            {
                root[ModLayout.GroupsPropertyName] = array;

                NoireLogger.LogDebug($"{array.Count} group file(s) of '{modDirectory}' were folded into its meta.", LogPrefix);
            }
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not fold the V3 files of '{modDirectory}' into its meta.", LogPrefix);
        }
    }

    private static JObject? ReadJsonObject(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);

            return string.IsNullOrWhiteSpace(text) ? null : JObject.Parse(text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            NoireLogger.LogDebug($"Could not read '{path}' ({ex.Message}).", LogPrefix);
            return null;
        }
    }

    private static Dictionary<string, GroupOnDisk> ReadGroupFiles(string modDirectory)
    {
        var groups = new Dictionary<string, GroupOnDisk>(StringComparer.Ordinal);

        if (!Directory.Exists(modDirectory))
            return groups;

        var filesByGroup = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var path in ModLayout.GroupFiles(modDirectory))
        {
            try
            {
                if (ModGroupFile.Deserialize(File.ReadAllText(path)) is not { } group)
                    continue;

                if (!filesByGroup.TryGetValue(group.Name, out var paths))
                    filesByGroup[group.Name] = paths = [];

                paths.Add(path);

                if (!groups.TryGetValue(group.Name, out var seen) || group.Options.Count > seen.Group.Options.Count)
                    groups[group.Name] = new GroupOnDisk(group, IndexInFileName(path), paths);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NoireLogger.LogDebug($"Could not read the group file '{path}' ({ex.Message}).", LogPrefix);
            }
        }

        return groups;
    }

    private static Dictionary<string, GroupOnDisk> ReadInlineGroups(string modDirectory)
    {
        var groups = new Dictionary<string, GroupOnDisk>(StringComparer.Ordinal);

        if (ModLayout.ReadMeta(modDirectory) is not { } root || root[ModLayout.GroupsPropertyName] is not JArray array)
            return groups;

        var index = 1;

        foreach (var entry in array.OfType<JObject>())
        {
            var place = index++;

            if (ModGroupFile.FromJson(entry) is not { } group || groups.ContainsKey(group.Name))
                continue;

            groups[group.Name] = new GroupOnDisk(group, place, NoFiles);
        }

        return groups;
    }

    private static bool WriteGroupFile(string modDirectory, ModGroup group, int index, IReadOnlyList<string>? knownFiles)
    {
        var fileName = ModGroupFile.FileNameFor(group.Name, index);

        try
        {
            if (knownFiles == null)
                RemoveRivalGroupFiles(modDirectory, group.Name, fileName);
            else
                RemoveKnownGroupFiles(knownFiles, group.Name, fileName);

            RemoveOtherFilesAtIndex(modDirectory, index, fileName);

            AtomicFile.WriteAllText(Path.Combine(modDirectory, fileName), ModGroupFile.Serialize(group));

            return true;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the group file for '{group.Name}'.", LogPrefix);
            return false;
        }
    }

    private static bool WriteInlineGroup(string modDirectory, ModGroup group, int index)
    {
        try
        {
            var root = ModLayout.ReadMeta(modDirectory) ?? new JObject();
            root["FileVersion"] = ModLayout.V4;

            var array = root[ModLayout.GroupsPropertyName] as JArray ?? [];
            var written = ModGroupFile.ToJson(group);

            var existing = array.OfType<JObject>()
                .FirstOrDefault(entry => string.Equals(entry["Name"]?.Value<string>(), group.Name, StringComparison.Ordinal));

            if (existing != null)
                existing.Replace(written);
            else
                array.Insert(Math.Clamp(index - 1, 0, array.Count), written);

            root[ModLayout.GroupsPropertyName] = array;

            if (root[ModLayout.DefaultPropertyName] == null)
                root[ModLayout.DefaultPropertyName] = SimpleV3ModWriter.BuildDefaultObject(new Dictionary<string, string>());

            ModLayout.WriteMeta(modDirectory, root);

            return true;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the group '{group.Name}' into the mod's meta.", LogPrefix);
            return false;
        }
    }

    private static void RemoveRivalGroupFiles(string modDirectory, string groupName, string keptFileName)
    {
        if (!Directory.Exists(modDirectory))
            return;

        foreach (var path in ModLayout.GroupFiles(modDirectory))
        {
            if (string.Equals(Path.GetFileName(path), keptFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (ModGroupFile.Deserialize(File.ReadAllText(path)) is { } rival && rival.Name == groupName)
                {
                    File.Delete(path);
                    NoireLogger.LogDebug(keptFileName.Length == 0
                        ? $"Removed '{Path.GetFileName(path)}', the last file of group '{groupName}'."
                        : $"Removed '{Path.GetFileName(path)}', a second file for group '{groupName}'.", LogPrefix);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NoireLogger.LogDebug($"Could not read or remove '{path}' ({ex.Message}).", LogPrefix);
            }
        }
    }

    private static void RemoveOtherFilesAtIndex(string modDirectory, int index, string keptFileName)
    {
        foreach (var path in Directory.GetFiles(modDirectory, $"{ModGroupFile.FileNamePrefix}{index:D3}_*.json"))
        {
            if (string.Equals(Path.GetFileName(path), keptFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            ModLayout.Delete(path);
        }
    }

    private static void RemoveKnownGroupFiles(IReadOnlyList<string> paths, string groupName, string keptFileName)
    {
        foreach (var path in paths)
        {
            if (string.Equals(Path.GetFileName(path), keptFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            ModLayout.Delete(path);

            NoireLogger.LogDebug($"Removed '{Path.GetFileName(path)}', a second file for group '{groupName}'.", LogPrefix);
        }
    }

    internal static int IndexInFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        if (name.Length <= ModGroupFile.FileNamePrefix.Length)
            return 0;

        var digits = new string(name[ModGroupFile.FileNamePrefix.Length..].TakeWhile(char.IsAsciiDigit).ToArray());

        return digits.Length > 0 && int.TryParse(digits, out var index) ? index : 0;
    }
}
