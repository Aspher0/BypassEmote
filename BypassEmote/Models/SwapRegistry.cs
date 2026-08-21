using System;
using System.Collections.Generic;

namespace BypassEmote.Models;

/// <summary> One built swap, kept as one option of the generated mod. </summary>
public sealed record SwapOptionEntry(
    string ContentKey,
    string GroupName,
    string OptionName,
    uint SourceEmote,
    uint TargetEmote,
    bool IsIdlePoseSwap,
    // Skeleton id -> game path -> mod-relative file. Every race the build covered, so a body change is a rewrite.
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FilesByRace,
    IReadOnlyDictionary<string, string>? UniqueNameByKey = null,
    IReadOnlyList<string>? InternalNames = null,
    bool FadeProtectedIntro = false,
    bool ClampedIntro = false,
    bool UniqueNames = false,
    bool InternalUniqueNames = false,
    // The plugin selected this option and may deselect it. A hand-made selection is never armed.
    bool SelectedByUs = false,
    long LastUsedStamp = 0,
    // The settings this target was chosen under. Anything else means the rules moved, so the next swap judges it.
    string? RulesStamp = null);

/// <summary> The target emote one source was handed, kept so it stays the same across restarts. </summary>
public sealed record DispatchRecord(uint SourceEmote, uint TargetEmote, long LastUseStamp);

/// <summary> Everything the group files cannot say about the options they hold. </summary>
public sealed record SwapRegistry(
    int SchemaVersion,
    Guid CollectionId,
    string? Skeleton,
    int AppliedPriority,
    IReadOnlyList<SwapOptionEntry> Entries,
    IReadOnlyList<string>? CompetingMods = null,
    IReadOnlyList<DispatchRecord>? Dispatch = null);

/// <summary> How one race resolved at build time: the file it reads and the paths it serves. </summary>
public readonly record struct RaceSourceInput(string Race, string ResolvedSourcePath, long StampTicks, string PathSignature);
