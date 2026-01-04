using BypassEmote.Helpers;
using BypassEmote.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Enums;
using NoireLib.Helpers;
using NoireLib.UpdateTracker;
using System;
using System.Collections.Generic;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace BypassEmote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> onEmoteHook = null!;

    private Hook<ShellCommandModule.Delegates.ExecuteCommandInner> ExecuteCommandInnerHook { get; init; }
    private Hook<EmoteManager.Delegates.ExecuteEmote> ExecuteEmoteHook { get; init; }

    private readonly List<Tuple<string, string>> commandNames = [
        new Tuple<string, string>("/bypassemote", "Opens Bypass Emote Configuration. Use with argument 'c' or 'config' to open the config menu: /bypassemote c|config." +
            "Use with argument <emote_name> to bypass any emote (including unlocked ones) on yourself: /bypassemote <emote_command>."),
        new Tuple<string, string>("/be", "Alias of /bypassemote."),
        new Tuple<string, string>("/bet", "Applies any emote to a targetted NPC. Usage: /bet <emote_command> or /bet stop. Only works on NPCs and owned minions/pets."),
        new Tuple<string, string>("/bem", "Applies any emote to your own minion if summoned, without needing to target it. Usage: /bem <emote_command> or /bem stop."),
        new Tuple<string, string>("/bep", "Applies any emote to your own pet (carbuncle/eos) if summoned, without needing to target it. Usage: /bep <emote_command> or /bep stop."),
        new Tuple<string, string>("/bec", "Applies any emote to your own chocobo if summoned, without needing to target it. Usage: /bec <emote_command> or /bec stop."),
    ];

    public readonly WindowSystem WindowSystem = new("BypassEmote");

    private EmoteWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }

    public unsafe Plugin()
    {
        NoireLibMain.Initialize(PluginInterface, this);
        ECommonsMain.Init(PluginInterface, this);

        Service.InitializeService(this);

        MainWindow = new EmoteWindow();
        ConfigWindow = new ConfigWindow();

#if DEBUG
        DebugWindow = new DebugWindow();
        WindowSystem.AddWindow(DebugWindow);
#endif

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        SetupUI();
        SetupCommands();

        ExecuteCommandInnerHook = NoireService.GameInteropProvider.HookFromAddress<ShellCommandModule.Delegates.ExecuteCommandInner>(
            ShellCommandModule.MemberFunctionPointers.ExecuteCommandInner,
            DetourExecuteCommand
        );
        ExecuteCommandInnerHook.Enable();

        ExecuteEmoteHook = NoireService.GameInteropProvider.HookFromAddress<EmoteManager.Delegates.ExecuteEmote>(
            EmoteManager.MemberFunctionPointers.ExecuteEmote,
            DetourExecuteEmote
        );
        ExecuteEmoteHook.Enable();

        try
        {
            // From https://github.com/RokasKil/EmoteLog/blob/master/EmoteLog/Hooks/EmoteReaderHook.cs#L11
            onEmoteHook = NoireService.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
            onEmoteHook.Enable();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "OnEmote Hook error");
        }

        // Listen for Condition Changes, to cancel emotes when casting and interacting with objects/NPCs
        NoireService.Condition.ConditionChange += OnConditionChanged;

        Service.Ipc.NotifyReady();

        var changelogManager = new NoireChangelogManager("ChangelogModule", true, true, Configuration.Instance.ShowChangelogOnUpdate);
        NoireLibMain.AddModule(changelogManager)?
            .SetTitleBarButtons(
            [
                new()
                {
                    Click = (e) => { Service.Plugin.OpenSettings(); },
                    Icon = FontAwesomeIcon.Cog,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip("Open settings"),
                },

                new()
                {
                    Click = (e) => { Service.OpenKofi(); },
                    Icon = FontAwesomeIcon.Heart,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip("Support me"),
                },
            ]);

        NoireLibMain.AddModule(new NoireUpdateTracker("UpdateTrackerModule",
            true,
            true,
            "https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/repo.json"));
    }

    // Track condition change and cancel emotes if the player starts casting, mounting, or interacting with an object/NPC
    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag.EqualsAny(
            ConditionFlag.Casting,
            ConditionFlag.Casting87,
            ConditionFlag.OccupiedInEvent,
            ConditionFlag.OccupiedInQuestEvent,
            ConditionFlag.Mounted))
        {
            if (value && NoireService.ObjectTable.LocalPlayer != null)
                EmotePlayer.StopLoop(NoireService.ObjectTable.LocalPlayer, true);
        }
    }

    private void SetupUI()
    {
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettings;
    }

    public void ToggleMainWindow() => MainWindow.Toggle();
    public void ToggleSettings() => ConfigWindow.Toggle();
    public void ToggleDebug() => DebugWindow.Toggle();
    public void OpenMainWindow() => MainWindow.IsOpen = true;
    public void OpenSettings() => ConfigWindow.IsOpen = true;
    public void OpenChangelog() => NoireLibMain.GetModule<NoireChangelogManager>()?.ShowWindow();

    private void SetupCommands()
    {
        for (int i = 0; i < commandNames.Count; i++)
        {
            var (command, help) = (commandNames[i].Item1, commandNames[i].Item2);
            NoireService.CommandManager.AddHandler(command, new CommandInfo(OnCommand)
            {
                HelpMessage = help,
                DisplayOrder = i,
            });
        }
    }

    private void OnCommand(string command, string args)
    {
        string[] splitArgs = args.Trim().Split(' ');
        splitArgs = splitArgs.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var arg = splitArgs.FirstOrDefault();

        switch (command)
        {
            case "/bypassemote":
            case "/be":
                HandleMainCommand(arg);
                return;

            case "/bet":
                HandleTargetCommand(arg);
                return;

            case "/bem":
                HandleMinionCommand(arg);
                return;

            case "/bep":
                HandlePetCommand(arg);
                return;

            case "/bec":
                HandleChocoboCommand(arg);
                return;
        }
    }

    private void HandleMainCommand(string? arg)
    {
        if (arg.IsNullOrWhitespace())
        {
            ToggleMainWindow();
            return;
        }

        if (arg == "c" || arg == "config")
        {
            ToggleSettings();
            return;
        }

        if (arg == "sync")
        {
            EmotePlayer.SyncEmotes(false);
            return;
        }

        if (arg == "syncall")
        {
            EmotePlayer.SyncEmotes(true);
            return;
        }

#if DEBUG
        if (arg == "d" || arg == "debug")
        {
            ToggleDebug();
            return;
        }
#endif

        if (NoireService.ObjectTable.LocalPlayer != null)
            HandleEmoteCommand(NoireService.ObjectTable.LocalPlayer, arg, "Usage: /be <emote_command> or /be stop");
        else
            NoireLogger.PrintToChat($"Error trying to process command");
    }

    private void HandleTargetCommand(string? arg)
    {
        if (NoireService.TargetManager.Target is not ICharacter target ||
            target is not INpc && target is not IBattleNpc)
        {
            NoireLogger.PrintToChat("No NPC targeted.");
            return;
        }

        // If minion (Companion) or pet (subkind == 2)
        if ((target.ObjectKind == ObjectKind.Companion || target.SubKind == 2 || target.SubKind == 3) && !CommonHelper.IsLocalObject(target))
        {
            NoireLogger.PrintToChat("You can only target your own minion, pet, chocobo.");
            return;
        }

        HandleEmoteCommand(target, arg, "Usage: /bet <emote_command> or /bet stop");
    }

    private void HandleMinionCommand(string? arg)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = CommonHelper.GetCompanionAddress(player);
        if (addr == 0)
        {
            NoireLogger.PrintToChat("No minion summoned.");
            return;
        }

        if (NoireService.ObjectTable.FirstOrDefault(o => o.Address == addr) is not ICharacter minion)
            return;

        HandleEmoteCommand(minion, arg, "Usage: /bem <emote_command> or /bem stop");
    }

    private void HandlePetCommand(string? arg)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = CommonHelper.GetPetAddress(player);
        if (addr == 0)
        {
            NoireLogger.PrintToChat("No pet summoned.");
            return;
        }

        if (NoireService.ObjectTable.FirstOrDefault(o => o.Address == addr) is not ICharacter pet)
            return;

        HandleEmoteCommand(pet, arg, "Usage: /bep <emote_command> or /bep stop");
    }

    private void HandleChocoboCommand(string? arg)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = CommonHelper.GetBuddyAddress(player);
        if (addr == 0)
        {
            NoireLogger.PrintToChat("No chocobo summoned.");
            return;
        }

        if (NoireService.ObjectTable.FirstOrDefault(o => o.Address == addr) is not ICharacter buddy)
            return;

        HandleEmoteCommand(buddy, arg, "Usage: /bec <emote_command> or /bec stop");
    }

    private static void HandleEmoteCommand(ICharacter character, string? arg, string usage)
    {
        if (arg.IsNullOrWhitespace())
        {
            NoireLogger.PrintToChat(usage);
            return;
        }

        if (arg == "stop")
        {
            EmotePlayer.StopLoop(character, true);
            return;
        }

        var emote = EmoteHelper.GetEmoteByCommand(arg);
        var isNumericArg = uint.TryParse(arg, out var commandUint);

        if (isNumericArg)
        {
            var emoteById = EmoteHelper.GetEmoteById(commandUint);
            emote = emoteById;
        }

        if (!emote.HasValue)
        {
            NoireLogger.PrintToChat($"Emote not found: {arg}\n{usage}");
            return;
        }

        EmotePlayer.PlayEmote(character, emote.Value);
    }

    // Detour the execute command function so we can check if the player is trying to execute an emote command
    // If they are, we check if they have the emote unlocked, if not unlocked we try to bypass it, if already unlocked, we let the game handle it
    private unsafe void DetourExecuteCommand(ShellCommandModule* commandModule, Utf8String* rawMessage, UIModule* uiModule)
    {
        ExecuteCommandInnerHook.Original(commandModule, rawMessage, uiModule);

        if (!Configuration.Instance.PluginEnabled)
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

        var chara = NoireService.ObjectTable.LocalPlayer;

        // Just a safety check, should never be null here
        if (chara == null)
            return;

        var isEmoteUnlocked = EmoteHelper.IsEmoteUnlocked(foundEmote.Value.RowId);

        if (isEmoteUnlocked)
            return;

        EmotePlayer.PlayEmote(chara, foundEmote.Value);
    }

    // Detour the execute emote function to stop any currently playing bypassed looping emotes before executing a new base/obtained game emote
    // Necessary since emote bypassing will prevent the player from executing any base/obtained emote otherwise
    private unsafe bool DetourExecuteEmote(EmoteManager* emoteManager, ushort emoteId, PlayEmoteOption* playEmoteOption)
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
                if (emoteCategory != EmoteCategory.Expressions)
                    EmotePlayer.StopLoop(chara, true);
            }
        }

        return ExecuteEmoteHook.Original(emoteManager, emoteId, playEmoteOption);
    }

    // Hooking this function to detect when an emote is played by any character (including the local player)
    // This is necessary if a player is playing a bypassed looping emote and then tries to play
    // a base/obtained game emote. In that case, we need to stop the bypassed looping emote first
    public unsafe void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            var character = CharacterHelper.TryGetCharacterFromAddress((nint)instigatorAddr);

            if (character != null)
            {
                var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

                if (trackedCharacter == null)
                    return;

                var emote = EmoteHelper.GetEmoteById(emoteId);

                if (!emote.HasValue || EmoteHelper.GetEmoteCategory(emote.Value) != EmoteCategory.Expressions)
                    EmotePlayer.StopLoop(character, true);

                if (emote != null)
                    EmotePlayer.PlayEmote(character, emote.Value);
            }
        }
        finally
        {
            onEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettings;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
#if DEBUG
        DebugWindow.Dispose();
#endif

        Service.Dispose();

        ExecuteCommandInnerHook?.Disable();
        ExecuteCommandInnerHook?.Dispose();

        ExecuteEmoteHook?.Disable();
        ExecuteEmoteHook?.Dispose();

        onEmoteHook?.Disable();
        onEmoteHook?.Dispose();

        NoireService.Condition.ConditionChange -= OnConditionChanged;

        ECommonsMain.Dispose();
        NoireLibMain.Dispose();

        foreach (var CommandName in commandNames)
        {
            NoireService.CommandManager.RemoveHandler(CommandName.Item1);
        }
    }
}
