using Dalamud.Game.ClientState.Objects.SubKinds;
using System;
using System.Numerics;

namespace BypassEmote;

public class TrackedCharacter
{
    public string UniqueId = new Guid().ToString();
    public IPlayerCharacter Character;
    public ushort ActiveLoopTimelineId;
    public Vector3 LastPlayerPosition;
    public float LastPlayerRotation;

    public TrackedCharacter(IPlayerCharacter character, ushort activeLoopTimelineId, Vector3 lastPlayerPos, float lastPlayerRot)
    {
        Character = character;
        ActiveLoopTimelineId = activeLoopTimelineId;
        LastPlayerPosition = lastPlayerPos;
        LastPlayerRotation = lastPlayerRot;
    }

    public void UpdateLastPosition()
    {
        LastPlayerPosition = Character.Position;
        LastPlayerRotation = Character.Rotation;
    }
}
