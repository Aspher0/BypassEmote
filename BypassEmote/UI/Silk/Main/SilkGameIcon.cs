using Dalamud.Bindings.ImGui;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal static class SilkGameIcon
{
    internal static bool Draw(ImDrawListPtr dl, uint iconId, Vector2 min, Vector2 max, float rounding, float alpha = 1f)
    {
        if (alpha <= 0.001f)
            return false;

        if (iconId == 0)
        {
            Placeholder(dl, min, max, rounding, alpha);
            return true;
        }

        using var profile = SilkProfile.Detail("SilkGameIcon.Draw");

        try
        {
            if (IconHelper.Get(iconId) is not { } texture)
            {
                Placeholder(dl, min, max, rounding, alpha);
                return true;
            }

            if (!texture.TryGetWrap(out var wrap, out var failure) || wrap == null)
            {
                if (failure == null)
                    return false;

                Placeholder(dl, min, max, rounding, alpha);
                return true;
            }

            var col = SilkMainDraw.Col(255, 255, 255, alpha);

            if (rounding > 0.5f)
                dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, col, rounding);
            else
                dl.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, col);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static void Placeholder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float alpha = 1f)
    {
        var s = SilkUi.Scale;
        var color = SilkMainDraw.Col(191, 211, 236, 0.25f * alpha);
        var dash = 3f * s;
        var r = MathF.Min(rounding, (max.X - min.X) * 0.5f);
        var x = min.X + r;

        while (x < max.X - r)
        {
            dl.AddLine(new Vector2(x, min.Y + 0.5f), new Vector2(MathF.Min(x + dash, max.X - r), min.Y + 0.5f), color);
            dl.AddLine(new Vector2(x, max.Y - 0.5f), new Vector2(MathF.Min(x + dash, max.X - r), max.Y - 0.5f), color);
            x += dash * 2f;
        }

        var y = min.Y + r;

        while (y < max.Y - r)
        {
            dl.AddLine(new Vector2(min.X + 0.5f, y), new Vector2(min.X + 0.5f, MathF.Min(y + dash, max.Y - r)), color);
            dl.AddLine(new Vector2(max.X - 0.5f, y), new Vector2(max.X - 0.5f, MathF.Min(y + dash, max.Y - r)), color);
            y += dash * 2f;
        }

        var css = MathF.Max(8f, (max.Y - min.Y) / MathF.Max(0.01f, s) * 0.46f);
        var qw = SilkFonts.Measure(SilkFace.Ui700, css, "?").X;
        var qlh = SilkFonts.LineHeight(SilkFace.Ui700, css);
        SilkFonts.Draw(dl, new Vector2(MathF.Round((min.X + max.X - qw) * 0.5f), MathF.Round((min.Y + max.Y - qlh) * 0.5f)),
            SilkMainDraw.WithAlpha(SilkPalette.U32(SilkPalette.Ink3), alpha), SilkFace.Ui700, css, "?");
    }
}
