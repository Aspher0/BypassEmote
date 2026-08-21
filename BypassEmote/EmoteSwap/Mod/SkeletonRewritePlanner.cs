using BypassEmote.Models;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

// A body change moves every option onto the newly drawn race's files
internal static class SkeletonRewritePlanner
{
    internal sealed record OptionRewrite(string GroupName, string OptionName, IReadOnlyDictionary<string, string> Files);

    internal sealed record RewritePlan(IReadOnlyList<OptionRewrite> Rewrites, IReadOnlyList<string> UncoveredKeys);

    internal static RewritePlan For(SwapRegistry registry, string newSkeleton)
    {
        var rewrites = new List<OptionRewrite>();
        var uncovered = new List<string>();

        if (registry.Skeleton == newSkeleton)
            return new RewritePlan(rewrites, uncovered);

        foreach (var entry in registry.Entries)
        {
            if (entry.FilesByRace.TryGetValue(newSkeleton, out var files))
                rewrites.Add(new OptionRewrite(entry.GroupName, entry.OptionName, files));
            else
                uncovered.Add(entry.ContentKey);
        }

        return new RewritePlan(rewrites, uncovered);
    }
}
