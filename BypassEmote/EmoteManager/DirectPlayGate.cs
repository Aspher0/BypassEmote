using BypassEmote.Models;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;

namespace BypassEmote;

/// <summary>
/// Whether Direct Play may play an animation on the player, given the state they are in.
/// </summary>
internal static class DirectPlayGate
{
    internal const string SafeModeMessage =
        "Due to detectability, you need to be in the base pose (pose 0) of your current stance to bypass emotes "
        + "in safe mode.";

    internal const string SafeModeRefusalKind = "directplay.safemode";

    internal static bool IsSafeState(byte poseIndex, EmoteCondition condition, int slotIndex)
        => poseIndex == 0
        || (condition is EmoteCondition.SittingInChair or EmoteCondition.SittingOnGround
            && IsAdditiveSlot(slotIndex));

    internal static bool IsAdditiveSlot(int slotIndex)
        => slotIndex is ActionTimelineSlots.UpperBody or ActionTimelineSlots.Adjust;

    internal static bool ShouldBlockSelfPlay(
        SelfBypassMode mode, bool unsafeEnabled, bool isLocalPlayer, bool isSafeState)
        => mode == SelfBypassMode.DirectPlay && !unsafeEnabled && isLocalPlayer && !isSafeState;
}
