using BypassEmote.UI.Silk.Main;
using Dalamud.Bindings.ImGui;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public enum SilkIcon
{
    Logo,
    Menu,
    Collapse,
    Expand,
    Close,
    Logs,
    Changelog,
    Settings,
    Check,
    Chevron,
    Plus,
    ArrowLeft,
    Warn,
    Ok,
    Search,
    Hand,
    Refresh,
    Export,
    Trash,
    Sliders,
    Tag,
    First,
    Prev,
    Next,
    Last,
    Sync,
    Cube,
    Star,
    Block,
    Swap,
    Hotbar,
    Play,
    Overrides,
}

public sealed class SilkIconPart
{
    internal SilkIconPart(Vector2[][] polylines, bool[] closed, float stroke, bool fill, bool roundCap)
    {
        Polylines = polylines;
        Closed = closed;
        Stroke = stroke;
        Fill = fill;
        RoundCap = roundCap;
    }

    internal Vector2[][] Polylines { get; }

    internal bool[] Closed { get; }

    internal float Stroke { get; }

    internal bool Fill { get; }

    internal bool RoundCap { get; }
}

public sealed class SilkIconShape
{
    internal SilkIconShape(float viewBox, SilkIconPart[] parts)
    {
        ViewBox = viewBox;
        Parts = parts;
    }

    public float ViewBox { get; }

    internal SilkIconPart[] Parts { get; }
}

public static class SilkIcons
{
    private const int MaxPoints = 512;

    private static readonly Vector2[] Scratch = new Vector2[MaxPoints];

    private static readonly SilkIconShape[] Shapes = BuildAll();

    private static readonly NoireMeshCache<IconKey> Meshes = new(256);

    private readonly record struct IconKey(SilkIconShape Shape, float Size);

    public static SilkIconShape Get(SilkIcon icon) => Shapes[(int)icon];

    public static void Draw(SilkIcon icon, Vector2 topLeft, float size, Vector4 color)
        => Draw(Shapes[(int)icon], topLeft, size, color);

    public static void DrawCentered(SilkIcon icon, Vector2 centre, float size, Vector4 color)
        => Draw(Shapes[(int)icon], centre - new Vector2(size * 0.5f, size * 0.5f), size, color);

    public static SilkIconShape Stroked(float viewBox, float stroke, bool roundCap, params string[] paths)
    {
        var parts = new SilkIconPart[paths.Length];

        for (var i = 0; i < paths.Length; i++)
            parts[i] = Path(paths[i], stroke, false, roundCap);

        return new SilkIconShape(viewBox, parts);
    }

    public static SilkIconShape Define(float viewBox, params SilkIconPart[] parts) => new(viewBox, parts);

    public static SilkIconPart Path(string d, float stroke, bool fill = false, bool roundCap = true)
    {
        var subpaths = SvgPathHelper.Flatten(d);
        var polylines = new Vector2[subpaths.Length][];
        var closed = new bool[subpaths.Length];

        for (var i = 0; i < subpaths.Length; i++)
        {
            polylines[i] = subpaths[i].Points;
            closed[i] = subpaths[i].Closed;
        }

        return new SilkIconPart(polylines, closed, stroke, fill, roundCap);
    }

    public static SilkIconPart Circle(float cx, float cy, float r, float stroke, bool fill = false)
    {
        var segments = 28;
        var points = new Vector2[segments];

        for (var i = 0; i < segments; i++)
        {
            var a = i * MathF.PI * 2f / segments;
            points[i] = new Vector2(cx + (MathF.Cos(a) * r), cy + (MathF.Sin(a) * r));
        }

        return new SilkIconPart([points], [true], stroke, fill, false);
    }

    public static SilkIconPart Rect(float x, float y, float w, float h, float rx, float stroke, bool fill = false)
    {
        var points = new List<Vector2>();
        rx = MathF.Min(rx, MathF.Min(w, h) * 0.5f);
        const int arc = 5;

        void Corner(float cx, float cy, float from)
        {
            for (var i = 0; i <= arc; i++)
            {
                var a = from + (i * MathF.PI * 0.5f / arc);
                points.Add(new Vector2(cx + (MathF.Cos(a) * rx), cy + (MathF.Sin(a) * rx)));
            }
        }

        if (rx <= 0f)
        {
            points.Add(new Vector2(x, y));
            points.Add(new Vector2(x + w, y));
            points.Add(new Vector2(x + w, y + h));
            points.Add(new Vector2(x, y + h));
        }
        else
        {
            Corner(x + w - rx, y + rx, -MathF.PI * 0.5f);
            Corner(x + w - rx, y + h - rx, 0f);
            Corner(x + rx, y + h - rx, MathF.PI * 0.5f);
            Corner(x + rx, y + rx, MathF.PI);
        }

        return new SilkIconPart([points.ToArray()], [true], stroke, fill, false);
    }

