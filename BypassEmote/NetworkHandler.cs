using Dalamud.Game.Network;
using NoireLib;
using System;

namespace BypassEmote;

public enum ClientTriggerCode : uint
{
    DrawWeapon = 1,
    ChangeTarget = 3,
    DoEmote = 500,
    CancelEmote = 502, // appears sometimes when interrupting the sleep emote
    EndEmoteLoop = 503,
    SetCPose = 505,
    StartCPose = 506,
    EndCPose = 507,
}

public enum WorldInteractionCode : uint
{
    EmoteSitObject = 501,
    EmoteStandObject = 504
}

/// <summary>
/// Thank you to senko for providing this
/// </summary>
public class NetworkHandler : IDisposable
{
    private uint worldInteractionCode = 964; // WorldInteractionHandler
    private uint clientTriggerCode = 533; // WorldInteractionHandler

    // For network verification
    private bool _netEmoting = false;
    private uint _netVerifyCPose = 0;

    public NetworkHandler()
    {
    }

    public void Dispose()
    {
    }

    /*
    List of known legitimate ways to trigger warnings here:
        - Changing zones
        - Jumping while CPosed
        - Jumping while doing a looping emote
        - Casting or using an action while CPosed
        - Cancelling the chair stand-up emote
    */

    public void VerifyNetworkMessage(nint dataPtr, ushort opCode, NetworkMessageDirection direction)
    {
        if (direction != NetworkMessageDirection.ZoneUp)
            return;

        if (dataPtr == 0)
            throw new Exception("Null dataPtr");

        var readInt = (nint offset) =>
        {
            unsafe
            {
                return *(uint*)(dataPtr + offset);
            }
        };

        var readLong = (nint offset) =>
        {
            unsafe
            {
                return *(ulong*)(dataPtr + offset);
            }
        };

        var readFloat = (nint offset) =>
        {
            unsafe
            {
                return *(float*)(dataPtr + offset);
            }
        };

        string warnMsg = "";
        var log = (string msg) =>
        {
            if (warnMsg.Length == 0)
                NoireLogger.LogInfo(msg);
            else
                NoireLogger.LogWarning($"{msg} ({warnMsg})");
        };

        // XXX These opcodes change every patch this is only valid for the current patch 7.35
        if (opCode == worldInteractionCode) // WorldInteractionHandler
        {
            var subId = (WorldInteractionCode)readInt(0x00);

            if (subId == WorldInteractionCode.EmoteSitObject)
            {
                var emoteId = readInt(0x04);
                var angleDeg = (int)float.Round((float)readInt(0x10) / 65536.0f * 360.0f);
                var x = readFloat(0x14);
                var y = readFloat(0x18);
                var z = readFloat(0x1C);
                // TODO: Can only verify this by knowing which emotes can be overlapped
                //if (_netEmoting)
                //    warnMsg = "was already emoting";
                if (emoteId == 0)
                    warnMsg = "emoteId should not be 0";
                if (emoteId != 50 && emoteId != 51 && emoteId != 88) // sit, stand, sleep
                    warnMsg = "unexpected emote id";
                log($"EmoteSitObject: emoteId={emoteId} (angle={angleDeg}deg, pos={x:F2},{y:F2},{z:F2})");
                _netEmoting = true;
            }
            else if (subId == WorldInteractionCode.EmoteStandObject)
            {
                var x = readFloat(0x14);
                var y = readFloat(0x18);
                var z = readFloat(0x1C);
                if (!_netEmoting)
                    warnMsg = "was not emoting";
                log($"EmoteStandObject (pos={x:F2},{y:F2},{z:F2})");
                _netEmoting = false;
            }
            else if ((uint)subId >= 500 && (uint)subId < 550)
            {
                warnMsg = "unknown worldinteraction emote packet";
                log($"Packet: #{(uint)subId}");
            }
        }
        else if (opCode == clientTriggerCode) // ClientTrigger
        {
            var subId = (ClientTriggerCode)readInt(0x00);

            if (subId == ClientTriggerCode.DoEmote)
            {
                var emoteId = readInt(0x04);
                var flags = readInt(0x0C); // 0x01=silent, 0x02=cancelled-by-movement
                var targetId = readLong(0x18);
                // TODO: Can only verify this by knowing which emotes cancel cpose
                //if (_netVerifyCPose != 0)
                //    warnMsg = "was still cposed";
                // TODO: Can only verify this by knowing which emotes can be overlapped
                //if (_netEmoting)
                //    warnMsg = "was already emoting";
                if (emoteId == 0)
                    warnMsg = "emoteId should not be 0";
                if (emoteId == 88 || emoteId == 95)
                    warnMsg = "emote hack";
                log($"Packet: Emote (emoteId={emoteId}, flags={flags:X}, targetId=0x{targetId:X})");
                // TODO: For non-looping emotes, detect when LocalPlayer's EmoteId == 0 again and clear this flag?
                // TODO: Facial-only emotes are not cancellable or need to be considered ever
                _netEmoting = ((flags & 0x02) == 0);
            }
            else if (subId == ClientTriggerCode.CancelEmote)
            {
                if (!_netEmoting)
                    warnMsg = "was not emoting"; // XXX: Client can trigger this (jumping while cposed for example)
                log($"Packet: CancelEmote");
                _netEmoting = false;
            }
            else if (subId == ClientTriggerCode.EndEmoteLoop)
            {
                if (!_netEmoting)
                    warnMsg = "was not emoting"; // XXX: Client can trigger this
                // TODO: Could also verify that it was a looping emote
                log($"Packet: EndEmoteLoop");
                _netEmoting = false;
            }
            else if (subId == ClientTriggerCode.StartCPose)
            {
                // TODO: Can only verify this by knowing which emotes accept cpose
                // 50 (sit), 52 (groundsit), 88 (sleep)
                //if (_netEmoting)
                //    warnMsg = "was already emoting";
                var poseId = readInt(0x08);
                if (_netVerifyCPose == poseId)
                    warnMsg = "was already cposed"; // XXX: Can happen legitimately by casting or using actions
                else if (_netVerifyCPose != 0 && poseId != _netVerifyCPose + 1 && poseId != 0)
                    warnMsg = "skipped poseId"; // XXX: Can happen legitimately by using actions which unsheath your weapon
                log($"Packet: StartCPose (poseId={poseId})");
                _netVerifyCPose = poseId;
            }
            else if (subId == ClientTriggerCode.SetCPose)
            {
                var poseId = readInt(0x08);
                if (_netVerifyCPose != poseId)
                    warnMsg = "wrong poseId";
                // Not really worth logging this unless something went wrong
                if (warnMsg.Length != 0)
                    log($"Packet: SetCPose (poseId={poseId})");
            }
            else if (subId == ClientTriggerCode.EndCPose)
            {
                if (_netVerifyCPose == 0)
                    warnMsg = "was not cposed";
                log($"Packet: StopCPose");
                _netVerifyCPose = 0;
            }
            else if ((uint)subId >= 500 && (uint)subId < 550)
            {
                warnMsg = "unknown emote packet";
                log($"Packet: #{(uint)subId}");
            }
        }
    }
}
