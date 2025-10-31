using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    public string UniqueId = Guid.NewGuid().ToString();
    public ulong? CID;
    public uint? BaseId;
    public Vector3 LastPlayerPosition;
    public float LastPlayerRotation;
    public bool IsWeaponDrawn;
    public uint? PlayingEmoteId = null;
    public bool ScheduledForRemoval = false;

    public TrackedCharacter(ulong? cid, uint? baseId, Vector3 lastPlayerPos, float lastPlayerRot, bool isWeaponDrawn, uint playingEmoteId)
    {
        CID = cid;
        BaseId = baseId;
        LastPlayerPosition = lastPlayerPos;
        LastPlayerRotation = lastPlayerRot;
        IsWeaponDrawn = isWeaponDrawn;
        PlayingEmoteId = playingEmoteId;
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
        else if (BaseId != null)
            character = CharacterHelper.TryGetCharacterFromBaseId(BaseId.Value);
        else
            return;

        if (character == null) return;
        LastPlayerPosition = character.Position;
        LastPlayerRotation = character.Rotation;
        IsWeaponDrawn = CharacterHelper.IsCharacterWeaponDrawn(character.Address);
    }
}
