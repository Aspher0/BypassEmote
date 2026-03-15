using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.IPC;
using System;

namespace BypassEmote;

[NoireIpcClass("BypassEmote")]
public static class IpcProvider
{
    public static int MajorVersion => 4;
    public static int MinorVersion => 0;

    private static bool _isReady { get; set; } = false;
    private static bool _disposed { get; set; } = false;

    internal static void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        OnDispose?.Invoke();
    }

    internal static void NotifyReady()
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
    [NoireIpc]
    public static (int Major, int Minor) ApiVersion()
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        return (MajorVersion, MinorVersion);
    }

    /// <summary>
    /// Indicates whether BypassEmote is ready for operation.
    /// </summary>
    /// <returns>true if ready; otherwise, false.</returns>
    [NoireIpc]
    public static bool IsReady() => _isReady && !_disposed;



    /// <summary>
    /// Updates the state for characters contained in the serialized json data.
    /// </summary>
    /// <param name="serializedData">The serialized <see cref="IpcData"/> containing state informations.</param>
    /// <param name="applyOwnedObjects">Determines whether to apply characterStates to owned objects such as companions, buddies and pets.</param>
    [NoireIpc("SetStateV1")]
    public static void SetState(string serializedData, bool applyOwnedObjects)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData ipcData = new IpcData(serializedData);

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            ipcData.ApplyAll(applyOwnedObjects);
        });
    }

    /// <summary>
    /// Sets the state of a specific character based on the provided serialized data.
    /// </summary>
    /// <param name="characterAddress">The address of the character to apply the data to.</param>
    /// <param name="serializedData">The serialized <see cref="CharacterState"/> to apply to the character.</param>
    [NoireIpc("SetStateForCharacterV1")]
    public static unsafe void SetStateForCharacter(nint characterAddress, string serializedData)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        CharacterState characterState = new CharacterState(serializedData);

        if (characterAddress != characterState.CharacterAddress)
        {
            // Character Mismatch, updating characterState to match character at specified address
            var castChar = CharacterHelper.GetCharacterFromAddress(characterAddress);

            if (castChar == null)
                throw new ArgumentException($"There was a mismatch between the specified character address and the address contained in the serialized data.", nameof(characterAddress));

            var native = CharacterHelper.GetCharacterAddress(castChar);

            characterState.BaseId = castChar.BaseId;
            characterState.Cid = native->ContentId;
        }

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            characterState.ApplyState(true);
        });
    }

    /// <summary>
    /// Clears the emote state for the specified character (i.e.: stops any playing emote).
    /// </summary>
    /// <param name="characterAddress">The address of the character you want to clear the state for.</param>
    [NoireIpc("ClearStateForCharacterV1")]
    public static void ClearStateForCharacter(nint characterAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.GetCharacterFromAddress(characterAddress);
            if (castChar != null)
                EmotePlayer.StopLoop(castChar, true);
        });
    }

    /// <summary>
    /// Gets the current state data of the specified character.<br/>
    /// Needs to be run on the framework thread for now. Subject to change later.
    /// </summary>
    /// <returns>A serialized ReceivedIpcData representing the specified character's current state.</returns>
    [NoireIpc("GetStateForCharacterV1")]
    public static string GetStateForCharacter(nint characterAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        var characterState = CommonHelper.GetCharacterState(characterAddress);
        return characterState.Serialize();
    }



    /// <summary>
    /// An event that fires when BypassEmote becomes ready.
    /// </summary>
    [NoireIpc]
    public static event Action? OnReady;

    /// <summary>
    /// An event that fires when BypassEmote is disposing.
    /// </summary>
    [NoireIpc]
    public static event Action? OnDispose;



    /// <summary>
    /// An event that fires when the state of the local player changes (i.e.: when bypassing or stopping emotes, or when the configuration changes).<br/>
    /// Contains:<br/>
    /// - The type of action <see cref="IpcActionType"/> that triggered the state change.<br/>
    /// - The recommended cache action <see cref="CacheAction"/> to perform.<br/>
    ///   0 Meaning you should add or update your cache with the new data.<br/>
    ///   1 Meaning you can safely delete cached data as there is no local character bypassing emotes anymore.<br/>
    /// - A boolean indicating whether the triggering character is the local player itself. A value of false meaning it is triggered by an owned entity (companion, buddy or pet).<br/>
    /// - The serialized data <see cref="IpcData"/> representing the new state.<br/><br/>
    /// 
    /// Will trigger when starting and stopping emotes, but also on configuration changes.<br/>
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will be fired 500ms after an emote starts playing, and fired twice on emote stop with 500ms delay in between (once immediately, once after 500ms delay).<br/>
    /// This is the recommended event to use if your intention is to sync multiple clients together, as the delays will help ensure all clients will receive messages properly.<br/>
    /// For immediate reaction to state changes, use <see cref="OnStateChangeImmediate"/> instead.
    /// </remarks>
    [NoireIpc("OnStateChangeV1")]
    public static event Action<int, int, bool, string>? OnStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except it fires only once and immediately when the state changes, instead of:<br/>
    /// - Being sent after a 500ms delay when starting an emote.<br/>
    /// - Being sent twice when stopping an emote (once immediately, once after 500ms delay).<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together, this is *NOT* recommended and you should use <see cref="OnStateChange"/> instead.
    /// </remarks>
    [NoireIpc("OnStateChangeImmediateV1")]
    public static event Action<int, int, bool, string>? OnStateChangeImmediate;
}
