using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;

namespace BypassEmote.EmoteSwap;

public static class ModeSwitcher
{
    public static void Apply(SelfBypassMode newMode)
    {
        if (NoireService.Framework.IsInFrameworkUpdateThread)
        {
            ApplyCore(newMode);
            return;
        }

        _ = NoireService.Framework.RunOnFrameworkThread(() => ApplyCore(newMode));
    }

    private static void ApplyCore(SelfBypassMode newMode)
    {
        if (Configuration.SelfBypassMode == newMode)
            return;

        if (newMode == SelfBypassMode.EmoteSwap)
            LeaveDirectPlay();
        else
            LeaveEmoteSwap();

        Configuration.SelfBypassMode = newMode;
    }

    // Local player only: the mode governs self bypass, so companion, pet and chocobo loops are left alone.
    private static void LeaveDirectPlay()
    {
        var localPlayer = NoireService.ObjectTable.LocalPlayer;

        if (localPlayer != null && CommonHelper.TryGetTrackedCharacterFromAddress(localPlayer.Address) != null)
            EmotePlayer.StopLoop(localPlayer, true);
    }

    private static void LeaveEmoteSwap()
    {
        var wasIdlePoseSwap = Service.SwapMods?.Current?.IsIdlePoseSwap == true;

        Service.EndWatcher?.StopWatching();
        Service.SwapMods?.Deactivate();

        if (wasIdlePoseSwap && NoireService.ObjectTable.LocalPlayer != null)
            Service.Penumbra?.RedrawLocalPlayer();
    }
}
