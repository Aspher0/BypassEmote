namespace BypassEmote.Models;

public enum SelfBypassMode : int
{
    DirectPlay = 0,
    EmoteSwap = 1,
}

public enum SwapModFlavor : int
{
    RealMod = 0,
    TemporaryMod = 1,
}

public enum SwapLifetime : int
{
    Ephemeral = 0,
    Lingering = 1,
}

public enum LoopMatchRule : int
{
    Strict = 0,
    AllowLoopOnOneShot = 1,
}

/// <summary> Whether a swap may move to a second equally good emote rather than wait for the first to be free. </summary>
public enum AlternateTargetsMode : int
{
    /// <summary> Never alternate. </summary>
    Off = 0,

    /// <summary> Alternates 2 emotes only while the cache break is down and a repeat would show stale frames. </summary>
    TwoEmotesWhenCacheBreakDown = 1,

    /// <summary> Alternates 2 emotes at all times. </summary>
    TwoEmotes = 2,
}

public enum TurnMatchRule : int
{
    Strict = 0,
    Lenient = 1,
    VeryStrict = 2,
}

public enum SoundMatchRule : int
{
    Strict = 0,
    Lenient = 1,
}

public enum IdlePoseFallback : int
{
    Never = 0,
    NothingElseFits = 1,
    Allowed = 2,
}

/// <summary>
/// What a swap does about a target emote one of the player's other mods already replaces.
/// </summary>
public enum ModdedTargetRule : int
{
    /// <summary> Land on them like any other emote. </summary>
    Allowed = 0,

    /// <summary> Step over them, and take one only when nothing else fits. </summary>
    LastResort = 1,

    /// <summary> Never take one, even when it costs the swap. </summary>
    Blocked = 2,
}
