using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.SubKinds;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.IPC;
using System;

namespace BypassEmote;

[NoireIpcClass("BypassEmote")]
public static class IpcProvider
{
    public static int MajorVersion => 4;
    public static int MinorVersion => 1;

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

    internal static unsafe void RaiseStateChange(string liveData, string? cacheData, bool isLocalPlayer)
    {
        if (_disposed)
            return;

        OnStateChange?.Invoke(liveData, cacheData, isLocalPlayer);

#if DEBUG
        if (NoireService.ObjectTable.LocalPlayer != null)
        {
            var native = CharacterHelper.GetCharacterAddress(NoireService.ObjectTable.LocalPlayer);
            Service.NetworkRelay.SendToAllPeers<(ulong, string, string?, bool)>((native->ContentId, liveData, cacheData, isLocalPlayer));
        }
#endif
    }

    internal static void RaiseStateChangeImmediate(string liveData, string? cacheData, bool isLocalPlayer)
    {
        if (_disposed)
            return;

        OnStateChangeImmediate?.Invoke(liveData, cacheData, isLocalPlayer);
    }

    internal static void RaiseLocalPlayerStateChange(string liveData, string? cacheData)
    {
        if (_disposed)
            return;

        OnLocalPlayerStateChange?.Invoke(liveData, cacheData);
    }

    internal static void RaiseLocalPlayerStateChangeImmediate(string liveData, string? cacheData)
    {
        if (_disposed)
            return;

        OnLocalPlayerStateChangeImmediate?.Invoke(liveData, cacheData);
    }

#if DEBUG
    public static void EnsureListeningRelay()
    {
        if (Service.NetworkRelay == null)
            return;

        Service.NetworkRelay.On<(ulong, string, string?, bool)>("Listen IPC Events", (data) =>
        {
            NoireService.Framework.RunOnFrameworkThread(() =>
            {
                NoireLogger.LogDebug($"Received IPC event, should cache: {data.Item3 != null}, data: {data}");
                var character = CharacterHelper.GetCharacterFromCID(data.Item1);
                if (character != null)
                {
                    SetState(data.Item2, true);
                }
            });
        });
    }
#endif


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
            characterState.Cid = castChar is IPlayerCharacter ? native->ContentId : 0UL;
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
    /// - The serialized live <see cref="IpcData"/> that should be relayed immediately to clients already in range.<br/>
    /// - The serialized cacheable <see cref="IpcData"/> snapshot, or null if cached data needs to be safely removed from the cache.<br/>
    /// - A boolean indicating whether the triggering character is the local player itself. A value of false means it is triggered by an owned entity (companion, buddy or pet).<br/><br/>
    /// 
    /// Will trigger when starting and stopping emotes, but also on configuration changes.
    /// </summary>
    /// <remarks>
    /// For technical reasons, this will be fired 500ms after an emote starts playing, and fired twice on emote stop with 500ms delay in between (once immediately, once after 500ms delay).<br/>
    /// This is the recommended event to use if your intention is to sync multiple clients together, as the delays will help ensure all clients will receive messages properly.<br/>
    /// For immediate reaction to state changes, use <see cref="OnStateChangeImmediate"/> instead.
    /// </remarks>
    [NoireIpc("OnStateChangeV1")]
    public static event Action<string, string?, bool>? OnStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except it fires only once and immediately when the state changes, instead of:<br/>
    /// - Being sent after a 500ms delay when starting an emote.<br/>
    /// - Being sent twice when stopping an emote (once immediately, once after 500ms delay).<br/>
    /// </summary>
    /// <remarks>
    /// If your intention is to sync multiple clients together, this is *NOT* recommended and you should use <see cref="OnStateChange"/> instead.
    /// </remarks>
    [NoireIpc("OnStateChangeImmediateV1")]
    public static event Action<string, string?, bool>? OnStateChangeImmediate;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except the serialized <see cref="IpcData"/> only accounts for the local player.<br/>
    /// The companion, pet and buddy states are always forced to their default stopped values, and cached data is only kept if the local player has cacheable state.<br/>
    /// </summary>
    /// <remarks>
    /// This follows the same delayed behavior as <see cref="OnStateChange"/>.
    /// </remarks>
    [NoireIpc("OnLocalPlayerStateChangeV1")]
    public static event Action<string, string?>? OnLocalPlayerStateChange;

    /// <summary>
    /// Same as <see cref="OnLocalPlayerStateChange"/>, except it fires only once and immediately when the state changes.
    /// </summary>
    /// <remarks>
    /// This follows the same immediate behavior as <see cref="OnStateChangeImmediate"/>, while still serializing only the local player's state.
    /// </remarks>
    [NoireIpc("OnLocalPlayerStateChangeImmediateV1")]
    public static event Action<string, string?>? OnLocalPlayerStateChangeImmediate;
}
