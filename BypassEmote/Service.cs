using BypassEmote.Data;
using BypassEmote.Helpers;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace BypassEmote;

public class Service
{
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider InteropProvider { get; private set; } = null!;
    [PluginService] internal static ISigScanner Scanner { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    public static Plugin Plugin { get; set; } = null!;
    public static Configuration? Configuration { get; set; }

    public static Lumina.Excel.ExcelSheet<Emote>? EmoteSheet;
    public static HashSet<(string, Emote)> EmoteCommands = [];
    public static HashSet<Emote> Emotes= [];
    public static List<(Emote, EmoteData.EmoteCategory)> LockedEmotes = [];

    public static ActionTimelinePlayer Player = new ActionTimelinePlayer();

    public static bool InterruptEmoteOnRotate { get; set; } = false;

    public static void InitializeService()
    {
        ClientState.Login += RefreshLockedEmotes;
        ClientState.Logout += (int type, int code) => ClearLockedEmoted();

        EmoteSheet = DataManager.GetExcelSheet<Emote>();

        InitializeConfig();
        InitializeEmotes();

        Framework.RunOnFrameworkThread(() =>
        {
            if (ClientState.IsLoggedIn && ClientState.LocalPlayer != null)
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
            Log.Error("Failed to read Emotes list");
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
            Log.Error("Failed to build Emotes list");
    }

    public static void RefreshLockedEmotes()
    {
        ClearLockedEmoted();

        if (!ClientState.IsLoggedIn || ClientState.LocalPlayer == null)
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

    public static void Dispose()
    {
        ClientState.Login -= RefreshLockedEmotes;
        ClientState.Logout -= (int type, int code) => ClearLockedEmoted();

        EmotePlayer.Dispose();
    }
}
