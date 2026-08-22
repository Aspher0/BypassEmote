using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class ModLayout
{
    internal const int V3 = 3;
    internal const int V4 = 4;

    internal const string MetaFileName = "meta.json";
    internal const string DefaultFileName = "default_mod.json";
    internal const string DefaultPropertyName = "DefaultData";
    internal const string GroupsPropertyName = "Groups";

    private const string LogPrefix = "[ModLayout] ";

    internal static int OnDisk(string modDirectory)
    {
        if (ReadMeta(modDirectory) is not { } meta)
            return V3;

        return meta["FileVersion"]?.Value<int?>() is { } version && version >= V4 ? V4 : V3;
    }

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
            NoireLogger.LogDebug($"Could not read the meta of '{modDirectory}' ({ex.Message}).", LogPrefix);
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

    internal static void RemoveV3Files(string modDirectory)
    {
        Delete(Path.Combine(modDirectory, DefaultFileName));

        foreach (var path in GroupFiles(modDirectory))
            Delete(path);
    }

    internal static IReadOnlyList<string> GroupFiles(string modDirectory)
    {
        try
        {
            return Directory.Exists(modDirectory)
                ? Directory.GetFiles(modDirectory, ModGroupFile.FileNamePrefix + "*.json")
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoireLogger.LogDebug($"Could not list the group files of '{modDirectory}' ({ex.Message}).", LogPrefix);
            return [];
        }
    }

    internal static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoireLogger.LogDebug($"Could not remove '{path}' ({ex.Message}).", LogPrefix);
        }
    }

    internal static bool ToV3(string modDirectory)
    {
        try
        {
            if (ReadMeta(modDirectory) is not { } meta)
                return false;

            var groups = meta[GroupsPropertyName] as JArray ?? [];

            foreach (var path in GroupFiles(modDirectory))
                Delete(path);

            var index = 1;

            foreach (var group in groups.OfType<JObject>())
            {
                var name = group["Name"]?.Value<string>() ?? string.Empty;
                var fileName = ModGroupFile.FileNameFor(name, index++);

                AtomicFile.WriteAllText(Path.Combine(modDirectory, fileName), group.ToString(Formatting.Indented));
            }

            var container = meta[DefaultPropertyName] as JObject ?? SimpleV3ModWriter.BuildDefaultObject(new Dictionary<string, string>());

            AtomicFile.WriteAllText(Path.Combine(modDirectory, DefaultFileName), container.ToString(Formatting.Indented));

            var downgraded = (JObject)meta.DeepClone();
            downgraded["FileVersion"] = V3;
            downgraded.Remove(DefaultPropertyName);
            downgraded.Remove(GroupsPropertyName);

            WriteMeta(modDirectory, downgraded);

            NoireLogger.LogDebug($"'{modDirectory}' was rewritten in the V3 layout ({groups.Count} group(s)).", LogPrefix);

            return true;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not rewrite '{modDirectory}' in the V3 layout.", LogPrefix);
            return false;
        }
    }
}
