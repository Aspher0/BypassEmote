using System.Collections.Generic;

namespace BypassEmote.Models;

public enum SoundClass : int
{
    Silent = 0,
    Sfx = 1,
    Voiceline = 2,
}

public enum TurnClass : int
{
    None = 0,
    Eyes = 1,
    Head = 2,
    Body = 3,
    Unknown = 4,
}

public sealed record VariantPaths(PostureFlags Posture, string RelativePapPath);

public enum IntroKind : int
{
    None = 0,
    Pap = 1,
    TmbOnly = 2,
}

/// <summary> Catalog attributes for one emote. IntroRelativePapPath is null when slot 1 is per-job or facial and has no shared pap. </summary>
public sealed record EmoteAttributes(
    uint RowId, string Command, EmotePlayType LoopKind, SoundClass Sound, TurnClass Turn,
    PostureFlags Postures, bool HasIntro, string? IntroRelativePapPath, bool EligibleTarget,
    IReadOnlyList<VariantPaths> Variants, bool CancelsOnRotate = false, IntroKind Intro = IntroKind.None,
    bool IsPoseFamily = false, IReadOnlyDictionary<string, string>? FaceLibraries = null,
    // ActionTimeline rows of the slots that carry a real body animation.
    IReadOnlyList<ushort>? AnimationTimelineIds = null)
{
    public string? FaceLibraryFor(string relativePapPath)
        => FaceLibraries != null && FaceLibraries.TryGetValue(relativePapPath, out var faceLibrary) ? faceLibrary : null;
}
