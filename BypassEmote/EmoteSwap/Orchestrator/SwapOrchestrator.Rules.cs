using BypassEmote.Models;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private void DropSwapsTheRulesNoLongerMake(IReadOnlyList<EmoteAttributes> pool, MatchConfig matchConfig,
        PostureFlags posture, IReadOnlyList<string> fallbackOrder)
    {
        var stamp = SwapRulesStamp.Current();

        if (_swapMods.Registry.Entries.All(entry => entry.RulesStamp == stamp))
            return;

        _swapMods.ApplyRulesPlan(stamp, entry => JudgeKeptSwap(entry, pool, matchConfig, posture, fallbackOrder));
    }

    private RegistryDecisions.RulesVerdict JudgeKeptSwap(SwapOptionEntry entry, IReadOnlyList<EmoteAttributes> pool,
        MatchConfig matchConfig, PostureFlags posture, IReadOnlyList<string> fallbackOrder)
    {
        if (entry.IsIdlePoseSwap)
        {
            return Configuration.IdlePoseLoops == IdlePoseFallback.Never
                ? RegistryDecisions.RulesVerdict.Drop
                : RegistryDecisions.RulesVerdict.Keep;
        }

        if (_catalog.Get(entry.SourceEmote) is not { } source || _catalog.Get(entry.TargetEmote) is not { } target)
            return RegistryDecisions.RulesVerdict.Unknown;

        if (!pool.Any(candidate => candidate.RowId == entry.TargetEmote))
            return RegistryDecisions.RulesVerdict.Unknown;

        var staleVulnerable = IsStaleVulnerableShape(target.LoopKind, target.Intro,
            SourceCarriesOwnDistinctIntroFile(source, fallbackOrder));

        if (!Spreads(staleVulnerable))
        {
            return BestMatchResolver.Resolve(source, pool, matchConfig, posture).Target?.RowId == entry.TargetEmote
                ? RegistryDecisions.RulesVerdict.Keep
                : RegistryDecisions.RulesVerdict.Drop;
        }

        var tier = BestMatchResolver.ResolveSameTier(source, pool, matchConfig, posture,
            wantedCount: RankBudget(),
            maxScoreDistance: BestMatchResolver.DistanceFor(Configuration.DispatchFidelity)).Tier;

        return tier.Any(candidate => candidate.RowId == entry.TargetEmote)
            ? RegistryDecisions.RulesVerdict.Keep
            : RegistryDecisions.RulesVerdict.Drop;
    }
}
