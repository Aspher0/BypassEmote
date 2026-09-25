using BypassEmote.UI.Skins;
using NoireLib.UI;
using System;

namespace BypassEmote.UI.Silk;

public static class SilkUi
{
    public static readonly UiCubicBezier EaseOut = new(0.22f, 1f, 0.36f, 1f);
    public static readonly UiCubicBezier EaseBack = new(0.34f, 1.56f, 0.64f, 1f);
    public static readonly UiCubicBezier EaseCat = new(0.34f, 1.3f, 0.5f, 1f);

    private static readonly WindowMenuSettings Defaults = new();

    private static NoireSkinnedWindowBase? entered;

    public static NoireSkinnedWindowBase? Window => entered ?? NoireSkinnedWindowBase.Current;

    public static WindowMenuSettings Settings => Window?.Options ?? Defaults;

    public static bool Active => BeSkins.SilkActive;

    public static float TextScale => StepScale(Settings.TextStep);

    private static WindowMenuSettings? sharedOptions;

    private static float StepScale(int step) => SilkPalette.TextSteps[Math.Clamp(step, 0, SilkPalette.TextSteps.Length - 1)];

    public static void WarmFonts(WindowMenuSettings? options = null)
    {
        sharedOptions = options ?? sharedOptions;

        if (sharedOptions == null || !BeSkins.SilkActive)
            return;

        SilkFonts.TextScale = StepScale(sharedOptions.TextStep);
        SilkFonts.Prewarm();
    }

    public static float Opacity => Math.Clamp(Settings.Opacity, 0.2f, 1f);

    public static bool ReducedMotion => Settings.ReducedMotion || NoireUI.HostReducedMotion;

    public static float Time => NoireUI.Time;

    public static float Scale => NoireUI.Scale;

    public static float Px(float css) => css * NoireUI.Scale;

    public static float FontPx(float css) => css * TextScale * NoireUI.Scale;

    public static void SyncTextScale()
    {
        if (MathF.Abs(SilkFonts.TextScale - TextScale) > 0.0001f)
            SilkFonts.TextScale = TextScale;
    }

    public static float Ease(string id, string subKey, float target, float seconds)
        => NoireAnim.Ease(id, subKey, target, ReducedMotion ? 0f : seconds, EaseOut.Curve);

    public static float Ease(string id, string subKey, float target, float seconds, UiCubicBezier curve)
        => NoireAnim.Ease(id, subKey, target, ReducedMotion ? 0f : seconds, curve.Curve);

    public static float EaseLinear(string id, string subKey, float target, float seconds)
        => NoireAnim.Ease(id, subKey, target, ReducedMotion ? 0f : seconds, UiEasing.Linear);

    public static float Css(string id, string subKey, float target, float seconds)
        => NoireAnim.Ease(id, subKey, target, ReducedMotion ? 0f : seconds, UiCubicBezier.Ease.Curve);

    internal static void Enter(NoireSkinnedWindowBase window)
    {
        entered = window;
        SyncTextScale();
    }

    internal static void Leave() => entered = null;
}