    public static void Draw(SilkIconShape shape, Vector2 topLeft, float size, Vector4 color)
    {
        if (!NoireLib.NoireService.IsInitialized())
            return;

        Draw(NoireShapes.DrawList, shape, topLeft, size, SilkPalette.U32(color));
    }

    public static int Draw(ImDrawListPtr drawList, SilkIconShape shape, Vector2 topLeft, float size, uint color)
    {
        using var profile = SilkProfile.Detail("SilkIcons.Draw");

        if (drawList.IsNull)
            return 0;

        var start = drawList.VtxBuffer.Size;

        if (((color >> 24) & 0xFF) == 0 || size <= 0f)
            return start;

        var key = new IconKey(shape, size);

        if (Meshes.TryReplay(drawList, key, topLeft, color))
            return start;

        using var recording = Meshes.Record(drawList, key, topLeft, color);
        var scale = size / shape.ViewBox;
        var buffer = Scratch.AsSpan();

        foreach (var part in shape.Parts)
        {
            var thickness = MathF.Max(0.75f, part.Stroke * scale);

            for (var p = 0; p < part.Polylines.Length; p++)
            {
                var source = part.Polylines[p];
                var count = Math.Min(source.Length, MaxPoints);

                if (count < 2)
                    continue;

                var centre = Vector2.Zero;

                for (var i = 0; i < count; i++)
                {
                    buffer[i] = topLeft + (source[i] * scale);
                    centre += buffer[i];
                }

                var points = buffer[..count];

                if (part.Fill)
                {
                    SilkMainDraw.Fan(drawList, centre / count, points, color, color);
                    drawList.AddPolyline(points, color, 1f, closed: true);
                    continue;
                }

                var closed = part.Closed[p];
                drawList.AddPolyline(points, color, thickness, closed);

                if (part.RoundCap && !closed)
                {
                    var radius = thickness * 0.5f;
                    drawList.AddCircleFilled(points[0], radius, color, 8);
                    drawList.AddCircleFilled(points[count - 1], radius, color, 8);
                }
            }
        }

        return start;
    }

