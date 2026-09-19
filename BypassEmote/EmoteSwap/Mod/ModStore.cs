using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

internal sealed record GroupOnDisk(ModGroup Group, int Index);

internal static class ModStore
{
    private const string LogPrefix = "[ModStore] ";

    private static readonly IReadOnlyDictionary<string, string> NoRedirects = new Dictionary<string, string>();

    internal static bool WriteMeta(string modDirectory, ModMeta meta,
        IReadOnlyDictionary<string, string> gamePathToRelativeFile)
    {
        var root = ModLayout.ReadMeta(modDirectory) ?? new JObject();

        foreach (var property in ModLayout.BuildMetaObject(meta).Properties())
            root[property.Name] = property.Value;

        root[ModLayout.DefaultPropertyName] = ModLayout.BuildDefaultObject(gamePathToRelativeFile);

        return ModLayout.WriteMeta(modDirectory, root);
    }

    internal static bool WriteMetaMissing(string modDirectory, ModMeta meta)
    {
        var root = ModLayout.ReadMeta(modDirectory) ?? ModLayout.BuildMetaObject(meta);
        root["FileVersion"] = ModLayout.FileVersion;
        root[ModLayout.DefaultPropertyName] ??= ModLayout.BuildDefaultObject(NoRedirects);

        return ModLayout.WriteMeta(modDirectory, root);
    }

    internal static Dictionary<string, GroupOnDisk> ReadGroups(string modDirectory)
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

            groups[group.Name] = new GroupOnDisk(group, place);
        }

        return groups;
    }

    internal static bool WriteGroup(string modDirectory, ModGroup group, int index)
    {
        try
        {
            var root = ModLayout.ReadMeta(modDirectory) ?? new JObject();
            root["FileVersion"] = ModLayout.FileVersion;

            var array = root[ModLayout.GroupsPropertyName] as JArray ?? [];
            var written = ModGroupFile.ToJson(group);

            var existing = array.OfType<JObject>()
                .FirstOrDefault(entry => string.Equals(entry["Name"]?.Value<string>(), group.Name, StringComparison.Ordinal));

            if (existing != null)
                existing.Replace(written);
            else
                array.Insert(Math.Clamp(index - 1, 0, array.Count), written);

            root[ModLayout.GroupsPropertyName] = array;
            root[ModLayout.DefaultPropertyName] ??= ModLayout.BuildDefaultObject(NoRedirects);

            ModLayout.WriteMeta(modDirectory, root);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to write the group '{group.Name}' into the mod's meta.", LogPrefix);
            return false;
        }
    }

    internal static void DropEmptyGroups(string modDirectory)
    {
        var empty = ReadGroups(modDirectory).Values
            .Where(onDisk => ModGroupFile.IsEmpty(onDisk.Group))
            .Select(onDisk => onDisk.Group.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (empty.Count == 0)
            return;

        try
        {
            if (ModLayout.ReadMeta(modDirectory) is not { } root || root[ModLayout.GroupsPropertyName] is not JArray array)
                return;

            root[ModLayout.GroupsPropertyName] = new JArray(array.OfType<JObject>()
                .Where(entry => !empty.Contains(entry["Name"]?.Value<string>() ?? string.Empty)));

            ModLayout.WriteMeta(modDirectory, root);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not drop the emptied groups of '{modDirectory}'.", LogPrefix);
        }
    }
}
