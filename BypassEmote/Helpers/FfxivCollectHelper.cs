using Dalamud.Game;
using Dalamud.Utility;
using Newtonsoft.Json;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BypassEmote.Helpers;

public sealed class FfxivCollectSource
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("text")]
    public string? Text { get; set; }
}

public sealed class FfxivCollectEntry
{
    [JsonProperty("id")]
    public int? Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("command")]
    public string? Command { get; set; }

    [JsonProperty("patch")]
    public string? Patch { get; set; }

    [JsonProperty("sources")]
    public List<FfxivCollectSource>? Sources { get; set; }

    public IReadOnlyList<string> Commands()
    {
        if (Command.IsNullOrWhitespace())
            return [];

        var parts = Command!.Split(',');
        var commands = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (!trimmed.IsNullOrEmpty())
                commands.Add(trimmed);
        }

        return commands;
    }
}

public sealed class FfxivCollectResponse<T>
{
    [JsonProperty("count")]
    public int Count { get; set; }

    [JsonProperty("results")]
    public List<T> Results { get; set; } = [];
}

internal static class FfxivCollectHelper
{
    private const string BaseUrl = "https://ffxivcollect.com/api";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);

    internal static string LocaleParam(ClientLanguage? language = null)
        => (language ?? NoireService.ClientState.ClientLanguage) switch
        {
            ClientLanguage.French => "fr",
            ClientLanguage.German => "de",
            ClientLanguage.Japanese => "ja",
            _ => "en",
        };

    internal static async Task<IReadOnlyList<FfxivCollectEntry>> GetEmotesAsync(CancellationToken token = default)
    {
        var url = $"{BaseUrl}/emotes?language={LocaleParam()}";

        var response = await RemoteJsonHelper
            .GetCachedJsonAsync<FfxivCollectResponse<FfxivCollectEntry>>(url, CacheLifetime, token)
            .ConfigureAwait(false);

        return response?.Results ?? [];
    }
}
