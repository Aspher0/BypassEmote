using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BypassEmote.EmoteSwap;

public sealed record ModMeta(string Name, string Author, string Description, string Version, string Website);

internal static class ModLayout
{
    private const string LogPrefix = "[ModLayout] ";

    internal const int FileVersion = 4;

    internal const string MetaFileName = "meta.json";
    internal const string DefaultPropertyName = "DefaultData";
    internal const string GroupsPropertyName = "Groups";

    internal static JObject? ReadMeta(string modDirectory)
    {
        try
        {
            var path = Path.Combine(modDirectory, MetaFileName);

            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);

            return string.IsNullOrWhiteSpace(text) ? null : JObject.Parse(text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Debug($"Could not read the meta of '{modDirectory}' ({ex.Message}).", LogPrefix);
            return null;
        }
    }

    internal static bool WriteMeta(string modDirectory, JObject meta)
        => WriteIfChanged(Path.Combine(modDirectory, MetaFileName), meta.ToString(Formatting.Indented));

    internal static bool WriteIfChanged(string path, string contents)
    {
        if (AlreadyHolds(path, contents))
            return false;

        AtomicFile.WriteAllText(path, contents);
        return true;
    }

    private static bool AlreadyHolds(string path, string contents)
    {
        try
        {
            return File.Exists(path)
                && File.ReadAllBytes(path).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(contents));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static JObject BuildMetaObject(ModMeta meta)
        => new()
        {
            ["FileVersion"] = FileVersion,
            ["Name"] = meta.Name,
            ["Author"] = meta.Author,
            ["Description"] = meta.Description,
            ["Version"] = meta.Version,
            ["Website"] = meta.Website,
            ["ModTags"] = new JArray(),
        };

    internal static JObject BuildDefaultObject(IReadOnlyDictionary<string, string> gamePathToRelativeFile)
    {
        var files = new JObject();
        foreach (var (gamePath, relativeFile) in gamePathToRelativeFile)
            files.Add(gamePath, relativeFile);

        return new JObject
        {
            ["Name"] = "",
            ["Priority"] = 0,
            ["Files"] = files,
            ["FileSwaps"] = new JObject(),
            ["Manipulations"] = new JArray(),
        };
    }
}
