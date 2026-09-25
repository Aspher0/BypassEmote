using BypassEmote.Helpers;
using BypassEmote.IPC;
using BypassEmote.Localization;
using BypassEmote.UI;
using BypassEmote.UI.Classic;
using BypassEmote.UI.Silk;
using BypassEmote.UI.Silk.Main;
using BypassEmote.UI.Skins;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Helpers;
using NoireLib.Helpers.ObjectExtensions;
using NoireLib.HistoryLogger;
using NoireLib.Localizer;
using NoireLib.UI;
using NoireLib.UpdateTracker;
using System.Threading.Tasks;

namespace BypassEmote;

public sealed partial class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    private MainWindow MainWindow { get; init; }
    private SettingsWindow SettingsWindow { get; init; }
#if DEBUG
    private DebugWindow DebugWindow { get; init; }
#endif
    private SwapPrompt SwapPrompt { get; init; }
    private ClassicHotbarPrompt ClassicHotbarPrompt { get; init; }
    private CreateModWindow CreateModWindow { get; init; }
    private HotbarWindow HotbarWindow { get; init; }
    private BeChangelogWindow BeChangelogWindow { get; init; }
    private BeLogsWindow BeLogsWindow { get; init; }

    public static bool UseSilkInterface => BeSkins.SilkActive;

    public readonly WindowSystem WindowSystem = new("BypassEmote");

    public Plugin()
    {
        NoireLibMain.Initialize(PluginInterface, this);

        SessionLog.Start($"Bypass Emote {typeof(Plugin).Assembly.GetName().Version} loading"
            + $" | NoireLib {typeof(NoireService).Assembly.GetName().Version}"
            + $" | Dalamud {typeof(IDalamudPluginInterface).Assembly.GetName().Version}"
            + $" | repository {PluginInterface.SourceRepository}");

        Service.InitializeService(this);

        SetupLocalization();
        BeSkins.Register();

        MainWindow = new MainWindow();
        SettingsWindow = new SettingsWindow();
        SwapPrompt = new SwapPrompt();
        ClassicHotbarPrompt = new ClassicHotbarPrompt();
        CreateModWindow = new CreateModWindow();
        HotbarWindow = new HotbarWindow();
        BeChangelogWindow = new BeChangelogWindow();
        BeLogsWindow = new BeLogsWindow();

        SilkMainView.Connect(MainWindow);
        SilkSettingsView.Connect(SettingsWindow);
        SilkCreateModView.Connect(CreateModWindow);
        SilkHotbarView.Connect(HotbarWindow);
        SilkChangelogView.Connect(BeChangelogWindow);
        SilkLogsView.Connect(BeLogsWindow);
        SilkUi.WarmFonts(MainWindow.Options);

#if DEBUG
        DebugWindow = new DebugWindow();
        WindowSystem.AddWindow(DebugWindow);
#endif

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(CreateModWindow);
        WindowSystem.AddWindow(HotbarWindow);
        WindowSystem.AddWindow(BeChangelogWindow);
        WindowSystem.AddWindow(BeLogsWindow);

        SetupUI();
        SetupCommands();

        NoireService.Condition.ConditionChange += OnConditionChanged;

        IpcProvider.NotifyReady();

        var promptPending = Configuration.SwapPromptPending;

        SetupModules(activateChangelog: !promptPending);

        if (promptPending)
            _ = ShowPromptThenChangelogAsync();
    }

    private static void SetupLocalization()
        => NoireLibMain.AddModule(new NoireLocalizer("LocalizerModule", enableLogging: false, defaultLocale: "en-US"))?
            .SetLogLanguage("en");

    private async Task ShowPromptThenChangelogAsync()
    {
        await SwapPrompt.ShowAsync();

        await AsyncHelper.RunOnFrameworkThreadAsync(
            () => NoireLibMain.GetModule<NoireChangelogManager>()?.Activate());
    }

    private void SetupUI()
    {
        PluginInterface.UiBuilder.Draw += DrawWindowSystem;
        PluginInterface.UiBuilder.Draw += HotbarDragDrop.Draw;
        PluginInterface.UiBuilder.Draw += SilkTooltip.Render;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettings;
    }

    private void DrawWindowSystem()
    {
        using var profile = NoireUI.Profiler.Measure(UiProfiler.RootScopeName);
        WindowSystem.Draw();
    }

    private void SetupModules(bool activateChangelog)
    {
        var changelogManager = new NoireChangelogManager(
            "ChangelogModule", activateChangelog, true, Configuration.ShowChangelogOnUpdate);
        NoireLibMain.AddModule(changelogManager)?
            .SetCustomWindow(BeChangelogWindow)
            .SetTitleBarButtons(
            [
                new()
                {
                    Click = (e) => { Service.Plugin.OpenSettings(); },
                    Icon = FontAwesomeIcon.Cog,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip(L.OpenSettings.Text),
                },

                new()
                {
                    Click = (e) => { Service.OpenKofi(); },
                    Icon = FontAwesomeIcon.Heart,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip(L.SupportMe.Text),
                },
            ]);

        NoireLibMain.AddModule(new NoireHistoryLogger("MessagesLogModule",
            persistLogs: false,
            allowUserTogglePersistence: false,
            allowUserClearInMemory: true,
            allowUserClearDatabase: false))?
            .SetCustomWindow(BeLogsWindow);

        NoireLibMain.AddModule(new NoireUpdateTracker("UpdateTrackerModule",
            true,
            true,
            "https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/repo.json"));
    }

    // Cancels emotes when the player starts casting, mounting, crafting, gathering or interacting with an NPC.
    // Direct play only
    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag.In(CharacterHelper.AnimationInterruptingConditions))
        {
            if (value && NoireService.ObjectTable.LocalPlayer != null)
                EmotePlayer.StopLoop(NoireService.ObjectTable.LocalPlayer, true);
        }
    }

    public void ToggleMainWindow() => MainWindow.Toggle();

    public void ToggleSettings() => SettingsWindow.Toggle();
