using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.Models;
using NoireLib;
using System;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private static readonly System.Numerics.Vector3 RefusalColor = NoireLib.Helpers.ColorHelper.HexToVector3("#E81313");
    private static readonly System.Numerics.Vector3 NoticeColor = NoireLib.Helpers.ColorHelper.HexToVector3("#FF8C1A");

    internal static ChatText NoMatchMessage(EmoteAttributes source, IReadOnlyList<NearMiss> diagnostics,
        Func<NearMiss, string?>? modNameFor = null)
        => ChatText.Join("\n", NoMatchLines(source, diagnostics, modNameFor));

    internal static IReadOnlyList<ChatText> NoMatchLines(EmoteAttributes source, IReadOnlyList<NearMiss> diagnostics,
        Func<NearMiss, string?>? modNameFor = null)
    {
        var lines = new List<ChatText>(diagnostics.Count + 1) { ChatText.Of(L.CouldNotSwap, "command", source.Command) };

        for (var index = 0; index < diagnostics.Count; index++)
        {
            var miss = diagnostics[index];

            var modName = miss.BlockedBy == BestMatchResolver.BlockedByModdedTarget
                ? modNameFor?.Invoke(miss)
                : null;

            lines.Add(ChatText.Of(index == 0 ? L.FoundBut : L.AlsoFoundBut, "reason",
                NearMissReason(miss.BlockedBy, Configuration.LoopMatching, Configuration.TurnMatching, Configuration.SoundMatching, modName),
                "command", miss.Candidate.Command));
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

            chat.AddText(lines[index].Display, RefusalColor);

            if (index > 0 && diagnostics[index - 1] is { } miss
                && directoryOf.TryGetValue(miss.Candidate.RowId, out var directory))
            {
                ModActionChatPayloads.Append(chat, directory, ModNameFor(directory) ?? directory);
            }
        }

        LogHelper.Error(ChatText.Join("\n", lines), "swap.no-match", chat);
    }

    internal static ChatText NearMissReason(string blockedBy, LoopMatchRule loopRule, TurnMatchRule turnRule,
        SoundMatchRule soundRule, string? blockingModName = null)
    {
        if (blockedBy == BestMatchResolver.BlockedByRules)
            return L.NearBlockedList;

        if (blockedBy == BestMatchResolver.BlockedByModdedTarget)
        {
            return string.IsNullOrEmpty(blockingModName)
                ? L.NearOtherMod
                : ChatText.Of(L.NearNamedMod, "mod", blockingModName);
        }

        if (blockedBy == "Loop" && loopRule != LoopMatchRule.Strict)
            return L.NearLoopKinds;

        if (blockedBy == "Turn" && turnRule == TurnMatchRule.VeryStrict)
            return L.NearTurnVeryStrict;

        if (blockedBy == "Sound")
        {
            return soundRule switch
            {
                SoundMatchRule.Strict => L.NearSoundStrict,
                SoundMatchRule.Lenient => L.NearSoundLenient,
                _ => L.NearSound,
            };
        }

        return ChatText.Of(L.NearStrict, "rule", blockedBy.ToLowerInvariant());
    }
}
