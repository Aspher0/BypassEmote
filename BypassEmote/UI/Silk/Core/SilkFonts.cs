using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public enum SilkFace
{
    Ui400,
    Ui500,
    Ui600,
    Ui700,
    Ui800,
    Mono400,
    Mono500,
    MonoBold,
}

public static class SilkFonts
{
    public const float BodyPx = 13f;
    public const float BodyLineHeight = 1.4f;

    private static readonly string[] Files =
    [
        "HankenGrotesk-Regular.ttf",
        "HankenGrotesk-Medium.ttf",
        "HankenGrotesk-SemiBold.ttf",
        "HankenGrotesk-Bold.ttf",
        "HankenGrotesk-ExtraBold.ttf",
        "JetBrainsMono-Regular.ttf",
        "JetBrainsMono-Medium.ttf",
    ];

    private static readonly int[] Slots = [0, 1, 2, 3, 4, 5, 6, 6];

    private static readonly float[][] UsedSizes =
    [
        [11f, 11.5f, 12f, 12.5f, 13f, 13.5f],
        [10.5f, 11f, 11.5f, 12f, 12.5f, 13f],
        [9.5f, 10f, 10.5f, 11f, 11.5f, 12f, 12.5f, 13f, 13.5f],
        [9f, 9.5f, 10f, 10.5f, 11f, 11.5f, 12f, 12.5f, 13f, 13.5f, 14f, 16f],
        [12.5f, 13.5f, 14f, 15f, 17f, 20f, 26f],
        [10.5f, 11f, 11.5f],
        [8.5f, 9.5f, 10f, 10.5f, 11f, 12f],
    ];

    private static readonly Dictionary<string, string> UpperCache = new(StringComparer.Ordinal);

    private static NoireFont?[]? faces;
    private static float textScale = 1f;
    private static bool failed;

    public static float TextScale
    {
        get => textScale;
        set
        {
            var clamped = Math.Clamp(value, 0.5f, 2f);

            if (MathF.Abs(clamped - textScale) < 0.0001f)
                return;

            textScale = clamped;

            ReleaseFaces();
            Prewarm();
        }
    }

    public static float Em(float cssPx) => cssPx * textScale;

    public static NoireFont? Face(SilkFace face)
    {
        var all = Load();
        return all == null ? null : all[Slots[(int)face]];
    }

    public static bool IsSynthetic(SilkFace face) => face == SilkFace.MonoBold;

    private static float Smear(SilkFace face, float cssPx)
        => IsSynthetic(face) ? NoireFont.SyntheticBoldPixels(Em(cssPx)) : 0f;

    public static void Prewarm()
    {
        var all = Load();

        if (all == null)
            return;

        Span<float> sizes = stackalloc float[16];

        for (var index = 0; index < all.Length; index++)
        {
            if (all[index] is not { } face)
                continue;

            var used = UsedSizes[index];


            for (var at = 0; at < used.Length; at++)
                sizes[at] = used[at] * textScale;

            face.Request(sizes[..used.Length]);
        }
    }

    public static NoireFontScope Push(SilkFace face, float cssPx)
        => Face(face) is { } font ? font.Push(Em(cssPx)) : default;

    public static Vector2 Measure(SilkFace face, float cssPx, string text, float letterSpacingPx = 0f, float maxWidth = 0f)
    {
        using var profile = SilkProfile.Detail("SilkFonts.Measure");

        if (Face(face) is { } font)
            return font.CalcSize(text, Em(cssPx), letterSpacingPx, maxWidth);

        return string.IsNullOrEmpty(text) ? Vector2.Zero : ImGui.CalcTextSize(text);
    }

    public static float FittedWidth(SilkFace face, float cssPx, string text, float room, float letterSpacingPx = 0f)
        => MathF.Min(Measure(face, cssPx, text, letterSpacingPx).X, MathF.Max(1f, room));

    public static Vector2 DrawFitted(ImDrawListPtr drawList, Vector2 position, uint color, SilkFace face, float cssPx, string text, float room,
        float letterSpacingPx = 0f)
    {
        room = MathF.Max(1f, room);

        if (Measure(face, cssPx, text, letterSpacingPx).X <= room + 0.5f)
            return Draw(drawList, position, color, face, cssPx, text, letterSpacingPx);

        var size = Draw(drawList, position, color, face, cssPx, text, letterSpacingPx, room);
        var max = position + new Vector2(room, LineHeight(face, cssPx));

        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(position, max))
            SilkTooltip.Hover(text, position, max);

