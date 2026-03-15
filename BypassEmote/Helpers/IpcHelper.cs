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

        //var ipcData = new CharacterState(ExecutedAction.None, localPlayer.BaseId, localPlayer.ObjectIndex, native->ContentId, 0);
        //IpcProvider.OnStateChangeImmediate?.Invoke(ipcData.Serialize());

        //DelayerHelper.CancelAll();
        //DelayerHelper.Start("BypassEmoteConfigChanged", 500.Milliseconds(), () =>
        //{
        //    IpcProvider.OnStateChange?.Invoke(ipcData.Serialize());
        //});

        // Make new IPCData
    }

    public static unsafe void NotifyEmoteStop(ICharacter character)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        // Fire IPC event only if local player or owned object is stopping a looped emote
        if (local == null || !CharacterHelper.IsLocalObject(character))
            return;

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);
        if (trackedCharacter == null)
            return;

        var native = CharacterHelper.GetCharacterAddress(character);

        // Tell IPC Callers that the emote has stopped immediately and again after the delay
        // Kinda hacky-whacky way to ensure the emote stop is registered properly with sync but it works.
        // This is needed to avoid the server position desync issue.
        // When player A moves and bypasses an emote, this player might still be moving on player B's screen when player A starts the emote, causing a false-positive "stop emote" message
        //var ipcDataStop = new CharacterState(CurrentState.StopEmote, character.BaseId, character.ObjectIndex, native->ContentId, 0).Serialize();

        //if (character.Address == local.Address)
        //{
        //    // Is local player
        //    IpcProvider.OnStateChangeImmediate?.Invoke(ipcDataStop);
        //    IpcProvider.OnEmoteStateStopImmediate?.Invoke();

        //    IpcProvider.OnStateChange?.Invoke(ipcDataStop);
        //    IpcProvider.OnEmoteStateStop?.Invoke();

        //    DelayerHelper.CancelAll();
        //    DelayerHelper.Start("StopBypassingEmote", 500.Milliseconds(), () =>
        //    {
        //        IpcProvider.OnStateChange?.Invoke(ipcDataStop);
        //        IpcProvider.OnEmoteStateStop?.Invoke();
        //    });
        //}
        //else
        //{
        //    // Is Companion
        //    IpcProvider.OnOwnedObjectStateChangeImmediate?.Invoke(character.Address, ipcDataStop);
        //    IpcProvider.OnOwnedObjectEmoteStateStopImmediate?.Invoke(character.Address);

        //    IpcProvider.OnOwnedObjectStateChange?.Invoke(character.Address, ipcDataStop);
        //    IpcProvider.OnOwnedObjectEmoteStateStop?.Invoke(character.Address);

        //    DelayerHelper.CancelAll();
        //    DelayerHelper.Start("StopBypassingEmoteOwnedObject", 500.Milliseconds(), () =>
        //    {
        //        IpcProvider.OnOwnedObjectStateChange?.Invoke(character.Address, ipcDataStop);
        //        IpcProvider.OnOwnedObjectEmoteStateStop?.Invoke(character.Address);
        //    });
        //}
    }

    internal static unsafe void NotifyEmoteStart(ICharacter character, Emote emote)
    {
        var local = NoireService.ObjectTable.LocalPlayer;

        if (local == null || !CharacterHelper.IsLocalObject(character))
            return;

        var native = CharacterHelper.GetCharacterAddress(character);

        // Fire IPC event after delay only if local player is the one playing the emote
        // The delay tries to ensure that your character has stopped moving on other clients (other players' screens) before notifying IPC subscribers
        // This is due to the slight desync/delay there is between 2 players when performing any action because this is how the game servers work
        // Without this delay, other players might see your character perform the bypassed emote, but then you will still be moving thus stopping the bypassed emote
        // This is also mitigated by the OnFrameworkUpdate check for position/rotation changes, but this delay helps a lot with consistency

        //var ipcData = new CharacterState(CurrentState.PlayEmote, character.BaseId, character.ObjectIndex, native->ContentId, emote.RowId);

        //if (character.Address == local.Address)
        //{
        //    // Is local player
        //    IpcProvider.OnStateChangeImmediate?.Invoke(ipcData.Serialize());
        //    IpcProvider.OnEmoteStateStartImmediate?.Invoke(ipcData.IsLoopedEmote(), ipcData.Serialize());

        //    DelayerHelper.CancelAll();
        //    DelayerHelper.Start("PlayBypassedEmote", 500.Milliseconds(), () =>
        //    {
        //        IpcProvider.OnStateChange?.Invoke(ipcData.Serialize());
        //        IpcProvider.OnEmoteStateStart?.Invoke(ipcData.IsLoopedEmote(), ipcData.Serialize());
        //    });
        //}
        //else
        //{
        //    // Is Companion
        //    IpcProvider.OnOwnedObjectStateChangeImmediate?.Invoke(character.Address, ipcData.Serialize());
        //    IpcProvider.OnOwnedObjectEmoteStateStartImmediate?.Invoke(character.Address, ipcData.IsLoopedEmote(), ipcData.Serialize());

        //    DelayerHelper.CancelAll();
        //    DelayerHelper.Start("PlayBypassedEmoteOwnedObject", 500.Milliseconds(), () =>
        //    {
        //        IpcProvider.OnOwnedObjectStateChange?.Invoke(character.Address, ipcData.Serialize());
        //        IpcProvider.OnOwnedObjectEmoteStateStart?.Invoke(character.Address, ipcData.IsLoopedEmote(), ipcData.Serialize());
        //    });
        //}
    }
}
