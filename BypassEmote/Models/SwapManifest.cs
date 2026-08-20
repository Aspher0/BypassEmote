using System;
using System.Collections.Generic;

namespace BypassEmote.Models;

/// <summary> The swap currently live in Penumbra, written to swap_manifest.json so a crash mid-swap can still be undone. </summary>
public sealed record SwapManifest(
    int SchemaVersion,
    uint SourceEmote,
    uint TargetEmote,
    string ResolvedSourcePath,
    // The source file as it was when it was read. Compared for exact equality, never for "newer than".
    long SourceStampTicks,
    Guid EnabledInCollection,
    // Game path -> where the file sits under the storage root: "swaps/{file}" for a real mod, "{file}" for a temporary one.
    IReadOnlyDictionary<string, string> RedirectedPaths,
    int AppliedPriority,
    int FlavorUsed, // (int)SwapModFlavor
    // No target emote: the redirects cover the player's own pose paps, and taking them down needs a redraw.
    bool IsIdlePoseSwap = false,
    bool FadeProtectedIntro = false, // the intro channel carries a real intro, at its full length
    bool ClampedIntro = false, // intro and loop were folded into one pap, the intro cut down to a frame
    bool UniqueNames = false, // every file in this swap is named after its own content
    IReadOnlyDictionary<string, string>? UniqueNameByKey = null, // ActionTimeline key -> the name the loader substitutes
    bool InternalUniqueNames = false, // gates the execute-tail wait skip; inert while false
    IReadOnlyList<string>? InternalNames = null, // the animation names these paps declare, for the residency probe
    string? Skeleton = null, // the body the bone data came from, which is the only body the output suits
    // Which paps it reads and which paths it serves them over. Two bodies landing on the same one must produce
    // byte-identical files, or one of them is served the other's.
    string? PathSignature = null,
    // By name, not by path: a mod ranked under this one still exists but never shows up in path resolution.
    IReadOnlyList<string>? CompetingMods = null);
