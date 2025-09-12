using BypassEmote.Helpers;
using BypassEmote.UI;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    internal static IpcProvider? Ipc { get; private set; }

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> OnEmoteHook = null!;

    private Hook<ShellCommandModule.Delegates.ExecuteCommandInner>? ExecuteCommandInnerHook { get; init; }

    private readonly List<Tuple<string, string>> commandNames = [
        new Tuple<string, string>("/bypassemote", "Opens Bypass Emote Configuration. Use with argument 'c' or 'config' to open the config menu: /bypassemote c|config"),
        new Tuple<string, string>("/be", "Alias of /bypassemote."),
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

#if DEBUG
        Service.CommandManager.AddHandler("/bet", new CommandInfo(OnCommand)
        {
            HelpMessage = "Applies an emote to the targetted actor. Usage: /bet <emote_command> or /bet stop"
        });
#endif
    }

    private void OnCommand(string command, string args)
    {
        string[] splitArgs = args.Split(' ');
        splitArgs = splitArgs.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        if (command == "/bypassemote" || command == "/be")
        {
            if (splitArgs.Length > 0 && new List<string>() { "config", "c" }.Contains(splitArgs[0]))
            {
                OpenSettings();
                return;
            }

            OpenMainWindow();
            return;
        }

#if DEBUG
        if (command == "/bet")
        {
            if (Service.TargetManager.Target is not ICharacter charaTarget)
            {
                Service.ChatGui.Print("No character targeted.");
                return;
            }

            if (splitArgs.Length > 0)
            {
                var emote = CommonHelper.TryGetEmoteFromStringCommand(splitArgs[0]);

                if (splitArgs[0] == "stop")
                {
                    EmotePlayer.StopLoop(charaTarget, true);
                    return;
                }

                if (emote == null)
                {
                    Service.ChatGui.Print($"Emote command not found: {splitArgs[0]}");
                    return;
                }

                EmotePlayer.PlayEmote(charaTarget, emote.Value);
            } else
            {
                Service.ChatGui.Print("Usage: /bet <emote_command> or /bet stop");
            }
        }
#endif
    }

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

        var command = message[1..];

        if (Service.ClientState.LocalPlayer == null)
        {
            Service.Log.Error("Player not ready.");
            ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
            return;
        }

        var foundEmote = LinqExtensions.FirstOrNull(Service.Emotes, e => e.Item1 == $"/{command}");

        if (!foundEmote.HasValue)
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

        var chara = Service.ClientState.LocalPlayer;
        EmotePlayer.PlayEmote(chara, foundEmote.Value.Item2);

        ExecuteCommandInnerHook!.Original(commandModule, rawMessage, uiModule);
    }

    // From https://github.com/RokasKil/EmoteLog/blob/master/EmoteLog/Hooks/EmoteReaderHook.cs#L11
    public unsafe void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        var character = CommonHelper.TryGetPlayerCharacterFromAddress((nint)instigatorAddr);

        if (character != null)
        {
            var trackedCharacter = CommonHelper.TryGetCharacterFromTrackedList(character.Address);

            if (trackedCharacter == null)
            {
                OnEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
                return;
            }

            EmotePlayer.StopLoop(character, true);
            var emote = Service.DataManager.GetExcelSheet<Emote>()?.GetRow(emoteId);
            if (emote != null)
                EmotePlayer.PlayEmote(character, emote.Value);
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

        OnEmoteHook?.Disable();
        OnEmoteHook?.Dispose();

        ECommonsMain.Dispose();

        foreach (var CommandName in commandNames)
        {
            Service.CommandManager.RemoveHandler(CommandName.Item1);
        }
    }
}
