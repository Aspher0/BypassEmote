using BypassEmote.Helpers;
using BypassEmote.IPC;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.EzIpcManager;
using NoireLib;
using NoireLib.Helpers;
using System;

namespace BypassEmote;

public class IpcProvider
{
    public static IpcProvider? Instance { get; private set; }

    public static int MajorVersion => 1;
    public static int MinorVersion => 1;

    public IpcProvider()
    {
        Instance = this;
        EzIPC.Init(this);
    }

    /// <summary>
    /// Gets the API version of this IPC provider.
    /// </summary>
    /// <returns>A tuple of the major and minor version numbers.</returns>
    [EzIPC("ApiVersion")]
    public (int Major, int Minor) ApiVersion() => (MajorVersion, MinorVersion);

    /// <summary>
    /// Plays the specified emote for the specified character.<br/>
    /// This method is obsolete. Use SetStateForPlayer instead.<br/>
    /// Will be removed with API Major Version 2.
    /// </summary>
    /// <param name="characterAddress">The address of the character to apply the emote to.</param>
    /// <param name="emoteId">The id of the emote to play, or 0 to stop any looping emote.</param>
    [Obsolete("Replaced with SetStateForPlayer")]
    [EzIPC("PlayEmoteById")]
    public void PlayEmoteById(IntPtr characterAddress, uint emoteId)
    {
        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
            if (castChar != null)
                EmotePlayer.PlayEmoteById(castChar, emoteId);
        });
    }

    /// <summary>
    /// Occurs when the local player plays an emote or stops a looping emote.<br/>
    /// This event is obsolete. Use OnStateChanged instead.<br/>
    /// Will be removed with API Major Version 2.
    /// </summary>
    /// <remarks>The event is triggered with an emote ID of 0 when stopping a looping emote.</remarks>
    [Obsolete("Replaced with OnStateChanged")]
    [EzIPCEvent("LocalEmotePlayed")]
    public Action<IPlayerCharacter, uint>? LocalEmotePlayed;




    /// <summary>
    /// Sets the data for the specified player, playing or stopping an emote as needed.
    /// </summary>
    /// <param name="characterAddress">The address of the character to apply the data to.</param>
    /// <param name="data">The serialized IpcData to apply to the character.</param>
    [EzIPC("SetStateForPlayer")]
    public void SetStateForPlayer(IntPtr characterAddress, string data)
    {
        IpcData ipcData = new IpcData(data);

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
            if (castChar != null)
                EmotePlayer.PlayEmoteById(castChar, ipcData.EmoteId);
        });
    }

    /// <summary>
    /// Gets the current data of the local player.
    /// </summary>
    /// <returns>A serialized IpcData representing the local player's current state.</returns>
    [EzIPC("GetStateForLocalPlayer")]
    public string GetStateForLocalPlayer()
    {
        IpcData data = new IpcData(0);

        var localPlayer = NoireService.ClientState.LocalPlayer;
        if (localPlayer == null) return data.Serialize();

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(localPlayer.Address);
        data.EmoteId = trackedCharacter?.PlayingEmoteId ?? 0;

        return data.Serialize();
    }

    /// <summary>
    /// An event that fires when the state of the local player changes (i.e.: when bypassing or stopping emotes).<br/>
    /// Contains the serialized IpcData representing the new state.
    /// </summary>
    [EzIPCEvent("OnStateChanged")]
    public Action<string>? OnStateChanged;
}
