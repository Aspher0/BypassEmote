using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class OptionNaming
{
    internal const string NoneOptionName = "None";
    private const int MaxModNameLength = 31;
    private static readonly HashSet<char> InvalidFileNameCharacters = [.. Path.GetInvalidFileNameChars()];

    internal static string StanceLabelFor(EmoteController.PoseType stance) => stance switch
    {
        EmoteController.PoseType.Idle => "Standing",
        EmoteController.PoseType.Sit => "Chair sit",
        EmoteController.PoseType.GroundSit => "Ground sit",
        EmoteController.PoseType.Doze => "Doze",
        _ => stance.ToString(),
    };

    internal static string IdlePoseGroupNameFor(EmoteController.PoseType stance, byte poseIndex)
        => $"Idle pose {poseIndex} - {StanceLabelFor(stance)}";

    internal static string GroupNameFor(string targetEmoteName, string? targetCommand, uint targetRowId,
        IReadOnlySet<string> takenGroupNames)
    {
        var name = targetEmoteName.Trim();

        var composed = name.Length == 0
            ? $"On: Emote #{targetRowId}"
            : string.IsNullOrWhiteSpace(targetCommand)
                ? $"On: {name}"
                : $"On: {name} ({targetCommand.Trim()})";

        return takenGroupNames.Contains(composed) ? $"{composed} (#{targetRowId})" : composed;
    }

    internal static string OptionNameFor(string sourceEmoteName, string? sourceModName, IReadOnlySet<string> takenOptionNames)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceModName)
            ? $"{sourceEmoteName.Trim()} (Vanilla)"
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
        => value.Length <= MaxModNameLength ? value : value[..MaxModNameLength] + "...";
}
