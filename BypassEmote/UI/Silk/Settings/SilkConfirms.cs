using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI.Silk.Settings;

internal static class SilkConfirms
{
    internal static string SyncServicesLine => L.SyncServicesLine.Text;

    internal static string SafeModeLimitLine => L.SafeModeLimitLine.Text;

    internal static string SafeModeIsNotAPromiseLine => L.SilkSafeModeNotAPromise.Text;

    internal static string UnsafeHeadline => L.UnsafeHeadline.Text;

    internal static string UnsafeReassurance => L.SilkUnsafeReassurance.Text;

    internal static string SafeDirectPlayTooltip => L.SafeDirectPlayTooltip.With("limit", SafeModeLimitLine);

    internal static string UnsafeDirectPlayTooltip => L.UnsafeDirectPlayTooltip.Text;

    private const float CountdownSeconds = 5f;

    private static readonly SilkParagraphList UnsafeWithSync = new(
        (L.UnsafeHeadline, SilkParagraphTone.Warn),
        (L.SilkUnsafeReassurance, SilkParagraphTone.Body),
        (L.SyncServicesLine, SilkParagraphTone.Body));

    private static readonly SilkParagraphList UnsafeWithoutSync = new(
        (L.UnsafeHeadline, SilkParagraphTone.Warn),
        (L.SilkUnsafeReassurance, SilkParagraphTone.Body));

    private static readonly SilkParagraphList SafeSwitch = new(
        (L.SyncServicesLine, SilkParagraphTone.Body),
        (L.SafeModeLimitLine, SilkParagraphTone.Body),
        (L.SilkSafeModeNotAPromise, SilkParagraphTone.Muted));

    private static ModalOptions? liveOptions;
    private static SilkParagraph[]? liveBody;
    private static SilkSettingsPainter? owner;
    private static bool confirmingUnsafe;
    private static float shownSince = -10f;
    private static bool showing;

    internal static bool Presenting(SilkSettingsPainter window) => Active(window) != null;

    internal static async Task SwitchToDirectPlayAsync(SilkSettingsPainter window, bool liftsTheLimit, bool preview)
    {
        var options = new ModalOptions
        {
            ConfirmLabel = L.SwitchToDirectPlay.Text,
            CancelLabel = L.Cancel.Text,
            Danger = liftsTheLimit,
            EnableAfterSeconds = liftsTheLimit ? CountdownSeconds : 0f,
            CustomDraw = true,
        };

        Claim(window, options, liftsTheLimit ? UnsafeWithSync.Array : SafeSwitch.Array);

        var confirmed = await NoireModal.ConfirmAsync(L.SwitchToDirectPlayTitle.Text, liftsTheLimit ? UnsafeWarningContent(true) : SafeContent(), options);

        if (confirmed && !preview)
            ModeSwitcher.Apply(SelfBypassMode.DirectPlay);
    }

    internal static async Task EnableUnsafeAsync(SilkSettingsPainter window, bool preview)
    {
        if (confirmingUnsafe)
            return;

        confirmingUnsafe = true;

        try
        {
            var options = new ModalOptions
            {
                ConfirmLabel = L.EnableUnsafe.Text,
                CancelLabel = L.Cancel.Text,
                Danger = true,
                EnableAfterSeconds = CountdownSeconds,
                CustomDraw = true,
            };

            Claim(window, options, UnsafeWithoutSync.Array);

            var confirmed = await NoireModal.ConfirmAsync(L.EnableUnsafeTitle.Text, UnsafeWarningContent(false), options);

            if (!confirmed || preview)
                return;

            await AsyncHelper.RunOnFrameworkThreadAsync(static () => Configuration.DirectPlayUnsafe = true);
        }
        finally
        {
            confirmingUnsafe = false;
        }
    }

    internal static void CancelMine()
    {
        if (NoireModal.Active is { } view && liveOptions != null && ReferenceEquals(view.Options, liveOptions))
            view.Cancel();
    }

