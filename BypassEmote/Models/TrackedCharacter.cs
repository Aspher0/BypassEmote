using BypassEmote.Helpers;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    public string UniqueId = new Guid().ToString();
    public ulong CID;
    public Vector3 LastPlayerPosition;
    public float LastPlayerRotation;
    public bool IsWeaponDrawn;

    public TrackedCharacter(ulong cid, Vector3 lastPlayerPos, float lastPlayerRot, bool isWeaponDrawn)
    {
        CID = cid;
        LastPlayerPosition = lastPlayerPos;
        LastPlayerRotation = lastPlayerRot;
        IsWeaponDrawn = isWeaponDrawn;
    }

    public void UpdateLastPosition()
    {
        var character = CommonHelper.TryGetPlayerCharacterFromCID(this.CID);
        if (character == null) return;
        LastPlayerPosition = character.Position;
        LastPlayerRotation = character.Rotation;
        IsWeaponDrawn = CommonHelper.IsCharacterWeaponDrawn(character.Address);
    }
}