    private static SilkIconShape[] BuildAll()
    {
        var all = new SilkIconShape[Enum.GetValues<SilkIcon>().Length];

        all[(int)SilkIcon.Logo] = Define(16f,
            Rect(3f, 7.5f, 10f, 6.5f, 1.8f, 0f, true),
            Path("M5.5 7.5V5a2.5 2.5 0 0 1 4.9-.7", 2f));
        all[(int)SilkIcon.Menu] = Stroked(16f, 1.6f, true, "M3 4.5h10M3 8h10M3 11.5h10");
        all[(int)SilkIcon.Collapse] = Stroked(16f, 1.7f, true, "M4 6.5l4 4 4-4");
        all[(int)SilkIcon.Expand] = Stroked(16f, 1.7f, true, "M4 10l4-4 4 4");
        all[(int)SilkIcon.Close] = Stroked(16f, 1.6f, true, "M4.5 4.5l7 7M11.5 4.5l-7 7");
        all[(int)SilkIcon.Logs] = Stroked(16f, 1.5f, true, "M3 4h10M3 8h10M3 12h6");
        all[(int)SilkIcon.Changelog] = Stroked(16f, 1.5f, false, "M4 2h5.5L12 4.5V14H4z", "M6 7.5h4M6 10.5h4");
        all[(int)SilkIcon.Settings] = Define(16f,
            Path("M6.8 1.8h2.4l.4 1.9 1.3.7 1.8-.7 1.2 2.1-1.5 1.3v1.5l1.5 1.3-1.2 2.1-1.8-.7-1.3.7-.4 1.9H6.8l-.4-1.9-1.3-.7-1.8.7-1.2-2.1 1.5-1.3V7.1L2.1 5.8l1.2-2.1 1.8.7 1.3-.7z", 1.5f, false, false),
            Circle(8f, 8f, 2f, 1.5f));
        all[(int)SilkIcon.Check] = Stroked(10f, 2.2f, true, "M2 5.2l2 2 4-4.4");
        all[(int)SilkIcon.Chevron] = Stroked(12f, 1.6f, true, "M3 4.5l3 3 3-3");
        all[(int)SilkIcon.Plus] = Stroked(16f, 1.6f, true, "M8 3v10M3 8h10");
        all[(int)SilkIcon.ArrowLeft] = Stroked(16f, 1.6f, true, "M13 8H3M6.5 4.5L3 8l3.5 3.5");
        all[(int)SilkIcon.Warn] = Define(16f,
            Path("M8 2l6.2 11H1.8z", 1.6f, false, false),
            Path("M8 6.5v3M8 11.5v.3", 1.6f));
        all[(int)SilkIcon.Ok] = Define(16f, Circle(8f, 8f, 6f, 1.8f), Path("M5.2 8.2l2 2 3.6-4", 1.8f));
        all[(int)SilkIcon.Search] = Define(16f, Circle(7f, 7f, 4.6f, 1.6f), Path("M10.5 10.5L14 14", 1.6f));
        all[(int)SilkIcon.Hand] = Stroked(16f, 1.5f, true,
            "M5 8V3.5a1 1 0 0 1 2 0V7M7 6.5V2.8a1 1 0 0 1 2 0V7M9 6.8V3.6a1 1 0 0 1 2 0V8M11 7.2a1 1 0 0 1 2 0v2.3A4.5 4.5 0 0 1 8.5 14h-.6a4 4 0 0 1-3.2-1.6L2.8 9.8a1 1 0 0 1 1.6-1.2L5 9.4");
        all[(int)SilkIcon.Refresh] = Stroked(16f, 1.6f, true, "M13.2 8a5.2 5.2 0 1 1-1.6-3.8", "M13.2 2.6v3h-3");
        all[(int)SilkIcon.Export] = Stroked(16f, 1.6f, true, "M8 2v8M5 5l3-3 3 3M3 10v3h10v-3");
        all[(int)SilkIcon.Trash] = Stroked(16f, 1.5f, true, "M3 4.5h10M6.5 4.5V3h3v1.5M4.5 4.5l.6 8.5h5.8l.6-8.5");
        all[(int)SilkIcon.Sliders] = Define(16f,
            Path("M2.5 4.5h6M11.5 4.5h2M2.5 11.5h2M7.5 11.5h6", 1.5f),
            Circle(10f, 4.5f, 1.6f, 1.5f),
            Circle(6f, 11.5f, 1.6f, 1.5f));
        all[(int)SilkIcon.Tag] = Define(16f,
            Path("M2.5 2.5h5.5l5.5 5.5-5.5 5.5-5.5-5.5z", 1.5f, false, false),
            Circle(5.5f, 5.5f, 1f, 0f, true));
        all[(int)SilkIcon.First] = Stroked(12f, 1.6f, true, "M6.5 3L3.5 6l3 3M9.5 3l-3 3 3 3");
        all[(int)SilkIcon.Prev] = Stroked(12f, 1.6f, true, "M7.5 3l-3 3 3 3");
        all[(int)SilkIcon.Next] = Stroked(12f, 1.6f, true, "M4.5 3l3 3-3 3");
        all[(int)SilkIcon.Last] = Stroked(12f, 1.6f, true, "M5.5 3l3 3-3 3M2.5 3l3 3-3 3");
        all[(int)SilkIcon.Sync] = Define(16f,
            Circle(5f, 5.5f, 2.2f, 1.5f),
            Circle(11f, 5.5f, 2.2f, 1.5f),
            Path("M1.5 13.5c.4-2.6 6.6-2.6 7 0M8.5 12c.6-1.6 5.4-1.6 6 1.5", 1.5f));
        all[(int)SilkIcon.Cube] = Stroked(16f, 1.5f, true, "M8 1.8l5.4 3.1v6.2L8 14.2l-5.4-3.1V4.9z", "M8 8v6.2M8 8l5.4-3.1M8 8L2.6 4.9");
        all[(int)SilkIcon.Star] = Define(16f, Path("M8 1.6l1.9 4 4.4.5-3.3 3 .9 4.4L8 11.3l-3.9 2.2.9-4.4-3.3-3 4.4-.5z", 0f, true));
        all[(int)SilkIcon.Block] = Define(16f, Circle(8f, 8f, 5.6f, 1.7f), Path("M4 12l8-8", 1.7f, false, false));
        all[(int)SilkIcon.Swap] = Stroked(16f, 1.5f, true, "M2.5 5.5h9l-2.5-2.5M13.5 10.5h-9l2.5 2.5");
        all[(int)SilkIcon.Hotbar] = Define(16f,
            Rect(1.8f, 9f, 3.6f, 3.6f, 0.8f, 1.5f),
            Rect(6.2f, 9f, 3.6f, 3.6f, 0.8f, 1.5f),
            Rect(10.6f, 9f, 3.6f, 3.6f, 0.8f, 1.5f));
        all[(int)SilkIcon.Play] = Define(16f, Path("M4.5 2.8v10.4a.6.6 0 0 0 .9.5l8.3-5.2a.6.6 0 0 0 0-1L5.4 2.3a.6.6 0 0 0-.9.5z", 0f, true));
        all[(int)SilkIcon.Overrides] = Define(16f,
            Path("M3 4h7M3 8h10M3 12h5", 1.5f),
            Circle(12.5f, 4f, 1.3f, 1.5f),
            Circle(10.5f, 12f, 1.3f, 1.5f));

        return all;
    }
}