    internal static void Present(SilkSettingsPainter window, Vector2 min, Vector2 max)
    {
        var view = Active(window);

        if (view == null || liveBody == null)
        {
            showing = false;
            return;
        }

        view.MarkPresented();

        if (!showing)
        {
            showing = true;
            shownSince = SilkUi.Time;
        }

        var scale = SilkUi.Scale;
        var t = SilkUi.ReducedMotion ? 1f : Math.Clamp((SilkUi.Time - shownSince) / 0.2f, 0f, 1f);
        var box = SilkUi.ReducedMotion ? 1f : UiCubicBezier.Ease.Evaluate(Math.Clamp((SilkUi.Time - shownSince) / 0.3f, 0f, 1f));

        SilkPaint.Fill(min, max, SilkPalette.Rgb(2, 4, 9, 0.62f * t), SilkChrome.Radius * scale);

        var width = MathF.Min(470f * scale, (max.X - min.X) * 0.86f);
        var inner = width - (40f * scale);
        var titleLine = SilkText.NaturalLine(17f, SilkWeight.ExtraBold);
        var bodyHeight = 0f;

        for (var i = 0; i < liveBody.Length; i++)
        {
            bodyHeight += SilkText.ParagraphHeight(liveBody[i].Text, 12.5f, BodyWeight(liveBody[i].Tone), inner, 1.55f);

            if (i > 0)
                bodyHeight += 9f * scale;
        }

        var buttonHeight = 34f * scale;
        var height = (20f * scale) + titleLine + (10f * scale) + bodyHeight + (18f * scale) + buttonHeight + (16f * scale);
        var shrink = 0.98f + (0.02f * box);
        var centre = new Vector2((min.X + max.X) * 0.5f, ((min.Y + max.Y) * 0.5f) + ((1f - box) * 8f * scale));
        var half = new Vector2(width * shrink * 0.5f, height * shrink * 0.5f);
        var boxMin = new Vector2(MathF.Round(centre.X - half.X), MathF.Round(centre.Y - half.Y));
        var boxMax = new Vector2(MathF.Round(centre.X + half.X), MathF.Round(centre.Y + half.Y));
        var radius = 14f * scale;
        var danger = view.Options.Danger;

        SilkPaint.PushClipExpanded(min, max, 0f);
        SilkSettingsKit.OuterShadow(boxMin, boxMax, new Vector2(0f, 30f * scale), 80f * scale, -10f * scale, SilkPalette.Rgb(0, 0, 0, 0.95f * t), radius);
        SilkPaint.PopClip();
        SilkPaint.Fill(boxMin, boxMax, SilkPalette.Alpha(SilkPalette.Popup, t), radius);
        SilkPaint.OuterRing(boxMin, boxMax, danger ? SilkPalette.Rgb(255, 111, 134, 0.45f * t) : SilkPalette.Fade(SilkPalette.Line2, t), radius, scale);

        var x = boxMin.X + (20f * scale);
        var y = boxMin.Y + (20f * scale);

        SilkText.DrawInBox(new Vector2(x, y), new Vector2(x + inner, y + titleLine), view.Title, 17f, SilkWeight.ExtraBold, SilkPalette.Alpha(SilkPalette.Ink, t), UiAlign.Start, -0.2f);
        y += titleLine + (10f * scale);

        for (var i = 0; i < liveBody.Length; i++)
        {
            if (i > 0)
                y += 9f * scale;

            var tone = liveBody[i].Tone;
            var color = tone switch
            {
                SilkParagraphTone.Warn => SilkPalette.Bad,
                SilkParagraphTone.Muted => SilkPalette.Ink3,
                _ => SilkPalette.Ink2,
            };

            y += SilkText.DrawParagraph(new Vector2(x, y), liveBody[i].Text, 12.5f, BodyWeight(tone), SilkPalette.Alpha(color, t), inner, 1.55f);
        }

        var confirmLabel = view.ConfirmLabel;
        var cancelLabel = view.CancelLabel;
        var confirmWidth = ButtonWidth(confirmLabel);
        var cancelWidth = cancelLabel.Length > 0 ? ButtonWidth(cancelLabel) : 0f;
        var buttonsY = boxMax.Y - (16f * scale) - buttonHeight;
        var right = boxMax.X - (20f * scale);
        var enabled = view.CanConfirm;

        if (cancelLabel.Length > 0)
        {
            var cancelMin = new Vector2(right - confirmWidth - (8f * scale) - cancelWidth, buttonsY);
            var cancelMax = cancelMin + new Vector2(cancelWidth, buttonHeight);

            if (SilkSettingsKit.Hit("##silkmodalcancel", cancelMin, cancelMax, out var cancelHovered))
                view.Cancel();

            if (cancelHovered)
                SilkPaint.Fill(cancelMin, cancelMax, SilkPalette.Fade(SilkPalette.HoverWash, t), 9f * scale);

            SilkPaint.InsetRing(cancelMin, cancelMax, SilkPalette.Fade(SilkPalette.Line2, t), 9f * scale, scale);
            SilkText.DrawInBox(cancelMin, cancelMax, cancelLabel, 12.5f, SilkWeight.Bold, SilkPalette.Alpha(cancelHovered ? SilkPalette.Ink : SilkPalette.Ink2, t), UiAlign.Center);
        }

        var okMin = new Vector2(right - confirmWidth, buttonsY);
        var okMax = okMin + new Vector2(confirmWidth, buttonHeight);
        var okHovered = false;

        if (SilkSettingsKit.Hit("##silkmodalok", okMin, okMax, out okHovered) && enabled)
            view.Confirm();

        var fade = (enabled ? 1f : 0.45f) * t;
        var fill = danger ? SilkPalette.Bad : okHovered && enabled ? SilkPalette.ModalOkHover : SilkPalette.Ice;

        SilkPaint.Fill(okMin, okMax, SilkPalette.Fade(fill, fade), 9f * scale);
        SilkText.DrawInBox(okMin, okMax, confirmLabel, 12.5f, SilkWeight.Bold, SilkPalette.Fade(danger ? SilkPalette.White : SilkPalette.PriText, fade), UiAlign.Center);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            view.Cancel();
    }

