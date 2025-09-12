using ECommons.EzIpcManager;
using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using BypassEmote.Helpers;

namespace BypassEmote;

public class IpcProvider
{
    public static IpcProvider? Instance { get; private set; }

    public static int MajorVersion => 1;
    public static int MinorVersion => 0;

    public IpcProvider()
    {
        Instance = this;
        EzIPC.Init(this);
    }

    [EzIPC("ApiVersion")]
    public (int Major, int Minor) ApiVersion() => (MajorVersion, MinorVersion);

    // Applies an emote to a character
    [EzIPC("PlayEmoteById")]
    public void PlayEmoteById(IntPtr characterAddress, uint emoteId)
    {
        Service.Framework.RunOnFrameworkThread(() =>
        {
            var castChar = CommonHelper.TryGetCharacterFromAddress(characterAddress);
            if (castChar != null)
                EmotePlayer.PlayEmoteById(castChar, emoteId);
        });
    }

    // Fires when the local player plays an emote
    [EzIPCEvent("LocalEmotePlayed")]
    public Action<IPlayerCharacter, uint>? LocalEmotePlayed;
}
