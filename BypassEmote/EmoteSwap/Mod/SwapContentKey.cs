using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BypassEmote.EmoteSwap;

internal static class SwapContentKey
{
    private const int KeyLength = 16;

    internal static string For(int rulesVersion, uint targetEmote, uint sourceEmote, IReadOnlyList<RaceSourceInput> races)
    {
        var material = new StringBuilder();

        material.Append("v").Append(rulesVersion)
            .Append("|tgt=").Append(targetEmote)
            .Append("|src=").Append(sourceEmote);

        foreach (var race in races.OrderBy(race => race.Race, StringComparer.Ordinal))
        {
            material.Append('|').Append(race.Race)
                .Append(':').Append(race.ResolvedSourcePath)
                .Append(':').Append(race.StampTicks)
                .Append(':').Append(race.PathSignature);
        }

        return Digest(material);
    }

    internal static string ForSource(int rulesVersion, uint sourceEmote, IReadOnlyList<RaceSourceInput> races)
    {
        var material = new StringBuilder();

        material.Append("v").Append(rulesVersion).Append("|src=").Append(sourceEmote);

        foreach (var race in races.OrderBy(race => race.Race, StringComparer.Ordinal))
        {
            material.Append('|').Append(race.Race)
                .Append(':').Append(race.ResolvedSourcePath)
                .Append(':').Append(race.StampTicks);
        }

        return Digest(material);
    }

    private static string Digest(StringBuilder material)
        => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(material.ToString())))
            .ToLowerInvariant()[..KeyLength];
}
