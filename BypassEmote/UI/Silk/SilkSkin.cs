using BypassEmote.Localization;
using BypassEmote.UI.Silk.Main;
using NoireLib.Changelog;
using NoireLib.HistoryLogger;
using NoireLib.UI;
using System.Collections.Generic;

namespace BypassEmote.UI.Silk;

internal sealed class SilkSkin : NoireSkin
{
    private readonly Dictionary<NoireSkinnedWindowBase, SilkChrome> chromes = new();

    public SilkSkin()
        : base("silk", L.SkinSilk)
    {
        View<MainWindow, SilkMainView>();
        View<SettingsWindow, SilkSettingsView>();
        View<CreateModWindow, SilkCreateModView>();
        View<HotbarWindow, SilkHotbarView>();
        View<BeChangelogWindow, SilkChangelogView>();
        View<BeLogsWindow, SilkLogsView>();
        Present<NoireChangelogWindow>(Presentation.Hidden);
        Present<NoireHistoryLogWindow>(Presentation.Hidden);
    }

    public override IFontSkin Fonts => SilkFontSkin.Instance;

    public override IChromeSkin ChromeFor(NoireSkinnedWindowBase window)
    {
        if (!chromes.TryGetValue(window, out var chrome))
        {
            chrome = window switch
            {
                MainWindow => new SilkChrome(window, true, 1f),
                SettingsWindow => new SilkChrome(window, false, SilkBackdrop.SettingsQuiet),
                BeChangelogWindow => new SilkChrome(window, false, SilkBackdrop.ChangelogQuiet),
                _ => new SilkChrome(window, false, SilkBackdrop.SecondaryQuiet),
            };

            chromes[window] = chrome;
        }

        return chrome;
    }

    internal SilkChrome ChromeOf(NoireSkinnedWindowBase window) => (SilkChrome)ChromeFor(window);
}
