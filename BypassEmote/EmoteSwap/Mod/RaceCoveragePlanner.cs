using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed record RacePaths(string Skeleton, IReadOnlyList<string> SourcePaths, IReadOnlyList<string> TargetPaths);

// Determines who a written-out mod actually reaches.
internal static class RaceCoveragePlanner
{
    public sealed record SharedFile(string Winner, IReadOnlyList<string> Losers);

    public sealed record Plan(IReadOnlyList<SharedFile> Shared, IReadOnlyList<string> AlsoReached);

    public static Plan For(IReadOnlyList<string> orderedRaces, IReadOnlySet<string> picked,
        Func<string, RacePaths?> pathsFor)
    {
        var supplierOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var losersByWinner = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var winnersInOrder = new List<string>();
        var pathsByRace = new Dictionary<string, RacePaths>(StringComparer.Ordinal);

        foreach (var race in orderedRaces)
        {
            if (!picked.Contains(race) || pathsFor(race) is not { } paths)
                continue;

            pathsByRace[race] = paths;

            foreach (var targetPath in paths.TargetPaths)
            {
                if (!supplierOf.TryGetValue(targetPath, out var winner))
                {
                    supplierOf[targetPath] = race;
                    continue;
                }

                if (race == winner || SameSource(pathsByRace[winner], paths))
                    continue;

                if (!losersByWinner.TryGetValue(winner, out var losers))
                {
                    losers = [];
                    losersByWinner[winner] = losers;
                    winnersInOrder.Add(winner);
                }

                if (!losers.Contains(race, StringComparer.Ordinal))
                    losers.Add(race);
            }
        }

        var alsoReached = new List<string>();

        foreach (var race in orderedRaces)
        {
            if (picked.Contains(race) || pathsFor(race) is not { } paths)
                continue;

            if (paths.TargetPaths.Any(supplierOf.ContainsKey))
                alsoReached.Add(race);
        }

        return new Plan(
            [.. winnersInOrder.Select(winner => new SharedFile(winner, losersByWinner[winner]))],
            alsoReached);
    }

    private static bool SameSource(RacePaths left, RacePaths right)
        => left.SourcePaths.SequenceEqual(right.SourcePaths, StringComparer.Ordinal);
}