        return size;
    }

    public static bool Truncates(SilkFace face, float cssPx, string text, float maxWidth, float letterSpacingPx = 0f)
        => Face(face) is { } font && font.IsTruncated(text, Em(cssPx), letterSpacingPx, maxWidth);

    public static Vector2 Draw(ImDrawListPtr drawList, Vector2 position, Vector4 color, SilkFace face, float cssPx, string text, float letterSpacingPx = 0f, float maxWidth = 0f)
        => Draw(drawList, position, ImGui.ColorConvertFloat4ToU32(color), face, cssPx, text, letterSpacingPx, maxWidth);

    public static Vector2 Draw(ImDrawListPtr drawList, Vector2 position, uint color, SilkFace face, float cssPx, string text, float letterSpacingPx = 0f, float maxWidth = 0f)
    {
        using var profile = SilkProfile.Detail("SilkFonts.Draw");

        if (Face(face) is { } font)
            return font.Draw(drawList, position, color, text, Em(cssPx), letterSpacingPx, maxWidth, Smear(face, cssPx));

        if (drawList.IsNull || string.IsNullOrEmpty(text))
            return Vector2.Zero;

        drawList.AddText(position, color, text);
        return ImGui.CalcTextSize(text);
    }

    public static Vector2 DrawInLine(ImDrawListPtr drawList, Vector2 lineTop, uint color, SilkFace face, float cssPx, float lineHeight, string text, float letterSpacingPx = 0f, float maxWidth = 0f)
        => Draw(drawList, lineTop + new Vector2(0f, TextTop(face, cssPx, lineHeight)), color, face, cssPx, text, letterSpacingPx, maxWidth);

    public static Vector2 Text(Vector4 color, SilkFace face, float cssPx, string text, float letterSpacingPx = 0f, float maxWidth = 0f)
    {
        if (Face(face) is { } font)
            return font.Text(color, text, Em(cssPx), letterSpacingPx, maxWidth, Smear(face, cssPx));

        ImGui.TextColored(color, text);
        return ImGui.GetItemRectSize();
    }

    public static float LineHeight(SilkFace face, float cssPx)
        => Face(face) is { } font ? font.LineHeight(Em(cssPx)) : ImGui.GetTextLineHeight();

    public static float LineBox(float cssPx, float lineHeight)
        => Em(cssPx) * lineHeight * NoireUI.Scale;

    public static float TextTop(SilkFace face, float cssPx, float lineHeight)
        => Face(face) is { } font ? font.HalfLeading(Em(cssPx), lineHeight) : (LineBox(cssPx, lineHeight) - ImGui.GetTextLineHeight()) * 0.5f;

    public static float Ascent(SilkFace face, float cssPx)
        => Face(face) is { } font ? font.Ascent(Em(cssPx)) : ImGui.GetFontSize() * 0.8f;

    public static float BodyLine => LineBox(BodyPx, BodyLineHeight);

    public static string Upper(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (UpperCache.TryGetValue(text, out var upper))
            return upper;

        if (UpperCache.Count > 2048)
            UpperCache.Clear();

        upper = text.ToUpperInvariant();
        UpperCache[text] = upper;

        return upper;
    }

    private static NoireFont?[]? Load()
    {
        if (faces != null || failed)
            return faces;

        var assembly = typeof(SilkFonts).Assembly;
        var loaded = new NoireFont?[Files.Length];

        for (var index = 0; index < Files.Length; index++)
        {
            try
            {
                loaded[index] = NoireFont.FromManifestResource(assembly, "BypassEmote.Silk.Fonts." + Files[index]);
            }
            catch (Exception ex)
            {
                NoireLib.NoireLogger.LogError(ex, $"Could not load the {Files[index]} font.", nameof(SilkFonts));
            }
        }

        if (Array.TrueForAll(loaded, f => f == null))
        {
            failed = true;
            return null;
        }

        faces = loaded;
        return faces;
    }

    public static void Dispose()
    {
        ReleaseFaces();
        UpperCache.Clear();
    }

    private static void ReleaseFaces()
    {
        if (faces != null)
        {
            foreach (var face in faces)
                face?.Dispose();
        }

        faces = null;
        failed = false;
    }
}
