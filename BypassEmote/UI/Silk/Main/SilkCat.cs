using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed class SilkCat : IDisposable
{
    private enum Mood
    {
        Hidden,
        Plead,
        Happy,
    }

    private const float HiddenY = 70f;
    private const float UpY = 8f;

    private readonly NoireRasterTexture earLeft = Layer("CatEarL", SilkCatArt.EarLeft());
    private readonly NoireRasterTexture earRight = Layer("CatEarR", SilkCatArt.EarRight());
    private readonly NoireRasterTexture body = Layer("CatBody", SilkCatArt.Body());
    private readonly NoireRasterTexture brows = Layer("CatBrows", SilkCatArt.PleadBrows());
    private readonly NoireRasterTexture eyes = Layer("CatEyes", SilkCatArt.PleadEyes());
    private readonly NoireRasterTexture mouth = Layer("CatMouth", SilkCatArt.PleadMouth());
    private readonly NoireRasterTexture happy = Layer("CatHappy", SilkCatArt.Happy());
    private readonly NoireRasterTexture nose = Layer("CatNose", SilkCatArt.Nose());
    private readonly NoireRasterTexture whiskerLeft = Layer("CatWhiskL", SilkCatArt.WhiskerLeft());
    private readonly NoireRasterTexture whiskerRight = Layer("CatWhiskR", SilkCatArt.WhiskerRight());
    private readonly NoireRasterTexture paws = new("CatPaws", SilkCatArt.Paws(), SilkCatArt.PawsView, SilkCatArt.PawsView);

    private Mood mood = Mood.Hidden;
    private bool hovering;
    private double showAt = -1;
    private double weightAt = -1;
    private double calmAt = -1;
    private double leaveAt = -1;
    private double dropAt = -1;
    private double nextHeart = -1;

    private Tween catY;
    private Tween face;
    private Tween pawsAlpha;
    private Tween pawsY;
    private Tween kofiY;
    private Tween kofiScaleY;
    private bool weighted;
    private bool up;
    private bool pawsOn;
    private bool faceHappy;

    private float lowerT;
    private float mix;
    private float tailPhase;
    private float kick;
    private float lastProgress;
    private double lastFrame = -1;

    internal SilkCat()
    {
        catY.Snap(HiddenY);
        face.Snap(0f);
        pawsAlpha.Snap(0f);
        pawsY.Snap(0f);
        kofiY.Snap(0f);
        kofiScaleY.Snap(1f);
    }

    private static NoireRasterTexture Layer(string name, RasterShape[] shapes) => new(name, shapes, SilkCatArt.View, SilkCatArt.Box);

    internal bool IsHappy => mood == Mood.Happy;

    internal bool Visible(float now) => catY.Value(now) < HiddenY - 0.01f;

    internal float KofiOffsetY(float now) => kofiY.Value(now);

    internal float KofiScaleY(float now) => kofiScaleY.Value(now);

    internal void Update(float now, bool kofiHovered, bool reduced, Action<Vector2>? puff, Vector2 heartAt)
    {
        if (kofiHovered != hovering)
        {
            hovering = kofiHovered;

            if (hovering)
            {
                if (mood == Mood.Hidden)
                    showAt = now + 1.0;
            }
            else
            {
                showAt = -1;

                if (mood == Mood.Plead)
                    Leave(now, reduced);
            }
        }

        if (showAt >= 0 && now >= showAt)
        {
            showAt = -1;
            Show(now, reduced);
        }

        if (weightAt >= 0 && now >= weightAt)
        {
            weightAt = -1;
            SetWeighted(true, now, reduced);
        }

        if (calmAt >= 0 && now >= calmAt)
        {
            calmAt = -1;
            Calm(now, reduced);
        }

        if (leaveAt >= 0 && now >= leaveAt)
        {
            leaveAt = -1;
            Leave(now, reduced);
        }

        if (dropAt >= 0 && now >= dropAt)
        {
            dropAt = -1;
            SetUp(false, now, reduced);
            SetFace(false, now, reduced);
        }

        if (mood == Mood.Happy && !reduced && nextHeart >= 0 && now >= nextHeart)
        {
            nextHeart += 0.45;

            if (nextHeart < now)
                nextHeart = now + 0.45;

            puff?.Invoke(heartAt);
        }

        if (!weighted)
            kofiY.Go(hovering ? -1f : 0f, now, reduced ? 0f : 0.45f, SilkUi.EaseBack);
    }

    internal void KofiClicked(float now, bool reduced)
    {
        if (mood is not (Mood.Plead or Mood.Happy))
            return;

        ClearTimers();
        mood = Mood.Happy;
        SetUp(true, now, reduced);
        SetPaws(true, now, reduced);
        SetWeighted(true, now, reduced);
        SetFace(true, now, reduced);
        nextHeart = reduced ? -1 : now + 0.45;
        calmAt = now + 5.0;
    }

    internal void CatClicked(float now, bool reduced)
    {
        if (mood == Mood.Happy)
            Calm(now, reduced);
    }

    private void ClearTimers()
    {
        weightAt = -1;
        calmAt = -1;
        leaveAt = -1;
        dropAt = -1;
        nextHeart = -1;
    }

    private void Show(float now, bool reduced)
    {
        mood = Mood.Plead;
        SetUp(true, now, reduced);
        SetPaws(true, now, reduced);
        weightAt = now + (reduced ? 0 : 0.43);
    }

    private void Leave(float now, bool reduced)
    {
        ClearTimers();
        mood = Mood.Hidden;
        SetPaws(false, now, reduced);
        SetWeighted(false, now, reduced);
        dropAt = now + (reduced ? 0 : 0.18);
    }

    private void Calm(float now, bool reduced)
    {
        if (mood != Mood.Happy)
            return;

        ClearTimers();
        mood = Mood.Plead;
        SetFace(false, now, reduced);

        if (!hovering)
            leaveAt = now + (reduced ? 0 : 0.9);
    }

    private void SetUp(bool value, float now, bool reduced)
    {
        up = value;
        catY.Go(value ? UpY : HiddenY, now, reduced ? 0f : 0.7f, SilkUi.EaseCat);
    }

    private void SetFace(bool value, float now, bool reduced)
    {
        faceHappy = value;
        face.Go(value ? 1f : 0f, now, reduced ? 0f : 0.5f, UiCubicBezier.Ease);
    }

    private void SetPaws(bool value, float now, bool reduced)
    {
        pawsOn = value;

        if (value)
            pawsAlpha.Go(1f, now, reduced ? 0f : 0.2f, UiCubicBezier.Ease, reduced ? 0f : 0.45f);
        else
            pawsAlpha.Go(0f, now, reduced ? 0f : 0.14f, UiCubicBezier.Ease);

        pawsY.Go(value && weighted ? 6f : 0f, now, reduced ? 0f : 0.45f, SilkUi.EaseBack);
    }

    private void SetWeighted(bool value, float now, bool reduced)
    {
        weighted = value;
        var seconds = reduced ? 0f : 0.45f;
        kofiY.Go(value ? 4f : hovering ? -1f : 0f, now, seconds, SilkUi.EaseBack);
        kofiScaleY.Go(value ? 0.955f : 1f, now, seconds, SilkUi.EaseBack);
        pawsY.Go(value && pawsOn ? 6f : 0f, now, seconds, SilkUi.EaseBack);
    }

    private static float Keyframes(float t, ReadOnlySpan<float> offsets, ReadOnlySpan<float> values)
    {
        for (var i = 1; i < offsets.Length; i++)
        {
            if (t <= offsets[i])
            {
                var span = offsets[i] - offsets[i - 1];
                var p = span > 0f ? (t - offsets[i - 1]) / span : 1f;
                return values[i - 1] + (values[i] - values[i - 1]) * UiCubicBezier.Ease.Evaluate(p);
            }
        }

        return values[^1];
    }

    private static float Cycle(float time, float period, float delay)
    {
        var t = time - delay;

        if (t < 0f)
            return 0f;

        return t % period / period;
    }

    private static readonly float[] TwitchAt = [0f, 0.86f, 0.9f, 0.94f, 1f];
    private static readonly float[] TwitchDeg = [0f, 0f, -12f, 5f, 0f];
    private static readonly float[] BlinkAt = [0f, 0.93f, 0.95f, 0.97f, 1f];
    private static readonly float[] BlinkScale = [1f, 1f, 0.12f, 1f, 1f];
    private static readonly float[] WhiskAt = [0f, 0.82f, 0.85f, 0.88f, 0.91f, 0.94f, 1f];
    private static readonly float[] WhiskDeg = [0f, 0f, 6f, -5f, 4f, -2f, 0f];

    internal Vector2 HeartPoint(Vector2 supportMin, float cx, float scale, float now)
    {
        var top = 22f + catY.Value(now) - 70f;
        return supportMin + new Vector2(cx, top + 10f) * scale;
    }

    internal bool HitHead(Vector2 mouse, Vector2 supportMin, float cx, float scale, float now)
    {
        var ty = catY.Value(now);
        var min = supportMin + new Vector2(cx - 39f, 22f + ty - 70f) * scale;
        var max = supportMin + new Vector2(cx + 39f, 22f) * scale;
        return mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y && ty < HiddenY - 1f;
    }

    internal void DrawHead(ImDrawListPtr dl, Vector2 supportMin, float wrapLeft, float wrapWidth, float scale, float now, float clock, bool reduced)
    {
        var ty = catY.Value(now);

        if (ty >= HiddenY - 0.01f)
            return;

        var cx = wrapLeft + wrapWidth * 0.5f;
        var clipMin = supportMin + new Vector2(wrapLeft, 22f - 110f) * scale;
        var clipMax = supportMin + new Vector2(wrapLeft + wrapWidth, 22f) * scale;
        var origin = supportMin + new Vector2(cx - 39f, 22f + ty - 70f) * scale;
        var size = SilkCatArt.Box * scale;
        Vector2 Map(Vector2 v) => origin + body.ToLocal(v) * scale;

        var time = reduced ? 0f : clock;
        var earL = reduced ? 0f : Keyframes(Cycle(time, 3.2f, 0f), TwitchAt, TwitchDeg);
        var earR = reduced ? 0f : Keyframes(Cycle(time, 3.2f, 1.3f), TwitchAt, TwitchDeg);
        var blink = reduced ? 1f : Keyframes(Cycle(time, 4.6f, 0f), BlinkAt, BlinkScale);
        var whL = reduced ? 0f : Keyframes(Cycle(time, 3.8f, 0f), WhiskAt, WhiskDeg);
        var whR = reduced ? 0f : Keyframes(Cycle(time, 3.8f, 0.07f), WhiskAt, WhiskDeg);
        var h = face.Value(now);

        dl.PushClipRect(clipMin, clipMax, true);

        Quad(dl, earLeft.Get(scale), origin, size, Map(SilkCatArt.EarLeftPivot), Vector2.One, earL, 1f);
        Quad(dl, earRight.Get(scale), origin, size, Map(SilkCatArt.EarRightPivot), Vector2.One, earR, 1f);
        Quad(dl, body.Get(scale), origin, size, origin, Vector2.One, 0f, 1f);

        if (h < 0.999f)
        {
            Quad(dl, brows.Get(scale), origin, size, origin, Vector2.One, 0f, 1f - h);
            Quad(dl, eyes.Get(scale), origin, size, Map(SilkCatArt.EyesPivot), new Vector2(1f, blink), 0f, 1f - h);
            Quad(dl, mouth.Get(scale), origin, size, origin, Vector2.One, 0f, 1f - h);
        }

        if (h > 0.001f)
            Quad(dl, happy.Get(scale), origin, size, origin, Vector2.One, 0f, h);

        Quad(dl, nose.Get(scale), origin, size, origin, Vector2.One, 0f, 1f);
        Quad(dl, whiskerLeft.Get(scale), origin, size, Map(SilkCatArt.WhiskerLeftPivot), Vector2.One, whL, 1f);
        Quad(dl, whiskerRight.Get(scale), origin, size, Map(SilkCatArt.WhiskerRightPivot), Vector2.One, whR, 1f);

        dl.PopClipRect();
    }

    private static void Quad(ImDrawListPtr dl, IDalamudTextureWrap? wrap, Vector2 min, Vector2 size, Vector2 pivot, Vector2 scale,
        float degrees, float alpha)
    {
        if (wrap == null || alpha <= 0.001f)
            return;

        var col = SilkMainDraw.Col(255, 255, 255, alpha);

        if (degrees == 0f && scale == Vector2.One)
        {
            dl.AddImage(wrap.Handle, min, min + size, Vector2.Zero, Vector2.One, col);
            return;
        }

        var r = degrees * MathF.PI / 180f;
        var c = MathF.Cos(r);
        var s = MathF.Sin(r);

        Vector2 T(Vector2 p)
        {
            var d = (p - pivot) * scale;
            return pivot + new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        }

        var max = min + size;
        dl.AddImageQuad(wrap.Handle, T(min), T(new Vector2(max.X, min.Y)), T(max), T(new Vector2(min.X, max.Y)),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), col);
    }

    internal void DrawPaws(ImDrawListPtr dl, Vector2 supportMin, float wrapLeft, float wrapWidth, float scale, float now)
    {
        var alpha = pawsAlpha.Value(now);

        if (alpha <= 0.001f || paws.Get(scale) is not { } wrap)
            return;

        var cx = wrapLeft + wrapWidth * 0.5f;
        var min = supportMin + new Vector2(cx - 30f, 5f + pawsY.Value(now)) * scale;
        dl.AddImage(wrap.Handle, min, min + SilkCatArt.PawsView * scale, Vector2.Zero, Vector2.One, SilkMainDraw.Col(255, 255, 255, alpha));
    }

    internal void DrawLower(ImDrawListPtr fg, Vector2 kofiBottomCentre, float scale, float now, bool reduced, bool hidden)
    {
        var frame = (double)now;
        var dt = lastFrame < 0 ? 0f : (float)Math.Min(0.033, Math.Max(0, frame - lastFrame));
        lastFrame = frame;

        var ty = catY.Value(now);
        var progress = MathF.Max(0f, (HiddenY - ty) / (HiddenY - UpY));

        if (!reduced)
        {
            lowerT += dt;
            mix += ((mood == Mood.Happy ? 1f : 0f) - mix) * MathF.Min(1f, dt * 2.2f);
            tailPhase += dt * (1.45f + 1.65f * mix);
            var dp = (progress - lastProgress) / (dt > 0f ? dt : 1f);
            kick = MathF.Max(kick * MathF.Pow(0.25f, dt), MathF.Min(0.9f, MathF.Abs(dp) * 0.08f));
        }

        lastProgress = progress;

        if (hidden || progress < 0.004f)
            return;

        SilkCatLower.Draw(fg, kofiBottomCentre, scale, progress, lowerT, mix, tailPhase, kick);
    }

    public void Dispose()
    {
        earLeft.Dispose();
        earRight.Dispose();
        body.Dispose();
        brows.Dispose();
        eyes.Dispose();
        mouth.Dispose();
        happy.Dispose();
        nose.Dispose();
        whiskerLeft.Dispose();
        whiskerRight.Dispose();
        paws.Dispose();
    }
}
