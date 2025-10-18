using BypassEmote.Data;
using BypassEmote.Helpers;
using Dalamud.Game;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using System;
using System.Linq;
using System.Threading;
using ECommons.DalamudServices.Legacy;
using BypassEmote.Models;
using NoireLib;

namespace BypassEmote;

public class Service
{
    public static int ConfigVersion => 0;

    public static Plugin Plugin { get; set; } = null!;
    public static Configuration? Configuration { get; set; }

    public static Lumina.Excel.ExcelSheet<Emote>? EmoteSheet;
    public static HashSet<(string, Emote)> EmoteCommands = [];
    public static HashSet<Emote> Emotes= [];
    public static List<(Emote, EmoteData.EmoteCategory)> LockedEmotes = [];

    // Dictionary: Emote RowId -> (patch, List of (source type, source text)) from ffxivcollect
    public static Dictionary<uint, (string? Patch, List<(string Type, string Text)> Sources)> EmoteSources { get; } = new();

    public static ActionTimelinePlayer Player = new ActionTimelinePlayer();

    public static bool InterruptEmoteOnRotate { get; set; } = false;

    private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    private static Timer? UpdateCheckTimer;
    private static bool UpdateNotificationShown = false;

    public static void InitializeService()
    {
        NoireService.ClientState.Login += RefreshLockedEmotes;
        NoireService.ClientState.Logout += (int type, int code) => ClearLockedEmoted();

        EmoteSheet = NoireService.DataManager.GetExcelSheet<Emote>();

        InitializeConfig();
        InitializeEmotes();

        // Fetch emote sources from FFXIVCollect API
        _ = FetchAndBuildEmoteSourcesAsync();

        StartUpdateCheckTimer();

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            if (NoireService.ClientState.IsLoggedIn && NoireService.ClientState.LocalPlayer != null)
                RefreshLockedEmotes();
        });
    }

    public static void InitializeConfig()
    {
        Configuration = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Save();
    }

    public static void InitializeEmotes()
    {
        if (EmoteSheet == null)
        {
            NoireLogger.LogError<Service>("Failed to read Emotes list, EmoteSheet is null.");
            return;
        }

        foreach (var emote in EmoteSheet)
        {
            var textCommand = emote.TextCommand.ValueNullable;
            
            var cmd = textCommand?.Command.ExtractText();
            if (!string.IsNullOrWhiteSpace(cmd)) EmoteCommands.Add((cmd, emote));

            var shortCmd = textCommand?.ShortCommand.ExtractText();
            if (!string.IsNullOrWhiteSpace(shortCmd)) EmoteCommands.Add((shortCmd, emote));

            var alias = textCommand?.Alias.ExtractText();
            if (!string.IsNullOrWhiteSpace(alias)) EmoteCommands.Add((alias, emote));

            var shortAlias = textCommand?.ShortAlias.ExtractText();
            if (!string.IsNullOrWhiteSpace(shortAlias)) EmoteCommands.Add((shortAlias, emote));

            Emotes.Add(emote);
        }

        if (EmoteCommands.Count == 0)
            NoireLogger.LogError<Service>("Failed to build Emotes list.");
    }

    public static void RefreshLockedEmotes()
    {
        ClearLockedEmoted();

        if (!NoireService.ClientState.IsLoggedIn || NoireService.ClientState.LocalPlayer == null)
            return;

        if (EmoteSheet == null || Emotes.Count == 0)
            return;
        
        foreach (var emote in Emotes)
        {
            if (!CommonHelper.IsEmoteUnlocked(emote.RowId))
                LockedEmotes.Add((emote, CommonHelper.GetEmoteCategory(emote)));
        }
    }

    public static void ClearLockedEmoted()
    {
        LockedEmotes.Clear();
    }

    public static void OpenKofi() => OpenLinkInDefaultBrowser("https://ko-fi.com/aspher0");

    public static void OpenLinkInDefaultBrowser(string url)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError<Service>(ex, $"Failed to open link.");
        }
    }

    public static void OnUpdateNotificationConfigChanged(bool enabled)
    {
        if (enabled)
        {
            UpdateNotificationShown = false;
            StartUpdateCheckTimer();
        }
        else
        {
            StopUpdateCheckTimer();
        }
    }

    private static void StartUpdateCheckTimer()
    {
        if (Configuration?.ShowUpdateNotification == true)
        {
            StopUpdateCheckTimer();
            
            UpdateCheckTimer = new Timer(async _ => await CheckForUpdateAsync(), 
                null, 
                TimeSpan.Zero, // Start now
                TimeSpan.FromMinutes(30));
        }
    }

    private static void StopUpdateCheckTimer()
    {
        UpdateCheckTimer?.Dispose();
        UpdateCheckTimer = null;
    }

    public static void Dispose()
    {
        NoireService.ClientState.Login -= RefreshLockedEmotes;
        NoireService.ClientState.Logout -= (int type, int code) => ClearLockedEmoted();

        StopUpdateCheckTimer();
        EmotePlayer.Dispose();
    }

    private static string GetGameLocaleParam()
    {
        var lang = NoireService.DataManager.Language;
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

            // Build a case-insensitive map of command -> Emote for quick lookup
            var commandToEmote = new Dictionary<string, Emote>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (cmd, emote) in EmoteCommands)
            {
                if (!string.IsNullOrWhiteSpace(cmd) && !commandToEmote.ContainsKey(cmd))
                    commandToEmote[cmd] = emote;
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

                    if (!cmd.StartsWith('/'))
                        cmd = "/" + cmd;

                    if (commandToEmote.TryGetValue(cmd, out var emote))
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
                                if (!EmoteSources.TryGetValue(emote.RowId, out var existing))
                                {
                                    existing = (r.patch, new List<(string Type, string Text)>());
                                    EmoteSources[emote.RowId] = existing;
                                }
                                else
                                {
                                    // Update patch if we don't have one yet
                                    if (string.IsNullOrWhiteSpace(existing.Patch) && !string.IsNullOrWhiteSpace(r.patch))
                                    {
                                        existing.Patch = r.patch;
                                        EmoteSources[emote.RowId] = existing;
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
                    // Optional: try fallback match by id if their id matches RowId
                    var id = (uint)r.id.Value;
                    foreach (var emote in Emotes)
                    {
                        if (emote.RowId == id)
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
                                    if (!EmoteSources.TryGetValue(emote.RowId, out var existing))
                                    {
                                        existing = (r.patch, new List<(string Type, string Text)>());
                                        EmoteSources[emote.RowId] = existing;
                                    }
                                    else
                                    {
                                        // Update patch if we don't have one yet
                                        if (string.IsNullOrWhiteSpace(existing.Patch) && !string.IsNullOrWhiteSpace(r.patch))
                                        {
                                            existing.Patch = r.patch;
                                            EmoteSources[emote.RowId] = existing;
                                        }
                                    }

                                    foreach (var t in entries)
                                    {
                                        if (!existing.Sources.Contains(t))
                                            existing.Sources.Add(t);
                                    }
                                }
                            }

                            break;
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

    private static async Task CheckForUpdateAsync()
    {
        try
        {
            // Skip if notifications are disabled or already shown
            if (Configuration?.ShowUpdateNotification != true || UpdateNotificationShown)
                return;

            var url = "https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/repo.json";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<RepoEntry>>(json, JsonOptions);
            if (entries is null || entries.Count == 0)
            {
                NoireLogger.LogWarning<Service>("Repo.json fetch returned no entries.");
                return;
            }

            var remote = entries.FirstOrDefault(e => string.Equals(e.InternalName, "BypassEmote", StringComparison.OrdinalIgnoreCase));
            if (remote == null || string.IsNullOrWhiteSpace(remote.AssemblyVersion))
                return;

            Version? remoteVersion = null;
            try { remoteVersion = new Version(remote.AssemblyVersion); } catch { }
            if (remoteVersion == null)
                return;

            var currentVersion = typeof(Plugin).Assembly.GetName(). Version ?? new Version(0, 0, 0, 0);

            if (currentVersion < remoteVersion)
            {
                UpdateNotificationShown = true;

                Plugin.PluginInterface.UiBuilder.AddNotification(
                    $"Bypass Emote has a new update available.\nCurrent version: {currentVersion}\nNew version: {remoteVersion}",
                    "Update Available",
                    NotificationType.Info,
                    300000);
            }
        }
        catch (Exception ex)
        {
            NoireLogger.LogError<Service>(ex, "Failed to check for updates.");
        }
    }
}
