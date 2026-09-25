using BypassEmote.Localization;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace BypassEmote.UI;

internal sealed class CreateModWindow : BeWindow
{
    public CreateModWindow()
        : base("BypassEmoteCreateMod", L.CreateModTitle, L.CreateModSubtitle)
    {
        DefaultSize = new Vector2(600f, 720f);
        MinimumSize = new Vector2(460f, 360f);
    }

    internal int Opening { get; private set; }

    internal Emote? OpenedFor { get; private set; }

    public void Show()
    {
        OpenedFor = null;
        Opening++;
        Open();
    }

    public void ShowFor(Emote emote)
    {
        OpenedFor = emote;
        Opening++;
        Open();
    }
}
