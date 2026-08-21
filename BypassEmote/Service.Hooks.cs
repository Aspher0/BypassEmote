using BypassEmote.Helpers;
using BypassEmote.Models;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Hooking;
using System;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace BypassEmote;

public partial class Service
{
    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);

    public static NoireHook<OnEmoteFuncDelegate>? OnEmoteHook;
    public static NoireHook<AgentEmote.Delegates.ExecuteEmote> AgentExecuteEmoteHook;
    public static NoireHook<EmoteManager.Delegates.ExecuteEmote> ExecuteEmoteHook;
    public static NoireHook<RaptureHotbarModule.Delegates.ExecuteSlot> ExecuteHotbarSlotHook;

    private const byte HotbarSlotNotExecuted = 0;
    private static bool inHotbarSlot;

    private static unsafe void InstallHooks()
    {
        AgentExecuteEmoteHook = new(DetourAgentExecuteEmote, true);
        ExecuteEmoteHook = new(DetourExecuteEmote, true);
        ExecuteHotbarSlotHook = new(DetourExecuteHotbarSlot, true);

        try
        {
            // From https://github.com/RokasKil/EmoteLog/blob/master/EmoteLog/Hooks/EmoteReaderHook.cs#L11
            OnEmoteHook = new("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour, true);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "OnEmote Hook error");
        }
    }

    private static void HandleEmote(Emote emote)
    {
        var chara = NoireService.ObjectTable.LocalPlayer;

        if (chara == null)
            return;

        var isEmoteUnlocked = EmoteHelper.IsEmoteUnlocked(emote.RowId);

        if (isEmoteUnlocked)
            return;

        EmotePlayer.PlayEmote(chara, emote);
    }

    private static unsafe byte DetourExecuteHotbarSlot(RaptureHotbarModule* thisPtr, RaptureHotbarModule.HotbarSlot* hotbarSlot)
    {
        if (Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap && TrySwapHotbarSlot(hotbarSlot))
            return HotbarSlotNotExecuted;

        byte ret;

        inHotbarSlot = true;

        try
        {
            ret = ExecuteHotbarSlotHook.Original(thisPtr, hotbarSlot);
        }
        finally
        {
            inHotbarSlot = false;
        }

        if (Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap)
            return ret;

        if (!Configuration.PluginEnabled || !Configuration.BypassOnHotbarSlotTriggered)
            return ret;

        if (hotbarSlot->CommandType != RaptureHotbarModule.HotbarSlotType.Emote)
            return ret;

        var emoteId = hotbarSlot->CommandId;
        var emote = EmoteHelper.GetEmoteById(emoteId);

        if (emote == null)
            return ret;

        HandleEmote(emote.Value);

        return ret;
    }

    private static unsafe bool TrySwapHotbarSlot(RaptureHotbarModule.HotbarSlot* hotbarSlot)
    {
        if (!Configuration.PluginEnabled || !Configuration.BypassOnHotbarSlotTriggered)
            return false;

        if (hotbarSlot == null || hotbarSlot->CommandType != RaptureHotbarModule.HotbarSlotType.Emote)
            return false;

        if (NoireService.ObjectTable.LocalPlayer == null)
            return false;

        if (EmoteHelper.GetEmoteById(hotbarSlot->CommandId) is not { } emote || LeaveToTheGame(emote.RowId))
            return false;

        if (IsPoseFamilySource(emote.RowId)) // Might be unnecessary since poses are all unlocked by default, but whatever
            return false;

        Orchestrator?.TrySwap(emote);
        return true;
    }

    // A /cpose cycle member, or Change Pose itself
    private static bool IsPoseFamilySource(uint emoteRowId)
        => Catalog?.Get(emoteRowId)?.IsPoseFamily == true;

    // Every emote source (chat command, game macro line, emote window) lands here
    // It also lands here before any unlock check, so this is better than hooking the ExecuteCommand function
    private static unsafe void DetourAgentExecuteEmote(
        AgentEmote* agent, ushort emoteId, PlayEmoteOption* playEmoteOption, bool addToHistory, bool liveUpdateHistory)
    {
        // A hotbar press already went through the slot hook
        var emote = inHotbarSlot ? null : ResolveSelfEmote(emoteId);

        if (emote.HasValue && Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap
            && !LeaveToTheGame(emote.Value.RowId) && !IsPoseFamilySource(emote.Value.RowId))
        {
            Orchestrator?.TrySwap(emote.Value);
            return;
        }

        AgentExecuteEmoteHook.Original(agent, emoteId, playEmoteOption, addToHistory, liveUpdateHistory);

        // Direct Play
        if (emote.HasValue && Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
            HandleEmote(emote.Value);
    }

    private static Emote? ResolveSelfEmote(ushort emoteId)
        => NoireService.ObjectTable.LocalPlayer == null ? null : EmoteHelper.GetEmoteById(emoteId);

    // Detour the execute emote function to stop any currently playing bypassed looping emotes before executing a new base/obtained game emote
    // Necessary since emote bypassing will prevent the player from executing any base/obtained emote otherwise
    private static unsafe bool DetourExecuteEmote(EmoteManager* emoteManager, ushort emoteId, PlayEmoteOption* playEmoteOption)
    {
        if (Orchestrator is { } orchestrator && SwapMods?.ArmedFor(emoteId) is { } armed
            && ShouldEndSwapBeforeExecuting(Configuration.SelfBypassMode, orchestrator.IsExecutingSwap, armed, emoteId)
            && Configuration.SwapLifetime != SwapLifetime.Never)
        {
            EndWatcher?.StopWatching();
            SwapMods.DeselectEntry(armed);
        }

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
                if (emoteCategory != NoireLib.Enums.EmoteCategory.Expressions)
                    EmotePlayer.StopLoop(chara, true);
            }
        }

        return ExecuteEmoteHook.Original(emoteManager, emoteId, playEmoteOption);
    }

    internal static bool ShouldEndSwapBeforeExecuting(SelfBypassMode mode, bool isExecutingSwap,
        SwapOptionEntry? armed, ushort emoteId)
        => mode == SelfBypassMode.EmoteSwap
        && !isExecutingSwap
        && armed != null
        && armed.TargetEmote == emoteId;

    // Hooking this function to detect when an emote is played by any character (including the local player)
    // This is necessary if a player is playing a bypassed looping emote and then tries to play
    // a base/obtained game emote. In that case, we need to stop the bypassed looping emote first.
    // Only for direct play mode
    private static void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            var character = CharacterHelper.GetCharacterFromAddress((nint)instigatorAddr);

            if (character != null)
            {
                var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

                if (trackedCharacter == null)
                    return;

                var emote = EmoteHelper.GetEmoteById(emoteId);

                if (!emote.HasValue || EmoteHelper.GetEmoteCategory(emote.Value) != NoireLib.Enums.EmoteCategory.Expressions)
                    EmotePlayer.StopLoop(character, true);

                if (emote != null)
                    EmotePlayer.PlayEmote(character, emote.Value);
            }
        }
        finally
        {
            OnEmoteHook?.Original(unk, instigatorAddr, emoteId, targetId, unk2);
        }
    }
}
