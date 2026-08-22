using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BypassEmote.EmoteSwap;

public sealed record ModMeta(string Name, string Author, string Description, string Version, string Website);

public static class SimpleV3ModWriter
{
    public const string MetaFileName = "meta.json";
    public const string DefaultFileName = "default_mod.json";

    public static bool Write(string modDirectory, ModMeta meta,
        IReadOnlyDictionary<string, string> gamePathToRelativeFile)
    {
        var wroteMeta = WriteIfChanged(Path.Combine(modDirectory, MetaFileName), BuildMeta(meta));
        var wroteMap = WriteIfChanged(Path.Combine(modDirectory, DefaultFileName), BuildDefaultMod(gamePathToRelativeFile));

        return wroteMeta || wroteMap;
    }

    public static bool WriteMissing(string modDirectory, ModMeta meta)
    {
        var metaPath = Path.Combine(modDirectory, MetaFileName);
        var mapPath = Path.Combine(modDirectory, DefaultFileName);

        var wroteMeta = !File.Exists(metaPath);
        if (wroteMeta)
            AtomicFile.WriteAllText(metaPath, BuildMeta(meta));

        var wroteMap = !File.Exists(mapPath);
        if (wroteMap)
            AtomicFile.WriteAllText(mapPath, BuildDefaultMod(new Dictionary<string, string>()));

        return wroteMeta || wroteMap;
    }

    private static string BuildMeta(ModMeta meta)
        => BuildMetaObject(meta).ToString(Formatting.Indented);

    internal static JObject BuildMetaObject(ModMeta meta)
        => new()
        {
            ["FileVersion"] = 3,
            ["Name"] = meta.Name,
            ["Author"] = meta.Author,
            ["Description"] = meta.Description,
            ["Version"] = meta.Version,
            ["Website"] = meta.Website,
            ["ModTags"] = new JArray(),
        };

    private static string BuildDefaultMod(IReadOnlyDictionary<string, string> gamePathToRelativeFile)
        => BuildDefaultObject(gamePathToRelativeFile).ToString(Formatting.Indented);

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

    private static bool WriteIfChanged(string path, string contents)
        => ModLayout.WriteIfChanged(path, contents);
}
