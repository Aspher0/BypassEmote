using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString HelpMain = new("command.main",
        "Opens the Bypass Emote main window.");
    public static readonly NoireString HelpSelfEmote = new("command.self_emote",
        "Bypasses any emote (including locked ones) on yourself, by command name or ID.");
    public static readonly NoireString HelpConfig = new("command.config",
        "Opens the configuration window.");
    public static readonly NoireString HelpSync = new("command.sync",
        "Restarts the emotes of every player and NPC around you, with their sounds.");
    public static readonly NoireString HelpSyncDirect = new("command.sync_direct",
        "Restarts only the emotes played with Direct Play.");
    public static readonly NoireString HelpChangelog = new("command.changelog",
        "Opens the changelog window.");
    public static readonly NoireString HelpStop = new("command.stop",
        "Stops the emote currently playing on yourself. Direct Play mode only.");
    public static readonly NoireString HelpLogs = new("command.logs",
        "Exports a zip with the plugin logs and settings, to send to the developer.");
    public static readonly NoireString HelpDebug = new("command.debug",
        "Opens the debug window.");
    public static readonly NoireString HelpHooks = new("command.hooks",
        "Shows the hooks window.");
    public static readonly NoireString HelpTarget = new("command.target",
        "Applies any emote to a targetted NPC. Only works on NPCs and owned minions/pets. Use /bet <emote_command> or /bet stop.");
    public static readonly NoireString HelpTargetStop = new("command.target_stop",
        "Stops the emote currently playing on your target.");
    public static readonly NoireString HelpTargetEmote = new("command.target_emote",
        "Plays the emote on your target, by command name or ID.");
    public static readonly NoireString HelpMinion = new("command.minion",
        "Applies any emote to your own minion if summoned, without needing to target it. Use /bem <emote_command> or /bem stop.");
    public static readonly NoireString HelpMinionStop = new("command.minion_stop",
        "Stops the emote currently playing on your minion.");
    public static readonly NoireString HelpMinionEmote = new("command.minion_emote",
        "Plays the emote on your minion, by command name or ID.");
    public static readonly NoireString HelpPet = new("command.pet",
        "Applies any emote to your own pet (carbuncle/eos) if summoned, without needing to target it. Use /bep <emote_command> or /bep stop.");
    public static readonly NoireString HelpPetStop = new("command.pet_stop",
        "Stops the emote currently playing on your pet.");
    public static readonly NoireString HelpPetEmote = new("command.pet_emote",
        "Plays the emote on your pet, by command name or ID.");
    public static readonly NoireString HelpChocobo = new("command.chocobo",
        "Applies any emote to your own chocobo if summoned, without needing to target it. Use /bec <emote_command> or /bec stop.");
    public static readonly NoireString HelpChocoboStop = new("command.chocobo_stop",
        "Stops the emote currently playing on your chocobo.");
    public static readonly NoireString HelpChocoboEmote = new("command.chocobo_emote",
        "Plays the emote on your chocobo, by command name or ID.");
}
