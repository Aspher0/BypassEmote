using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed partial class SilkMainPainter
{
    private static readonly float[] PressAt = [0f, 1f / 3f, 2f / 3f, 1f];
    private static readonly float[] PressScale = [1f, 0.94f, 1.04f, 1f];
    private static readonly float[] GradientStops = [0f, 1f];
    private static readonly uint[] KofiGradient = [SilkMainDraw.Hex(0xff6b68), SilkMainDraw.Hex(0xf0504d)];
    private static readonly uint[] DiscordGradient = [SilkMainDraw.Hex(0x6873f4), SilkMainDraw.Hex(0x4f5be6)];
    private static readonly float SkewShine = MathF.Tan(18f * MathF.PI / 180f);

    private readonly NoireRasterTexture kofiIcon = new("KofiIcon", SilkCatArt.Kofi(), new Vector2(24f), new Vector2(19f));
    private readonly NoireRasterTexture discordIcon = new("DiscordIcon", SilkCatArt.Discord(), new Vector2(24f), new Vector2(19f));

    private Tween kofiShine;
    private Tween discordShine;
    private Tween kofiGlow;
    private Tween discordGlow;
    private Tween discordLift;
    private Tween kofiLift;
    private float kofiPressAt = -10f;
    private float discordPressAt = -10f;
    private Vector2 supportMin;
    private float wrapLeft;
    private float wrapWidth;
    private Vector2 kofiBottomCentre;
    private bool kofiVisible;
    private static Action<Vector2>? puffAction;

    private void DrawSupport(float top)
    {
        var width = WidthCss;
        supportMin = P(0f, top - 4f);
        var buttonW = (width - 28f - 8f) * 0.5f;
        wrapLeft = 14f;
        wrapWidth = width * 0.5f - 18f;

        var kofiMin = P(14f, top + 8f);
        var kofiMax = kofiMin + new Vector2(buttonW, 32f) * s;
        var discordMin = P(14f + buttonW + 8f, top + 8f);
        var discordMax = discordMin + new Vector2(buttonW, 32f) * s;

        var kofiPressed = Button("kofi"u8, kofiMin, kofiMax, out var kofiHovered, out _);
        var discordPressed = Button("discord"u8, discordMin, discordMax, out var discordHovered, out _);
        var malou = Configuration.AdoptMalou;
        var catHit = malou && cat.IsHappy && cat.HitHead(ImGui.GetMousePos(), supportMin, wrapLeft + wrapWidth * 0.5f, s, now)
            && ImGui.IsWindowHovered() && !kofiHovered;

        if (catHit && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            cat.CatClicked(now, reduced);

        var heartAt = cat.HeartPoint(supportMin, wrapLeft + wrapWidth * 0.5f, s, now);
        puffAction ??= static at => SilkBurst.Puff(at, SilkUi.Scale);
        cat.Update(now, malou && kofiHovered, reduced, reduced ? null : puffAction, heartAt);

        if (kofiPressed)
        {
            if (!reduced)
            {
                SilkBurst.Burst(kofiMin, kofiMax, BurstKind.Kofi, s);
                kofiPressAt = now;
            }

            if (malou)
                cat.KofiClicked(now, reduced);

            Service.OpenKofi();
        }

        if (discordPressed)
        {
            if (!reduced)
                discordPressAt = now;

            Service.OpenDiscord();
        }

        cat.DrawHead(dl, supportMin, wrapLeft, wrapWidth, s, now, now, reduced);

        var kofiLiftY = Ease(ref kofiLift, kofiHovered ? -1f : 0f, 0.45f, SilkUi.EaseBack);
        var kofiTy = (malou ? cat.KofiOffsetY(now) : kofiLiftY) * s;
        var kofiSy = cat.KofiScaleY(now);
        var press = PressScale01(kofiPressAt);
        var bottomCentre = new Vector2((kofiMin.X + kofiMax.X) * 0.5f, kofiMax.Y);

        var start = dl.VtxBuffer.Size;
        DrawSupportButton(dl, kofiMin, kofiMax, true, kofiHovered, ref kofiShine, ref kofiGlow);

        if (press != null)
            SilkMainDraw.Transform(dl, start, bottomCentre, new Vector2(press.Value), 0f, Vector2.Zero);
        else
            SilkMainDraw.Transform(dl, start, bottomCentre, new Vector2(1f, kofiSy), 0f, new Vector2(0f, kofiTy));

        kofiBottomCentre = press != null ? bottomCentre : bottomCentre + new Vector2(0f, kofiTy);
        kofiVisible = true;

        var discordLiftY = Ease(ref discordLift, discordHovered ? -1f : 0f, 0.2f, SilkUi.EaseOut) * s;
        var discordPress = PressScale01(discordPressAt);
        start = dl.VtxBuffer.Size;
        DrawSupportButton(dl, discordMin, discordMax, false, discordHovered, ref discordShine, ref discordGlow);

        if (discordPress != null)
            SilkMainDraw.Transform(dl, start, (discordMin + discordMax) * 0.5f, new Vector2(discordPress.Value), 0f, Vector2.Zero);
        else
            SilkMainDraw.Transform(dl, start, Vector2.Zero, Vector2.One, 0f, new Vector2(0f, discordLiftY));

        cat.DrawPaws(dl, supportMin, wrapLeft, wrapWidth, s, now);
    }

    private float? PressScale01(float at)
    {
        if (reduced)
            return null;

        var t = (now - at) / 0.42f;

        if (t is < 0f or >= 1f)
            return null;

        return Piecewise(SilkUi.EaseBack.Evaluate(t), PressAt, PressScale);
    }

    private void DrawSupportButton(ImDrawListPtr dl, Vector2 min, Vector2 max, bool kofi, bool hovered, ref Tween shine, ref Tween glow)
    {
        var r = 11f * s;
        var hot = Ease(ref glow, hovered ? 1f : 0f, 0.3f, UiCubicBezier.Ease);

        var dropColor = kofi
            ? SilkMainDraw.LerpCol(SilkMainDraw.Col(0, 0, 0, 0.6f), SilkMainDraw.Col(255, 94, 91, 0.55f), hot)
            : SilkMainDraw.LerpCol(SilkMainDraw.Col(0, 0, 0, 0.6f), SilkMainDraw.Col(88, 101, 242, 0.55f), hot);
        var offset = (4f + 2f * hot) * s;
        var blur = (10f + 6f * hot) * s;
        var spread = (-6f - 2f * hot) * s;
        SilkMainDraw.Shadow(dl, min, max, r, dropColor, blur, spread, new Vector2(0f, offset));

        var start = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, SilkMainDraw.Col(255, 255, 255), r);
        SilkMainDraw.ShadeVertical(dl, start, min.Y, max.Y, GradientStops, kofi ? KofiGradient : DiscordGradient);

        var shineT = Ease(ref shine, hovered ? 1f : 0f, 0.7f, SilkUi.EaseOut);

        if (shineT > 0.001f && shineT < 0.999f)
        {
            var w = max.X - min.X;
            var bandW = w * 0.4f;
            var left = min.X + (-0.6f + 1.9f * shineT) * w;
            SilkMainDraw.RoundedShine(dl, min, max, r, left, bandW, SkewShine, 0.35f, 3f * s);
        }

        SilkMainDraw.InsetRing(dl, min, max, r, SilkMainDraw.Col(255, 255, 255, 0.16f + 0.08f * hot));

        var label = kofi ? L.SupportOnKofi.Text : L.Discord.Text;
        var showIcon = WidthCss > 450f;
        var iconW = showIcon ? (19f + 10f) * s : 0f;
        var room = max.X - min.X - (24f * s) - iconW;
        var textW = SilkFonts.FittedWidth(SilkFace.Ui700, 13f, label, room);
        var x = (min.X + max.X - textW - iconW) * 0.5f;
        var midY = (min.Y + max.Y) * 0.5f;

        if (showIcon && (kofi ? kofiIcon : discordIcon).Get(s) is { } wrap)
        {
            var iconMin = new Vector2(MathF.Round(x), MathF.Round(midY - 9.5f * s));
            dl.AddImage(wrap.Handle, iconMin, iconMin + new Vector2(19f * s), Vector2.Zero, Vector2.One, uint.MaxValue);
        }

        x += iconW;
        var lh = SilkFonts.LineHeight(SilkFace.Ui700, 13f);
        SilkFonts.DrawFitted(dl, new Vector2(MathF.Round(x), MathF.Round(midY - lh * 0.5f)), uint.MaxValue, SilkFace.Ui700, 13f, label, room);
    }

    private void DrawCatLower()
    {
        if (!kofiVisible || dl.IsNull)
            return;

        dl.PushClipRectFullScreen();
        cat.DrawLower(dl, kofiBottomCentre, s, now, reduced, Collapsed);
        dl.PopClipRect();
        kofiVisible = false;
    }
}