    private static float ButtonWidth(string label) => MathF.Ceiling(SilkText.Width(label, 12.5f, SilkWeight.Bold) + (28f * SilkUi.Scale));

    private static SilkWeight BodyWeight(SilkParagraphTone tone) => tone == SilkParagraphTone.Warn ? SilkWeight.Bold : SilkWeight.Medium;

    private static NoireModalView? Active(SilkSettingsPainter window)
    {
        if (!ReferenceEquals(owner, window) || liveOptions == null)
            return null;

        var view = NoireModal.Active;
        return view != null && ReferenceEquals(view.Options, liveOptions) ? view : null;
    }

    private static void Claim(SilkSettingsPainter window, ModalOptions options, SilkParagraph[] body)
    {
        owner = window;
        liveOptions = options;
        liveBody = body;
        showing = false;
    }

    private static NoireContent UnsafeWarningContent(bool withSync)
    {
        var danger = NoireTheme.Current.Resolve(ThemeColor.Danger);

        var content = new NoireContent()
            .AddIcon(FontAwesomeIcon.ExclamationTriangle, danger)
            .AddSpacing(6f)
            .AddText(UnsafeHeadline, danger)
            .AddNewLine()
            .AddNewLine()
            .AddText(UnsafeReassurance);

        if (withSync)
            content.AddNewLine().AddNewLine().AddText(SyncServicesLine);

        return content;
    }

    private static NoireContent SafeContent()
        => new NoireContent()
            .AddText(SyncServicesLine)
            .AddNewLine()
            .AddNewLine()
            .AddText(SafeModeLimitLine)
            .AddNewLine()
            .AddNewLine()
            .AddText(SafeModeIsNotAPromiseLine, NoireTheme.Current.Resolve(ThemeColor.TextMuted));
}
