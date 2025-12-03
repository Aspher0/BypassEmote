using BypassEmote.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Network;
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
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Enums;
using NoireLib.Helpers;
using NoireLib.UpdateTracker;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace BypassEmote;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    internal static IpcProvider? Ipc { get; private set; }

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> onEmoteHook = null!;

    private Hook<ShellCommandModule.Delegates.ExecuteCommandInner> ExecuteCommandInnerHook { get; init; }
    private Hook<EmoteManager.Delegates.ExecuteEmote> ExecuteEmoteHook { get; init; }

    private readonly List<Tuple<string, string>> commandNames = [
        new Tuple<string, string>("/bypassemote", "Opens Bypass Emote Configuration. Use with argument 'c' or 'config' to open the config menu: /bypassemote c|config. Use with argument <emote_name> to bypass any emote (including unlocked ones) on yourself: /be <emote_command>."),
        new Tuple<string, string>("/be", "Alias of /bypassemote."),
        new Tuple<string, string>("/bet", "Applies any emote to a targetted NPC. Usage: /bet <emote_command> or /bet stop. Only works on NPCs."),
    ];

    public readonly WindowSystem WindowSystem = new("BypassEmote");

    private EmoteWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }


    // ======================================
    public static NetworkHandler NetworkHandler { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate byte ProcessZonePacketUpDelegate(nint a1, nint dataPtr, nint a3, byte a4);
    private static Hook<ProcessZonePacketUpDelegate> ProcessZonePacketUpHook;

    public delegate void ZoneUpMessageDelegate(nint dataPtr, ushort opCode);
    public static event ZoneUpMessageDelegate OnZoneUpMessageDelegate;
    // ======================================

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

        Ipc = new IpcProvider();

#if DEBUG
        // ======================================
        ProcessZonePacketUpHook = NoireService.GameInteropProvider.HookFromAddress<ProcessZonePacketUpDelegate>(NoireService.SigScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70"), NetworkMessageDetour);
        ProcessZonePacketUpHook.Enable();

        NetworkHandler = new NetworkHandler();
        OnZoneUpMessageDelegate += OnZoneUpMessage;
        // ======================================
#endif

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

    // ======================================
    private static byte NetworkMessageDetour(nint a1, nint dataPtr, nint a3, byte a4)
    {
        if (dataPtr != 0)
            OnZoneUpMessageDelegate?.Invoke(dataPtr + 0x20, (ushort)Marshal.ReadInt16(dataPtr));
        return ProcessZonePacketUpHook.Original(a1, dataPtr, a3, a4);
    }

    public void OnZoneUpMessage(nint dataPtr, ushort opCode)
    {
        try
        {
            NetworkHandler.VerifyNetworkMessage(dataPtr, opCode, NetworkMessageDirection.ZoneUp);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception in OnNetworkMessage");
        }
    }
    // ======================================

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
        string[] splitArgs = args.Split(' ');
        splitArgs = splitArgs.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        if (command == "/bypassemote" || command == "/be")
        {
            if (splitArgs.Length == 0)
            {
                ToggleMainWindow();
                return;
            }

            switch (splitArgs[0])
            {
                case "sync":
                    {
                        EmotePlayer.SyncEmotes(false);
                        return;
                    }
                case "syncall":
                    {
                        EmotePlayer.SyncEmotes(true);
                        return;
                    }
#if DEBUG
                case "debug":
                    {
                        DebugWindow.Toggle();
                        return;
                    }
#endif
                default:
                    break;
            }
            var emote = EmoteHelper.GetEmoteByCommand(splitArgs[0]);

            if (emote != null && NoireService.ObjectTable.LocalPlayer != null)
            {
                EmotePlayer.PlayEmote(NoireService.ObjectTable.LocalPlayer, emote.Value);
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

                var emote = EmoteHelper.GetEmoteByCommand(splitArgs[0]);

                if (emote == null)
                {
                    NoireLogger.PrintToChat($"Emote command not found: {splitArgs[0]}");
                    return;
                }

                EmotePlayer.PlayEmote(npcTarget, emote.Value);
            }
            else
            {
                NoireLogger.PrintToChat("Usage: /bet <emote_command> or /bet stop");
            }
        }
    }

    // Detour the execute command function so we can check if the player is trying to execute an emote command
    // If they are, we check if they have the emote unlocked, if not unlocked we try to bypass it, if already unlocked, we let the game handle it
    private unsafe void DetourExecuteCommand(ShellCommandModule* commandModule, Utf8String* rawMessage, UIModule* uiModule)
    {
        ExecuteCommandInnerHook.Original(commandModule, rawMessage, uiModule);

        if (!Configuration.Instance.PluginEnabled)
            return;

        var seMsg = SeString.Parse(Helpers.CommonHelper.GetUtf8Span(rawMessage));
        var message = Helpers.CommonHelper.Utf8StringToPlainText(seMsg);

        if (message.IsNullOrEmpty() || !message.StartsWith('/'))
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

        var trackedCharacter = Helpers.CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

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
                var trackedCharacter = Helpers.CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

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

#if DEBUG
        // ======================================
        OnZoneUpMessageDelegate -= OnZoneUpMessage;
        ProcessZonePacketUpHook?.Disable();
        ProcessZonePacketUpHook?.Dispose();
        // ======================================
#endif

        ECommonsMain.Dispose();
        NoireLibMain.Dispose();

        foreach (var CommandName in commandNames)
        {
            NoireService.CommandManager.RemoveHandler(CommandName.Item1);
        }
    }
}
