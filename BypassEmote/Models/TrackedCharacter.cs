using BypassEmote.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    public string UniqueId = Guid.NewGuid().ToString();
    public bool IsLocalObject;
    public ulong? CID;
    public uint? BaseId; // For NPCs
    public ushort? ObjectIndex; // For NPCs, specifically mannequins since you can have multiple mannequins of the same retainer
    public Vector3 LastPlayerPosition;
    public float LastPlayerRotation;
    public bool IsWeaponDrawn;
    public uint? PlayingEmoteId = null;
    public IpcData? ReceivedIpcData = null; // Received from IPC *only* (AKA: Another player)
    public bool ScheduledForRemoval = false;


    public TrackedCharacter(
        bool isLocalObject,
        ulong? cid,
        uint? baseId,
        ushort? objectIndex,
        Vector3 lastPlayerPos,
        float lastPlayerRot,
        bool isWeaponDrawn,
        uint playingEmoteId,
        IpcData? ipcData = null)
    {
        IsLocalObject = isLocalObject;
        CID = cid;
        BaseId = baseId;
        ObjectIndex = objectIndex;
        LastPlayerPosition = lastPlayerPos;
        LastPlayerRotation = lastPlayerRot;
        IsWeaponDrawn = isWeaponDrawn;
        PlayingEmoteId = playingEmoteId;
        ReceivedIpcData = ipcData;
    }

    public void UpdatePlayingEmoteId(Emote emote)
    {
        PlayingEmoteId = emote.RowId;
    }

    public void UpdateLastPosition()
    {
        ICharacter? character;

        if (CID != null)
            character = CharacterHelper.TryGetCharacterFromCID(CID.Value);
        else if (BaseId != null && ObjectIndex != null)
            character = Helpers.CommonHelper.TryGetCharacterFromBaseIdAndObjectIndex(BaseId.Value, ObjectIndex.Value);
        else
            return;

        if (character == null) return;
        LastPlayerPosition = character.Position;
        LastPlayerRotation = character.Rotation;
        IsWeaponDrawn = CharacterHelper.IsCharacterWeaponDrawn(character.Address);
    }
}
