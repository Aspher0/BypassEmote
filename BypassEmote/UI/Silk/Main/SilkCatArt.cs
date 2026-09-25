using NoireLib.Helpers;
using NoireLib.UI;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal static class SilkCatArt
{
    internal static readonly Vector2 View = new(92f, 82f);
    internal static readonly Vector2 Box = new(78f, 70f);
    internal static readonly Vector2 PawsView = new(60f, 14f);

    internal static readonly Vector2 EarLeftPivot = new(28.1f, 40f);
    internal static readonly Vector2 EarRightPivot = new(63.9f, 40f);
    internal static readonly Vector2 EyesPivot = new(46f, 49f);
    internal static readonly Vector2 WhiskerLeftPivot = new(35f, 65.9f);
    internal static readonly Vector2 WhiskerRightPivot = new(57f, 65.9f);

    private static NoireGradient Fur() => NoireGradient.Radial(
            GradientStop.At(Rgb(0xf8b673), 0f), GradientStop.At(Rgb(0xee9c4f), 0.75f), GradientStop.At(Rgb(0xd9823a), 1f))
        .WithCenter(0.5f, 0.38f)
        .WithRadius(1.24f);

    private static Vector4 Rgb(uint rgb) => ColorHelper.RgbToVector4(rgb);

    private static RasterShape Stroke(string d, uint rgb, float width, float opacity = 1f)
        => RasterShape.Path(d).Stroked(Rgb(rgb), width) with { Opacity = opacity };

    private static RasterShape Fill(string d, uint rgb, float opacity = 1f)
        => RasterShape.Path(d).Filled(Rgb(rgb)) with { Opacity = opacity };

    private static RasterShape Ell(float cx, float cy, float rx, float ry, uint rgb, float opacity = 1f)
        => RasterShape.Ellipse(new Vector2(cx, cy), new Vector2(rx, ry)).Filled(Rgb(rgb)) with { Opacity = opacity };

    internal static RasterShape[] EarLeft() =>
    [
        RasterShape.Path("M17 40 C15 27 17 15 22 8 C29 12 36 19 40 27 Z").Filled(Fur()),
        Fill("M22 34 C21 26 22 19 24.5 14 C28.5 17 32 21 34 26 Z", 0xffc6be),
        Stroke("M24 20 l3 5 M22.5 25 l3.5 3", 0xfff4ea, 0.8f, 0.8f),
    ];

    internal static RasterShape[] EarRight() =>
    [
        RasterShape.Path("M75 40 C77 27 75 15 70 8 C63 12 56 19 52 27 Z").Filled(Fur()),
        Fill("M70 34 C71 26 70 19 67.5 14 C63.5 17 60 21 58 26 Z", 0xffc6be),
        Stroke("M68 20 l-3 5 M69.5 25 l-3.5 3", 0xfff4ea, 0.8f, 0.8f),
    ];

    internal static RasterShape[] Body() =>
    [
        RasterShape.Path("M13 51 C13 35 27 25 46 25 C65 25 79 35 79 51 C79 58 76.5 63 72.5 66.5 L77 69.5 L69 70 C63 75 55 77.5 46 77.5 C37 77.5 29 75 23 70 L15 69.5 L19.5 66.5 C15.5 63 13 58 13 51 Z").Filled(Fur()),
        Stroke("M39 27 q2 4 0 8 M46 26 v9 M53 27 q-2 4 0 8", 0xc9722e, 2.4f, 0.85f),
        Stroke("M14.5 47 l6 1.5 M14 52 l5.5 .5 M77.5 47 l-6 1.5 M78 52 l-5.5 .5", 0xc9722e, 2f, 0.7f),
        Ell(41f, 64.5f, 6.5f, 5f, 0xfff0de),
        Ell(51f, 64.5f, 6.5f, 5f, 0xfff0de),
        Ell(46f, 69f, 5f, 3.5f, 0xfff0de),
    ];

    internal static RasterShape[] PleadBrows() =>
    [
        Stroke("M25 38 q5.5 -4.5 11 -2.5 M67 38 q-5.5 -4.5 -11 -2.5", 0x8a4d24, 1.8f),
    ];

    internal static RasterShape[] PleadEyes() =>
    [
        Ell(32.5f, 49f, 7.6f, 8.4f, 0x2b1b26),
        Ell(59.5f, 49f, 7.6f, 8.4f, 0x2b1b26),
        Ell(32.5f, 53f, 5.2f, 3.4f, 0x6d4a60, 0.55f),
        Ell(59.5f, 53f, 5.2f, 3.4f, 0x6d4a60, 0.55f),
        Ell(35.2f, 45.4f, 3.1f, 3.1f, 0xffffff),
        Ell(62.2f, 45.4f, 3.1f, 3.1f, 0xffffff),
        Ell(30f, 52.4f, 1.3f, 1.3f, 0xffffff, 0.9f),
        Ell(57f, 52.4f, 1.3f, 1.3f, 0xffffff, 0.9f),
        Stroke("M26.5 55.3 q6 2.6 12 0 M53.5 55.3 q6 2.6 12 0", 0xbfe6ff, 1.2f, 0.8f),
    ];

    internal static RasterShape[] PleadMouth() =>
    [
        Stroke("M42.5 66.5 q1.75 1.8 3.5 0 q1.75 1.8 3.5 0", 0x7a4420, 1.5f),
        Ell(24f, 61f, 4.5f, 2.5f, 0xff8f9f, 0.45f),
        Ell(68f, 61f, 4.5f, 2.5f, 0xff8f9f, 0.45f),
    ];

    internal static RasterShape[] Happy() =>
    [
        Stroke("M23.5 36.5 q6 -6.5 12.5 -3.5 M68.5 36.5 q-6 -6.5 -12.5 -3.5", 0x8a4d24, 1.8f),
        Ell(32.5f, 49.5f, 9.2f, 10.2f, 0x2b1b26),
        Ell(59.5f, 49.5f, 9.2f, 10.2f, 0x2b1b26),
        Ell(32.5f, 54.5f, 6.6f, 4.4f, 0x8a5a78, 0.6f),
        Ell(59.5f, 54.5f, 6.6f, 4.4f, 0x8a5a78, 0.6f),
        Ell(36f, 44.8f, 3.9f, 3.9f, 0xffffff),
        Ell(63f, 44.8f, 3.9f, 3.9f, 0xffffff),
        Ell(29.2f, 53.8f, 1.9f, 1.9f, 0xffffff),
        Ell(56.2f, 53.8f, 1.9f, 1.9f, 0xffffff),
        Ell(37.6f, 51f, 1f, 1f, 0xffffff, 0.9f),
        Ell(64.6f, 51f, 1f, 1f, 0xffffff, 0.9f),
        Stroke("M25.5 57 q7 3.2 14 0 M52.5 57 q7 3.2 14 0", 0xbfe6ff, 1.4f, 0.9f),
        Stroke("M42 66 q2 2.2 4 0 q2 2.2 4 0", 0x7a4420, 1.5f),
        Fill("M44.6 67.2 q1.4 2.2 2.8 0 z", 0xff8fa3),
        Ell(23f, 61f, 6f, 3.2f, 0xff7f93, 0.8f),
        Ell(69f, 61f, 6f, 3.2f, 0xff7f93, 0.8f),
    ];

    internal static RasterShape[] Nose() =>
    [
        RasterShape.Path("M43.6 60.2 h4.8 l-2.4 2.8 z").Filled(Rgb(0xe0707f)).Stroked(Rgb(0xe0707f), 1f),
    ];

    internal static RasterShape[] WhiskerLeft() =>
    [
        Stroke("M35 64 q-11 -3 -22 -1 M35 66.5 q-10 0 -21 3", 0xffffff, 0.9f, 0.75f),
    ];

    internal static RasterShape[] WhiskerRight() =>
    [
        Stroke("M57 64 q11 -3 22 -1 M57 66.5 q10 0 21 3", 0xffffff, 0.9f, 0.75f),
    ];

    internal static RasterShape[] Paws() =>
    [
        RasterShape.Ellipse(new Vector2(14f, 8f), new Vector2(10f, 6f)).Filled(Rgb(0xf4a95c)).Stroked(Rgb(0xd9823a), 1f),
        RasterShape.Ellipse(new Vector2(46f, 8f), new Vector2(10f, 6f)).Filled(Rgb(0xf4a95c)).Stroked(Rgb(0xd9823a), 1f),
        Stroke("M11 5v4M15 5v4M43 5v4M47 5v4", 0xd9823a, 1.2f),
    ];

    internal static RasterShape[] Kofi() =>
    [
        Fill("M2.5 5.5h14.2a4.3 4.3 0 0 1 0 8.6h-.9a5.8 5.8 0 0 1-5.6 4.4H7.8a5.3 5.3 0 0 1-5.3-5.3z", 0xffffff),
        Fill("M16.3 8.2a1.7 1.7 0 1 1 0 3.4h-.6V8.2zM9.3 15.3L6.6 12.7a1.7 1.7 0 0 1 2.4-2.4l.3.3.3-.3a1.7 1.7 0 0 1 2.4 2.4z", 0xf0504d),
    ];

    internal static RasterShape[] Discord() =>
    [
        Fill("M19.3 5.4A16.6 16.6 0 0 0 15.2 4l-.5 1a15.3 15.3 0 0 0-5.4 0l-.5-1a16.6 16.6 0 0 0-4.1 1.4C2.1 9.3 1.4 13.2 1.8 17a16.7 16.7 0 0 0 5 2.6l1.1-1.7a10.6 10.6 0 0 1-1.7-.8l.4-.3a11.9 11.9 0 0 0 10.8 0l.4.3a10.6 10.6 0 0 1-1.7.8l1.1 1.7a16.7 16.7 0 0 0 5-2.6c.5-4.4-.7-8.3-2.9-11.6zM8.5 14.7c-1 0-1.8-.9-1.8-2s.8-2 1.8-2 1.8.9 1.8 2-.8 2-1.8 2zm7 0c-1 0-1.8-.9-1.8-2s.8-2 1.8-2 1.8.9 1.8 2-.8 2-1.8 2z", 0xffffff),
    ];
}
