using System.Collections.Generic;

namespace BypassEmote.Models;

public sealed record MatchConfig(LoopMatchRule Loop, TurnMatchRule Turn, SoundMatchRule Sound,
    IReadOnlySet<uint>? BlockedTargets = null,
    // Emotes another of the player's mods already replaces, refused when ModdedTargetRule.Blocked.
    IReadOnlySet<uint>? ModdedTargets = null);

public sealed record NearMiss(EmoteAttributes Candidate, string BlockedBy); // the first hard filter it failed: "Loop" | "Sound" | "Turn" | "Rules" | "Modded"

/// <summary> The emote a swap should land on, and the emotes it could not use. </summary>
/// <param name="Target">The emote to swap onto, or null when nothing fitted.</param>
/// <param name="Diagnostics">Near misses, nearest first, one reason each. Empty when nothing was refused.</param>
public sealed record MatchResult(EmoteAttributes? Target, IReadOnlyList<NearMiss> Diagnostics)
{
    public NearMiss? Diagnostic => Diagnostics.Count > 0 ? Diagnostics[0] : null;
}
