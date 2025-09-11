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

    public static Plugin Plugin { get; set; } = null!;

    public static Lumina.Excel.ExcelSheet<Emote>? emoteCommands;
    public static HashSet<(string, Emote)> Emotes = [];

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
}
