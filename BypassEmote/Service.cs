using BypassEmote.IPC;
using BypassEmote.Models;
using Dalamud.Game;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BypassEmote;

public class Service
{
    public static Plugin Plugin { get; set; } = null!;
    public static SimpleHeels_IPC_Caller SimpleHeelsIpcCaller { get; } = new SimpleHeels_IPC_Caller();
    public static IpcProvider Ipc { get; private set; } = new IpcProvider();

    public static List<(Emote, NoireLib.Enums.EmoteCategory)> LockedEmotes = [];

    // Dictionary: Emote RowId -> (patch, List of (source type, source text)) from ffxivcollect
    public static Dictionary<uint, (string? Patch, List<(string Type, string Text)> Sources)> EmoteSources { get; } = new();

    public static ActionTimelinePlayer Player = new ActionTimelinePlayer();

    private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public static void InitializeService(Plugin plugin)
    {
        Plugin = plugin;

        NoireService.ClientState.Login += RefreshLockedEmotes;
        NoireService.ClientState.Logout += (int type, int code) => ClearLockedEmotes();

        _ = FetchAndBuildEmoteSourcesAsync();

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            if (NoireService.ClientState.IsLoggedIn && NoireService.ObjectTable.LocalPlayer != null)
                RefreshLockedEmotes();
        });
    }

    public static void RefreshLockedEmotes()
    {
        ClearLockedEmotes();

        if (!NoireService.ClientState.IsLoggedIn || NoireService.ObjectTable.LocalPlayer == null)
            return;

        var emoteSheet = ExcelSheetHelper.GetSheet<Emote>();

        if (emoteSheet == null)
            return;

        foreach (var emote in emoteSheet)
        {
            if (!EmoteHelper.IsEmoteUnlocked(emote.RowId))
                LockedEmotes.Add((emote, EmoteHelper.GetEmoteCategory(emote)));
        }
    }

    public static void ClearLockedEmotes()
    {
        LockedEmotes.Clear();
    }

    public static void OpenKofi() => SystemHelper.OpenUrl("https://ko-fi.com/aspher0");

    private static string GetGameLocaleParam()
    {
        var lang = NoireService.ClientState.ClientLanguage;

        return lang switch
        {
            ClientLanguage.French => "fr",
            ClientLanguage.German => "de",
            ClientLanguage.Japanese => "ja",
            _ => "en",
        };
    }

    private static async Task FetchAndBuildEmoteSourcesAsync()
    {
        try
        {
            var lang = GetGameLocaleParam();
            var url = $"https://ffxivcollect.com/api/emotes?language={lang}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<FfxivCollectResponse>(json, JsonOptions);
            if (data?.results is null || data.results.Count == 0)
            {
                NoireLogger.LogWarning<Service>("FFXIVCollect emotes API returned no results.");
                return;
            }

            lock (EmoteSources)
                EmoteSources.Clear();

            foreach (var r in data.results)
            {
                if (string.IsNullOrWhiteSpace(r.command))
                    continue;

                var parts = r.command.Split(',');
                var matched = false;
                foreach (var part in parts)
                {
                    var cmd = part.Trim();
                    if (string.IsNullOrEmpty(cmd)) continue;

                    var emote = EmoteHelper.GetEmoteByCommand(cmd);
                    if (emote.HasValue)
                    {
                        var entries = new HashSet<(string Type, string Text)>();
                        if (r.sources != null)
                        {
                            foreach (var s in r.sources)
                            {
                                if (!string.IsNullOrWhiteSpace(s.text))
                                {
                                    var ty = string.IsNullOrWhiteSpace(s.type) ? "Unknown" : s.type!;
                                    entries.Add((ty, s.text!));
                                }
                            }
                        }

                        if (entries.Count > 0 || !string.IsNullOrWhiteSpace(r.patch))
                        {
                            lock (EmoteSources)
                            {
                                if (!EmoteSources.TryGetValue(emote.Value.RowId, out var existing))
                                {
                                    existing = (r.patch, new List<(string Type, string Text)>());
                                    EmoteSources[emote.Value.RowId] = existing;
                                }
                                else
                                {
                                    // Update patch if we don't have one yet
                                    if (string.IsNullOrWhiteSpace(existing.Patch) && !string.IsNullOrWhiteSpace(r.patch))
                                    {
                                        existing.Patch = r.patch;
                                        EmoteSources[emote.Value.RowId] = existing;
                                    }
                                }

                                foreach (var t in entries)
                                {
                                    if (!existing.Sources.Contains(t))
                                        existing.Sources.Add(t);
                                }
                            }
                        }

                        matched = true;
                        // Do not break; multiple commands might map to the same emote but it's fine
                    }
                }

                if (!matched && r.id.HasValue)
                {
                    // Optional: try fallback match by id
                    var id = (uint)r.id.Value;
                    var emote = EmoteHelper.GetEmoteById(id);
                    if (emote.HasValue)
                    {
                        var entries = new HashSet<(string Type, string Text)>();
                        if (r.sources != null)
                        {
                            foreach (var s in r.sources)
                            {
                                if (!string.IsNullOrWhiteSpace(s.text))
                                {
                                    var ty = string.IsNullOrWhiteSpace(s.type) ? "Unknown" : s.type!;
                                    entries.Add((ty, s.text!));
                                }
                            }
                        }

                        if (entries.Count > 0 || !string.IsNullOrWhiteSpace(r.patch))
                        {
                            lock (EmoteSources)
                            {
                                if (!EmoteSources.TryGetValue(emote.Value.RowId, out var existing))
                                {
                                    existing = (r.patch, new List<(string Type, string Text)>());
                                    EmoteSources[emote.Value.RowId] = existing;
                                }
                                else
                                {
                                    // Update patch if we don't have one yet
                                    if (string.IsNullOrWhiteSpace(existing.Patch) && !string.IsNullOrWhiteSpace(r.patch))
                                    {
                                        existing.Patch = r.patch;
                                        EmoteSources[emote.Value.RowId] = existing;
                                    }
                                }

                                foreach (var t in entries)
                                {
                                    if (!existing.Sources.Contains(t))
                                        existing.Sources.Add(t);
                                }
                            }
                        }
                    }
                }
            }

            NoireLogger.LogInfo<Service>($"Built EmoteSources for {EmoteSources.Count} emotes from FFXIVCollect.");
        }
        catch (Exception ex)
        {
            NoireLogger.LogError<Service>(ex, $"Failed to fetch FFXIVCollect emote sources.");
        }
    }

    public static void Dispose()
    {
        NoireService.ClientState.Login -= RefreshLockedEmotes;
        NoireService.ClientState.Logout -= (int type, int code) => ClearLockedEmotes();

        Ipc.Dispose();
        EmotePlayer.Dispose();
    }
}
