using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.Models;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal const string NoOverrideTargetKind = "swap.override-empty";

    private const int MaxRefusalsReported = 3;

    internal static EmoteOverride? OverrideFor(uint resolvedRowId, uint pressedRowId)
        => OverrideResolver.For(Configuration.EmoteOverrides, resolvedRowId)
        ?? OverrideResolver.For(Configuration.EmoteOverrides, pressedRowId);

    private EmoteAttributes? ChooseOverrideTarget(EmoteAttributes source, EmoteOverride configured,
        IReadOnlyList<EmoteAttributes> pool, string skeleton, IReadOnlyList<string> fallbackOrder, Guid collectionId)
    {
        var eligible = OverrideResolver.Eligible(configured, pool);
        var rule = Configuration.ModdedTargets;

        if (eligible.Count > 0 && rule != ModdedTargetRule.Allowed)
        {
            ForgetChangedTargetsOfAnotherCollection(collectionId);
            PrimeChangedTargets(eligible, skeleton, fallbackOrder);

            eligible = OverrideResolver.ApplyModdedRule(eligible, rule,
                candidate => ChangedByAnotherMod(candidate, skeleton, fallbackOrder) != null);
        }

        if (eligible.Count == 0)
            return null;

        var plainBest = eligible[0];

        var staleVulnerable = IsStaleVulnerableShape(plainBest.LoopKind, plainBest.Intro,
            SourceCarriesOwnDistinctIntroFile(source, fallbackOrder));

        return Spreads(staleVulnerable) ? ResolveDispatchedOverride(source, eligible) : plainBest;
    }

    private void ReportNoOverrideTarget(EmoteAttributes source, EmoteOverride configured,
        IReadOnlyList<EmoteAttributes> pool, string skeleton, IReadOnlyList<string> fallbackOrder)
    {
        var usableHere = pool.Select(candidate => candidate.RowId).ToHashSet();
        var rule = Configuration.ModdedTargets;
        var refusals = new List<(uint RowId, OverrideResolver.Refusal Refusal)>(configured.Targets.Count);

        foreach (var rowId in configured.Targets)
        {
            var attributes = _catalog.Get(rowId);
            var unlocked = EmoteHelper.IsEmoteUnlocked(rowId);
            var playableHere = usableHere.Contains(rowId);

            var changed = attributes != null
                && unlocked
                && playableHere
                && rule != ModdedTargetRule.Allowed
                && ChangedByAnotherMod(attributes, skeleton, fallbackOrder) != null;

            var refusal = OverrideResolver.RefusalFor(attributes, unlocked, playableHere, changed, rule);

            if (refusal != OverrideResolver.Refusal.None)
                refusals.Add((rowId, refusal));
        }

        LogHelper.Error(NoOverrideTargetMessage(source, refusals, NameOf), NoOverrideTargetKind);
    }

    internal static ChatText NoOverrideTargetMessage(EmoteAttributes source,
        IReadOnlyList<(uint RowId, OverrideResolver.Refusal Refusal)> refusals, Func<uint, string> nameOf)
    {
        var lines = new List<ChatText>(refusals.Count + 2)
        {
            ChatText.Of(L.NoOverrideTarget, "command", source.Command),
        };

        foreach (var (rowId, refusal) in refusals.Take(MaxRefusalsReported))
            lines.Add(ChatText.Of(L.OverrideSkipped, "reason", (ChatText)OverrideResolver.ReasonText(refusal), "emote", nameOf(rowId)));

        if (refusals.Count > MaxRefusalsReported)
        {
            var more = refusals.Count - MaxRefusalsReported;
            lines.Add(L.CountChat(L.MoreSkipped, more));
        }

        return ChatText.Join("\n", lines);
    }

    internal static string NameOf(uint emoteRowId)
        => EmoteHelper.GetEmoteById(emoteRowId) is { } emote
            ? CommonHelper.GetEmoteName(emote)
            : L.EmoteNumber.With("id", emoteRowId.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
