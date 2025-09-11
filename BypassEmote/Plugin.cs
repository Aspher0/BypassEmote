using BypassEmote.Helpers;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Linq;

namespace BypassEmote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    public static TrackedCharacter? tc = null;

    internal static IpcProvider? Ipc { get; private set; }

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> OnEmoteHook = null!;

    private Hook<ShellCommandModule.Delegates.ExecuteCommandInner>? ExecuteCommandInnerHook { get; init; }

    public unsafe Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        PluginInterface.Create<Service>();
        Service.Plugin = this;

        Service.InitializeEmotes();

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

    private void SetupCommands()
    {
#if DEBUG
        Service.CommandManager.AddHandler("/bet", new CommandInfo(OnCommand)
        {
            HelpMessage = "Applies an emote to the targetted actor. Usage: /bet <emote_command> or /bet stop"
        });

        Service.CommandManager.AddHandler("/getaddr", new CommandInfo(OnCommand)
        {
            HelpMessage = "Gets the address of the targetted character"
        });
#endif
    }

    private void OnCommand(string command, string args)
    {
#if DEBUG
        if (command == "/bet")
        {
            if (Service.TargetManager.Target is not IPlayerCharacter charaTarget)
            {
                Service.ChatGui.Print("No player targeted.");
                return;
            }

            string[] splitArgs = args.Split(' ');
            splitArgs = splitArgs.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

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
        } else if (command == "getaddr")
        {
            if (Service.TargetManager.Target is not IPlayerCharacter charaTarget)
            {
                Service.ChatGui.Print("No player targeted.");
                return;
            }
            Service.ChatGui.Print($"Target address: {charaTarget.Address}");
        }
#endif
    }

    private unsafe void DetourExecuteCommand(ShellCommandModule* commandModule, Utf8String* rawMessage, UIModule* uiModule)
    {
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
            var trackedCharacter = CommonHelper.TryGetCharacterFromTrackedList(character);

            if (trackedCharacter == null)
                return;

            EmotePlayer.StopLoop(character, true);
            var emote = Service.DataManager.GetExcelSheet<Emote>()?.GetRow(emoteId);
            if (emote != null)
                EmotePlayer.PlayEmote(character, emote.Value);
        }

        OnEmoteHook.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    public void Dispose()
    {
        ExecuteCommandInnerHook?.Disable();
        ExecuteCommandInnerHook?.Dispose();

        OnEmoteHook?.Disable();
        OnEmoteHook?.Dispose();

        EmotePlayer.Dispose();
        ECommonsMain.Dispose();
    }
}
