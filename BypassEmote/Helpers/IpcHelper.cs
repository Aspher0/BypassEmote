using BypassEmote.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System.Linq;

namespace BypassEmote.Helpers;

public static class IpcHelper
{
    internal static unsafe void NotifyConfigChanged()
    {
        var anyTrackedLocalObject = EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.IsLocalObject);
        if (anyTrackedLocalObject == null)
            return;

        var localPlayer = NoireService.ObjectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        var native = CharacterHelper.GetCharacterAddress(localPlayer);

        var provider = Service.Ipc;
        var ipcData = new IpcData(ActionType.ConfigUpdate, localPlayer.BaseId, localPlayer.ObjectIndex, native->ContentId, 0);
        provider.OnStateChangeImmediate?.Invoke(ipcData.Serialize());

        DelayerHelper.CancelAll();
        DelayerHelper.Start("BypassEmoteConfigChanged", () =>
        {
            provider.OnStateChange?.Invoke(ipcData.Serialize());
        }, 500);
    }

    public static unsafe void NotifyEmoteStop(ICharacter character)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        // Fire IPC event only if local player or owned object is stopping a looped emote
        if (local == null || !CommonHelper.IsLocalObject(character))
            return;

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);
        if (trackedCharacter == null)
            return;

        var native = CharacterHelper.GetCharacterAddress(character);

        // Tell IPC Callers that the emote has stopped immediately and again after the delay
        // Kinda hacky-whacky way to ensure the emote stop is registered properly with sync but it works.
        // This is needed to avoid the server position desync issue.
        // When player A moves and bypasses an emote, this player might still be moving on player B's screen when player A starts the emote, causing a false-positive "stop emote" message
        var provider = Service.Ipc;
        var ipcDataStop = new IpcData(ActionType.StopEmote, character.BaseId, character.ObjectIndex, native->ContentId, 0).Serialize();

        if (character.Address == local.Address)
        {
            // Is local player
            provider?.OnStateChangeImmediate?.Invoke(ipcDataStop);
            provider?.OnEmoteStateStopImmediate?.Invoke();

            provider?.OnStateChange?.Invoke(ipcDataStop);
            provider?.OnEmoteStateStop?.Invoke();

            DelayerHelper.CancelAll();
            DelayerHelper.Start("StopBypassingEmote", () =>
            {
                provider?.OnStateChange?.Invoke(ipcDataStop);
                provider?.OnEmoteStateStop?.Invoke();
            }, 500);
        }
        else
        {
            // Is Companion
            provider?.OnOwnedObjectStateChangeImmediate?.Invoke(character.Address, ipcDataStop);
            provider?.OnOwnedObjectEmoteStateStopImmediate?.Invoke(character.Address);

            provider?.OnOwnedObjectStateChange?.Invoke(character.Address, ipcDataStop);
            provider?.OnOwnedObjectEmoteStateStop?.Invoke(character.Address);

            DelayerHelper.CancelAll();
            DelayerHelper.Start("StopBypassingEmoteOwnedObject", () =>
            {
                provider?.OnOwnedObjectStateChange?.Invoke(character.Address, ipcDataStop);
                provider?.OnOwnedObjectEmoteStateStop?.Invoke(character.Address);
            }, 500);
        }
    }

    internal static unsafe void NotifyEmoteStart(ICharacter character, Emote emote)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        if (local == null || !CommonHelper.IsLocalObject(character))
            return;

        var native = CharacterHelper.GetCharacterAddress(character);

        // Fire IPC event after delay only if local player is the one playing the emote
        // The delay tries to ensure that your character has stopped moving on other clients (other players' screens) before notifying IPC subscribers
        // This is due to the slight desync/delay there is between 2 players when performing any action because this is how the game servers work
        // Without this delay, other players might see your character perform the bypassed emote, but then you will still be moving thus stopping the bypassed emote
        // This is also mitigated by the OnFrameworkUpdate check for position/rotation changes, but this delay helps a lot with consistency
        var provider = Service.Ipc;
        var ipcData = new IpcData(ActionType.PlayEmote, character.BaseId, character.ObjectIndex, native->ContentId, emote.RowId);

        if (character.Address == local.Address)
        {
            // Is local player
            provider?.OnStateChangeImmediate?.Invoke(ipcData.Serialize());
            provider?.OnEmoteStateStartImmediate?.Invoke(ipcData.IsLoopedEmote(), ipcData.Serialize());

            DelayerHelper.CancelAll();
            DelayerHelper.Start("PlayBypassedEmote", () =>
            {
                provider?.OnStateChange?.Invoke(ipcData.Serialize());
                provider?.OnEmoteStateStart?.Invoke(ipcData.IsLoopedEmote(), ipcData.Serialize());
            }, 500);
        }
        else
        {
            // Is Companion
            provider?.OnOwnedObjectStateChangeImmediate?.Invoke(character.Address, ipcData.Serialize());
            provider?.OnOwnedObjectEmoteStateStartImmediate?.Invoke(character.Address, ipcData.IsLoopedEmote(), ipcData.Serialize());

            DelayerHelper.CancelAll();
            DelayerHelper.Start("PlayBypassedEmoteOwnedObject", () =>
            {
                provider?.OnOwnedObjectStateChange?.Invoke(character.Address, ipcData.Serialize());
                provider?.OnOwnedObjectEmoteStateStart?.Invoke(character.Address, ipcData.IsLoopedEmote(), ipcData.Serialize());
            }, 500);
        }
    }
}
