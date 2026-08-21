using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

internal static class RegistryDecisions
{
    internal sealed record ReconcilePlan(
        IReadOnlyList<SwapOptionEntry> Entries,
        IReadOnlyList<(string Group, string Option)> OrphanOptions);

    internal static SwapOptionEntry? FindByKey(SwapRegistry registry, string contentKey)
        => registry.Entries.FirstOrDefault(entry => entry.ContentKey == contentKey);

    internal static SwapOptionEntry? FindArmedByTarget(SwapRegistry registry, uint targetEmote)
        => registry.Entries.FirstOrDefault(entry => entry.SelectedByUs && entry.TargetEmote == targetEmote);

    internal static SwapOptionEntry? EvictionCandidate(SwapRegistry registry, string groupName, int cap)
    {
        if (cap <= 0)
            return null;

        var inGroup = registry.Entries.Where(entry => entry.GroupName == groupName).ToList();

        if (inGroup.Count < cap)
            return null;

        return inGroup.Where(entry => !entry.SelectedByUs)
            .OrderBy(entry => entry.LastUsedStamp)
            .FirstOrDefault();
    }

    internal static ReconcilePlan Reconcile(SwapRegistry registry,
        IReadOnlyDictionary<string, IReadOnlyList<string>> optionsOnDisk,
        IReadOnlyDictionary<string, string> selectedOptionByGroup)
    {
        var kept = new List<SwapOptionEntry>(registry.Entries.Count);
        var claimed = new HashSet<(string, string)>();

        foreach (var entry in registry.Entries)
        {
            if (!optionsOnDisk.TryGetValue(entry.GroupName, out var options) || !options.Contains(entry.OptionName))
                continue;

            claimed.Add((entry.GroupName, entry.OptionName));

            var selected = selectedOptionByGroup.TryGetValue(entry.GroupName, out var selection)
                && selection == entry.OptionName;

            kept.Add(entry with { SelectedByUs = entry.SelectedByUs && selected });
        }

        var orphans = new List<(string, string)>();

        foreach (var (groupName, options) in optionsOnDisk)
        {
            foreach (var option in options)
            {
                if (option != OptionNaming.NoneOptionName && !claimed.Contains((groupName, option)))
                    orphans.Add((groupName, option));
            }
        }

        return new ReconcilePlan(kept, orphans);
    }

    internal sealed record RulesPlan(IReadOnlyList<SwapOptionEntry> Entries, IReadOnlyList<SwapOptionEntry> Dropped);

    internal static RulesPlan PlanForSwap(SwapRegistry registry, string stamp, string sourceKey, uint sourceEmote,
        uint keptTarget, string? pressedKey, int keptCap)
    {
        var kept = new List<SwapOptionEntry>(registry.Entries.Count);
        var dropped = new List<SwapOptionEntry>();

        foreach (var entry in registry.Entries)
        {
            if (!Concerns(entry, stamp, sourceKey, sourceEmote) || entry.ContentKey == pressedKey)
            {
                kept.Add(entry);
                continue;
            }

            if (entry.TargetEmote == keptTarget)
                kept.Add(entry with { RulesStamp = stamp, SourceKey = sourceKey });
            else
                dropped.Add(entry);
        }

        TrimToCap(kept, dropped, pressedKey, keptCap);

        return new RulesPlan(kept, dropped);
    }

    private static bool Concerns(SwapOptionEntry entry, string stamp, string sourceKey, uint sourceEmote)
    {
        if (entry.SourceKey == null)
            return entry.SourceEmote == sourceEmote;

        return entry.SourceKey == sourceKey && entry.RulesStamp != stamp;
    }

    private static void TrimToCap(List<SwapOptionEntry> kept, List<SwapOptionEntry> dropped, string? pressedKey, int cap)
    {
        if (cap <= 0)
            return;

        foreach (var group in kept.GroupBy(entry => entry.GroupName, StringComparer.Ordinal).ToList())
        {
            var overCap = group.Count() - cap;

            if (overCap <= 0)
                continue;

            var goners = group
                .Where(entry => !entry.SelectedByUs && entry.ContentKey != pressedKey)
                .OrderBy(entry => entry.LastUsedStamp)
                .Take(overCap)
                .ToList();

            foreach (var goner in goners)
            {
                kept.RemoveAll(candidate => candidate.ContentKey == goner.ContentKey);
                dropped.Add(goner);
            }
        }
    }
}
