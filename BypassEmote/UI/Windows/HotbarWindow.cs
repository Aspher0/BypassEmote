using BypassEmote.Localization;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace BypassEmote.UI;

internal sealed class HotbarWindow : BeWindow
{
    public HotbarWindow()
        : base("BypassEmoteHotbar", L.HotbarSubtitle, L.HotbarSubtitle)
    {
        DefaultSize = new Vector2(620f, 560f);
        MinimumSize = new Vector2(460f, 360f);
    }

    public Emote? Emote { get; internal set; }

    internal int Opening { get; private set; }

    public void ShowFor(Emote emote)
    {
        Emote = emote;
        Opening++;
        Open();
    }
}
