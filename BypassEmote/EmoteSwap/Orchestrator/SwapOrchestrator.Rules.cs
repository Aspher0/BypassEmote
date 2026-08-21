using BypassEmote.Models;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal static string SourceKeyFor(EmoteAttributes source, IReadOnlyList<RaceBuildInput> races)
        => SwapContentKey.ForSource(EmoteAttributeCatalog.RulesVersion, source.RowId,
            [.. races.Select(race => race.Source)]);

    private void DropSwapsTheRulesNoLongerMake(EmoteAttributes source, string sourceKey, uint chosenTarget)
        => _swapMods.ApplyRulesPlan(SwapRulesStamp.Current(), sourceKey, source.RowId, chosenTarget);
}
