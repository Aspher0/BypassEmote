using BypassEmote.Helpers;
using BypassEmote.UI;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
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
    private readonly Hook<OnEmoteFuncDelegate> OnEmoteHook = null!;

    private Hook<ShellCommandModule.Delegates.ExecuteCommandInner>? ExecuteCommandInnerHook { get; init; }
    private Hook<EmoteManager.Delegates.ExecuteEmote>? ExecuteEmoteHook { get; init; }

    private readonly List<Tuple<string, string>> commandNames = [
        new Tuple<string, string>("/bypassemote", "Opens Bypass Emote Configuration. Use with argument 'c' or 'config' to open the config menu: /bypassemote c|config. Use with argument <emote_name> to bypass any emote (including unlocked ones) on yourself: /be <emote_command>."),
        new Tuple<string, string>("/be", "Alias of /bypassemote."),
        new Tuple<string, string>("/bet", "Applies any emote to a targetted NPC. Usage: /bet <emote_command> or /bet stop. Only works on NPCs."),
    ];

    public readonly WindowSystem WindowSystem = new("BypassEmote");

    private UIBuilder MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public unsafe Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        PluginInterface.Create<Service>();
        Service.Plugin = this;

        Service.InitializeService();

        MainWindow = new UIBuilder();
        ConfigWindow = new ConfigWindow();
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        SetupUI();
        SetupCommands();

        Ipc = new IpcProvider();

        ExecuteCommandInnerHook = Service.InteropProvider.HookFromAddress<ShellCommandModule.Delegates.ExecuteCommandInner>(
            ShellCommandModule.MemberFunctionPointers.ExecuteCommandInner,
            DetourExecuteCommand
        );
        ExecuteCommandInnerHook.Enable();

        ExecuteEmoteHook = Service.InteropProvider.HookFromAddress<EmoteManager.Delegates.ExecuteEmote>(
            EmoteManager.MemberFunctionPointers.ExecuteEmote,
            DetourExecuteEmote
        );
        ExecuteEmoteHook.Enable();

        try
        {
            OnEmoteHook = Service.InteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
            OnEmoteHook.Enable();
        }
        catch (Exception ex)
        {
            Service.Log.Error("OnEmote Hook error");
        }
    }

    private void SetupUI()
    {
        PluginInterface.UiBuilder.Draw += () => WindowSystem.Draw();
        PluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
    }

    public void OpenMainWindow() => MainWindow.Toggle();
    public void OpenSettings() => ConfigWindow.Toggle();

    private void SetupCommands()
    {
        // Register primary commands with incremental DisplayOrder based on iteration index
        for (int i = 0; i < commandNames.Count; i++)
        {
            var (command, help) = (commandNames[i].Item1, commandNames[i].Item2);
            Service.CommandManager.AddHandler(command, new CommandInfo(OnCommand)
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
                OpenMainWindow();
                return;
            }

            var emote = CommonHelper.TryGetEmoteFromStringCommand(splitArgs[0]);

            if (emote != null && Service.ClientState.LocalPlayer != null)
            {
                EmotePlayer.PlayEmote(Service.ClientState.LocalPlayer, emote.Value);
                return;
            }

            OpenSettings();
            return;
        }

        if (command == "/bet")
        {
            if (Service.TargetManager.Target is not INpc npcTarget || Service.TargetManager.Target.ObjectKind == ObjectKind.Companion)
            {
                Service.ChatGui.Print("No NPC targeted.");
                return;
            }

            if (splitArgs.Length > 0)
            {
                if (splitArgs[0] == "stop")
                {
                    EmotePlayer.StopLoop(npcTarget, true);
                    return;
                }

                var emote = CommonHelper.TryGetEmoteFromStringCommand(splitArgs[0]);

                if (emote == null)
                {
                    Service.ChatGui.Print($"Emote command not found: {splitArgs[0]}");
                    return;
                }

                EmotePlayer.PlayEmote(npcTarget, emote.Value);
            } else
            {
                Service.ChatGui.Print("Usage: /bet <emote_command> or /bet stop");
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

        var seMsg = SeString.Parse(CommonHelper.GetUtf8Span(rawMessage));
        var message = CommonHelper.Utf8StringToPlainText(seMsg);

        if (!message.StartsWith('/'))
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        if (Service.ClientState.LocalPlayer == null)
        {
            Service.Log.Error("Player not ready.");
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var foundEmote = LinqExtensions.FirstOrNull(Service.EmoteCommands, e => e.Item1 == message);

        if (!foundEmote.HasValue)
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var chara = Service.ClientState.LocalPlayer;

        // Just a safety check, should never be null here
        if (chara == null)
        {
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var isEmoteUnlocked = CommonHelper.IsEmoteUnlocked(foundEmote.Value.Item2.RowId);

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
        var chara = Service.ClientState.LocalPlayer;

        // Just a safety check, should never be null here
        if (chara == null)
            return ExecuteEmoteHook!.Original(emoteManager, emoteId, playEmoteOption);

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

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
        var character = CommonHelper.TryGetCharacterFromAddress((nint)instigatorAddr);

        if (character != null)
        {
            var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

            if (trackedCharacter == null)
            {
                OnEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
                return;
            }

            EmotePlayer.StopLoop(character, true);
            var emote = Service.DataManager.GetExcelSheet<Emote>()?.GetRow(emoteId);
            if (emote != null)
            {
                EmotePlayer.PlayEmote(character, emote.Value);
            }
        }

        OnEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

        Service.Dispose();

        ExecuteCommandInnerHook?.Disable();
        ExecuteCommandInnerHook?.Dispose();

        ExecuteEmoteHook?.Disable();
        ExecuteEmoteHook?.Dispose();

        OnEmoteHook?.Disable();
        OnEmoteHook?.Dispose();

        ECommonsMain.Dispose();

        foreach (var CommandName in commandNames)
        {
            Service.CommandManager.RemoveHandler(CommandName.Item1);
        }
    }
}
