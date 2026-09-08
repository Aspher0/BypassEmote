using BypassEmote.Enums;
using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public static class OverrideResolver
{
    public enum Refusal
    {
        None,
        NotConfigured,
        Locked,
        NeverATarget,
        NotHere,
        ChangedByAMod,
    }

    public static EmoteOverride? For(IReadOnlyList<EmoteOverride>? overrides, uint sourceRowId)
        => overrides?.FirstOrDefault(entry => entry.SourceEmote == sourceRowId && entry.Targets.Count > 0);

    public static bool Covers(IReadOnlyList<EmoteOverride>? overrides, uint sourceRowId)
        => For(overrides, sourceRowId) != null;

    public static List<EmoteAttributes> Eligible(EmoteOverride configured, IReadOnlyList<EmoteAttributes> pool)
    {
        var byRowId = new Dictionary<uint, EmoteAttributes>(pool.Count);

        foreach (var candidate in pool)
            byRowId.TryAdd(candidate.RowId, candidate);

        var eligible = new List<EmoteAttributes>(configured.Targets.Count);
        var taken = new HashSet<uint>();

        foreach (var rowId in configured.Targets)
        {
            if (byRowId.TryGetValue(rowId, out var candidate) && taken.Add(rowId))
                eligible.Add(candidate);
        }

        return eligible;
    }

    public static List<EmoteAttributes> ApplyModdedRule(List<EmoteAttributes> eligible, ModdedTargetRule rule,
        Func<EmoteAttributes, bool> changedByAnotherMod)
    {
        if (rule == ModdedTargetRule.Allowed || eligible.Count == 0)
            return eligible;

        var clean = eligible.Where(candidate => !changedByAnotherMod(candidate)).ToList();

        if (clean.Count > 0)
            return clean;

        return rule == ModdedTargetRule.Blocked ? [] : eligible;
    }

    public static Refusal RefusalFor(EmoteAttributes? attributes, bool unlocked, bool usableHere,
        bool changedByAnotherMod, ModdedTargetRule rule)
    {
        if (attributes == null)
            return Refusal.NotConfigured;

        if (!attributes.EligibleTarget)
            return Refusal.NeverATarget;

        if (!unlocked)
            return Refusal.Locked;

        if (!usableHere)
            return Refusal.NotHere;

        if (changedByAnotherMod && rule == ModdedTargetRule.Blocked)
            return Refusal.ChangedByAMod;

        return Refusal.None;
    }

    public static string ReasonText(Refusal refusal) => refusal switch
    {
        Refusal.Locked => "you have not unlocked it",
        Refusal.NeverATarget => "it can never be a swap target",
        Refusal.NotHere => "it cannot be played in your current state",
        Refusal.ChangedByAMod => "one of your mods changes it, and your settings block those",
        Refusal.NotConfigured => "there is no animation data for it",
        _ => "it is available",
    };
}
