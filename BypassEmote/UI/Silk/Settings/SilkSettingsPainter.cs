using BypassEmote.Localization;
using BypassEmote.UI.Skins;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Settings;

internal sealed partial class SilkSettingsPainter
{
    private static readonly TextList TabLabels = new(L.PageGeneral, L.PageBypassMode, L.PageOverrides);
    private static readonly string[] TabIds = ["##silktab0", "##silktab1", "##silktab2"];
    private static readonly string[] TabHoverKeys = ["tabhover0", "tabhover1", "tabhover2"];
    private static readonly SilkIcon[] TabIcons = [SilkIcon.Sliders, SilkIcon.Swap, SilkIcon.Overrides];

    private readonly SilkScrollArea[] scrolls = [new(), new(), new()];

    private SettingsPage page = SettingsPage.General;
    private float pageSince = -10f;
    private bool pagePending;

    private readonly SettingsWindow window;
    private readonly string animKey;

    internal SilkSettingsPainter(SettingsWindow window)
    {
        this.window = window;
        animKey = "SilkWindow." + window.Id;
    }

    private string AnimationKey => animKey;

    private SilkBackdrop Backdrop => BeSkins.Silk.ChromeOf(window).Backdrop;

    public SettingsPage Page
    {
        get => page;
        set
        {
            if (page == value)
                return;

            page = value;
            pageSince = SilkUi.Time;
            scrolls[(int)value].Snap();
            pagePending = true;
        }
    }

    public uint? PendingOverrideSource { get; set; }

    public DateTime UnsafeAttentionUntil
    {
        get => window.UnsafeAttentionUntil;
        set => window.UnsafeAttentionUntil = value;
    }

    public void SwitchToOverrides(uint sourceRowId)
    {
        PendingOverrideSource = sourceRowId;
        ShowOverrideFor(sourceRowId);
        Page = SettingsPage.Overrides;
    }

    private void TakeRequests()
    {
        if (window.TakeRequestedOverride() is { } source)
            SwitchToOverrides(source);

        if (window.TakeRequestedPage() is { } requested)
            Page = requested;
    }

    internal void DrawBody(Vector2 min, Vector2 max)
    {
        TakeRequests();

        var scale = SilkUi.Scale;
        var blocked = SilkConfirms.Presenting(this);

        if (pagePending)
        {
            pagePending = false;
            pageSince = SilkUi.Time;
            CloseOverlays();

            if (!SilkUi.ReducedMotion)
                Backdrop.Wave();
        }

        if (blocked)
            ImGui.BeginDisabled();

        try
        {
            var top = min.Y;

            if (GateVisible)
            {
                var gateHeight = DrawGate(new Vector2(min.X + (14f * scale), top + (6f * scale)), max.X - min.X - (28f * scale));
                top += (12f * scale) + gateHeight;
            }

            var tabsMin = new Vector2(min.X + (14f * scale), top + (4f * scale));
            var tabsMax = new Vector2(max.X - (14f * scale), tabsMin.Y + (44f * scale));
            DrawTabs(tabsMin, tabsMax);

            DrawPage(new Vector2(min.X, tabsMax.Y), max, blocked);
        }
        finally
        {
            if (blocked)
                ImGui.EndDisabled();
        }
    }

    internal void DrawOverlay(Vector2 min, Vector2 max) => SilkConfirms.Present(this, min, max);

