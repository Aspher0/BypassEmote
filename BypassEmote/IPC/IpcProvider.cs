using ECommons.EzIpcManager;
using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Serilog.Core;
using BypassEmote.Helpers;

namespace BypassEmote;

public class IpcProvider
{
    public static IpcProvider? Instance { get; private set; }

    public IpcProvider()
    {
        Instance = this;
        EzIPC.Init(this);
    }

    // Applies an emote to a character
    [EzIPC("PlayEmoteById")]
    public void PlayEmoteById(IntPtr character, uint emoteId)
    {
        Service.Framework.RunOnFrameworkThread(() =>
        {
            var playerCharacter = CommonHelper.TryGetPlayerCharacterFromAddress(character);
            EmotePlayer.PlayEmoteById(playerCharacter, emoteId);
        });
    }

    // Fires when the local player plays an emote
    [EzIPCEvent("LocalEmotePlayed")]
    public Action<IPlayerCharacter, uint>? LocalEmotePlayed;
}
