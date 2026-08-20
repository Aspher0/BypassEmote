using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.SubKinds;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.IPC;
using System;

namespace BypassEmote.IPC;

// Prefix for every IPC method. Example: BypassEmote.ApiVersion
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
            Service.Networker.Send(new NetworkRelayIpcMessage
            {
                ContentId = native->ContentId,
                LiveData = liveData,
                CacheData = cacheData,
                IsLocalPlayer = isLocalPlayer,
            });
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
    private sealed class NetworkRelayIpcMessage
    {
        public ulong ContentId { get; set; }
        public string LiveData { get; set; } = string.Empty;
        public string? CacheData { get; set; }
        public bool IsLocalPlayer { get; set; }
    }

    public static void EnsureListeningRelay()
    {
        if (Service.Networker == null)
            return;

        Service.Networker.On<NetworkRelayIpcMessage>((_, data) =>
        {
            NoireLogger.LogDebug($"Received IPC event, should cache: {data.CacheData != null}, data: ({data.ContentId}, {data.LiveData}, {data.CacheData}, {data.IsLocalPlayer})");
            var character = CharacterHelper.GetCharacterFromCID(data.ContentId);
            if (character != null)
            {
                SetState(data.LiveData, true);
            }
        }, key: "Listen IPC Events");
    }
#endif


    /// <summary>
    /// Gets the API version of this IPC provider.
    /// </summary>
    /// <returns>A tuple of the major and minor version numbers.</returns>
    [NoireIpc] // BypassEmote.ApiVersion
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
    [NoireIpc] // BypassEmote.IsReady
    public static bool IsReady() => _isReady && !_disposed;



    /// <summary>
    /// Updates the state for characters contained in the serialized json data.<br/>
    /// Prefer this over <see cref="SetStateForCharacter"/>.
    /// </summary>
    /// <param name="serializedData">The serialized <see cref="IpcData"/> containing state informations.</param>
    /// <param name="applyOwnedObjects">Determines whether to also apply characterStates to owned objects such as companions, buddies and pets.</param>
    [NoireIpc("SetStateV1")] // BypassEmote.SetStateV1
    public static void SetState(string serializedData, bool applyOwnedObjects)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        IpcData ipcData = new IpcData(serializedData);

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            if (IgnoresIncomingState(ipcData.PlayerData.CharacterAddress, nameof(SetState)))
            {
                // Only the player half is refused; companion, pet and buddy states still apply.
                ipcData.ApplyAll(applyOwnedObjects, includePlayer: false);
                return;
            }

            ipcData.ApplyAll(applyOwnedObjects);
        });
    }

    /// <summary>
    /// Sets the state of a specific character based on the provided serialized data.<br/>
    /// You probably want to use <see cref="SetState"/> instead.
    /// </summary>
    /// <param name="characterAddress">The address of the character to apply the data to.</param>
    /// <param name="serializedData">The serialized <see cref="CharacterState"/> to apply to the character.</param>
    [NoireIpc("SetStateForCharacterV1")] // BypassEmote.SetStateForCharacterV1
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
            if (IgnoresIncomingState(characterState.CharacterAddress, nameof(SetStateForCharacter)))
                return;

            characterState.ApplyState(true);
        });
    }

    /// <summary>
    /// Clears the emote state for the specified character (i.e.: stops any playing emote).
    /// </summary>
    /// <param name="characterAddress">The address of the character you want to clear the state for.</param>
    [NoireIpc("ClearStateForCharacterV1")] // BypassEmote.ClearStateForCharacterV1
    public static void ClearStateForCharacter(nint characterAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException("IpcProvider");

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.GetCharacterFromAddress(characterAddress);

            if (castChar == null)
                return;

            if (IgnoresIncomingState(castChar.Address, nameof(ClearStateForCharacter)))
                return;

            EmotePlayer.StopLoop(castChar, true);
        });
    }

    // In Emote Swap mode the local player's emotes are real game emotes, so applying a received state to them
    // would force the client-side animation the mode avoids. Only the local player is ever refused.
    private static bool IgnoresIncomingState(nint characterAddress, string entryPoint)
    {
        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
            return false;

        if (characterAddress == nint.Zero || NoireService.ObjectTable.LocalPlayer is not { } localPlayer
            || characterAddress != localPlayer.Address)
            return false;

        NoireLogger.LogDebug($"{entryPoint}: local player state skipped, Emote Swap mode plays their emotes as real game emotes.");
        return true;
    }

    /// <summary>
    /// Gets the current state data of the specified character.<br/>
    /// Needs to be run on the framework thread for now. Subject to change later.
    /// </summary>
    /// <returns>A serialized ReceivedIpcData representing the specified character's current state.</returns>
    [NoireIpc("GetStateForCharacterV1")] // BypassEmote.GetStateForCharacterV1
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
    [NoireIpc] // BypassEmote.OnReady
    public static event Action? OnReady;

    /// <summary>
    /// An event that fires when BypassEmote is disposing.
    /// </summary>
    [NoireIpc] // BypassEmote.OnDispose
    public static event Action? OnDispose;



    /// <summary>
    /// An event that fires when the state of the local player changes (i.e.: when bypassing or stopping emotes, or when the configuration changes).<br/>
    /// Contains:<br/>
    /// - The serialized live <see cref="IpcData"/> that should be relayed immediately to clients already in range.<br/>
    /// - The serialized cacheable <see cref="IpcData"/> snapshot, or null if cached data needs to be safely removed from the cache.<br/>
    /// - A boolean indicating whether the triggering character is the local player itself. A value of false means it is triggered by an owned entity (companion, buddy or pet).<br/><br/>
    /// Fired 500ms after an emote starts, and twice on emote stop (once immediately, once after 500ms).<br/>
    /// Recommended for syncing multiple clients together. For an immediate reaction, use <see cref="OnStateChangeImmediate"/>.
    /// </summary>
    [NoireIpc("OnStateChangeV1")] // BypassEmote.OnStateChangeV1
    public static event Action<string, string?, bool>? OnStateChange;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except it fires only once and immediately when the state changes, instead of:<br/>
    /// - Being sent after a 500ms delay when starting an emote.<br/>
    /// - Being sent twice when stopping an emote (once immediately, once after 500ms delay).<br/>
    /// Not recommended for syncing multiple clients together, use <see cref="OnStateChange"/> for that.
    /// </summary>
    [NoireIpc("OnStateChangeImmediateV1")] // BypassEmote.OnStateChangeImmediateV1
    public static event Action<string, string?, bool>? OnStateChangeImmediate;

    /// <summary>
    /// Same as <see cref="OnStateChange"/>, except the serialized <see cref="IpcData"/> only accounts for the local player.<br/>
    /// The companion, pet and buddy states are always forced to their default stopped values, and cached data is only kept if the local player has cacheable state.<br/>
    /// Same delayed behavior as <see cref="OnStateChange"/>.
    /// </summary>
    [NoireIpc("OnLocalPlayerStateChangeV1")] // BypassEmote.OnLocalPlayerStateChangeV1
    public static event Action<string, string?>? OnLocalPlayerStateChange;

    /// <summary>
    /// Same as <see cref="OnLocalPlayerStateChange"/>, except it fires only once and immediately when the state changes.<br/>
    /// Same immediate behavior as <see cref="OnStateChangeImmediate"/>, while still serializing only the local player's state.
    /// </summary>
    [NoireIpc("OnLocalPlayerStateChangeImmediateV1")] // BypassEmote.OnLocalPlayerStateChangeImmediateV1
    public static event Action<string, string?>? OnLocalPlayerStateChangeImmediate;
}
