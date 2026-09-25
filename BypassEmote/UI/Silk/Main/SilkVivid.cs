using NoireLib.Helpers;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal static class SilkVivid
{
    internal static readonly Vector3 Ice = new(191f, 211f, 236f);

    internal static Vector3 Get(uint iconId)
        => IconHelper.GetVividColor(iconId) is { } color ? new Vector3(color.X, color.Y, color.Z) * 255f : Ice;

    internal static Vector4 Rgba(Vector3 color, float alpha = 1f)
        => new(color.X / 255f, color.Y / 255f, color.Z / 255f, alpha);
}
