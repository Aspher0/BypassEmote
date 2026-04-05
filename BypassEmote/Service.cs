using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Hooking;
using NoireLib.NetworkRelay;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using static BypassEmote.Plugin;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace BypassEmote;

public class Service
{
    public static Plugin Plugin { get; set; } = null!;

    public static List<(Emote, NoireLib.Enums.EmoteCategory)> LockedEmotes = [];

    // Dictionary: Emote RowId -> (patch, List of (source type, source text)) from ffxivcollect
    public static Dictionary<uint, (string? Patch, List<(string Type, string Text)> Sources)> EmoteSources { get; } = new();

    public static ActionTimelinePlayer ActionTimelinePlayer = new ActionTimelinePlayer();

    private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerSettings JsonOptions = new JsonSerializerSettings { };

#if DEBUG
    public static NoireNetworkRelay NetworkRelay { get; set; }
#endif

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    public static HookWrapper<OnEmoteFuncDelegate>? OnEmoteHook;
    public static HookWrapper<ShellCommandModule.Delegates.ExecuteCommandInner> ExecuteCommandInnerHook;
    public static HookWrapper<EmoteManager.Delegates.ExecuteEmote> ExecuteEmoteHook;
    public static HookWrapper<RaptureHotbarModule.Delegates.ExecuteSlot> ExecuteHotbarSlotHook;

    public unsafe static void InitializeService(Plugin plugin)
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

#if DEBUG
        NetworkRelay = NoireLibMain.AddModule(new NoireNetworkRelay("NetworkRelay", port: 53740, enableReliableTransport: false));
        IpcProvider.EnsureListeningRelay();
#endif

        ExecuteCommandInnerHook = new(DetourExecuteCommand, true);
        ExecuteEmoteHook = new(DetourExecuteEmote, true);
        ExecuteHotbarSlotHook = new(DetourExecuteHotbarSlot, Configuration.BypassOnHotbarSlotTriggered);

        try
        {
            // From https://github.com/RokasKil/EmoteLog/blob/master/EmoteLog/Hooks/EmoteReaderHook.cs#L11
            OnEmoteHook = new("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour, true);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "OnEmote Hook error");
        }
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




    private static void HandleEmote(Emote emote)
    {
        var chara = NoireService.ObjectTable.LocalPlayer;

        if (chara == null)
            return;

        var isEmoteUnlocked = EmoteHelper.IsEmoteUnlocked(emote.RowId);

        if (isEmoteUnlocked)
            return;

        EmotePlayer.PlayEmote(chara, emote);
    }

    private static unsafe byte DetourExecuteHotbarSlot(RaptureHotbarModule* thisPtr, RaptureHotbarModule.HotbarSlot* hotbarSlot)
    {
        var ret = ExecuteHotbarSlotHook.Original(thisPtr, hotbarSlot);

        if (hotbarSlot->CommandType != RaptureHotbarModule.HotbarSlotType.Emote)
            return ret;

        var emoteId = hotbarSlot->CommandId;
        var emote = EmoteHelper.GetEmoteById(emoteId);

        if (emote == null)
            return ret;

        HandleEmote(emote.Value);

        return ret;
    }

    // Detour the execute command function so we can check if the player is trying to execute an emote command
    // If they are, we check if they have the emote unlocked, if not unlocked we try to bypass it, if already unlocked, we let the game handle it
    private static unsafe void DetourExecuteCommand(ShellCommandModule* commandModule, Utf8String* rawMessage, UIModule* uiModule)
    {
        ExecuteCommandInnerHook.Original(commandModule, rawMessage, uiModule);

        if (!Configuration.PluginEnabled)
            return;

        var seMsg = SeString.Parse(CommonHelper.GetUtf8Span(rawMessage));
        var message = CommonHelper.Utf8StringToPlainText(seMsg);

        if (string.IsNullOrEmpty(message) || !message.StartsWith('/'))
            return;

        if (NoireService.ObjectTable.LocalPlayer == null)
            return;

        var command = message.Split(' ')[0];
        var foundEmote = EmoteHelper.GetEmoteByCommand(command.TrimStart('/'));

        if (!foundEmote.HasValue)
            return;

        HandleEmote(foundEmote.Value);
    }

    // Detour the execute emote function to stop any currently playing bypassed looping emotes before executing a new base/obtained game emote
    // Necessary since emote bypassing will prevent the player from executing any base/obtained emote otherwise
    private static unsafe bool DetourExecuteEmote(EmoteManager* emoteManager, ushort emoteId, PlayEmoteOption* playEmoteOption)
    {
        var chara = NoireService.ObjectTable.LocalPlayer;

        if (chara == null)
            return ExecuteEmoteHook.Original(emoteManager, emoteId, playEmoteOption);

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

        if (trackedCharacter != null)
        {
            var emote = EmoteHelper.GetEmoteById(emoteId);
            if (emote.HasValue)
            {
                var emoteCategory = EmoteHelper.GetEmoteCategory(emote.Value);
                if (emoteCategory != NoireLib.Enums.EmoteCategory.Expressions)
                    EmotePlayer.StopLoop(chara, true);
            }
        }

        // Call the original at the end so that the game executes the new emote after we stop the bypassed looping emote, if any
        return ExecuteEmoteHook.Original(emoteManager, emoteId, playEmoteOption);
    }

    // Hooking this function to detect when an emote is played by any character (including the local player)
    // This is necessary if a player is playing a bypassed looping emote and then tries to play
    // a base/obtained game emote. In that case, we need to stop the bypassed looping emote first
    private static void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            var character = CharacterHelper.GetCharacterFromAddress((nint)instigatorAddr);

            if (character != null)
            {
                var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

                if (trackedCharacter == null)
                    return;

                var emote = EmoteHelper.GetEmoteById(emoteId);

                if (!emote.HasValue || EmoteHelper.GetEmoteCategory(emote.Value) != NoireLib.Enums.EmoteCategory.Expressions)
                    EmotePlayer.StopLoop(character, true);

                if (emote != null)
                    EmotePlayer.PlayEmote(character, emote.Value);
            }
        }
        finally
        {
            OnEmoteHook?.Original(unk, instigatorAddr, emoteId, targetId, unk2);
        }
    }

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
            var data = JsonConvert.DeserializeObject<FfxivCollectResponse>(json, JsonOptions);
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

        IpcProvider.Dispose();
        EmotePlayer.Dispose();
    }
}
