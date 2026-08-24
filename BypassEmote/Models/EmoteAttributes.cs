using BypassEmote.Enums;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.Models;

public sealed record VariantPaths(PostureFlags Posture, string RelativePapPath, bool WeaponMotion = false);

public sealed record EmoteAttributes(
    uint RowId, string Command, EmotePlayType LoopKind, SoundClass Sound, TurnClass Turn,
    PostureFlags Postures, bool HasIntro, string? IntroRelativePapPath, bool EligibleTarget,
    IReadOnlyList<VariantPaths> Variants, bool CancelsOnRotate = false, IntroKind Intro = IntroKind.None,
    bool IsPoseFamily = false, IReadOnlyDictionary<string, string>? FaceLibraries = null,
    // ActionTimeline rows of the slots that carry a real body animation.
    IReadOnlyList<ushort>? AnimationTimelineIds = null,
    // Slot 1 plays from a per-weapon folder rather than the shared one.
    bool IntroIsWeaponMotion = false)
{
    public string? FaceLibraryFor(string relativePapPath)
        => FaceLibraries != null && FaceLibraries.TryGetValue(relativePapPath, out var faceLibrary) ? faceLibrary : null;

    public bool HasWeaponMotionVariants()
        => IntroIsWeaponMotion || Variants.Any(variant => variant.WeaponMotion);
}
