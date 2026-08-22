using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using System.Linq;

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

        // patch approval only for Emote Swap
        Service.PatchApproval?.OnModeChanged();
    }

    private static void LeaveDirectPlay()
    {
        var localPlayer = NoireService.ObjectTable.LocalPlayer;

        if (localPlayer != null && CommonHelper.TryGetTrackedCharacterFromAddress(localPlayer.Address) != null)
            EmotePlayer.StopLoop(localPlayer, true);
    }

    private static void LeaveEmoteSwap()
    {
        var wasIdlePoseSwap = Service.SwapMods?.Registry.Entries
            .Any(entry => entry.SelectedByUs && entry.IsIdlePoseSwap) == true;

        Service.EndWatcher?.StopWatching();
        Service.SwapMods?.DeselectAll();

        if (wasIdlePoseSwap && NoireService.ObjectTable.LocalPlayer != null)
            Service.Penumbra?.RedrawLocalPlayer();
    }
}
