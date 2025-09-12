using BypassEmote.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    public string UniqueId = Guid.NewGuid().ToString();
    public ulong? CID;
    public uint? DataId;
    public Vector3 LastPlayerPosition;
    public float LastPlayerRotation;
    public bool IsWeaponDrawn;

    public TrackedCharacter(ulong? cid, uint? dataId, Vector3 lastPlayerPos, float lastPlayerRot, bool isWeaponDrawn)
    {
        CID = cid;
        DataId = dataId;
        LastPlayerPosition = lastPlayerPos;
        LastPlayerRotation = lastPlayerRot;
        IsWeaponDrawn = isWeaponDrawn;
    }

    public void UpdateLastPosition()
    {
        ICharacter? character;

        if (CID != null)
            character = CommonHelper.TryGetCharacterFromCID(CID.Value);
        else if (DataId != null)
            character = CommonHelper.TryGetCharacterFromDataId(DataId.Value);
        else
            return;

        if (character == null) return;
        LastPlayerPosition = character.Position;
        LastPlayerRotation = character.Rotation;
        IsWeaponDrawn = CommonHelper.IsCharacterWeaponDrawn(character.Address);
    }
}
