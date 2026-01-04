using BypassEmote.Helpers;
using BypassEmote.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.EzIpcManager;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Linq;

namespace BypassEmote;

public class IpcProvider
{
    public static int MajorVersion => 3;
    public static int MinorVersion => 0;

    private bool _isReady { get; set; } = false;
    private bool _disposed { get; set; } = false;

    public IpcProvider()
    {
        EzIPC.Init(this);
    }

    internal void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        OnDispose?.Invoke();
    }

    internal void NotifyReady()
    {
        if (_isReady)
            return;

        _isReady = true;
        OnReady?.Invoke();
    }




    /// <summary>
    /// Gets the API version of this IPC provider.
    /// </summary>
    /// <returns>A tuple of the major and minor version numbers.</returns>
    [EzIPC("ApiVersion")]
    public (int Major, int Minor) ApiVersion()
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        return (MajorVersion, MinorVersion);
    }

    /// <summary>
    /// Indicates whether BypassEmote is ready for operation.
    /// </summary>
    /// <returns>true if ready; otherwise, false.</returns>
    [EzIPC("IsReady")]
    public bool IsReady() => _isReady && !_disposed;

    /// <summary>
    /// Sets the data for the character specified in the ipcData, playing or stopping an emote as needed.<br/>
    /// Stopping an emote is also possible with <see cref="ClearStateForCharacter(nint)"/>.
    /// </summary>
    /// <param name="data">The serialized ReceivedIpcData to apply.</param>
    [EzIPC("SetState")]
    public unsafe void SetState(string serializedData)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData ipcData = new IpcData(serializedData);

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var lookingForChar = CommonHelper.GetCharacterFromBaseIdOrCid(ipcData.BaseId, ipcData.Cid);

            if (lookingForChar == null)
                return;

            var castChar = NoireService.ObjectTable.OfType<ICharacter>().FirstOrDefault(o => o.Address == lookingForChar.Address);

            if (castChar != null)
                EmotePlayer.OnReceiveIPCData(castChar, ipcData);
        });
    }

    /// <summary>
    /// Use <see cref="SetState(string)"/> unless you specifically need to target a character by address.<br/>
    /// If the character address does not match the one in the data, it will update the data to match the character at the specified address.<br/><br/>
    /// 
    /// Sets the data for the specified character, playing or stopping an emote as needed.<br/>
    /// Stopping an emote is also possible with <see cref="ClearStateForCharacter(nint)"/>.
    /// </summary>
    /// <param name="characterAddress">The address of the character to apply the data to.</param>
    /// <param name="data">The serialized ReceivedIpcData to apply to the character.</param>
    [EzIPC("SetStateForCharacter")]
    public unsafe void SetStateForCharacter(nint characterAddress, string serializedData)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData ipcData = new IpcData(serializedData);

        if (characterAddress != ipcData.CharacterAddress)
        {
            // Character Mismatch, updating data to match character at specified address
            var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);

            if (castChar == null)
                return;

            var native = CharacterHelper.GetCharacterAddress(castChar);

            ipcData.BaseId = castChar.BaseId;
            ipcData.Cid = native->ContentId;
        }

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.TryGetCharacterFromAddress(ipcData.CharacterAddress);
            if (castChar != null)
                EmotePlayer.OnReceiveIPCData(castChar, ipcData);
        });
    }

    /// <summary>
    /// Clears the emote state for the specified character (i.e.: stops any playing emote).
    /// </summary>
    /// <param name="characterAddress">The address of the character you want to clear the state for.</param>
    [EzIPC("ClearStateForCharacter")]
    public void ClearStateForCharacter(nint characterAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
            if (castChar != null)
                EmotePlayer.StopLoop(castChar, true);
        });
    }

    /// <summary>
    /// Gets the current state data of the specified character.<br/>
    /// Needs to be run on the framework thread for now. Subject to change later.
    /// </summary>
    /// <returns>A serialized ReceivedIpcData representing the specified character's current state.</returns>
    [EzIPC("GetStateForCharacter")]
    public unsafe string GetStateForCharacter(nint characterAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData data = new IpcData(ActionType.Unknown, 0, 0, 0, 0);

        var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
        if (castChar == null)
            return data.Serialize();

        var native = CharacterHelper.GetCharacterAddress(castChar);

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(castChar.Address);
        data.EmoteId = trackedCharacter?.PlayingEmoteId ?? 0;
        data.ActionType = data.EmoteId == 0 ? ActionType.StopEmote : ActionType.PlayEmote;
        data.BaseId = castChar.BaseId;
        data.Cid = native->ContentId;
        data.StopOwnedObjectEmoteOnMove = trackedCharacter?.ReceivedIpcData?.StopOwnedObjectEmoteOnMove ?? Configuration.Instance.StopOwnedObjectEmoteOnMove;

        return data.Serialize();
    }



    /// <summary>
    /// An event that fires when BypassEmote becomes ready.
    /// </summary>
    [EzIPCEvent("OnReady")]
    public Action? OnReady;

    /// <summary>
    /// An event that fires when BypassEmote is disposing.
    /// </summary>
    [EzIPCEvent("OnDispose")]
    public Action? OnDispose;



    /// <summary>
    /// An event that fires when the state of the local player changes (i.e.: when bypassing or stopping emotes, or when the configuration changes).<br/>
    /// Contains the serialized ReceivedIpcData representing the new state.<br/>
    /// Will trigger both when starting and stopping emotes, but also on configuration changes.<br/>
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will be fired 500ms after an emote starts playing, and fired twice on emote stop with 500ms delay in between (once immediately, once after 500ms delay).<br/>
    /// This is the recommended event to use if your intention is to sync multiple clients together, and the delays will help ensure all clients will receive messages properly.<br/>
    /// This is also the recommended event to use for relaying data, since configuration changes will be captured.<br/>
    /// Optionally, use <see cref="OnEmoteStateStart"/> and <see cref="OnEmoteStateStop"/> to cache data if needed.<br/>
    /// For immediate reaction to state changes, use <see cref="OnStateChangeImmediate"/> instead.<br/><br/>
    /// 
    /// If you only want to know when the local player *starts* bypassing an emote, use <see cref="OnEmoteStateStart"/>.<br/>
    /// If you only want to know when the local player *stops* bypassing an emote, use <see cref="OnEmoteStateStop"/>.<br/>
    /// </remarks>
    [EzIPCEvent("OnStateChange")]
    public Action<string>? OnStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except it fires only once and immediately when the state changes, instead of:<br/>
    /// - Being sent after a 500ms delay when starting an emote.<br/>
    /// - Being sent twice when stopping an emote (once immediately, once after 500ms delay).<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together (relay messages), this is *NOT* recommended and you should use <see cref="OnStateChange"/> instead.
    /// </remarks>
    [EzIPCEvent("OnStateChangeImmediate")]
    public Action<string>? OnStateChangeImmediate;

    /// <summary>
    /// Same as <see cref="OnStateChange"/> but for companions, pets and buddies (i.e. minions, carbuncle/eos and chocobos).<br/>
    /// Contains the address of the companion/pet/buddy and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnOwnedObjectStateChange")]
    public Action<nint, string>? OnOwnedObjectStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChangeImmediate"/> but for companions, pets and buddies (i.e. minions, carbuncle/eos and chocobos).<br/>
    /// Contains the address of the companion/pet/buddy and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnOwnedObjectStateChangeImmediate")]
    public Action<nint, string>? OnOwnedObjectStateChangeImmediate;



    /// <summary>
    /// An event that fires when the local player starts bypassing an emote.<br/>
    /// Contains:<br/>
    /// - A boolean indicating whether the emote is looping (true = looping, false = one-shot or facial expression).<br/>
    /// - The serialized ReceivedIpcData representing the emote state.<br/>
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will be fired 500ms after the emote actually starts playing.<br/>
    /// This is the recommended event to use if your intention is to cache data for syncing multiple clients together, and the delays will help ensure all clients will receive messages properly.<br/>
    /// This is *not* recommended as a way to relay data for syncing purposes, as configuration changes won't be captured. Use <see cref="OnStateChange"/> instead.<br/><br/>
    /// 
    /// For immediate reaction to the emote start, use <see cref="OnEmoteStateStartImmediate"/> instead.<br/><br/>
    /// 
    /// Will *not* trigger when stopping any emote.
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStart")]
    public Action<bool, string>? OnEmoteStateStart;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStart"/>, but fires immediately when the emote starts playing instead of 500ms later.<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together (relay messages), this is *NOT* recommended and you should use <see cref="OnStateChange"/> instead.<br/>
    /// If you want to cache data, use <see cref="OnEmoteStateStart"/> instead.
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStartImmediate")]
    public Action<bool, string>? OnEmoteStateStartImmediate;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStart"/> but for companions, pets and buddies (i.e. minions, carbuncle/eos and chocobos).<br/>
    /// Contains the address of the companion/pet/buddy, a boolean indicating whether the emote is looping and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnOwnedObjectEmoteStateStart")]
    public Action<nint, bool, string>? OnOwnedObjectEmoteStateStart;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStartImmediate"/> but for companions, pets and buddies (i.e. minions, carbuncle/eos and chocobos).<br/>
    /// Contains the address of the companion/pet/buddy, a boolean indicating whether the emote is looping and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnOwnedObjectEmoteStateStartImmediate")]
    public Action<nint, bool, string>? OnOwnedObjectEmoteStateStartImmediate;



    /// <summary>
    /// An event that fires when the state of a character is cleared (i.e.: when stopping a looping emote).<br/>
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will *always* be fired twice, once immediately on stop, one a second time 500ms later.<br/>
    /// Please take it in consideration if you are subscribing to it. Subject to change later.<br/>
    /// This is the recommended event to use if your intention is to cache data for syncing multiple clients together, and the delays will help ensure all clients will receive messages properly.<br/>
    /// This is *not* recommended as a way to relay data for syncing purposes, as configuration changes won't be captured. Use <see cref="OnStateChange"/> instead.<br/><br/>
    /// 
    /// For immediate reaction to the emote stop, use <see cref="OnEmoteStateStopImmediate"/> instead.<br/><br/>
    /// 
    /// Will *not* trigger when starting to bypass an emote.<br/>
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStop")]
    public Action? OnEmoteStateStop;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStop"/>, but fires only once and immediately when the emote is stopped instead of twice (once immediately, once after 500ms delay).<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together (relay messages), this is *NOT* recommended and you should use <see cref="OnStateChange"/> instead.<br/>
    /// If you want to cache data, use <see cref="OnEmoteStateStop"/> instead.
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStopImmediate")]
    public Action? OnEmoteStateStopImmediate;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStop"/> but for companions, pets and buddies (i.e. minions, carbuncle/eos and chocobos).<br/>
    /// Contains the address of the companion/pet/buddy.
    /// </summary>
    [EzIPCEvent("OnOwnedObjectEmoteStateStop")]
    public Action<nint>? OnOwnedObjectEmoteStateStop;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStopImmediate"/> but for companions, pets and buddies (i.e. minions, carbuncle/eos and chocobos).<br/>
    /// Contains the address of the companion/pet/buddy.
    /// </summary>
    [EzIPCEvent("OnOwnedObjectEmoteStateStopImmediate")]
    public Action<nint>? OnOwnedObjectEmoteStateStopImmediate;
}
