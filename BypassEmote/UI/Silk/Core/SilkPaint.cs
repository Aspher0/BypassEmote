using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public static class SilkPaint
{
    public static void Fill(Vector2 min, Vector2 max, Vector4 color, float radius)
    {
        if (color.W <= 0f || max.X <= min.X || max.Y <= min.Y)
            return;

        NoireShapes.Rect(min, max, color, radius > 0f ? CornerShape.Rounded : CornerShape.Square, radius);
    }

    public static void Fill(Vector2 min, Vector2 max, Vector4 color, float radius, RectCorners corners)
    {
        if (color.W <= 0f || max.X <= min.X || max.Y <= min.Y)
            return;

        NoireShapes.Rect(min, max, color, radius > 0f ? CornerShape.Rounded : CornerShape.Square, radius, corners);
    }

    public static void InsetRing(Vector2 min, Vector2 max, Vector4 color, float radius, float width = 1f)
    {
        if (color.W <= 0f || width <= 0f)
            return;

        var half = width * 0.5f;
        NoireShapes.RectOutline(min + new Vector2(half, half), max - new Vector2(half, half), color, width,
            radius > 0f ? CornerShape.Rounded : CornerShape.Square, MathF.Max(0f, radius - half));
    }

    public static void OuterRing(Vector2 min, Vector2 max, Vector4 color, float radius, float width = 1f)
    {
        if (color.W <= 0f || width <= 0f)
            return;

        var half = width * 0.5f;
        NoireShapes.RectOutline(min - new Vector2(half, half), max + new Vector2(half, half), color, width, CornerShape.Rounded, radius + half);
    }

    public static void BoxShadow(Vector2 min, Vector2 max, Vector2 offset, float blur, float spread, Vector4 color, float radius)
    {
        if (color.W <= 0f)
            return;

        var boxMin = min + offset - new Vector2(spread, spread);
        var boxMax = max + offset + new Vector2(spread, spread);
        var half = blur * 0.5f;
        var innerMin = boxMin + new Vector2(half, half);
        var innerMax = boxMax - new Vector2(half, half);

        if (innerMax.X < innerMin.X)
        {
            var mid = (boxMin.X + boxMax.X) * 0.5f;
            innerMin.X = innerMax.X = mid;
        }

        if (innerMax.Y < innerMin.Y)
        {
            var mid = (boxMin.Y + boxMax.Y) * 0.5f;
            innerMin.Y = innerMax.Y = mid;
        }

        var corner = MathF.Max(0f, radius + spread - half);

        if (blur <= 0f)
        {
            Fill(boxMin, boxMax, color, MathF.Max(0f, radius + spread));
            return;
        }

        NoireShapes.Glow(innerMin, innerMax, color, blur, CornerShape.Rounded, corner);
    }

    public static void Glow(Vector2 min, Vector2 max, float blur, float spread, Vector4 color, float radius)
        => BoxShadow(min, max, Vector2.Zero, blur, spread, color, radius);

    public static void AngleGradient(Vector2 min, Vector2 max, float cssDegrees, Vector4 from, Vector4 to, float radius)
    {
        var size = max - min;
        var angle = cssDegrees * MathF.PI / 180f;
        var direction = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var length = (MathF.Abs(size.X * direction.X) + MathF.Abs(size.Y * direction.Y)) * 0.5f;
        var centre = (min + max) * 0.5f;

        NoireShapes.Gradient(centre - (direction * length), centre + (direction * length), from, to, (min, max, radius),
            static s => NoireShapes.Rect(s.min, s.max, Vector4.One, s.radius > 0f ? CornerShape.Rounded : CornerShape.Square, s.radius));
    }

    public static void HorizontalGradient(Vector2 min, Vector2 max, Vector4 from, Vector4 to, float radius)
        => NoireShapes.Gradient(new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), from, to, (min, max, radius),
            static s => NoireShapes.Rect(s.min, s.max, Vector4.One, s.radius > 0f ? CornerShape.Rounded : CornerShape.Square, s.radius));

    public static void VerticalGradient(Vector2 min, Vector2 max, Vector4 from, Vector4 to, float radius)
        => NoireShapes.Gradient(new Vector2(min.X, min.Y), new Vector2(min.X, max.Y), from, to, (min, max, radius),
            static s => NoireShapes.Rect(s.min, s.max, Vector4.One, s.radius > 0f ? CornerShape.Rounded : CornerShape.Square, s.radius));

    public static void Circle(Vector2 centre, float radius, Vector4 color)
    {
        if (color.W <= 0f || radius <= 0f)
            return;

        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        var packed = SilkPalette.U32(color);

        if (Circles.TryReplay(drawList, radius, centre, packed))
            return;

        using var recording = Circles.Record(drawList, radius, centre, packed);
        drawList.AddCircleFilled(centre, radius, packed, Math.Clamp((int)MathF.Ceiling(radius * 2.2f), 12, 64));
    }

    private static readonly NoireMeshCache<float> Circles = new(64);

    public static void Line(Vector2 from, Vector2 to, Vector4 color, float width = 1f)
    {
        if (color.W <= 0f)
            return;

        var drawList = NoireShapes.DrawList;

        if (!drawList.IsNull)
            drawList.AddLine(from, to, SilkPalette.U32(color), width);
    }

    public static void HLine(float x1, float x2, float y, Vector4 color)
        => Fill(new Vector2(x1, y), new Vector2(x2, y + 1f), color, 0f);

    public static void VLine(float x, float y1, float y2, Vector4 color)
        => Fill(new Vector2(x, y1), new Vector2(x + 1f, y2), color, 0f);

    public static void DashedRing(Vector2 min, Vector2 max, float radius, Vector4 color, float dash, float gap)
    {
        if (color.W <= 0f)
            return;

        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        Span<Vector2> path = stackalloc Vector2[NoireShapes.MaxRectPathPoints];
        var count = NoireShapes.RectPath(path, min + new Vector2(0.5f, 0.5f), max - new Vector2(0.5f, 0.5f), CornerShape.Rounded, MathF.Max(0f, radius - 0.5f));

        if (count < 2)
            return;

        var packed = SilkPalette.U32(color);
        var period = dash + gap;
        var walked = 0f;

        for (var i = 0; i < count; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % count];
            var length = Vector2.Distance(a, b);

            if (length <= 0f)
                continue;

            var direction = (b - a) / length;
            var at = 0f;

            while (at < length)
            {
                var phase = walked % period;
                var step = phase < dash ? MathF.Min(dash - phase, length - at) : MathF.Min(period - phase, length - at);

                if (phase < dash)
                    drawList.AddLine(a + (direction * at), a + (direction * (at + step)), packed, 1f);

                at += step;
                walked += step;
            }
        }
    }

    public static void PushClipExpanded(Vector2 min, Vector2 max, float reach)
    {
        var drawList = NoireShapes.DrawList;

        if (!drawList.IsNull)
            drawList.PushClipRect(min - new Vector2(reach, reach), max + new Vector2(reach, reach), false);
    }

    public static void PopClip()
    {
        var drawList = NoireShapes.DrawList;

        if (!drawList.IsNull)
            drawList.PopClipRect();
    }

    public static bool Contains(Vector2 min, Vector2 max, Vector2 point)
        => point.X >= min.X && point.X < max.X && point.Y >= min.Y && point.Y < max.Y;

    public static ImDrawListPtr Foreground => ImGui.GetForegroundDrawList();
}
