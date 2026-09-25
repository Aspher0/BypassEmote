using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString MainTitle = new("window.main.title", "Bypass Emote - Locked Emotes");
    public static readonly NoireString MainSubtitle = new("window.main.subtitle", "Locked emotes");
    public static readonly NoireString SettingsTitle = new("window.settings.title", "Bypass Emote");
    public static readonly NoireString SettingsSubtitle = new("window.settings.subtitle", "Settings");
    public static readonly NoireString CreateModTitle = new("window.create_mod.title", "Bypass Emote - Create a mod");
    public static readonly NoireString CreateModSubtitle = new("window.create_mod.subtitle", "Create a mod");
    public static readonly NoireString HotbarSubtitle = new("window.hotbar.subtitle", "Assign to a hotbar");
    public static readonly NoireString ChangelogSubtitle = new("window.changelog.subtitle", "Changelog");
    public static readonly NoireString LogsSubtitle = new("window.logs.subtitle", "Logs");

    public static readonly NoireString Brand = new("chrome.brand", "Bypass Emote");
    public static readonly NoireString AlwaysOnTop = new("chrome.on_top", "Always on top");
    public static readonly NoireString WindowOptions = new("chrome.options", "Window options");
    public static readonly NoireString Expand = new("chrome.expand", "Expand");
    public static readonly NoireString Collapse = new("chrome.collapse", "Collapse");
    public static readonly NoireString MenuSharedNote = new("chrome.menu.shared", "Shared by every Bypass Emote window.");
    public static readonly NoireString MenuClickThroughNote = new("chrome.menu.click_through", "The windows let clicks through to the game. The title bars stay clickable.");
    public static readonly NoireString MenuHintGpose = new("chrome.menu.hint.gpose", "Keeps the windows open while gpose is active.");
    public static readonly NoireString MenuHintUiHidden = new("chrome.menu.hint.ui_hidden", "Keeps the windows open when you hide the game UI.");
    public static readonly NoireString MenuHintCutscenes = new("chrome.menu.hint.cutscenes", "Keeps the windows open during cutscenes.");
    public static readonly NoireString MenuHintAutoHide = new("chrome.menu.hint.auto_hide", "Keeps the windows open whenever the game hides its own UI.");

    public static readonly NoireString SkinSilk = new("skin.silk", "New interface");
    public static readonly NoireString SkinClassic = new("skin.classic", "Classic interface");

    public static readonly NoireString AllLogs = new("title.logs", "All logs");
    public static readonly NoireString ChangelogButton = new("title.changelog", "Changelog");
    public static readonly NoireString SettingsButton = new("title.settings", "Settings");
    public static readonly NoireString OpenSettings = new("title.open_settings", "Open settings");
    public static readonly NoireString ShowEveryLogs = new("title.every_logs", "Show every logs");
    public static readonly NoireString ShowChangelogs = new("title.changelogs", "Show changelogs");
    public static readonly NoireString JoinDiscord = new("title.discord", "Join the Discord");
    public static readonly NoireString SupportMe = new("title.kofi", "Support me");
}
