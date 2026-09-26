using NoireLib.UI;

namespace BypassEmote.UI.Silk;

internal sealed class SilkFontSkin : IFontSkin
{
    internal static readonly SilkFontSkin Instance = new();

    public bool IsReady => SilkFonts.Ready;

    public void Load()
    {
        SilkFonts.Parked = false;
        SilkUi.WarmFonts();
    }

    public void Unload() => SilkFonts.Parked = true;

    public void Push(TextRole role)
    {
    }

    public void Pop()
    {
    }
}
