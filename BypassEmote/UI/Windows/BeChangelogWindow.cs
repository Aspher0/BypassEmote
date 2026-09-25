using BypassEmote.Localization;
using System.Numerics;

namespace BypassEmote.UI;

internal sealed class BeChangelogWindow : BeWindow
{
    public BeChangelogWindow()
        : base("BypassEmoteChangelog", L.ChangelogSubtitle, L.ChangelogSubtitle)
    {
        DefaultSize = new Vector2(850f, 600f);
        MinimumSize = new Vector2(850f, 600f);
        FixedSize = true;
    }
}
