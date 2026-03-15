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

        var liveData = new IpcData().Serialize();
        var cacheData = BuildCacheableIpcData()?.Serialize();

        if (cacheData == null)
            return;

        NotifyStateChange(liveData, cacheData, true);
    }

    public static void NotifyEmoteStop(ICharacter character)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        if (local == null || !CharacterHelper.IsLocalObject(character))
            return;

        var liveData = BuildLiveIpcData(character, ExecutedAction.StoppedEmote, 0).Serialize();
        var cacheData = BuildCacheableIpcData()?.Serialize();
        NotifyStateChange(liveData, cacheData, character.Address == local.Address);
    }

    public static void NotifyEmoteStart(ICharacter character, Emote emote)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        if (local == null || !CharacterHelper.IsLocalObject(character))
            return;

        var liveData = BuildLiveIpcData(character, ExecutedAction.StartedEmote, emote.RowId).Serialize();
        var cacheData = BuildCacheableIpcData()?.Serialize();
        NotifyStateChange(liveData, cacheData, character.Address == local.Address);
    }

    public static void NotifyLocalStateDisposed(bool isLocalPlayer)
    {
        NotifyStateChange(new IpcData().Serialize(), null, isLocalPlayer, false);
    }

    private static void NotifyStateChange(string liveData, string? cacheData, bool isLocalPlayer, bool sendDelayed = true)
    {
        IpcProvider.RaiseStateChangeImmediate(liveData, cacheData, isLocalPlayer);

        if (cacheData == null) // This is a stop event, we send it immediately and then again with a delay
            IpcProvider.RaiseStateChange(liveData, cacheData, isLocalPlayer);

        if (sendDelayed)
        {
            DelayerHelper.Start("DelayedStateChange", 500.Milliseconds(), () =>
            {
                IpcProvider.RaiseStateChange(liveData, cacheData, isLocalPlayer);
            });
        }
    }

    private static IpcData? BuildCacheableIpcData()
    {
        var cacheableData = new IpcData().ToCacheableData();
        return cacheableData.HasAnyCacheableState() ? cacheableData : null;
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
}
