using BypassEmote.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace BypassEmote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    internal static IpcProvider? Ipc { get; private set; }

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> onEmoteHook = null!;

    private Hook<ShellCommandModule.Delegates.ExecuteCommandInner>? ExecuteCommandInnerHook { get; init; }
    private Hook<EmoteManager.Delegates.ExecuteEmote>? ExecuteEmoteHook { get; init; }

    private readonly List<Tuple<string, string>> commandNames = [
        new Tuple<string, string>("/bypassemote", "Opens Bypass Emote Configuration. Use with argument 'c' or 'config' to open the config menu: /bypassemote c|config. Use with argument <emote_name> to bypass any emote (including unlocked ones) on yourself: /be <emote_command>."),
        new Tuple<string, string>("/be", "Alias of /bypassemote."),
        new Tuple<string, string>("/bet", "Applies any emote to a targetted NPC. Usage: /bet <emote_command> or /bet stop. Only works on NPCs."),
    ];

    public readonly WindowSystem WindowSystem = new("BypassEmote");

    private EmoteWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public unsafe Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);
        NoireLibMain.Initialize(PluginInterface, this);

        Service.Plugin = this;

        Service.InitializeService();

        MainWindow = new EmoteWindow();
        ConfigWindow = new ConfigWindow();
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        SetupUI();
        SetupCommands();

        Ipc = new IpcProvider();

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
            onEmoteHook = NoireService.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
            onEmoteHook.Enable();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "OnEmote Hook error");
        }

        var cm = new NoireChangelogManager(true, "ChangelogModule", Service.Configuration!.ShowChangelogOnUpdate);
        NoireLibMain.AddModule(cm)?
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
    }

    private void SetupUI()
    {
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettings;
    }

    public void ToggleMainWindow() => MainWindow.Toggle();
    public void ToggleSettings() => ConfigWindow.Toggle();
    public void OpenMainWindow() => MainWindow.IsOpen = true;
    public void OpenSettings() => ConfigWindow.IsOpen = true;
    public void OpenChangelog() => NoireLibMain.GetModule<NoireChangelogManager>()?.ShowChangelogWindow();

    private void SetupCommands()
    {
        // Register primary commands with incremental DisplayOrder based on iteration index
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
        string[] splitArgs = args.Split(' ');
        splitArgs = splitArgs.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        if (command == "/bypassemote" || command == "/be")
        {
            if (splitArgs.Length == 0)
            {
                ToggleMainWindow();
                return;
            }

            var emote = Helpers.CommonHelper.TryGetEmoteFromStringCommand(splitArgs[0]);

            if (emote != null && NoireService.ClientState.LocalPlayer != null)
            {
                EmotePlayer.PlayEmote(NoireService.ClientState.LocalPlayer, emote.Value);
                return;
            }

            ToggleSettings();
            return;
        }

        if (command == "/bet")
        {
            if (NoireService.TargetManager.Target is not INpc npcTarget || NoireService.TargetManager.Target.ObjectKind == ObjectKind.Companion)
            {
                NoireLogger.PrintToChat("No NPC targeted.");
                return;
            }

            if (splitArgs.Length > 0)
            {
                if (splitArgs[0] == "stop")
                {
                    EmotePlayer.StopLoop(npcTarget, true);
                    return;
                }

                var emote = Helpers.CommonHelper.TryGetEmoteFromStringCommand(splitArgs[0]);

                if (emote == null)
                {
                    NoireLogger.PrintToChat($"Emote command not found: {splitArgs[0]}");
                    return;
                }

                EmotePlayer.PlayEmote(npcTarget, emote.Value);
            } else
            {
                NoireLogger.PrintToChat("Usage: /bet <emote_command> or /bet stop");
            }
        }
    }

    // Detour the execute command function so we can check if the player is trying to execute an emote command
    // If they are, we check if they have the emote unlocked, if not unlocked we try to bypass it, if already unlocked, we let the game handle it
    private unsafe void DetourExecuteCommand(ShellCommandModule* commandModule, Utf8String* rawMessage, UIModule* uiModule)
    {
        if (!Service.Configuration!.PluginEnabled)
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var seMsg = SeString.Parse(Helpers.CommonHelper.GetUtf8Span(rawMessage));
        var message = Helpers.CommonHelper.Utf8StringToPlainText(seMsg);

        if (message.IsNullOrEmpty() || !message.StartsWith('/'))
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        if (NoireService.ClientState.LocalPlayer == null)
        {
            NoireLogger.LogError(this, "Player not ready.", "[DetourExecuteCommand] ");
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var foundEmote = LinqExtensions.FirstOrNull(Service.EmoteCommands, e => e.Item1 == message);

        if (!foundEmote.HasValue)
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var chara = NoireService.ClientState.LocalPlayer;

        // Just a safety check, should never be null here
        if (chara == null)
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var isEmoteUnlocked = Helpers.CommonHelper.IsEmoteUnlocked(foundEmote.Value.Item2.RowId);

        if (isEmoteUnlocked)
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        EmotePlayer.PlayEmote(chara, foundEmote.Value.Item2);

        ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
    }

    // Detour the execute emote function to stop any currently playing bypassed looping emotes before executing a new base/obtained game emote
    // Necessary since emote bypassing will prevent the player from executing any base/obtained emote
    private unsafe bool DetourExecuteEmote(EmoteManager* emoteManager, ushort emoteId, PlayEmoteOption* playEmoteOption)
    {
        var chara = NoireService.ClientState.LocalPlayer;

        // Just a safety check, should never be null here
        if (chara == null)
            return ExecuteEmoteHook!.Original(emoteManager, emoteId, playEmoteOption);

        var trackedCharacter = Helpers.CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

        if (trackedCharacter != null)
            EmotePlayer.StopLoop(chara, true);

        return ExecuteEmoteHook!.Original(emoteManager, emoteId, playEmoteOption);
    }

    // Hooking this function to detect when an emote is played by any character (including the local player)
    // This is necessary if a player is playing a bypassed looping emote and then tries to play
    // a base/obtained game emote. In that case, we need to stop the bypassed looping emote first
    // From https://github.com/RokasKil/EmoteLog/blob/master/EmoteLog/Hooks/EmoteReaderHook.cs#L11
    public unsafe void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        var character = CharacterHelper.TryGetCharacterFromAddress((nint)instigatorAddr);

        if (character != null)
        {
            var trackedCharacter = Helpers.CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

            if (trackedCharacter == null)
            {
                onEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
                return;
            }

            EmotePlayer.StopLoop(character, true);
            var emote = NoireService.DataManager.GetExcelSheet<Emote>()?.GetRow(emoteId);
            if (emote != null)
            {
                EmotePlayer.PlayEmote(character, emote.Value);
            }
        }

        onEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettings;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

        Service.Dispose();

        ExecuteCommandInnerHook?.Disable();
        ExecuteCommandInnerHook?.Dispose();

        ExecuteEmoteHook?.Disable();
        ExecuteEmoteHook?.Dispose();

        onEmoteHook?.Disable();
        onEmoteHook?.Dispose();

        ECommonsMain.Dispose();
        NoireLibMain.Dispose();

        foreach (var CommandName in commandNames)
        {
            NoireService.CommandManager.RemoveHandler(CommandName.Item1);
        }
    }
}
