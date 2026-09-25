using Dalamud.Bindings.ImGui;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal enum BurstKind
{
    Kofi,
    Discord,
}

internal static class SilkBurst
{
    private enum Shape : byte
    {
        Heart,
        Star,
        Pad,
    }

    private struct Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float G;
        public float Drag;
        public float T;
        public float Life;
        public float Size;
        public uint Col;
        public Shape Shape;
        public float Rot;
        public float Vr;
        public float Wf;
        public float Wa;
    }

    private struct Flash
    {
        public Vector2 Pos;
        public float R;
        public Vector3 Rgb;
        public float T;
    }

    private const int MaxParticles = 256;
    private const int MaxFlashes = 8;

    private static readonly Particle[] Particles = new Particle[MaxParticles];
    private static readonly Flash[] Flashes = new Flash[MaxFlashes];
    private static int particleCount;
    private static int flashCount;
    private static double lastTime = -1;

    private static readonly uint[] KofiPalette =
    [
        SilkMainDraw.Hex(0xff5e5b), SilkMainDraw.Hex(0xff8a88), SilkMainDraw.Hex(0xffd0cf), SilkMainDraw.Hex(0xffffff),
        SilkMainDraw.Hex(0xffb86b),
    ];

    private static readonly uint[] DiscordPalette =
    [
        SilkMainDraw.Hex(0x5865f2), SilkMainDraw.Hex(0x8c95ff), SilkMainDraw.Hex(0xc9ceff), SilkMainDraw.Hex(0xffffff),
        SilkMainDraw.Hex(0x57f287),
    ];

    private static readonly uint[] PuffPalette = [SilkMainDraw.Hex(0xff8a88), SilkMainDraw.Hex(0xffd0cf), SilkMainDraw.Hex(0xff5e5b)];

    internal static bool Active => particleCount > 0 || flashCount > 0;

    private static float Rnd() => Random.Shared.NextSingle();

    internal static void Clear()
    {
        particleCount = 0;
        flashCount = 0;
    }

    internal static void Puff(Vector2 at, float scale)
    {
        for (var i = 0; i < 3; i++)
        {
            Add(new Particle
            {
                Pos = at + new Vector2((Rnd() - 0.5f) * 40f * scale, 0f),
                Vel = new Vector2((Rnd() - 0.5f) * 1.2f, -1.2f - Rnd() * 1.4f) * scale,
                G = -0.01f * scale,
                Drag = 0.985f,
                Life = 1.2f + Rnd() * 0.5f,
                Size = (5f + Rnd() * 4f) * scale,
                Col = PuffPalette[i % 3],
                Shape = Shape.Heart,
                Rot = (Rnd() - 0.5f) * 0.6f,
                Vr = (Rnd() - 0.5f) * 0.02f,
                Wf = 4f,
                Wa = 4f * scale,
            });
        }
    }

    internal static void Burst(Vector2 min, Vector2 max, BurstKind kind, float scale)
    {
        var kofi = kind == BurstKind.Kofi;
        var centre = (min + max) * 0.5f;
        var width = max.X - min.X;

        if (flashCount < MaxFlashes)
        {
            Flashes[flashCount++] = new Flash
            {
                Pos = centre,
                R = MathF.Max(width, 160f * scale),
                Rgb = kofi ? new Vector3(255, 94, 91) : new Vector3(88, 101, 242),
            };
        }

        var palette = kofi ? KofiPalette : DiscordPalette;
        var n = kofi ? 46 : 60;

        for (var i = 0; i < n; i++)
        {
            var a = kofi ? -MathF.PI / 2f + (Rnd() - 0.5f) * MathF.PI * 1.3f : Rnd() * MathF.PI * 2f;
            var v = kofi ? 3f + Rnd() * 7f : 4f + Rnd() * 8f;
            var sx = centre.X + (Rnd() - 0.5f) * width * 0.8f;

            Add(new Particle
            {
                Pos = new Vector2(sx, centre.Y),
                Vel = new Vector2(MathF.Cos(a) * v * (kofi ? 0.6f : 1f), MathF.Sin(a) * v) * scale,
                G = (kofi ? -0.02f : 0.18f) * scale,
                Drag = kofi ? 0.965f : 0.95f,
                Life = kofi ? 1.3f + Rnd() * 0.8f : 0.9f + Rnd() * 0.6f,
                Size = (kofi ? 7f + Rnd() * 9f : 3f + Rnd() * 6f) * scale,
                Col = palette[i % palette.Length],
                Shape = kofi ? (i % 4 != 0 ? Shape.Heart : Shape.Star) : (i % 3 != 0 ? Shape.Pad : Shape.Star),
                Rot = Rnd() * 6f,
                Vr = kofi ? (Rnd() - 0.5f) * 0.05f : (Rnd() - 0.5f) * 0.3f,
                Wf = 3f + Rnd() * 3f,
                Wa = (kofi ? 4f + Rnd() * 6f : 0f) * scale,
            });
        }
    }

    private static void Add(Particle particle)
    {
        if (particleCount >= MaxParticles)
            return;

        Particles[particleCount++] = particle;
    }

    internal static void Draw(ImDrawListPtr dl, double now)
    {
        var dt = lastTime < 0 ? 0.016f : (float)Math.Min(0.033, Math.Max(0, now - lastTime));
        lastTime = now;

        if (!Active)
            return;

        Span<Vector2> rim = stackalloc Vector2[40];

        for (var i = 0; i < flashCount; i++)
        {
            ref var f = ref Flashes[i];
            f.T += dt;
            var a = MathF.Max(0f, 1f - f.T / 0.35f);
            var n = SilkMainDraw.Ellipse(rim, f.Pos, f.R, f.R, 40);
            SilkMainDraw.Fan(dl, f.Pos, rim[..n], SilkMainDraw.Col(f.Rgb, 0.55f * a), SilkMainDraw.Col(f.Rgb, 0f));
        }

        var step = dt * 60f;

        for (var i = 0; i < particleCount; i++)
        {
            ref var p = ref Particles[i];
            p.T += dt;
            var drag = MathF.Pow(p.Drag, step);
            p.Vel.X *= drag;
            p.Vel.Y = p.Vel.Y * drag + p.G * step;
            p.Pos += p.Vel * step;
            p.Rot += p.Vr * step;

            var alpha = MathF.Max(0f, 1f - p.T / p.Life);
            var size = p.Size * (p.T < 0.12f ? p.T / 0.12f : 1f);
            var at = new Vector2(p.Pos.X + MathF.Sin(p.T * p.Wf) * p.Wa, p.Pos.Y);
            var col = SilkMainDraw.WithAlpha(p.Col, alpha);
            var count = p.Shape switch
            {
                Shape.Heart => HeartPoints(rim, size),
                Shape.Star => StarPoints(rim, size),
                _ => PadPoints(rim, size),
            };

            var c = MathF.Cos(p.Rot);
            var s = MathF.Sin(p.Rot);

            for (var k = 0; k < count; k++)
            {
                var q = rim[k];
                rim[k] = at + new Vector2(q.X * c - q.Y * s, q.X * s + q.Y * c);
            }

            var centreLocal = p.Shape == Shape.Heart ? new Vector2(0f, size * 0.45f) : Vector2.Zero;
            var centre = at + new Vector2(centreLocal.X * c - centreLocal.Y * s, centreLocal.X * s + centreLocal.Y * c);
            SilkMainDraw.Fan(dl, centre, rim[..count], col, col);
        }

        var write = 0;

        for (var i = 0; i < particleCount; i++)
        {
            if (Particles[i].T < Particles[i].Life)
                Particles[write++] = Particles[i];
        }

        particleCount = write;
        write = 0;

        for (var i = 0; i < flashCount; i++)
        {
            if (Flashes[i].T < 0.35f)
                Flashes[write++] = Flashes[i];
        }

        flashCount = write;
    }

    private static int HeartPoints(Span<Vector2> pts, float s)
    {
        var n = 0;
        var start = new Vector2(0f, s * 0.3f);
        n += Geometry2DHelper.SampleCubic(pts[n..], start, new Vector2(0f, -s * 0.1f), new Vector2(-s * 0.55f, -s * 0.1f), new Vector2(-s * 0.55f, s * 0.25f), 8);
        n += Geometry2DHelper.SampleCubic(pts[n..], new Vector2(-s * 0.55f, s * 0.25f), new Vector2(-s * 0.55f, s * 0.55f), new Vector2(-s * 0.15f, s * 0.7f), new Vector2(0f, s * 0.95f), 6, includeStart: false);
        n += Geometry2DHelper.SampleCubic(pts[n..], new Vector2(0f, s * 0.95f), new Vector2(s * 0.15f, s * 0.7f), new Vector2(s * 0.55f, s * 0.55f), new Vector2(s * 0.55f, s * 0.25f), 6, includeStart: false);
        n += Geometry2DHelper.SampleCubic(pts[n..], new Vector2(s * 0.55f, s * 0.25f), new Vector2(s * 0.55f, -s * 0.1f), new Vector2(0f, -s * 0.1f), start, 7, includeStart: false);
        return n;
    }

    private static int StarPoints(Span<Vector2> pts, float s)
    {
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.PI / 4f;
            var r = i % 2 != 0 ? s * 0.35f : s;
            pts[i] = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }

        return 8;
    }

    private static int PadPoints(Span<Vector2> pts, float s)
    {
        var h = s * 0.5f;
        var r = s * 0.3f;
        return SilkMainDraw.RoundRectPath(pts, new Vector2(-h), new Vector2(h), r, 3);
    }
}
