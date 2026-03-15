using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    // TODO : Change this class to handle:
    // 1) EmotePlayer Characters + Owned Objects (Companions, Pets, Buddies, etc.) where owned objects and the player are contained in a single data structure just like IpcData
    // 2) NPCs (Mannequins, etc.) where each NPC is its own TrackedCharacter

    public string UniqueId = Guid.NewGuid().ToString();
    public bool IsLocalObject;
    public ulong? CID;
    public uint? BaseId; // For NPCs, companions, pets and buddies
    public ushort? ObjectIndex; // For NPCs, specifically mannequins since you can have multiple mannequins of the same retainer
    public Vector3 LastPosition;
    public float LastRotation;
    public bool IsWeaponDrawn;
    public uint? PlayingEmoteId = null;
    public CharacterState? ReceivedIpcData = null; // Received from IPC *only* (AKA: Another player)
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
        CharacterState? ipcData = null)
    {
        IsLocalObject = isLocalObject;
        CID = cid;
        BaseId = baseId;
        ObjectIndex = objectIndex;
        LastPosition = lastPlayerPos;
        LastRotation = lastPlayerRot;
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
            character = CharacterHelper.GetCharacterFromCID(CID.Value);
        else if (BaseId != null && ObjectIndex != null)
            character = Helpers.CommonHelper.TryGetCharacterFromBaseIdAndObjectIndex(BaseId.Value, ObjectIndex.Value);
        else
            return;

        if (character == null) return;
        LastPosition = character.Position;
        LastRotation = character.Rotation;
        IsWeaponDrawn = CharacterHelper.IsCharacterWeaponDrawn(character.Address);
    }
}
