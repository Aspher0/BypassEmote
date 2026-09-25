using BypassEmote.Localization;
using System;
using System.Numerics;

namespace BypassEmote.UI;

internal enum SettingsPage
{
    General,
    BypassMode,
    Overrides,
}

internal sealed class SettingsWindow : BeWindow
{
    private static readonly TimeSpan UnsafeAttentionDuration = TimeSpan.FromSeconds(5);

    public SettingsWindow()
        : base("BypassEmoteSettings", L.SettingsTitle, L.SettingsSubtitle)
    {
        DefaultSize = new Vector2(680f, 700f);
        MinimumSize = new Vector2(520f, 420f);
    }

    internal SettingsPage? RequestedPage { get; private set; }

    internal uint? RequestedOverride { get; private set; }

    internal DateTime UnsafeAttentionUntil { get; set; } = DateTime.MinValue;

    public void ShowOverridesFor(uint sourceRowId)
    {
        RequestedOverride = sourceRowId;
        RequestedPage = SettingsPage.Overrides;
    }

    public void ShowBypassModeWithAttention()
    {
        RequestedPage = SettingsPage.BypassMode;
        UnsafeAttentionUntil = DateTime.UtcNow + UnsafeAttentionDuration;
    }

    internal SettingsPage? TakeRequestedPage()
    {
        var page = RequestedPage;
        RequestedPage = null;
        return page;
    }

    internal uint? TakeRequestedOverride()
    {
        var source = RequestedOverride;
        RequestedOverride = null;
        return source;
    }
}
