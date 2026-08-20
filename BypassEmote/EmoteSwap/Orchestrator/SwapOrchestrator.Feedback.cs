using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using System;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal const string NoMatchKind = "swap.no-match";

    private static readonly System.Numerics.Vector3 RefusalColor = NoireLib.Helpers.ColorHelper.HexToVector3("#E81313");

    private static readonly System.Numerics.Vector3 NoticeColor = NoireLib.Helpers.ColorHelper.HexToVector3("#FF8C1A");

    internal static string NoMatchMessage(EmoteAttributes source, IReadOnlyList<NearMiss> diagnostics,
        Func<NearMiss, string?>? modNameFor = null)
        => string.Join('\n', NoMatchLines(source, diagnostics, modNameFor));

    internal static IReadOnlyList<string> NoMatchLines(EmoteAttributes source, IReadOnlyList<NearMiss> diagnostics,
        Func<NearMiss, string?>? modNameFor = null)
    {
        var lines = new List<string>(diagnostics.Count + 1) { $"Could not swap /{source.Command}." };

        for (var index = 0; index < diagnostics.Count; index++)
        {
            var miss = diagnostics[index];

            var modName = miss.BlockedBy == BestMatchResolver.BlockedByModdedTarget
                ? modNameFor?.Invoke(miss)
                : null;

            lines.Add((index == 0 ? "Found /" : "Also found /")
                + miss.Candidate.Command
                + " but "
                + NearMissReason(miss.BlockedBy, Configuration.LoopMatching, Configuration.TurnMatching, modName));
        }

        return lines;
    }

    private void ReportNoMatch(EmoteAttributes source, IReadOnlyList<NearMiss> diagnostics, string skeleton,
        IReadOnlyList<string> fallbackOrder)
    {
        var directoryOf = new Dictionary<uint, string>();

        string? ModNameOf(NearMiss miss)
        {
            if (ChangedByAnotherMod(miss.Candidate, skeleton, fallbackOrder) is not { Length: > 0 } directory)
                return null;

            directoryOf[miss.Candidate.RowId] = directory;
            return ModNameFor(directory);
        }

        var lines = NoMatchLines(source, diagnostics, ModNameOf);
        var chat = NoireLogger.CreateChatMessageBuilder();

        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
                chat.AddText("\n");

            chat.AddText(lines[index], RefusalColor);

            if (index > 0 && diagnostics[index - 1] is { } miss
                && directoryOf.TryGetValue(miss.Candidate.RowId, out var directory))
            {
                ModActionChatPayloads.Append(chat, directory, ModNameFor(directory) ?? directory);
            }
        }

        FeedbackHelper.Error(string.Join('\n', lines), NoMatchKind, chat);
    }

    internal static string NearMissReason(string blockedBy, LoopMatchRule loopRule, TurnMatchRule turnRule,
        string? blockingModName = null)
    {
        if (blockedBy == BestMatchResolver.BlockedByRules)
            return "it is on your blocked targets list.";

        if (blockedBy == BestMatchResolver.BlockedByModdedTarget)
        {
            return string.IsNullOrEmpty(blockingModName)
                ? "another of your mods targets it. Your configuration blocked it."
                : $"your mod \"{blockingModName}\" targets it. Your configuration blocked it.";
        }

        if (blockedBy == "Loop" && loopRule != LoopMatchRule.Strict)
            return "the loop kinds do not match.";

        if (blockedBy == "Turn" && turnRule == TurnMatchRule.VeryStrict)
            return "turn matching is very strict.";

        return $"{blockedBy.ToLowerInvariant()} matching is strict.";
    }
}
