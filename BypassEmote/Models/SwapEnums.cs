namespace BypassEmote.Models;

public enum SelfBypassMode : int
{
    DirectPlay = 0,
    EmoteSwap = 1,
}

public enum SwapLifetime : int
{
    WhenEmoteEnds = 0,
    WhenTargetPlayed = 1,
    Never = 2,
}

public enum SwapBehavior : int
{
    KeepAll = 0,
    OneAtATime = 1,
}

public enum LoopMatchRule : int
{
    Strict = 0,
    AllowLoopOnOneShot = 1,
}

public enum CachedDispatchMode : int
{
    Off = 0,
    WhenNecessary = 1,
    On = 2,
}

public enum DispatchFidelity : int
{
    SameRank = 0,
    OneRankBelow = 1,
    AnythingAllowed = 2,
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
    Off = 2,
}

public enum IdlePoseFallback : int
{
    Never = 0,
    NothingElseFits = 1,
    Allowed = 2,
}

public enum ModdedTargetRule : int
{
    Allowed = 0,
    LastResort = 1,
    Blocked = 2,
}
