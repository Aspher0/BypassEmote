using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed record ModGroupOption(string Name, IReadOnlyDictionary<string, string> Files);

public sealed record ModGroup(string Name, IReadOnlyList<ModGroupOption> Options);

// Reads and writes one Penumbra option group. Single selection, option 0 is always the empty "None".
internal static class ModGroupFile
{
    internal const string FileNamePrefix = "group_";

    private static readonly IReadOnlyDictionary<string, string> NoFiles = new Dictionary<string, string>();

    internal static ModGroup NewGroup(string name)
        => new(name, [new ModGroupOption(OptionNaming.NoneOptionName, NoFiles)]);

    // A new option goes right behind the empty one, never at the end. Penumbra's mod panel draws its option list from a
    // cache that a reload does not invalidate, so an index past the list it still holds throws while it is on screen.
    // Index 1 exists in both the old list and the new one, and the newest swap reading first is a bonus.
    internal static ModGroup Add(ModGroup group, ModGroupOption option)
        => group with { Options = [group.Options[0], option, .. group.Options.Skip(1)] };

    internal static ModGroup Remove(ModGroup group, string optionName)
    {
        if (optionName == OptionNaming.NoneOptionName)
            return group;

        return group with
        {
            Options = group.Options.Where(option => option.Name != optionName).ToList(),
        };
    }

    // Nothing but the empty option left, so the group serves no swap any more.
    internal static bool IsEmpty(ModGroup group)
        => group.Options.All(option => option.Name == OptionNaming.NoneOptionName);

    internal static ModGroup WithFiles(ModGroup group, string optionName, IReadOnlyDictionary<string, string> files)
        => group with
        {
            Options = group.Options
                .Select(option => option.Name == optionName ? option with { Files = files } : option)
                .ToList(),
        };

    internal static string FileNameFor(string groupName, int index)
        => $"{FileNamePrefix}{index:D3}_{OptionNaming.FileNamePartFor(groupName)}.json";

    internal static string Serialize(ModGroup group)
    {
        var options = new JArray();

        foreach (var option in group.Options)
        {
            var files = new JObject();
            foreach (var (gamePath, relativeFile) in option.Files)
                files.Add(gamePath, relativeFile);

            options.Add(new JObject
            {
                ["Name"] = option.Name,
                ["Description"] = string.Empty,
                ["Files"] = files,
                ["FileSwaps"] = new JObject(),
                ["Manipulations"] = new JArray(),
            });
        }

        return new JObject
        {
            ["Version"] = 0,
            ["Name"] = group.Name,
            ["Description"] = string.Empty,
            ["Image"] = string.Empty,
            ["Page"] = 0,
            ["Priority"] = 0,
            ["Type"] = "Single",
            ["DefaultSettings"] = 0,
            ["Options"] = options,
        }.ToString(Formatting.Indented);
    }

    internal static ModGroup? Deserialize(string json)
    {
        try
        {
            if (JObject.Parse(json) is not { } root || root["Name"]?.Value<string>() is not { } name)
                return null;

            var options = new List<ModGroupOption>();

            foreach (var option in root["Options"] as JArray ?? [])
            {
                if (option["Name"]?.Value<string>() is not { } optionName)
                    continue;

                var files = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var file in (option["Files"] as JObject ?? []).Properties())
                {
                    if (file.Value.Value<string>() is { } relativeFile)
                        files[file.Name] = relativeFile;
                }

                options.Add(new ModGroupOption(optionName, files));
            }

            return options.Count == 0 ? null : new ModGroup(name, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
