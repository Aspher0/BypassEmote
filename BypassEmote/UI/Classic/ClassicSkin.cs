using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.HistoryLogger;
using NoireLib.UI;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Classic;

internal sealed class ClassicSkin : NoireSkin
{
    private static readonly List<TitleBarButton> MainButtons =
    [
        new()
        {
            Click = static m => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenSettings(); },
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new(2, 2),
            ShowTooltip = static () => ImGui.SetTooltip(L.OpenSettings.Text),
        },
        new()
        {
            Click = static m => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenMessageJournal(); },
            Icon = FontAwesomeIcon.TimesCircle,
            IconOffset = new(2, 2),
            ShowTooltip = static () => ImGui.SetTooltip(L.ShowEveryLogs.Text),
        },
        new()
        {
            Click = static m => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenChangelog(); },
            Icon = FontAwesomeIcon.Book,
            IconOffset = new(2, 2),
            ShowTooltip = static () => ImGui.SetTooltip(L.ShowChangelogs.Text),
        },
        new()
        {
            Click = static m => { if (m == ImGuiMouseButton.Left) Service.OpenDiscord(); },
            Icon = FontAwesomeIcon.Comments,
            IconOffset = new(2, 2),
            ShowTooltip = static () => ImGui.SetTooltip(L.JoinDiscord.Text),
        },
        new()
        {
            Click = static m => { if (m == ImGuiMouseButton.Left) Service.OpenKofi(); },
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new(2, 2),
            ShowTooltip = static () => ImGui.SetTooltip(L.SupportMe.Text),
        },
    ];

    private static readonly List<TitleBarButton> SettingsButtons =
    [
        new()
        {
            Click = static m => { if (m == ImGuiMouseButton.Left) Service.OpenKofi(); },
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new(2, 2),
            ShowTooltip = static () => ImGui.SetTooltip(L.SupportMe.Text),
        },
    ];

    public ClassicSkin()
        : base("classic", L.SkinClassic)
    {
        View<MainWindow, ClassicMainView>();
        View<SettingsWindow, ClassicSettingsView>();
        View<CreateModWindow, ClassicCreateModView>();
        View<BeChangelogWindow, ClassicChangelogView>();
        View<BeLogsWindow, ClassicLogsView>();
        Present<HotbarWindow>(Presentation.Hidden);
        Present<NoireChangelogWindow>(Presentation.Hidden);
        Present<NoireHistoryLogWindow>(Presentation.Hidden);
    }

    internal static void SetUpNative(BeWindow window)
    {
        window.BgAlpha = null;
        window.Size = null;
        window.DisableFadeInFadeOut = false;
        window.AllowPinning = true;
        window.AllowClickthrough = true;

        switch (window)
        {
            case MainWindow:
                window.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                window.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(400, 500),
                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
                };
                SetButtons(window, MainButtons);
                break;
            case SettingsWindow:
                window.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                window.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(495, 400),
                    MaximumSize = new Vector2(495, float.MaxValue),
                };
                SetButtons(window, SettingsButtons);
                break;
            case BeChangelogWindow:
                window.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                window.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(750, 500),
                    MaximumSize = new Vector2(750, 500),
                };
                SetButtons(window, NoireLibMain.GetModule<NoireChangelogManager>()?.TitleBarButtons);
                break;
            case BeLogsWindow:
                window.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                window.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(800, 420),
                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
                };
                SetButtons(window, NoireLibMain.GetModule<NoireHistoryLogger>()?.TitleBarButtons);
                break;
            default:
                window.Flags = ImGuiWindowFlags.None;
                window.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(560, 400),
                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
                };
                window.TitleBarButtons.Clear();
                break;
        }
    }

    internal static string ChangelogTitle => NoireLibMain.GetModule<NoireChangelogManager>()?.DisplayWindowName ?? string.Empty;

    internal static string LogsTitle => NoireLibMain.GetModule<NoireHistoryLogger>()?.DisplayWindowName ?? string.Empty;

    private static void SetButtons(Window window, List<TitleBarButton>? buttons)
    {
        if (buttons == null)
            return;

        if (window.TitleBarButtons.Count == buttons.Count)
            return;

        window.TitleBarButtons.Clear();
        window.TitleBarButtons.AddRange(buttons);
    }
}
