using BypassEmote.Localization;
using NoireLib.UI;
using System.Numerics;

namespace BypassEmote.UI;

internal sealed class MainWindow : BeWindow
{
    public MainWindow()
        : base("BypassEmoteMain", L.MainTitle, L.MainSubtitle)
    {
        DefaultSize = new Vector2(620f, 800f);
        MinimumSize = new Vector2(380f, 520f);

        HeaderButton("logs", NoireIcon.Logs, L.AllLogs, static () => Service.Plugin.OpenMessageJournal());
        HeaderButton("changelog", NoireIcon.Changelog, L.ChangelogButton, static () => Service.Plugin.OpenChangelog());
        HeaderButton("settings", NoireIcon.Settings, L.SettingsButton, static () => Service.Plugin.OpenSettings());
    }
}