    private void DrawTabs(Vector2 min, Vector2 max)
    {
        var scale = SilkUi.Scale;
        var radius = 12f * scale;

        SilkPaint.Fill(min, max, SilkPalette.Rgb(0, 0, 0, 0.3f), radius);
        SilkPaint.InsetRing(min, max, SilkPalette.Line2, radius, scale);

        var pad = 4f * scale;
        var gap = 4f * scale;
        var width = (max.X - min.X - (pad * 2f) - (gap * 2f)) / 3f;
        var height = 36f * scale;
        var index = (int)page;

        var pillLeft = SilkUi.Ease(AnimationKey, "tabpill", pad + ((width + gap) * index), 0.45f);
        var pillMin = new Vector2(min.X + pillLeft, min.Y + pad);
        var pillMax = pillMin + new Vector2(width, height);
        var pillRadius = 9f * scale;

        SilkPaint.PushClipExpanded(min, max, 30f * scale);
        SilkSettingsKit.OuterShadow(pillMin, pillMax, new Vector2(0f, 8f * scale), 22f * scale, -10f * scale, SilkPalette.Ice, pillRadius);
        SilkPaint.PopClip();
        SilkPaint.VerticalGradient(pillMin, pillMax, SilkPalette.Rgb(191, 211, 236, 0.2f), SilkPalette.Rgb(191, 211, 236, 0.08f), pillRadius);
        SilkPaint.InsetRing(pillMin, pillMax, SilkPalette.Rgb(191, 211, 236, 0.4f), pillRadius, scale);

        for (var i = 0; i < 3; i++)
        {
            var bMin = new Vector2(min.X + pad + ((width + gap) * i), min.Y + pad);
            var bMax = bMin + new Vector2(width, height);

            if (SilkSettingsKit.Hit(TabIds[i], bMin, bMax, out var hovered) && i != index)
                Page = (SettingsPage)i;

            var on = SilkUi.Css(AnimationKey, TabIds[i], i == (int)page ? 1f : 0f, 0.25f);
            var hover = SilkUi.Css(AnimationKey, TabHoverKeys[i], hovered ? 1f : 0f, 0.25f);
            var color = SilkPalette.Mix(SilkPalette.Mix(SilkPalette.Ink3, SilkPalette.Ink2, hover), SilkPalette.Ink, on);

            var icon = 14f * scale;
            var room = MathF.Max(1f, width - icon - (28f * scale));
            var fullWidth = SilkText.Width(TabLabels[i], 13f, SilkWeight.SemiBold);
            var textWidth = MathF.Min(fullWidth, room);
            var content = icon + (8f * scale) + textWidth;
            var x = MathF.Round(bMin.X + ((width - content) * 0.5f));
            var textMin = new Vector2(x + icon + (8f * scale), bMin.Y);

            SilkIcons.Draw(TabIcons[i], new Vector2(x, MathF.Round(((bMin.Y + bMax.Y) * 0.5f) - (icon * 0.5f))), icon, color);

            if (fullWidth <= room)
            {
                SilkText.DrawInBox(textMin, bMax, TabLabels[i], 13f, SilkWeight.SemiBold, color);
            }
            else
            {
                SilkText.DrawInBox(textMin, new Vector2(textMin.X + room, bMax.Y), TabLabels[i], 13f, SilkWeight.SemiBold, color);

                if (hovered)
                    SilkTooltip.Hover(TabLabels[i], bMin, bMax);
            }
        }
    }

    private void DrawPage(Vector2 min, Vector2 max, bool blocked)
    {
        var scale = SilkUi.Scale;
        var scroll = scrolls[(int)page];
        var offset = scroll.Begin(min, max, !blocked && !OverlayOpen);
        var drawList = ImGui.GetWindowDrawList();
        var elapsed = SilkUi.Time - pageSince;
        var t = SilkUi.ReducedMotion ? 1f : SilkUi.EaseOut.Evaluate(Math.Clamp(elapsed / 0.4f, 0f, 1f));
        var lift = MathF.Round((1f - t) * 6f * scale);
        var start = SilkSettingsKit.VertexCount(drawList);

        ImGui.PushClipRect(min, max, true);

        float content;

        try
        {
            var wide = page == SettingsPage.Overrides;
            var available = max.X - min.X - (28f * scale);
            var width = wide ? available : MathF.Min(640f * scale, available);
            var left = MathF.Round(min.X + ((max.X - min.X - width) * 0.5f));
            var top = min.Y + (6f * scale) - offset + lift;
            var origin = new Vector2(left, top);

            var used = page switch
            {
                SettingsPage.General => DrawGeneral(origin, width),
                SettingsPage.BypassMode => DrawMode(origin, width),
                _ => DrawOverrides(origin, width, max.Y - min.Y - (24f * scale)),
            };

            content = (6f * scale) + used + (18f * scale);
        }
        finally
        {
            ImGui.PopClipRect();
        }

        SilkSettingsKit.FadeSince(drawList, start, t);
        scroll.End(min, max, content);
        ImGui.SetScrollY(0f);
    }

    internal void OnClose()
    {
        CloseOverlays();
    }
}
