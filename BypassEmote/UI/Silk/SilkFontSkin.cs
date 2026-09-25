using NoireLib.UI;

namespace BypassEmote.UI.Silk;

internal sealed class SilkFontSkin : IFontSkin
{
    internal static readonly SilkFontSkin Instance = new();

    public void Load() => SilkUi.WarmFonts();

    public void Unload() => SilkFonts.Dispose();

    public void Push(TextRole role)
    {
    }

    public void Pop()
    {
    }
}
