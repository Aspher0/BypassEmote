using System;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public static class SilkPalette
{
    public static readonly Vector4 Ice = Rgb(191, 211, 236);
    public static readonly Vector4 Acc = Ice;
    public static readonly Vector4 Vio = Rgb(158, 140, 255);
    public static readonly Vector4 Ink = Hex(0xedf2fa);
    public static readonly Vector4 Ink2 = Hex(0xa6b0c2);
    public static readonly Vector4 Ink3 = Hex(0x687288);
    public static readonly Vector4 Line = Rgb(191, 211, 236, 0.09f);
    public static readonly Vector4 Line2 = Rgb(191, 211, 236, 0.16f);
    public static readonly Vector4 Panel = Rgb(9, 13, 22, 0.66f);
    public static readonly Vector4 Ok = Hex(0x72e0b4);
    public static readonly Vector4 Bad = Hex(0xff6f86);
    public static readonly Vector4 Star = Hex(0xffd36e);
    public static readonly Vector4 Warn = Hex(0xffbe6e);

    public static readonly Vector4 Desk = Hex(0x02040a);
    public static readonly Vector4 WindowBg = Rgb(5, 8, 15);
    public static readonly Vector4 WindowRing = Rgb(191, 211, 236, 0.12f);
    public static readonly Vector4 WindowTopHighlight = Rgb(255, 255, 255, 0.06f);
    public static readonly Vector4 LogoTo = Hex(0x8ea6c8);
    public static readonly Vector4 LogoGlyph = Hex(0x0a1020);
    public static readonly Vector4 LogoGlow = Rgb(191, 211, 236, 0.8f);

    public static readonly Vector4 CbtnHoverBg = Rgb(191, 211, 236, 0.07f);

    public static readonly Vector4 WctlDivider = Rgb(255, 255, 255, 0.08f);
    public static readonly Vector4 WctlText = Hex(0x7d8594);
    public static readonly Vector4 WctlHoverBg = Rgb(255, 255, 255, 0.06f);
    public static readonly Vector4 WctlHoverText = Hex(0xeef1f6);
    public static readonly Vector4 CloseHoverBg = Rgb(255, 107, 125, 0.16f);
    public static readonly Vector4 CloseHoverText = Hex(0xffb3bd);
    public static readonly Vector4 PinMark = Hex(0x6fe3b0);

    public static readonly Vector4 ResizeMark = Rgb(255, 255, 255, 0.22f);
    public static readonly Vector4 ClickThroughOutline = Rgb(255, 255, 255, 0.18f);

    public static readonly Vector4 MenuBg = Rgb(14, 17, 23, 0.98f);
    public static readonly Vector4 MenuRing = Rgb(255, 255, 255, 0.10f);
    public static readonly Vector4 MenuShadow = Rgb(0, 0, 0, 0.95f);
    public static readonly Vector4 MenuHeading = Hex(0x6b7382);
    public static readonly Vector4 MenuLabel = Hex(0xaab2c0);
    public static readonly Vector4 MenuValue = Hex(0xeef1f6);
    public static readonly Vector4 MenuTrack = Rgb(255, 255, 255, 0.10f);
    public static readonly Vector4 MenuThumbRing = Rgb(191, 211, 236, 0.35f);
    public static readonly Vector4 MenuThumbShadow = Rgb(0, 0, 0, 0.6f);
    public static readonly Vector4 MenuToggleText = Hex(0x8a93a3);
    public static readonly Vector4 MenuToggleBg = Rgb(255, 255, 255, 0.03f);
    public static readonly Vector4 MenuToggleRing = Rgb(255, 255, 255, 0.06f);
    public static readonly Vector4 MenuToggleOnBg = Rgb(191, 211, 236, 0.12f);
    public static readonly Vector4 MenuToggleOnRing = Rgb(191, 211, 236, 0.35f);
    public static readonly Vector4 MenuDotOff = Rgb(255, 255, 255, 0.14f);

    public static readonly Vector4 TipBg = Hex(0x161a22);
    public static readonly Vector4 TipRing = Rgb(255, 255, 255, 0.12f);
    public static readonly Vector4 TipShadow = Rgb(0, 0, 0, 0.9f);
    public static readonly Vector4 TipText = Hex(0xeef1f6);

    public static readonly Vector4 Popup = Hex(0x0b1120);
    public static readonly Vector4 PopupShadow = Rgb(0, 0, 0, 0.9f);
    public static readonly Vector4 Sunken = Rgb(0, 0, 0, 0.35f);
    public static readonly Vector4 SunkenSoft = Rgb(0, 0, 0, 0.25f);
    public static readonly Vector4 HoverWash = Rgb(191, 211, 236, 0.07f);
    public static readonly Vector4 HoverWashStrong = Rgb(191, 211, 236, 0.08f);
    public static readonly Vector4 IceWash10 = Rgb(191, 211, 236, 0.10f);
    public static readonly Vector4 IceWash16 = Rgb(191, 211, 236, 0.16f);
    public static readonly Vector4 IceRing30 = Rgb(191, 211, 236, 0.30f);
    public static readonly Vector4 IceRing60 = Rgb(191, 211, 236, 0.60f);
    public static readonly Vector4 FocusHalo = Rgb(191, 211, 236, 0.08f);
    public static readonly Vector4 SectText = Rgb(191, 211, 236, 0.75f);

    public static readonly Vector4 SwitchOff = Rgb(191, 211, 236, 0.10f);
    public static readonly Vector4 SwitchKnob = Hex(0xb8c3d6);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public static readonly Vector4 PriText = Hex(0x07101e);
    public static readonly Vector4 PriHover = Hex(0xd3e2f4);
    public static readonly Vector4 PriRing = Rgb(255, 255, 255, 0.25f);
    public static readonly Vector4 PriShadow = Rgb(0, 0, 0, 0.6f);
    public static readonly Vector4 ModalOkHover = Hex(0xd6e4f5);

    public static readonly Vector4 WarnBg = Rgb(255, 190, 110, 0.06f);
    public static readonly Vector4 WarnRing = Rgb(255, 190, 110, 0.20f);
    public static readonly Vector4 WarnText = Hex(0xffd6a6);
    public static readonly Vector4 GateBg = Rgb(255, 190, 110, 0.07f);
    public static readonly Vector4 GateRing = Rgb(255, 190, 110, 0.30f);
    public static readonly Vector4 BadBg = Rgb(255, 111, 134, 0.06f);
    public static readonly Vector4 BadRing = Rgb(255, 111, 134, 0.25f);
    public static readonly Vector4 BadText = Hex(0xffc2cc);
    public static readonly Vector4 DangerBg = Rgb(255, 111, 134, 0.05f);
    public static readonly Vector4 DangerRing = Rgb(255, 111, 134, 0.22f);
    public static readonly Vector4 HoldFill = Rgb(255, 111, 134, 0.30f);
    public static readonly Vector4 OkBg = Rgb(114, 224, 180, 0.05f);
    public static readonly Vector4 OkRing = Rgb(114, 224, 180, 0.20f);
    public static readonly Vector4 DoneBg = Rgb(114, 224, 180, 0.07f);
    public static readonly Vector4 DoneRing = Rgb(114, 224, 180, 0.30f);
    public static readonly Vector4 DoneText = Hex(0xbff3dc);
    public static readonly Vector4 InfoBg = Rgb(191, 211, 236, 0.06f);
    public static readonly Vector4 InfoRing = Rgb(191, 211, 236, 0.18f);
    public static readonly Vector4 VioLight = Hex(0xc9bfff);
    public static readonly Vector4 TableHead = Hex(0x0a0f1b);

    public static readonly float[] TextSteps = [0.85f, 0.92f, 1f, 1.1f, 1.2f];

    public static Vector4 Rgb(int r, int g, int b, float a = 1f)
        => new(r / 255f, g / 255f, b / 255f, a);

    public static Vector4 Hex(uint rgb, float a = 1f)
        => new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, a);

    public static Vector4 Alpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, alpha);

    public static Vector4 Fade(Vector4 color, float factor)
        => new(color.X, color.Y, color.Z, color.W * factor);

    public static Vector4 Mix(Vector4 from, Vector4 to, float t)
        => Vector4.Lerp(from, to, Math.Clamp(t, 0f, 1f));

    public static uint U32(Vector4 color)
    {
        var r = (uint)Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255);
        var g = (uint)Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255);
        var b = (uint)Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255);
        var a = (uint)Math.Clamp((int)MathF.Round(color.W * 255f), 0, 255);
        return (a << 24) | (b << 16) | (g << 8) | r;
    }
}
