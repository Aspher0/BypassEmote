using BypassEmote.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BypassEmote.EmoteSwap;

// The settings that decide which target a source is given. An option stamped with anything else was chosen under
// rules that have changed since, so the next swap judges it again before it stays in the mod.
internal static class SwapRulesStamp
{
    internal static string Current()
        => Compose(Configuration.LoopMatching, Configuration.TurnMatching, Configuration.SoundMatching,
            Configuration.ModdedTargets, Configuration.IdlePoseLoops, Configuration.CachedDispatch,
            Configuration.MaxTargetsPerRank, Configuration.DispatchFidelity, Configuration.MaxKeptSwapsPerTarget,
            Configuration.BlockedTargetEmotesEmoteSwap);

    internal static string Compose(LoopMatchRule loop, TurnMatchRule turn, SoundMatchRule sound,
        ModdedTargetRule modded, IdlePoseFallback idlePose, CachedDispatchMode dispatch, int targetsPerRank,
        DispatchFidelity fidelity, int keptPerTarget, IReadOnlyCollection<uint> blockedTargets)
    {
        var material = new StringBuilder()
            .Append("l").Append((int)loop)
            .Append("t").Append((int)turn)
            .Append("s").Append((int)sound)
            .Append("m").Append((int)modded)
            .Append("i").Append((int)idlePose)
            .Append("d").Append((int)dispatch)
            .Append("n").Append(targetsPerRank)
            .Append("f").Append((int)fidelity)
            .Append("k").Append(keptPerTarget)
            .Append("b");

        foreach (var rowId in blockedTargets.Distinct().OrderBy(rowId => rowId))
            material.Append(rowId).Append(',');

        return material.ToString();
    }
}
