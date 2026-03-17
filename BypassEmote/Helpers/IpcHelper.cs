using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Helpers.ObjectExtensions;

namespace BypassEmote.Helpers;

public static class IpcHelper
{
    public static void NotifyConfigChanged()
    {
        if (!CommonHelper.HasAnyLocalLoopedEmote())
            return;

        var localPlayer = NoireService.ObjectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        var shouldNotifyLocalPlayerStateChange = CommonHelper.TryGetTrackedCharacterFromAddress(localPlayer.Address) != null;

        var liveData = new IpcData().Serialize();
        var cacheData = BuildCacheableIpcData()?.Serialize();
        var localPlayerLiveData = shouldNotifyLocalPlayerStateChange ? BuildLocalPlayerOnlyIpcData().Serialize() : null;
        var localPlayerCacheData = shouldNotifyLocalPlayerStateChange ? BuildLocalPlayerOnlyCacheableIpcData()?.Serialize() : null;

        if (cacheData == null)
            return;

        NotifyStateChange(liveData, cacheData, localPlayerLiveData, localPlayerCacheData, true, shouldNotifyLocalPlayerStateChange, cacheData == null);
    }

    public static void NotifyEmoteStop(ICharacter character)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        if (local == null || !CharacterHelper.IsLocalObject(character))
            return;

        var isTriggeringLocalPlayer = character.Address == local.Address;

        var liveData = BuildLiveIpcData(character, ExecutedAction.StoppedEmote, 0).Serialize();
        var cacheData = BuildCacheableIpcData()?.Serialize();
        var localPlayerLiveData = isTriggeringLocalPlayer ? BuildLocalPlayerOnlyLiveIpcData(character, ExecutedAction.StoppedEmote, 0).Serialize() : null;
        var localPlayerCacheData = isTriggeringLocalPlayer ? BuildLocalPlayerOnlyCacheableIpcData()?.Serialize() : null;
        NotifyStateChange(liveData, cacheData, localPlayerLiveData, localPlayerCacheData, isTriggeringLocalPlayer, isTriggeringLocalPlayer, true);
    }

    public static void NotifyEmoteStart(ICharacter character, Emote emote)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        if (local == null || !CharacterHelper.IsLocalObject(character))
            return;

        var isTriggeringLocalPlayer = character.Address == local.Address;

        var liveData = BuildLiveIpcData(character, ExecutedAction.StartedEmote, emote.RowId).Serialize();
        var cacheData = BuildCacheableIpcData()?.Serialize();
        var localPlayerLiveData = isTriggeringLocalPlayer ? BuildLocalPlayerOnlyLiveIpcData(character, ExecutedAction.StartedEmote, emote.RowId).Serialize() : null;
        var localPlayerCacheData = isTriggeringLocalPlayer ? BuildLocalPlayerOnlyCacheableIpcData()?.Serialize() : null;
        NotifyStateChange(liveData, cacheData, localPlayerLiveData, localPlayerCacheData, isTriggeringLocalPlayer, isTriggeringLocalPlayer, false);
    }

    public static void NotifyLocalStateDisposed(bool isLocalPlayer)
    {
        var liveData = new IpcData().Serialize();
        var localPlayerLiveData = isLocalPlayer ? BuildLocalPlayerOnlyIpcData().Serialize() : null;
        NotifyStateChange(liveData, null, localPlayerLiveData, null, isLocalPlayer, isLocalPlayer, false);
    }

    private static void NotifyStateChange(string liveData, string? cacheData, string? localPlayerLiveData, string? localPlayerCacheData, bool isLocalPlayer, bool shouldNotifyLocalPlayerStateChange, bool sendDelayed = true)
    {
        IpcProvider.RaiseStateChangeImmediate(liveData, cacheData, isLocalPlayer);

        if (shouldNotifyLocalPlayerStateChange && localPlayerLiveData != null)
            IpcProvider.RaiseLocalPlayerStateChangeImmediate(localPlayerLiveData, localPlayerCacheData);

        IpcProvider.RaiseStateChange(liveData, cacheData, isLocalPlayer);

        if (shouldNotifyLocalPlayerStateChange && localPlayerLiveData != null)
            IpcProvider.RaiseLocalPlayerStateChange(localPlayerLiveData, localPlayerCacheData);

        if (sendDelayed)
        {
            DelayerHelper.Start("DelayedStateChange", 500.Milliseconds(), () =>
            {
                IpcProvider.RaiseStateChange(liveData, cacheData, isLocalPlayer);

                if (shouldNotifyLocalPlayerStateChange && localPlayerLiveData != null)
                    IpcProvider.RaiseLocalPlayerStateChange(localPlayerLiveData, localPlayerCacheData);
            });
        }
    }

    private static IpcData? BuildCacheableIpcData()
    {
        var cacheableData = new IpcData().ToCacheableData();
        return cacheableData.HasAnyCacheableState() ? cacheableData : null;
    }

    private static IpcData? BuildLocalPlayerOnlyCacheableIpcData()
    {
        var localPlayerOnlyData = BuildLocalPlayerOnlyIpcData();

        if (!localPlayerOnlyData.PlayerData.IsCacheable)
            return null;

        localPlayerOnlyData.PlayerData = localPlayerOnlyData.PlayerData.ToCacheableState();
        return localPlayerOnlyData;
    }

    private static CurrentState GetLiveCurrentState(uint emoteId)
    {
        if (emoteId == 0)
            return CurrentState.Stopped;

        var emote = EmoteHelper.GetEmoteById(emoteId);

        if (emote != null && CommonHelper.GetEmotePlayType(emote.Value) == EmotePlayType.Looped)
            return CurrentState.PlayingEmote;

        return CurrentState.Stopped;
    }

    private static IpcData BuildLiveIpcData(ICharacter character, ExecutedAction executedAction, uint emoteId)
    {
        var ipcData = new IpcData();
        var characterState = CommonHelper.CreateCharacterState(character.Address, executedAction, GetLiveCurrentState(emoteId), emoteId);
        ipcData.TrySetCharacterState(character.Address, characterState);
        return ipcData;
    }

    private static IpcData BuildLocalPlayerOnlyIpcData(CharacterState? playerState = null)
    {
        var ipcData = new IpcData();
        ipcData.PlayerData = playerState ?? ipcData.PlayerData;
        ipcData.CompanionData = new CharacterState();
        ipcData.PetData = new CharacterState();
        ipcData.BuddyData = new CharacterState();
        return ipcData;
    }

    private static IpcData BuildLocalPlayerOnlyLiveIpcData(ICharacter triggeringCharacter, ExecutedAction executedAction, uint emoteId)
    {
        var localPlayer = NoireService.ObjectTable.LocalPlayer;

        if (localPlayer == null)
            return BuildLocalPlayerOnlyIpcData();

        var playerState = triggeringCharacter.Address == localPlayer.Address
            ? CommonHelper.CreateCharacterState(localPlayer.Address, executedAction, GetLiveCurrentState(emoteId), emoteId)
            : CommonHelper.GetCharacterState(localPlayer.Address);

        return BuildLocalPlayerOnlyIpcData(playerState);
    }
}
