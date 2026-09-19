using System;
using System.Collections.Generic;

namespace BypassEmote.Models;

public sealed record SwapOptionEntry(
    string ContentKey,
    string GroupName,
    string OptionName,
    uint SourceEmote,
    uint TargetEmote,
    bool IsIdlePoseSwap,
    // Skeleton id -> game path -> mod-relative file.
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FilesByRace,
    bool FadeProtectedIntro = false,
    bool ClampedIntro = false,
    bool SelectedByUs = false,
    long LastUsedStamp = 0,
    string? RulesStamp = null,
    string? SourceKey = null,
    byte IdlePoseIndex = 0,
    string? SourceServedBy = null);

public sealed record DispatchRecord(uint SourceEmote, uint TargetEmote, long LastUseStamp,
    string? RulesStamp = null);

public sealed record SwapRegistry(
    int SchemaVersion,
    Guid CollectionId,
    string? Skeleton,
    int AppliedPriority,
    IReadOnlyList<SwapOptionEntry> Entries,
    IReadOnlyList<string>? CompetingMods = null,
    IReadOnlyList<DispatchRecord>? Dispatch = null);

public readonly record struct RaceSourceInput(string Race, string ResolvedSourcePath, long StampTicks, string PathSignature);
