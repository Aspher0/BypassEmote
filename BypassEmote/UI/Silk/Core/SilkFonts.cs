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

    private sealed class Tier(float scale, float uiScale, NoireFontSet set)
    {
        public float Scale { get; } = scale;

        public float UiScale { get; } = uiScale;

        public NoireFontSet Set { get; } = set;
    }

    private static readonly List<Tier> Tiers = [];
    private static readonly List<NoireFontSet> Sets = [];
    private static float prebuiltFor = float.NaN;
    private static float droppedFor = float.NaN;

    private static NoireFont?[]? faces;
    private static Tier? shown;
    private static float wanted = 1f;
    private static bool failed;

    public static float TextScale => shown?.Scale ?? wanted;

    public static bool Ready => shown != null;

    public static void Want(float scale)
    {
        wanted = Math.Clamp(scale, 0.5f, 2f);

        if (Load() is not { } all)
            return;

        var uiScale = NoireUI.Scale;
        var tier = Find(wanted, uiScale) ?? Create(all, [wanted], uiScale, $"Silk {wanted * 100f:0}%");

        if (prebuiltFor != uiScale)
        {
            prebuiltFor = uiScale;
            PrebuildSteps(all, uiScale);
        }

        if (!ReferenceEquals(tier, shown) && tier.Set.IsBuilt)
            shown = tier;

        if (shown is not { } current || current.UiScale != uiScale)
            return;

        if (droppedFor != uiScale)
        {
            droppedFor = uiScale;
            DropOtherUiScales(uiScale);
        }

        if (NoireScriptFonts.CurrentLanguageOnly && Tiers.TrueForAll(t => t.Set.IsBuilt))
            NoireScriptFonts.CurrentLanguageOnly = false;
    }

    public static float Em(float cssPx) => cssPx * TextScale;

    public static NoireFont? Face(SilkFace face)
    {
        var all = Load();
        return all == null ? null : all[Slots[(int)face]];
    }

    public static bool IsSynthetic(SilkFace face) => face == SilkFace.MonoBold;

    private static float Smear(SilkFace face, float cssPx)
        => IsSynthetic(face) ? NoireFont.SyntheticBoldPixels(Em(cssPx)) : 0f;

    private static Tier? Find(float scale, float uiScale)
    {
        foreach (var tier in Tiers)
        {
            if (MathF.Abs(tier.Scale - scale) < 0.0001f && MathF.Abs(tier.UiScale - uiScale) < 0.0001f)
                return tier;
        }

        return null;
    }

    private static Tier Create(NoireFont?[] all, List<float> scales, float uiScale, string name)
    {
        var set = new NoireFontSet(name);
        Span<float> sizes = stackalloc float[16];

        foreach (var scale in scales)
        {
            for (var index = 0; index < all.Length; index++)
            {
                if (all[index] is not { } face)
                    continue;

                var used = UsedSizes[index];

                for (var at = 0; at < used.Length; at++)
                    sizes[at] = used[at] * scale;

                set.Add(face, sizes[..used.Length]);
            }
        }

        set.Build();
        Sets.Add(set);

        Tier? first = null;

        foreach (var scale in scales)
        {
            var tier = new Tier(scale, uiScale, set);
            Tiers.Add(tier);
            first ??= tier;
        }

        return first!;
    }

    private static void PrebuildSteps(NoireFont?[] all, float uiScale)
    {
        var missing = new List<float>();

        foreach (var step in SilkPalette.TextSteps)
        {
            if (Find(step, uiScale) == null)
                missing.Add(step);
        }

        if (missing.Count > 0)
            Create(all, missing, uiScale, "Silk other sizes");
    }

    private static void DropOtherUiScales(float uiScale)
    {
        Tiers.RemoveAll(tier => MathF.Abs(tier.UiScale - uiScale) >= 0.0001f && !ReferenceEquals(tier, shown));

        for (var index = Sets.Count - 1; index >= 0; index--)
        {
            var set = Sets[index];

            if (Tiers.Exists(tier => ReferenceEquals(tier.Set, set)))
                continue;

            set.Dispose();
            Sets.RemoveAt(index);
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
        foreach (var set in Sets)
            set.Dispose();

        Sets.Clear();
        Tiers.Clear();
        shown = null;
        prebuiltFor = float.NaN;
        droppedFor = float.NaN;

        if (faces != null)
        {
            foreach (var face in faces)
                face?.Dispose();
        }

        faces = null;
        failed = false;
    }
}
