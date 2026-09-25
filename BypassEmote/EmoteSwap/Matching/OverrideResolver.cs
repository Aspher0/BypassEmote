using BypassEmote.Enums;
using BypassEmote.Localization;
using BypassEmote.Models;
using NoireLib.Localizer;
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

    public static NoireString ReasonText(Refusal refusal) => refusal switch
    {
        Refusal.Locked => L.ReasonLocked,
        Refusal.NeverATarget => L.ReasonNeverATarget,
        Refusal.NotHere => L.ReasonNotHere,
        Refusal.ChangedByAMod => L.ReasonChangedByAMod,
        Refusal.NotConfigured => L.ReasonNotConfigured,
        _ => L.ReasonAvailable,
    };
}
