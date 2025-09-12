using BypassEmote.Helpers;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System;

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
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!; // provide game icons

    public static Plugin Plugin { get; set; } = null!;
    public static Configuration? Configuration { get; set; }

    public static Lumina.Excel.ExcelSheet<Emote>? emoteCommands;
    public static HashSet<(string, Emote)> Emotes = [];
    public static List<(Emote, CommonHelper.EmoteCategory)> LockedEmotes = [];

    public static ActionTimelinePlayer Player = new ActionTimelinePlayer();

    public static bool InterruptEmoteOnRotate { get; set; } = false;

    public static void InitializeService()
    {
        InitializeConfig();
        InitializeEmotes();
        RefreshLockedEmotes();
    }

    public static void InitializeConfig()
    {
        Configuration = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Save();
    }

    // From dodingdaga's Copycat
    public static void InitializeEmotes()
    {
        emoteCommands = DataManager.GetExcelSheet<Emote>();
        if (emoteCommands == null)
        {
            Log.Error("Failed to read Emotes list");
            return;
        }

        foreach (var emote in emoteCommands)
        {
            var cmd = emote.TextCommand.ValueNullable?.Command.ExtractText();
            if (!string.IsNullOrEmpty(cmd)) Emotes.Add((cmd, emote));
            cmd = emote.TextCommand.ValueNullable?.ShortCommand.ExtractText();
            if (!string.IsNullOrEmpty(cmd)) Emotes.Add((cmd, emote));
            cmd = emote.TextCommand.ValueNullable?.Alias.ExtractText();
            if (!string.IsNullOrEmpty(cmd)) Emotes.Add((cmd, emote));
            cmd = emote.TextCommand.ValueNullable?.ShortAlias.ExtractText();
            if (!string.IsNullOrEmpty(cmd)) Emotes.Add((cmd, emote));
        }
        if (Emotes.Count == 0)
            Log.Error("Failed to build Emotes list");
    }

    public static void RefreshLockedEmotes()
    {
        LockedEmotes.Clear();

        if (emoteCommands == null || Emotes.Count == 0)
            return;

        // Deduplicate by displayed emote name
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, emote) in Emotes)
        {
            if (!CommonHelper.IsEmoteUnlocked(emote.RowId))
            {
                var name = emote.Name.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    name = emote.TextCommand.ValueNullable?.Command.ExtractText() ?? $"Emote {emote.RowId}";

                if (seenNames.Add(name))
                {
                    LockedEmotes.Add((emote, CommonHelper.GetRealEmoteCategory(emote)));
                }
            }
        }

        // Show latest emotes first
        LockedEmotes.Reverse();
    }

    public static void Dispose()
    {
        EmotePlayer.Dispose();
    }
}
