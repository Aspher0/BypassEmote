using System;
using System.IO;

namespace BypassEmote.EmoteSwap;

internal static class ServedPath
{
    internal const string Vanilla = "vanilla";

    internal static string Describe(string requestedPath, string resolvedPath, string? modRoot,
        Func<string, bool> isOwnPath, Func<string, string?> modNameOf)
    {
        if (resolvedPath == requestedPath)
            return Vanilla;

        if (!Path.IsPathRooted(resolvedPath))
            return $"file swap to {resolvedPath}";

        if (isOwnPath(resolvedPath))
            return "the generated mod";

        if (SwapModManager.ModDirectoryFromDiskPath(resolvedPath, modRoot) is not { } directory)
            return $"a file outside the mod folder ({resolvedPath})";

        var name = modNameOf(directory) is { Length: > 0 } modName ? modName : directory;

        return $"mod \"{name}\" ({UnderRoot(resolvedPath, modRoot!)})";
    }

    private static string UnderRoot(string path, string root)
        => path.Replace('\\', '/')[(root.Replace('\\', '/').TrimEnd('/').Length + 1)..];
}
