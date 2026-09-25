using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal struct Tween
{
    private float from;
    private float to;
    private float start;
    private float delay;
    private float duration;
    private UiCubicBezier? curve;
    private bool set;

    internal readonly float Target => to;

    internal void Snap(float value)
    {
        from = to = value;
        duration = 0f;
        set = true;
    }

    internal void Go(float target, float now, float seconds, UiCubicBezier easing, float wait = 0f)
    {
        if (!set)
        {
            Snap(target);
            return;
        }

        if (target == to)
            return;

        from = Value(now);
        to = target;
        start = now;
        delay = wait;
        duration = seconds;
        curve = easing;
    }

    internal readonly float Value(float now)
    {
        if (duration <= 0f)
            return to;

        var p = (now - start - delay) / duration;

        if (p <= 0f)
            return from;

        if (p >= 1f)
            return to;

        return from + (to - from) * (curve?.Evaluate(p) ?? p);
    }

    internal readonly bool Running(float now) => duration > 0f && now - start - delay < duration;
}

internal static class SilkMainDraw
{
    internal static unsafe Vector2 WhiteUv(ImDrawListPtr dl)
    {
        var data = dl.Handle->Data;
        return data == null ? ImGui.GetFontTexUvWhitePixel() : data->TexUvWhitePixel;
    }

    internal static unsafe bool PushWhite(ImDrawListPtr dl)
    {
        var white = ImGui.GetFontTexIdWhitePixel();

        if (dl.Handle->CmdHeader.TextureId.Handle == white.Handle)
            return false;

        dl.PushTextureID(white);
        return true;
    }

    internal static void PopWhite(ImDrawListPtr dl, bool pushed)
    {
        if (pushed)
            dl.PopTextureID();
    }

    internal static uint Col(byte r, byte g, byte b, float a = 1f)
        => ((uint)Math.Clamp(a * 255f + 0.5f, 0f, 255f) << 24) | ((uint)b << 16) | ((uint)g << 8) | r;

    internal static uint Col(Vector3 rgb, float a = 1f)
        => Col((byte)Math.Clamp(rgb.X + 0.5f, 0f, 255f), (byte)Math.Clamp(rgb.Y + 0.5f, 0f, 255f), (byte)Math.Clamp(rgb.Z + 0.5f, 0f, 255f), a);

