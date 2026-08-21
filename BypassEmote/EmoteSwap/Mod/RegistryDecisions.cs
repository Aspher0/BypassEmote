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

    /// <summary> What the rules in force say about an option that was chosen under older ones. </summary>
    internal enum RulesVerdict
    {
        /// <summary> Nothing at hand can judge it, so it waits for a swap that can. </summary>
        Unknown,

        /// <summary> The rules would still hand this source this target. </summary>
        Keep,

        /// <summary> They would not, so the option has to go. </summary>
        Drop,
    }

    internal sealed record RulesPlan(IReadOnlyList<SwapOptionEntry> Entries, IReadOnlyList<SwapOptionEntry> Dropped);

    internal static RulesPlan JudgeAgainstRules(SwapRegistry registry, string stamp, string? pressedKey,
        Func<SwapOptionEntry, RulesVerdict> judge, int keptCap, int maxJudgements)
    {
        var kept = new List<SwapOptionEntry>(registry.Entries.Count);
        var dropped = new List<SwapOptionEntry>();
        var judged = 0;

        foreach (var entry in registry.Entries)
        {
            if (entry.RulesStamp == stamp || entry.ContentKey == pressedKey || judged >= maxJudgements)
            {
                kept.Add(entry);
                continue;
            }

            switch (judge(entry))
            {
                case RulesVerdict.Drop:
                    judged++;
                    dropped.Add(entry);
                    break;

                case RulesVerdict.Keep:
                    judged++;
                    kept.Add(entry with { RulesStamp = stamp });
                    break;

                default:
                    kept.Add(entry);
                    break;
            }
        }

        TrimToCap(kept, dropped, pressedKey, keptCap);

        return new RulesPlan(kept, dropped);
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
