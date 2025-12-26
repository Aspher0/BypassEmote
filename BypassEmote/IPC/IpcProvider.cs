using BypassEmote.Helpers;
using BypassEmote.IPC;
using ECommons.EzIpcManager;
using NoireLib;
using NoireLib.Helpers;
using System;

namespace BypassEmote;

public class IpcProvider
{
    public static int MajorVersion => 2;
    public static int MinorVersion => 1;

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
    /// Sets the data for the specified character, playing or stopping an emote as needed.<br/>
    /// Stopping an emote is also possible with <see cref="ClearStateForCharacter(nint)"/>.
    /// </summary>
    /// <param name="characterAddress">The address of the character to apply the data to.</param>
    /// <param name="data">The serialized IpcData to apply to the character.</param>
    [EzIPC("SetStateForCharacter")]
    public void SetStateForCharacter(IntPtr characterAddress, string data)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData ipcData = new IpcData(data);

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
            if (castChar != null)
                EmotePlayer.PlayEmoteById(castChar, ipcData.EmoteId);
        });
    }

    /// <summary>
    /// Clears the emote state for the specified character (i.e.: stops any playing emote).
    /// </summary>
    /// <param name="characterAddress">The address of the character you want to clear the state for.</param>
    [EzIPC("ClearStateForCharacter")]
    public void ClearStateForCharacter(IntPtr characterAddress)
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
    /// Gets the current state data of the local player.<br/>
    /// Needs to be run on the framework thread for now. Subject to change later.
    /// </summary>
    /// <returns>A serialized IpcData representing the local player's current state.</returns>
    [EzIPC("GetStateForLocalPlayer")]
    public string GetStateForLocalPlayer()
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData data = new IpcData(0, 0);

        var localPlayer = NoireService.ObjectTable.LocalPlayer;
        if (localPlayer == null) return data.Serialize();

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(localPlayer.Address);
        data.EmoteId = trackedCharacter?.PlayingEmoteId ?? 0;
        data.CharacterAddress = localPlayer.Address;

        return data.Serialize();
    }

    /// <summary>
    /// Gets the current state data of the specified character.<br/>
    /// Needs to be run on the framework thread for now. Subject to change later.
    /// </summary>
    /// <returns>A serialized IpcData representing the specified character's current state.</returns>
    [EzIPC("GetStateForCharacter")]
    public string GetStateForCharacter(IntPtr characterAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData data = new IpcData(0, characterAddress);

        var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
        if (castChar == null)
            return data.Serialize();

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(castChar.Address);
        data.EmoteId = trackedCharacter?.PlayingEmoteId ?? 0;

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
    /// An event that fires when the state of the local player changes (i.e.: when bypassing or stopping emotes).<br/>
    /// Contains the serialized IpcData representing the new state.<br/>
    /// Will trigger both when starting and stopping emotes.<br/>
    /// Useful if you are going to deserialize the IpcData anyway, otherwise you might want to use <see cref="OnEmoteStateStart"/> and <see cref="OnEmoteStateStop"/> instead.
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will be fired 500ms after an emote starts playing, and fired twice on emote stop with 500ms delay in between (once immediately, once after 500ms delay).<br/>
    /// This is the recommended event to use if your intention is to sync multiple clients together, as the delays help ensure all clients will receive messages properly.<br/>
    /// For immediate reaction to state changes, use <see cref="OnStateChangeImmediate"/> instead.<br/><br/>
    /// 
    /// If you only want to know when the local player *starts* bypassing an emote, use <see cref="OnEmoteStateStart"/>.<br/>
    /// If you only want to know when the local player *stops* bypassing an emote, use <see cref="OnEmoteStateStop"/>.<br/>
    /// </remarks>
    [EzIPCEvent("OnStateChange")]
    public Action<string>? OnStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except it fires only once and immediately when the state changes, instead of:<br/>
    /// - Being sent after a 500ms del☺ay when starting an emote.<br/>
    /// - Being sent twice when stopping an emote (once immediately, once after 500ms delay).<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together, this is *NOT* recommended and you should use <see cref="OnStateChange"/> instead.
    /// </remarks>
    [EzIPCEvent("OnStateChangeImmediate")]
    public Action<string>? OnStateChangeImmediate;

    /// <summary>
    /// Same as <see cref="OnStateChange"/> but for companions such as minions.
    /// Contains the address of the companion and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnCompanionStateChange")]
    public Action<nint, string>? OnCompanionStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChangeImmediate"/> but for companions such as minions.<br/>
    /// Contains the address of the companion and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnCompanionStateChangeImmediate")]
    public Action<nint, string>? OnCompanionStateChangeImmediate;



    /// <summary>
    /// An event that fires when the local player starts bypassing an emote.<br/>
    /// Contains:<br/>
    /// - A boolean indicating whether the emote is looping (true = looping, false = one-shot or facial expression).<br/>
    /// - The serialized IpcData representing the emote state.<br/>
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will be fired 500ms after the emote actually starts playing.<br/>
    /// This is the recommended event to use if your intention is to sync multiple clients together, as the delays help ensure all clients will receive messages properly.<br/>
    /// For immediate reaction to the emote start, use <see cref="OnEmoteStateStartImmediate"/> instead.<br/>
    /// 
    /// Will *not* trigger when stopping any emote.
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStart")]
    public Action<bool, string>? OnEmoteStateStart;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStart"/>, but fires immediately when the emote starts playing instead of 500ms later.<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together, this is *NOT* recommended and you should use <see cref="OnEmoteStateStart"/> instead.<br/><br/>
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStartImmediate")]
    public Action<bool, string>? OnEmoteStateStartImmediate;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStart"/> but for companions such as minions.
    /// Contains the address of the companion, a boolean indicating whether the emote is looping and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnCompanionEmoteStateStart")]
    public Action<nint, bool, string>? OnCompanionEmoteStateStart;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStartImmediate"/> but for companions such as minions.<br/>
    /// Contains the address of the companion, a boolean indicating whether the emote is looping and the serialized IPC data.
    /// </summary>
    [EzIPCEvent("OnCompanionEmoteStateStartImmediate")]
    public Action<nint, bool, string>? OnCompanionEmoteStateStartImmediate;



    /// <summary>
    /// An event that fires when the state of a character is cleared (i.e.: when stopping a looping emote).<br/>
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will *always* be fired twice, once immediately on stop, one a second time 500ms later.<br/>
    /// Please take it in consideration if you are subscribing to it. Subject to change later.<br/>
    /// This is the recommended event to use if your intention is to sync multiple clients together, as the delays help ensure all clients will receive messages properly.<br/><br/>
    /// 
    /// Will *not* trigger when starting to bypass an emote.<br/>
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStop")]
    public Action? OnEmoteStateStop;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStop"/>, but fires only once and immediately when the emote is stopped instead of twice (once immediately, once after 500ms delay).<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together, this is *NOT* recommended and you should use <see cref="OnEmoteStateStop"/> instead.
    /// </remarks>
    [EzIPCEvent("OnEmoteStateStopImmediate")]
    public Action? OnEmoteStateStopImmediate;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStop"/> but for companions such as minions.
    /// Contains the address of the companion.
    /// </summary>
    [EzIPCEvent("OnCompanionEmoteStateStop")]
    public Action<nint>? OnCompanionEmoteStateStop;

    /// <summary>
    /// Same as <see cref="OnEmoteStateStopImmediate"/> but for companions such as minions.<br/>
    /// Contains the address of the companion.
    /// </summary>
    [EzIPCEvent("OnCompanionEmoteStateStopImmediate")]
    public Action<nint>? OnCompanionEmoteStateStopImmediate;
}
