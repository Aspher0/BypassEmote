using BypassEmote.Enums;
using BypassEmote.Localization;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Localizer;

namespace BypassEmote;

/// <summary>
/// Whether Direct Play may play an animation or not based on if unsafe mod is enabled
/// </summary>
internal static class DirectPlayGate
{
    internal static NoireString SafeModeMessage => L.SafeModeRefusal;

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
