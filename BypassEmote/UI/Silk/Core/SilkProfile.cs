using NoireLib.UI;

namespace BypassEmote.UI.Silk;

internal static class SilkProfile
{
    internal static UiProfileScope Detail(string name)
        => NoireUI.Profiler.Detailed ? NoireUI.Profiler.Measure(name) : default;
}
