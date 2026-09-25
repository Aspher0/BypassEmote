using BypassEmote.Localization;
using System.Numerics;

namespace BypassEmote.UI;

internal sealed class BeLogsWindow : BeWindow
{
    public BeLogsWindow()
        : base("BypassEmoteLogs", L.LogsSubtitle, L.LogsSubtitle)
    {
        DefaultSize = new Vector2(960f, 640f);
        MinimumSize = new Vector2(460f, 360f);
    }
}
