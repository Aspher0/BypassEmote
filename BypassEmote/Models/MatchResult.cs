using BypassEmote.Enums;
using System.Collections.Generic;

namespace BypassEmote.Models;

public sealed record MatchConfig(LoopMatchRule Loop, TurnMatchRule Turn, SoundMatchRule Sound,
    IReadOnlySet<uint>? BlockedTargets = null,
    IReadOnlySet<uint>? ModdedTargets = null);

public sealed record NearMiss(EmoteAttributes Candidate, string BlockedBy);

public sealed record MatchResult(EmoteAttributes? Target, IReadOnlyList<NearMiss> Diagnostics)
{
    public NearMiss? Diagnostic => Diagnostics.Count > 0 ? Diagnostics[0] : null;
}
