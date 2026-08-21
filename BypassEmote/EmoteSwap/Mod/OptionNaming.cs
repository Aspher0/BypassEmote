using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class OptionNaming
{
    internal const string NoneOptionName = "None";

    internal const string IdlePoseGroupName = "Idle pose";

    private const string GroupPrefix = "On: ";

    private const string VanillaSuffix = "(Vanilla)";

    private static readonly HashSet<char> InvalidFileNameCharacters = [.. Path.GetInvalidFileNameChars()];

    private const int MaxModNameLength = 31;

    private const string Ellipsis = "...";

    internal static string GroupNameFor(string targetEmoteName, string? targetCommand, uint targetRowId,
        IReadOnlySet<string> takenGroupNames)
    {
        var name = targetEmoteName.Trim();

        var composed = name.Length == 0
            ? $"{GroupPrefix}Emote #{targetRowId}"
            : string.IsNullOrWhiteSpace(targetCommand)
                ? $"{GroupPrefix}{name}"
                : $"{GroupPrefix}{name} ({targetCommand.Trim()})";

        return takenGroupNames.Contains(composed) ? $"{composed} (#{targetRowId})" : composed;
    }

    internal static string OptionNameFor(string sourceEmoteName, string? sourceModName, IReadOnlySet<string> takenOptionNames)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceModName)
            ? $"{sourceEmoteName.Trim()} {VanillaSuffix}"
            : $"{Ellipsize(sourceModName.Trim())} | ({sourceEmoteName.Trim()})";

        if (baseName.Length == 0)
            baseName = "Swap";

        if (!takenOptionNames.Contains(baseName))
            return baseName;

        for (var version = 2; ; version++)
        {
            var candidate = $"{baseName} - V{version}";

            if (!takenOptionNames.Contains(candidate))
                return candidate;
        }
    }

    internal static string FileNamePartFor(string groupName)
    {
        var folded = new StringBuilder(groupName.Length);

        foreach (var character in groupName.ToLowerInvariant())
            folded.Append(InvalidFileNameCharacters.Contains(character) ? '_' : character);

        return folded.ToString();
    }

    private static string Ellipsize(string value)
        => value.Length <= MaxModNameLength ? value : value[..MaxModNameLength] + Ellipsis;
}
