using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public sealed class SilkBackdrop
{
    public const float SettingsQuiet = 0.55f;
    public const float ChangelogQuiet = 0.5f;
    public const float SecondaryQuiet = 0.8f;
    public const float GroupExtension = 330f;
    public const float MaxDrift = 0.5f;

    public static readonly Ribbon[] Ribbons =
    [
        new(0.18f, 0.06f, 5f, 0.35f, 0f, 0.03f, Rgb(158, 140, 255), 0.16f),
        new(0.36f, 0.08f, 4f, 0.28f, 2f, 0.05f, Rgb(191, 211, 236), 0.15f),
        new(0.55f, 0.07f, 6f, 0.22f, 4f, 0.035f, Rgb(120, 170, 255), 0.16f),
        new(0.72f, 0.06f, 3.5f, 0.3f, 1f, 0.045f, Rgb(191, 211, 236), 0.12f),
        new(0.88f, 0.05f, 5.5f, 0.25f, 3f, 0.03f, Rgb(158, 140, 255), 0.13f),
    ];

    private readonly bool group;

    private SilkBackdrop(RibbonFieldOptions options, bool group)
    {
        this.group = group;
        Field = new NoireRibbonField(options);
    }

    public NoireRibbonField Field { get; }

    public bool IsGroup => group;

    public bool Frozen
    {
        get => Field.Frozen;
        set => Field.Frozen = value;
    }

    public static SilkBackdrop CreateGroup()
    {
        var options = new RibbonFieldOptions
        {
            Ribbons = Ribbons,
            Samples = 90,
            MaxDriftPixels = MaxDrift,
            Overscan = 0f,
            PrimaryFrequencyScale = 1.6f,
            SecondaryFrequencyScale = 3.7f,
            ThicknessFrequency = 5f,
            LeanSpace = RibbonLeanSpace.Screen,
            LeanSharpness = 60f,
            EdgeAlpha = 0.25f,
            FadeAlpha = 0.85f,
            VignetteInnerRadius = 0.3f,
            VignetteOuterRadius = 0.62f,
            VignetteAlpha = 0.55f,
        };

        return new SilkBackdrop(options, true);
    }

    public static SilkBackdrop CreateSingle(float quiet = 1f)
    {
        var ribbons = new Ribbon[Ribbons.Length];

        for (var i = 0; i < ribbons.Length; i++)
            ribbons[i] = Ribbons[i].Quieter(quiet);

        return new SilkBackdrop(new RibbonFieldOptions { Ribbons = ribbons, MaxDriftPixels = MaxDrift }, false);
    }

    public void Tick(Vector2 min, Vector2 max, Vector2? mouse)
    {
        if (group)
        {
            var extension = new Vector2(GroupExtension * NoireUI.Scale, 0f);
            Field.Update(min - extension, max + extension, mouse);
        }
        else
        {
            Field.Update(min, max, mouse);
        }
    }

    public void Tick(Vector2 min, Vector2 max)
    {
        Vector2? mouse = null;

        if (ImGui.IsMousePosValid())
        {
            var position = ImGui.GetMousePos();

            if (position.X >= min.X && position.Y >= min.Y && position.X < max.X && position.Y < max.Y)
                mouse = position;
        }

        Tick(min, max, mouse);
    }

    public void Paint(Vector2 min, Vector2 max, float rounding, float opacity)
        => Field.Draw(NoireShapes.DrawList, min, max, rounding, Math.Clamp(opacity, 0f, 1f));

    public void Paint(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float opacity)
        => Field.Draw(drawList, min, max, rounding, Math.Clamp(opacity, 0f, 1f));

    public void Lean(Vector4? color) => Field.Lean(color);

    public void Wave(float? screenX = null, Vector4? color = null) => Field.Wave(screenX, color);

    private static Vector3 Rgb(int r, int g, int b) => new Vector3(r, g, b) / 255f;
}
