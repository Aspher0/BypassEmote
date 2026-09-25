using BypassEmote.UI.Classic;
using BypassEmote.UI.Silk;
using NoireLib.UI;

namespace BypassEmote.UI.Skins;

internal static class BeSkins
{
    public static readonly SilkSkin Silk = new();

    public static readonly ClassicSkin Classic = new();

    public static bool SilkActive => ReferenceEquals(NoireSkins.Active, Silk);

    public static bool ClassicActive => ReferenceEquals(NoireSkins.Active, Classic);

    public static void Register() => NoireSkins.Register(Silk, Classic);
}
