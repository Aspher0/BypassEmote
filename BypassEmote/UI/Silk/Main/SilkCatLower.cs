using Dalamud.Bindings.ImGui;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal static class SilkCatLower
{
    private const float W = 140f;
    private const float H = 150f;
    private const int TailSegments = 20;
    private const float TailLength = 70f;

    private static readonly float[] BodyStops = [0f, 0.42f, 0.72f, 0.83f, 1f];
    private static readonly uint[] BodyColors =
    [
        SilkMainDraw.Hex(0xe38b43), SilkMainDraw.Hex(0xf0a055), SilkMainDraw.Hex(0xf2a55c), SilkMainDraw.Hex(0xf8dcbc),
        SilkMainDraw.Hex(0xfff2e2),
    ];

    private static readonly float[] TailStops = [0f, 0.25f, 0.8f, 1f];
    private static readonly uint[] TailColors =
    [
        SilkMainDraw.Hex(0xe38b43), SilkMainDraw.Hex(0xf0a055), SilkMainDraw.Hex(0xec9a50), SilkMainDraw.Hex(0xb86a2c),
    ];

    private static readonly float[] BellyStops = [0f, 0.75f, 1f];
    private static readonly uint[] BellyColors =
    [
        SilkMainDraw.Col(255, 243, 228, 0.95f), SilkMainDraw.Col(255, 238, 216, 0.6f), SilkMainDraw.Col(255, 238, 216, 0f),
    ];

    private static Vector2 origin;
    private static float s;

    private static Vector2 P(float x, float y) => origin + new Vector2(x, y) * s;

    private static Vector2 P(Vector2 v) => origin + v * s;

    internal static void Draw(ImDrawListPtr dl, Vector2 kofiBottomCentre, float scale, float progress, float t, float mix,
        float tailPhase, float kick)
    {
        s = scale;
        origin = kofiBottomCentre - new Vector2(W * 0.5f * scale, 0f);
        var off = -(1f - MathF.Min(1f, progress)) * 96f;
        var cx = W / 2f;

        dl.PushClipRect(origin, origin + new Vector2(W, H) * scale, true);

        Tail(dl, off, mix, tailPhase, kick);

        var start = dl.VtxBuffer.Size;
        Body(dl, off, cx);
        var legLeft = Leg(dl, -1, off, t, mix, kick);
        var legRight = Leg(dl, 1, off, t, mix, kick);
        SilkMainDraw.Shade(dl, start, P(0f, off - 24f), P(0f, off + 38f), BodyStops, BodyColors);

        Belly(dl, off, cx);

        var soft = SilkMainDraw.Col(184, 96, 38, 0.3f);
        Quadratic(dl, P(cx - 23f, off - 2f), P(cx - 19f, off + 10f), P(cx - 10f, off + 15f), soft, 1.5f);
        Quadratic(dl, P(cx + 23f, off - 2f), P(cx + 19f, off + 10f), P(cx + 10f, off + 15f), soft, 1.5f);

        var line = SilkMainDraw.Col(184, 96, 38, 0.4f);
        Line(dl, P(cx - 22f, off - 14f), P(cx - 17f, off - 12f), line, 2f);
        Line(dl, P(cx + 22f, off - 14f), P(cx + 17f, off - 12f), line, 2f);

        Toes(dl, legLeft.Bottom, legLeft.Angle);
        Toes(dl, legRight.Bottom, legRight.Angle);

        dl.PopClipRect();
    }

    private static void Line(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float width)
    {
        var w = width * s;
        dl.AddLine(a, b, col, w);
        dl.AddCircleFilled(a, w * 0.5f, col, 8);
        dl.AddCircleFilled(b, w * 0.5f, col, 8);
    }

    private static void Quadratic(ImDrawListPtr dl, Vector2 a, Vector2 c, Vector2 b, uint col, float width)
    {
        Span<Vector2> pts = stackalloc Vector2[13];
        Geometry2DHelper.SampleQuadratic(pts, a, c, b, pts.Length - 1);

        var w = width * s;
        dl.AddPolyline(pts, col, w);

        dl.AddCircleFilled(a, w * 0.5f, col, 8);
        dl.AddCircleFilled(b, w * 0.5f, col, 8);
    }

    private static Vector2 Rot(Vector2 p, Vector2 c, float a)
    {
        var sn = MathF.Sin(a);
        var co = MathF.Cos(a);
        var d = p - c;
        return new Vector2(c.X + d.X * co - d.Y * sn, c.Y + d.X * sn + d.Y * co);
    }

    private static void Tube(ReadOnlySpan<Vector2> path, Span<Vector2> left, Span<Vector2> right, bool tail)
    {
        var count = path.Length;

        for (var i = 0; i < count; i++)
        {
            var u = i / (float)(count - 1);
            var q = path[Math.Min(count - 1, i + 1)];
            var o = path[Math.Max(0, i - 1)];
            var d = q - o;
            var len = d.Length();

            if (len <= 0f)
                len = 1f;

            d /= len;

            var w = tail
                ? 4.6f * (1f - 0.22f * u) + 0.9f * MathF.Sin(MathF.PI * MathF.Min(1f, u * 1.1f))
                : u < 0.68f ? 7.8f - 2.9f * u / 0.68f : 4.9f + 1.6f * MathF.Sin((u - 0.68f) / 0.32f * MathF.PI / 2f);

            left[i] = new Vector2(path[i].X - d.Y * w, path[i].Y + d.X * w);
            right[i] = new Vector2(path[i].X + d.Y * w, path[i].Y - d.X * w);
        }
    }

    private static int Outline(ReadOnlySpan<Vector2> left, ReadOnlySpan<Vector2> right, Vector2 tip, Span<Vector2> output,
        out int arcStart, out int arcCount)
    {
        var n = 0;

        foreach (var p in left)
            output[n++] = P(p);

        var tl = left[^1];
        var tr = right[^1];
        var r = Vector2.Distance(tl, tip);
        var a0 = MathF.Atan2(tl.Y - tip.Y, tl.X - tip.X);
        var a1 = MathF.Atan2(tr.Y - tip.Y, tr.X - tip.X);
        var sweep = a1 - a0;

        while (sweep > 0f)
            sweep -= MathF.PI * 2f;

        while (sweep <= -MathF.PI * 2f)
            sweep += MathF.PI * 2f;

        arcStart = n;
        arcCount = 9;

        for (var i = 1; i <= arcCount; i++)
        {
            var a = a0 + sweep * i / (arcCount + 1);
            output[n++] = P(tip + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r);
        }

        for (var i = right.Length - 1; i >= 0; i--)
            output[n++] = P(right[i]);

        return n;
    }

    private static unsafe void FillTube(ImDrawListPtr dl, ReadOnlySpan<Vector2> left, ReadOnlySpan<Vector2> right, Vector2 tip)
    {
        Span<Vector2> sl = stackalloc Vector2[left.Length];
        Span<Vector2> sr = stackalloc Vector2[right.Length];

        for (var i = 0; i < left.Length; i++)
        {
            sl[i] = P(left[i]);
            sr[i] = P(right[i]);
        }

        var white = SilkMainDraw.Col(255, 255, 255);
        SilkMainDraw.Strip(dl, sl, sr, white);

        Span<Vector2> outline = stackalloc Vector2[left.Length + right.Length + 12];
        var n = Outline(left, right, tip, outline, out var arcStart, out var arcCount);
        Span<Vector2> cap = stackalloc Vector2[arcCount + 2];
        cap[0] = sl[^1];

        for (var i = 0; i < arcCount; i++)
            cap[i + 1] = outline[arcStart + i];

        cap[arcCount + 1] = sr[^1];
        SilkMainDraw.Fan(dl, P(tip), cap, white, white, false);

        dl.AddPolyline(outline[..n], white, MathF.Max(1f, s), closed: true);
    }

    private static void Tail(ImDrawListPtr dl, float off, float mix, float tailPhase, float kick)
    {
        Span<Vector2> path = stackalloc Vector2[TailSegments + 1];
        var seg = TailLength / TailSegments;
        path[0] = new Vector2(W / 2f + 3f, off + 12f);

        for (var i = 0; i < TailSegments; i++)
        {
            var u = (i + 0.5f) / TailSegments;
            var hold = MathF.Min(1f, MathF.Pow(u / 0.14f, 2f));
            var amp = 0.3f + 0.12f * mix + MathF.Min(0.3f, kick * 0.4f);
            var sway = amp * (0.35f + 0.65f * u) * MathF.Sin(tailPhase - u * 0.9f);
            var curl = 1.15f * MathF.Pow(u, 3f) * MathF.Sin(tailPhase - u * 1.2f - 0.5f);
            var a = MathF.PI / 2f + hold * (sway + curl);
            path[i + 1] = path[i] + new Vector2(MathF.Cos(a), MathF.Sin(a)) * seg;
        }

        Span<Vector2> left = stackalloc Vector2[TailSegments + 1];
        Span<Vector2> right = stackalloc Vector2[TailSegments + 1];
        Tube(path, left, right, true);

        var start = dl.VtxBuffer.Size;
        FillTube(dl, left, right, path[TailSegments]);
        SilkMainDraw.Shade(dl, start, P(0f, off), P(0f, off + TailLength), TailStops, TailColors);

        var stripe = SilkMainDraw.Col(176, 92, 34, 0.5f);

        foreach (var i in (ReadOnlySpan<int>)[6, 10, 14, 17])
            dl.AddQuadFilled(P(left[i]), P(left[i + 1]), P(right[i + 1]), P(right[i]), stripe);
    }

    private static void Body(ImDrawListPtr dl, float off, float cx)
    {
        Span<Vector2> pts = stackalloc Vector2[4 * 10 + 1];
        var n = 0;
        var a = new Vector2(cx - 21f, off - 24f);
        pts[n++] = P(a);
        n = CubicInto(pts, n, a, new Vector2(cx - 22f, off - 10f), new Vector2(cx - 26f, off), new Vector2(cx - 24f, off + 9f));
        n = CubicInto(pts, n, new Vector2(cx - 24f, off + 9f), new Vector2(cx - 22f, off + 19f), new Vector2(cx - 10f, off + 23f), new Vector2(cx, off + 23f));
        n = CubicInto(pts, n, new Vector2(cx, off + 23f), new Vector2(cx + 10f, off + 23f), new Vector2(cx + 22f, off + 19f), new Vector2(cx + 24f, off + 9f));
        n = CubicInto(pts, n, new Vector2(cx + 24f, off + 9f), new Vector2(cx + 26f, off), new Vector2(cx + 22f, off - 10f), new Vector2(cx + 21f, off - 24f));

        var white = SilkMainDraw.Col(255, 255, 255);
        SilkMainDraw.Fan(dl, P(cx, off), pts[..n], white, white);
        dl.AddPolyline(pts[..n], white, MathF.Max(1f, s), closed: true);
    }

    private static int CubicInto(Span<Vector2> pts, int n, Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1)
    {
        var end = n + Geometry2DHelper.SampleCubic(pts[n..], p0, c1, c2, p1, 10, includeStart: false);

        for (var i = n; i < end; i++)
            pts[i] = P(pts[i]);

        return end;
    }

    private readonly record struct LegPose(Vector2 Bottom, float Angle);

    private static LegPose Leg(ImDrawListPtr dl, int side, float off, float t, float mix, float kick)
    {
        var cx = W / 2f;
        var a = side * (0.03f * MathF.Sin(t * 1.1f + side * 0.9f) + mix * 0.05f * MathF.Sin(t * 3.4f + side * 1.6f))
            + kick * 0.15f * MathF.Sin(t * 9f + side);
        var top = new Vector2(cx + side * 12.5f, off + 6f);
        var mid = Rot(new Vector2(cx + side * 14f, off + 20f), top, a);
        var bot = Rot(new Vector2(cx + side * 14.6f, off + 30f), top, a * 1.3f);

        Span<Vector2> key = [top, mid, bot];
        Span<Vector2> curve = stackalloc Vector2[21];
        var n = Geometry2DHelper.SampleCatmullRom(curve, key, 10);
        Span<Vector2> left = stackalloc Vector2[n];
        Span<Vector2> right = stackalloc Vector2[n];
        Tube(curve[..n], left, right, false);
        FillTube(dl, left, right, bot);

        return new LegPose(bot, a);
    }

    private static void Toes(ImDrawListPtr dl, Vector2 bottom, float angle)
    {
        var col = SilkMainDraw.Col(200, 150, 110, 0.65f);
        var r = angle * 0.8f;
        Line(dl, P(bottom + Rotate(new Vector2(-2.2f, 3.2f), r)), P(bottom + Rotate(new Vector2(-2.2f, 5.4f), r)), col, 0.9f);
        Line(dl, P(bottom + Rotate(new Vector2(2.2f, 3.2f), r)), P(bottom + Rotate(new Vector2(2.2f, 5.4f), r)), col, 0.9f);
    }

    private static Vector2 Rotate(Vector2 v, float a)
    {
        var c = MathF.Cos(a);
        var sn = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * sn, v.X * sn + v.Y * c);
    }

    private static void Belly(ImDrawListPtr dl, float off, float cx)
    {
        var centre = new Vector2(cx, off + 8f);
        const int spokes = 28;
        const int rings = 4;
        Span<Vector2> ring = stackalloc Vector2[spokes];
        var white = SilkMainDraw.Col(255, 255, 255);
        var start = dl.VtxBuffer.Size;

        Span<Vector2> inner = stackalloc Vector2[spokes];

        for (var k = 1; k <= rings; k++)
        {
            var f = k / (float)rings;

            for (var i = 0; i < spokes; i++)
            {
                var a = MathF.PI * 2f * i / spokes;
                ring[i] = P(centre + new Vector2(MathF.Cos(a) * 9f, MathF.Sin(a) * 13f) * f);
            }

            if (k == 1)
                SilkMainDraw.Fan(dl, P(centre), ring, white, white);
            else
                RingStrip(dl, inner, ring, white);

            ring.CopyTo(inner);
        }

        var verts = dl.VtxBuffer.AsSpan();
        var c = P(centre);

        for (var i = start; i < verts.Length; i++)
        {
            var d = Vector2.Distance(verts[i].Pos, c) / s;
            var p = Math.Clamp((d - 1f) / 11f, 0f, 1f);
            verts[i].Col = SilkMainDraw.Sample(p, BellyStops, BellyColors);
        }
    }

    private static void RingStrip(ImDrawListPtr dl, ReadOnlySpan<Vector2> inner, ReadOnlySpan<Vector2> outer, uint col)
    {
        Span<Vector2> a = stackalloc Vector2[inner.Length + 1];
        Span<Vector2> b = stackalloc Vector2[outer.Length + 1];
        inner.CopyTo(a);
        outer.CopyTo(b);
        a[^1] = inner[0];
        b[^1] = outer[0];
        SilkMainDraw.Strip(dl, a, b, col);
    }
}
