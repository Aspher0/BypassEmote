using Dalamud.Game;
using Dalamud.Utility;
using Newtonsoft.Json;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    private const string CacheFolderName = "FfxivCollectCache";
    private const string LogPrefix = "[FfxivCollect] ";

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

        var response = await GetCachedAsync<FfxivCollectResponse<FfxivCollectEntry>>(url, token).ConfigureAwait(false);

        return response?.Results ?? [];
    }

    // Serves the copy on disk while it is younger than the lifetime, and falls back to a stale copy when the site
    // cannot be reached, so a start without network still has a catalog.
    private static async Task<T?> GetCachedAsync<T>(string url, CancellationToken token) where T : class
    {
        var cachePath = CachePathFor(url);

        if (cachePath != null && IsFresh(cachePath) && ReadCache<T>(cachePath) is { } fresh)
            return fresh;

        var json = await HttpHelper.GetStringAsync(url, token).ConfigureAwait(false);

        if (json == null)
            return cachePath == null ? null : ReadCache<T>(cachePath);

        var parsed = Deserialize<T>(json, url);

        if (parsed != null && cachePath != null)
            WriteCache(cachePath, json);

        return parsed;
    }

    private static string? CachePathFor(string url)
    {
        var configDirectory = FileHelper.GetPluginConfigDirectory();

        if (configDirectory.IsNullOrWhitespace())
            return null;

        // Named after the URL's hash so a query string cannot produce an unusable file name.
        var tag = EncryptionHelper.ShortTag(url, 16);

        return Path.Combine(configDirectory, CacheFolderName, $"{tag}.json");
    }

    private static bool IsFresh(string path)
    {
        try
        {
            return File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < CacheLifetime;
        }
        catch
        {
            // A copy whose timestamp cannot be read cannot be shown to be fresh.
            return false;
        }
    }

    private static T? ReadCache<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? Deserialize<T>(File.ReadAllText(path), path) : null;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError($"Could not read the cached response at '{path}': {ex.Message}", LogPrefix);
            return null;
        }
    }

    private static void WriteCache(string path, string json)
    {
        var directory = Path.GetDirectoryName(path);

        if (!directory.IsNullOrWhitespace() && !FileHelper.EnsureDirectoryExists(directory))
            return;

        FileHelper.ReplaceFileAtomically(path, Encoding.UTF8.GetBytes(json));
    }

    private static T? Deserialize<T>(string json, string source) where T : class
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"The response from '{source}' was not readable as {typeof(T).Name}.", LogPrefix);
            return null;
        }
    }
}