#if DEBUG
    public void ToggleDebug() => DebugWindow.Toggle();
#endif

    public void OpenMainWindow()
    {
        if (UseSilkInterface)
            MainWindow.Open();
        else
            MainWindow.IsOpen = true;
    }

    public void OpenSettings()
    {
        if (UseSilkInterface)
            SettingsWindow.Open();
        else
            SettingsWindow.IsOpen = true;
    }

    public void OpenAssignHotbar(Emote emote)
    {
        if (UseSilkInterface)
            HotbarWindow.ShowFor(emote);
        else
            _ = ClassicHotbarPrompt.ShowAsync(emote);
    }

    public void OpenCreateMod() => CreateModWindow.Show();

    public void OpenCreateMod(Emote emote) => CreateModWindow.ShowFor(emote);

    public void OpenOverrides(uint sourceRowId)
    {
        SettingsWindow.ShowOverridesFor(sourceRowId);

        if (UseSilkInterface)
            SettingsWindow.Open();
        else
            SettingsWindow.IsOpen = true;
    }

    public void OpenBypassModeWithUnsafeAttention()
    {
        SettingsWindow.ShowBypassModeWithAttention();

        if (UseSilkInterface)
            SettingsWindow.Open();
        else
            SettingsWindow.IsOpen = true;
    }

    public void OpenChangelog() => NoireLibMain.GetModule<NoireChangelogManager>()?.ShowWindow();
    public void OpenMessageJournal() => NoireLibMain.GetModule<NoireHistoryLogger>()?.ShowWindow();

    public void SetClassicInterface(bool classic)
    {
        if (BeSkins.ClassicActive == classic)
            return;

        HotbarWindow.IsOpen = false;
        NoireSkins.Use(classic ? BeSkins.Classic : BeSkins.Silk);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawWindowSystem;
        PluginInterface.UiBuilder.Draw -= HotbarDragDrop.Draw;
        PluginInterface.UiBuilder.Draw -= SilkTooltip.Render;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettings;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        SettingsWindow.Dispose();
        CreateModWindow.Dispose();
        HotbarWindow.Dispose();
        BeChangelogWindow.Dispose();
        BeLogsWindow.Dispose();
        SilkMainView.DisposePainter();

        SilkFonts.Dispose();
#if DEBUG
        DebugWindow.Dispose();
#endif

        Service.Dispose();

        NoireService.Condition.ConditionChange -= OnConditionChanged;

        NoireLibMain.Dispose();
    }
}
