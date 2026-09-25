using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed class SilkPlayPill
{
    private const float Life = 2.6f;

    private uint sourceIcon;
    private uint targetIcon;
    private bool swapped;
    private Vector3 accent = SilkVivid.Ice;
    private float shownAt = -100f;
    private Tween presence;

    internal void Show(SilkEmoteEntry entry, bool wasSwapped, uint target, Vector3 color, float now)
    {
        sourceIcon = entry.IconId;
        targetIcon = target;
        swapped = wasSwapped;
        accent = color;
        shownAt = now;
        presence.Snap(0f);
        presence.Go(1f, now, SilkUi.ReducedMotion ? 0f : 0.4f, SilkUi.EaseOut);
    }

    internal void Draw(ImDrawListPtr dl, Vector2 anchor, float s, float now, bool reduced)
    {
        if (now - shownAt > Life + 0.5f)
            return;

        if (now - shownAt >= Life && presence.Target > 0f)
            presence.Go(0f, now, reduced ? 0f : 0.4f, SilkUi.EaseOut);

        var p = presence.Value(now);

        if (p <= 0.001f)
            return;

        var label = L.EmotePlayed.Text;
        var swappedLabel = L.EmoteSwapped.Text;
        var text = swapped ? swappedLabel : label;
        var textW = SilkFonts.Measure(SilkFace.Ui700, 12.5f, text).X;
        var inner = 24f * s + 10f * s;

        if (swapped)
            inner += 14f * s + 10f * s + 24f * s + 10f * s;

        var width = 8f * s + inner + textW + 14f * s;
        var height = 36f * s;
        var cx = anchor.X;
        var bottom = anchor.Y + (1f - p) * 8f * s;
        var min = new Vector2(MathF.Round(cx - width * 0.5f), MathF.Round(bottom - height));
        var max = min + new Vector2(width, height);
        var radius = height * 0.5f;

        var start = dl.VtxBuffer.Size;
        SilkMainDraw.Shadow(dl, min, max, radius, SilkMainDraw.Col(0, 0, 0, 0.9f), 40f * s, -10f * s, new Vector2(0f, 14f * s));
        SilkMainDraw.Shadow(dl, min, max, radius, SilkMainDraw.Col(accent, 0.7f), 30f * s, -8f * s);
        dl.AddRectFilled(min, max, SilkMainDraw.Col(10, 15, 26, 0.96f), radius);
        SilkMainDraw.OuterRing(dl, min, max, radius, SilkMainDraw.Col(accent, 0.45f));

        var x = min.X + 8f * s;
        var iconTop = min.Y + 6f * s;
        SilkGameIcon.Draw(dl, sourceIcon, new Vector2(x, iconTop), new Vector2(x + 24f * s, iconTop + 24f * s), 7f * s);
        x += 24f * s + 10f * s;

        if (swapped)
        {
            SilkIcons.Draw(dl, SilkMainIcons.Arrow, new Vector2(x, min.Y + 11f * s), 14f * s, SilkMainDraw.Col(accent));
            x += 14f * s + 10f * s;
            SilkGameIcon.Draw(dl, targetIcon, new Vector2(x, iconTop), new Vector2(x + 24f * s, iconTop + 24f * s), 7f * s);
            x += 24f * s + 10f * s;
        }

        var lh = SilkFonts.LineHeight(SilkFace.Ui700, 12.5f);
        SilkFonts.Draw(dl, new Vector2(MathF.Round(x), MathF.Round(min.Y + (height - lh) * 0.5f)), SilkPalette.U32(SilkPalette.Ink), SilkFace.Ui700, 12.5f, text);

        var progress = Math.Clamp((now - shownAt) / Life, 0f, 1f);

        if (progress > 0f)
        {
            var lineMin = new Vector2(min.X + 14f * s, max.Y - 1f * s);
            var full = width - 28f * s;
            dl.AddRectFilled(lineMin, lineMin + new Vector2(full * progress, 2f * s), SilkMainDraw.Col(accent), 2f * s * MathF.Min(1f, progress * 20f));
        }

        SilkMainDraw.MultiplyAlpha(dl, start, p);
    }
}