    internal static uint Hex(uint rgb, float a = 1f)
        => Col((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

    internal static uint WithAlpha(uint col, float mul)
    {
        var alpha = (uint)Math.Clamp(((col >> 24) & 0xFF) * mul + 0.5f, 0f, 255f);
        return (col & 0x00FFFFFFu) | (alpha << 24);
    }

    internal static Vector3 Rgb(uint rgb) => new((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    internal static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    internal static int CornerSegments(float radius) => radius <= 0.5f ? 1 : Math.Clamp((int)MathF.Ceiling(radius * 0.6f), 3, 12);

    internal static int RoundRectPath(Span<Vector2> points, Vector2 min, Vector2 max, float radius, int segments)
    {
        radius = MathF.Max(0f, MathF.Min(radius, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f));
        var n = 0;
        Corner(points, ref n, new Vector2(min.X + radius, min.Y + radius), radius, MathF.PI, segments);
        Corner(points, ref n, new Vector2(max.X - radius, min.Y + radius), radius, MathF.PI * 1.5f, segments);
        Corner(points, ref n, new Vector2(max.X - radius, max.Y - radius), 0f + radius, 0f, segments);
        Corner(points, ref n, new Vector2(min.X + radius, max.Y - radius), radius, MathF.PI * 0.5f, segments);
        return n;
    }

    private static void Corner(Span<Vector2> points, ref int n, Vector2 centre, float radius, float startAngle, int segments)
    {
        for (var i = 0; i <= segments; i++)
        {
            var a = startAngle + MathF.PI * 0.5f * i / segments;
            points[n++] = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }
    }

    private static float Erfc(float x)
    {
        var z = MathF.Abs(x);
        var t = 1f / (1f + 0.5f * z);
        var r = t * MathF.Exp(-z * z - 1.26551223f + t * (1.00002368f + t * (0.37409196f + t * (0.09678418f
            + t * (-0.18628806f + t * (0.27886807f + t * (-1.13520398f + t * (1.48851587f
            + t * (-0.82215223f + t * 0.17087277f)))))))));
        return x >= 0f ? r : 2f - r;
    }

    private static readonly float[] ShadowSteps = [-1f, -0.6f, -0.3f, 0f, 0.3f, 0.6f, 1f, 1.4f];

    internal static unsafe void Shadow(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, uint color, float blur,
        float spread = 0f, Vector2 offset = default)
    {
        if (dl.IsNull || ((color >> 24) & 0xFF) == 0)
            return;

        min += offset - new Vector2(spread);
        max += offset + new Vector2(spread);
        radius = MathF.Max(0f, radius + spread);

        if (max.X <= min.X || max.Y <= min.Y)
            return;

        if (blur < 0.5f)
        {
            dl.AddRectFilled(min, max, color, radius);
            return;
        }

        var pushed = PushWhite(dl);
        var key = new ShadowKey(max - min, radius, color, blur);

        if (!ShadowMeshes.TryReplay(dl, key, min))
        {
            using var recording = ShadowMeshes.Record(dl, key, min);
            ShadowMesh(dl, min, max, radius, color, blur);
        }

        PopWhite(dl, pushed);
    }

    private readonly record struct ShadowKey(Vector2 Size, float Radius, uint Color, float Blur);

    private static readonly NoireMeshCache<ShadowKey> ShadowMeshes = new(128);

    private static unsafe void ShadowMesh(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, uint color, float blur)
    {
        var sigma = blur * 0.5f;
        var segs = Math.Clamp((int)MathF.Ceiling((radius + blur) * 0.35f), 3, 10);
        var per = 4 * (segs + 1);
        var levels = ShadowSteps.Length;
        Span<Vector2> ring = stackalloc Vector2[per];
        var vtxCount = per * levels + 1;
        var idxCount = per * 3 + (levels - 1) * per * 6;

        dl.PrimReserve(idxCount, vtxCount);
        var baseVertex = dl.VtxCurrentIdx;
        var verts = dl.VtxBuffer.AsSpan()[^vtxCount..];
        var idx = dl.IdxBuffer.AsSpan()[^idxCount..];
        var white = WhiteUv(dl);
        var rgb = color & 0x00FFFFFFu;
        var baseAlpha = ((color >> 24) & 0xFF) / 255f;
        var half = (max - min) * 0.5f;
        var centre = (min + max) * 0.5f;
        var inner = MathF.Min(half.X, half.Y);

        var v = 0;

        for (var level = 0; level < levels; level++)
        {
            var d = ShadowSteps[level] * blur;
            d = MathF.Max(d, -inner + 0.01f);
            var r = MathF.Max(0f, radius + d);
            RoundRectPath(ring, min - new Vector2(d), max + new Vector2(d), r, segs);
            var a = 0.5f * Erfc(d / (sigma * 1.41421356f));

            if (level == levels - 1)
                a = 0f;

            var packed = rgb | ((uint)Math.Clamp(a * baseAlpha * 255f + 0.5f, 0f, 255f) << 24);

            for (var i = 0; i < per; i++)
                verts[v++] = new ImDrawVert { Pos = ring[i], Uv = white, Col = packed };
        }

        var innerAlpha = 0.5f * Erfc(MathF.Max(ShadowSteps[0] * blur, -inner + 0.01f) / (sigma * 1.41421356f));
        verts[v++] = new ImDrawVert { Pos = centre, Uv = white, Col = rgb | ((uint)Math.Clamp(innerAlpha * baseAlpha * 255f + 0.5f, 0f, 255f) << 24) };

        var k = 0;
        var centreIndex = (ushort)(baseVertex + vtxCount - 1);

        for (var i = 0; i < per; i++)
        {
            idx[k++] = centreIndex;
            idx[k++] = (ushort)(baseVertex + i);
            idx[k++] = (ushort)(baseVertex + (i + 1) % per);
        }

        for (var level = 0; level < levels - 1; level++)
        {
            var a0 = (ushort)(baseVertex + level * per);
            var b0 = (ushort)(baseVertex + (level + 1) * per);

            for (var i = 0; i < per; i++)
            {
                var j = (i + 1) % per;
                idx[k++] = (ushort)(a0 + i);
                idx[k++] = (ushort)(b0 + i);
                idx[k++] = (ushort)(b0 + j);
                idx[k++] = (ushort)(a0 + i);
                idx[k++] = (ushort)(b0 + j);
                idx[k++] = (ushort)(a0 + j);
            }
        }

        var native = dl.Handle;
        native->VtxCurrentIdx += (uint)vtxCount;
        native->VtxWritePtr += vtxCount;
        native->IdxWritePtr += idxCount;
    }

    internal static void InsetRing(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, uint color, float width = 1f)
    {
        if (((color >> 24) & 0xFF) == 0)
            return;

        var key = new RingKey(max - min, radius, width, true);

        if (RingMeshes.TryReplay(dl, key, min, color))
            return;

        using var recording = RingMeshes.Record(dl, key, min, color);
        var h = width * 0.5f;
        dl.AddRect(min + new Vector2(h), max - new Vector2(h), color, MathF.Max(0f, radius - h), ImDrawFlags.None, width);
    }

    private readonly record struct RingKey(Vector2 Size, float Radius, float Width, bool Inset);

    private static readonly NoireMeshCache<RingKey> RingMeshes = new(256);

    internal static void OuterRing(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, uint color, float width = 1f)
    {
        if (((color >> 24) & 0xFF) == 0)
            return;

        var key = new RingKey(max - min, radius, width, false);

        if (RingMeshes.TryReplay(dl, key, min, color))
            return;

        using var recording = RingMeshes.Record(dl, key, min, color);
        var h = width * 0.5f;
        dl.AddRect(min - new Vector2(h), max + new Vector2(h), color, radius + h, ImDrawFlags.None, width);
    }

    internal static void ShadeVertical(ImDrawListPtr dl, int start, float top, float bottom, ReadOnlySpan<float> stops,
        ReadOnlySpan<uint> colors)
        => Shade(dl, start, new Vector2(0f, top), new Vector2(0f, bottom), stops, colors);

    internal static void Shade(ImDrawListPtr dl, int start, Vector2 from, Vector2 to, ReadOnlySpan<float> stops,
        ReadOnlySpan<uint> colors)
    {
        var verts = dl.VtxBuffer.AsSpan();
        var axis = to - from;
        var len2 = axis.LengthSquared();

        if (len2 < 1e-4f || start >= verts.Length)
            return;

        for (var i = start; i < verts.Length; i++)
        {
            ref var vert = ref verts[i];
            var p = Math.Clamp(Vector2.Dot(vert.Pos - from, axis) / len2, 0f, 1f);
            var c = Sample(p, stops, colors);
            var existing = ((vert.Col >> 24) & 0xFF) / 255f;
            vert.Col = WithAlpha(c, existing);
        }
    }

    internal static uint Sample(float p, ReadOnlySpan<float> stops, ReadOnlySpan<uint> colors)
    {
        if (p <= stops[0])
            return colors[0];

        for (var s = 1; s < stops.Length; s++)
        {
            if (p <= stops[s])
            {
                var t = (p - stops[s - 1]) / MathF.Max(1e-5f, stops[s] - stops[s - 1]);
                return LerpCol(colors[s - 1], colors[s], t);
            }
        }

        return colors[^1];
    }

    internal static uint LerpCol(uint a, uint b, float t)
    {
        uint Ch(int shift)
        {
            var x = (a >> shift) & 0xFF;
            var y = (b >> shift) & 0xFF;
            return (uint)Math.Clamp(x + (y - (float)x) * t + 0.5f, 0f, 255f) << shift;
        }

        return Ch(0) | Ch(8) | Ch(16) | Ch(24);
    }

    internal static void MultiplyAlpha(ImDrawListPtr dl, int start, float alpha)
    {
        if (alpha >= 0.999f)
            return;

        var verts = dl.VtxBuffer.AsSpan();

        for (var i = start; i < verts.Length; i++)
            verts[i].Col = WithAlpha(verts[i].Col, alpha);
    }

    internal static void Transform(ImDrawListPtr dl, int start, Vector2 pivot, Vector2 scale, float rotation, Vector2 translate)
    {
        if (scale == Vector2.One && rotation == 0f && translate == Vector2.Zero)
            return;

        var verts = dl.VtxBuffer.AsSpan();
        var c = MathF.Cos(rotation);
        var s = MathF.Sin(rotation);

        for (var i = start; i < verts.Length; i++)
        {
            ref var vert = ref verts[i];
            var d = (vert.Pos - pivot) * scale;
            vert.Pos = pivot + new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c) + translate;
        }
    }

    internal static unsafe void Fan(ImDrawListPtr dl, Vector2 centre, ReadOnlySpan<Vector2> rim, uint centreColor, uint rimColor,
        bool closed = true)
    {
        var n = rim.Length;

        if (n < 2)
            return;

        var tris = closed ? n : n - 1;
        var vtxCount = n + 1;
        var idxCount = tris * 3;
        var pushed = PushWhite(dl);
        dl.PrimReserve(idxCount, vtxCount);
        var baseVertex = dl.VtxCurrentIdx;
        var verts = dl.VtxBuffer.AsSpan()[^vtxCount..];
        var idx = dl.IdxBuffer.AsSpan()[^idxCount..];
        var white = WhiteUv(dl);

        verts[0] = new ImDrawVert { Pos = centre, Uv = white, Col = centreColor };

        for (var i = 0; i < n; i++)
            verts[i + 1] = new ImDrawVert { Pos = rim[i], Uv = white, Col = rimColor };

        var k = 0;

        for (var i = 0; i < tris; i++)
        {
            idx[k++] = (ushort)baseVertex;
            idx[k++] = (ushort)(baseVertex + 1 + i);
            idx[k++] = (ushort)(baseVertex + 1 + (i + 1) % n);
        }

        var native = dl.Handle;
        native->VtxCurrentIdx += (uint)vtxCount;
        native->VtxWritePtr += vtxCount;
        native->IdxWritePtr += idxCount;
        PopWhite(dl, pushed);
    }

    internal static unsafe void Strip(ImDrawListPtr dl, ReadOnlySpan<Vector2> left, ReadOnlySpan<Vector2> right, uint color)
    {
        var n = Math.Min(left.Length, right.Length);

        if (n < 2)
            return;

        var vtxCount = n * 2;
        var idxCount = (n - 1) * 6;
        var pushed = PushWhite(dl);
        dl.PrimReserve(idxCount, vtxCount);
        var baseVertex = dl.VtxCurrentIdx;
        var verts = dl.VtxBuffer.AsSpan()[^vtxCount..];
        var idx = dl.IdxBuffer.AsSpan()[^idxCount..];
        var white = WhiteUv(dl);

        for (var i = 0; i < n; i++)
        {
            verts[i * 2] = new ImDrawVert { Pos = left[i], Uv = white, Col = color };
            verts[i * 2 + 1] = new ImDrawVert { Pos = right[i], Uv = white, Col = color };
        }

        var k = 0;

        for (var i = 0; i < n - 1; i++)
        {
            var a = (ushort)(baseVertex + i * 2);
            idx[k++] = a;
            idx[k++] = (ushort)(a + 1);
            idx[k++] = (ushort)(a + 2);
            idx[k++] = (ushort)(a + 1);
            idx[k++] = (ushort)(a + 3);
            idx[k++] = (ushort)(a + 2);
        }

        var native = dl.Handle;
        native->VtxCurrentIdx += (uint)vtxCount;
        native->VtxWritePtr += vtxCount;
        native->IdxWritePtr += idxCount;
        PopWhite(dl, pushed);
    }

    internal static unsafe void RoundedShine(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, float bandLeft, float bandWidth,
        float skew, float peakAlpha, float step)
    {
        var cy = (min.Y + max.Y) * 0.5f;
        var half = (max.Y - min.Y) * 0.5f * MathF.Abs(skew);
        var from = MathF.Max(min.X, bandLeft - half);
        var to = MathF.Min(max.X, bandLeft + bandWidth + half);

        if (to <= from || bandWidth <= 0f)
            return;

        var columns = Math.Clamp((int)MathF.Ceiling((to - from) / MathF.Max(1f, step)) + 1, 2, 256);
        var vtxCount = columns * 2;
        var idxCount = (columns - 1) * 6;
        var pushed = PushWhite(dl);
        dl.PrimReserve(idxCount, vtxCount);
        var baseVertex = dl.VtxCurrentIdx;
        var verts = dl.VtxBuffer.AsSpan()[^vtxCount..];
        var idx = dl.IdxBuffer.AsSpan()[^idxCount..];
        var white = WhiteUv(dl);

        for (var i = 0; i < columns; i++)
        {
            var x = from + (to - from) * i / (columns - 1);
            var inset = 0f;

            if (x < min.X + radius)
            {
                var d = min.X + radius - x;
                inset = radius - MathF.Sqrt(MathF.Max(0f, radius * radius - d * d));
            }
            else if (x > max.X - radius)
            {
                var d = x - (max.X - radius);
                inset = radius - MathF.Sqrt(MathF.Max(0f, radius * radius - d * d));
            }

            var top = min.Y + inset;
            var bottom = max.Y - inset;
            verts[i * 2] = new ImDrawVert { Pos = new Vector2(x, top), Uv = white, Col = ShineColor(x + (top - cy) * skew, bandLeft, bandWidth, peakAlpha) };
            verts[i * 2 + 1] = new ImDrawVert { Pos = new Vector2(x, bottom), Uv = white, Col = ShineColor(x + (bottom - cy) * skew, bandLeft, bandWidth, peakAlpha) };
        }

        var k = 0;

        for (var i = 0; i < columns - 1; i++)
        {
            var a = (ushort)(baseVertex + i * 2);
            idx[k++] = a;
            idx[k++] = (ushort)(a + 1);
            idx[k++] = (ushort)(a + 2);
            idx[k++] = (ushort)(a + 1);
            idx[k++] = (ushort)(a + 3);
            idx[k++] = (ushort)(a + 2);
        }

        var native = dl.Handle;
        native->VtxCurrentIdx += (uint)vtxCount;
        native->VtxWritePtr += vtxCount;
        native->IdxWritePtr += idxCount;
        PopWhite(dl, pushed);
    }

    private static uint ShineColor(float x, float bandLeft, float bandWidth, float peakAlpha)
    {
        var u = (x - bandLeft) / bandWidth;
        var profile = u <= 0f || u >= 1f ? 0f : u < 0.5f ? u * 2f : (1f - u) * 2f;
        return Col(255, 255, 255, peakAlpha * profile);
    }

    internal static int Ellipse(Span<Vector2> points, Vector2 centre, float rx, float ry, int count)
    {
        count = Math.Min(count, points.Length);

        for (var i = 0; i < count; i++)
        {
            var a = MathF.PI * 2f * i / count;
            points[i] = centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
        }

        return count;
    }

    internal static int SegmentsFor(float radius) => Math.Clamp((int)MathF.Ceiling(radius * 1.2f), 8, 48);

    internal static void RadialGlow(ImDrawListPtr dl, Vector2 centre, float rx, float ry, uint inner, uint outer)
    {
        Span<Vector2> rim = stackalloc Vector2[48];
        var n = Ellipse(rim, centre, rx, ry, SegmentsFor(MathF.Max(rx, ry)));
        Fan(dl, centre, rim[..n], inner, outer);
    }
}
