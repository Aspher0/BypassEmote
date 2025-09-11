using BypassEmote.Helpers;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    public string UniqueId = new Guid().ToString();
    public ulong CID;
    public ushort ActiveLoopTimelineId;
    public Vector3 LastPlayerPosition;
    public float LastPlayerRotation;
    public bool IsWeaponDrawn;

    public TrackedCharacter(ulong cid, ushort activeLoopTimelineId, Vector3 lastPlayerPos, float lastPlayerRot, bool isWeaponDrawn)
    {
        CID = cid;
        ActiveLoopTimelineId = activeLoopTimelineId;
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

    }
}
