using BypassEmote.IPC;
using BypassEmote.UI;
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
using NoireLib.UpdateTracker;
using System.Threading.Tasks;

namespace BypassEmote;

public sealed partial class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    private EmoteWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
#if DEBUG
    private DebugWindow DebugWindow { get; init; }
#endif
    private SwapPromptWindow SwapPromptWindow { get; init; }
    private AssignHotbarWindow AssignHotbarWindow { get; init; }
    private CreateModWindow CreateModWindow { get; init; }

    public readonly WindowSystem WindowSystem = new("BypassEmote");

    public Plugin()
    {
        NoireLibMain.Initialize(PluginInterface, this);

        Service.InitializeService(this);

        MainWindow = new EmoteWindow();
        ConfigWindow = new ConfigWindow();
        SwapPromptWindow = new SwapPromptWindow();
        AssignHotbarWindow = new AssignHotbarWindow();
        CreateModWindow = new CreateModWindow();

#if DEBUG
        DebugWindow = new DebugWindow();
        WindowSystem.AddWindow(DebugWindow);
#endif

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(CreateModWindow);

        SetupUI();
        SetupCommands();

        NoireService.Condition.ConditionChange += OnConditionChanged;

        IpcProvider.NotifyReady();

        var promptPending = Configuration.SwapPromptPending;

        SetupModules(activateChangelog: !promptPending);

        if (promptPending)
            _ = ShowPromptThenChangelogAsync();
    }

    private async Task ShowPromptThenChangelogAsync()
    {
        await SwapPromptWindow.ShowAsync();

        await AsyncHelper.RunOnFrameworkThreadAsync(
            () => NoireLibMain.GetModule<NoireChangelogManager>()?.Activate());
    }

    private void SetupUI()
    {
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.Draw += HotbarDragDrop.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettings;

        ApplyUiHideFlags();
    }

    internal static void ApplyUiHideFlags()
    {
        PluginInterface.UiBuilder.DisableGposeUiHide = Configuration.ShowWindowsInGpose;
        PluginInterface.UiBuilder.DisableUserUiHide = Configuration.ShowWindowsWhenUiHidden;
    }

    private void SetupModules(bool activateChangelog)
    {
        var changelogManager = new NoireChangelogManager(
            "ChangelogModule", activateChangelog, true, Configuration.ShowChangelogOnUpdate);
        NoireLibMain.AddModule(changelogManager)?
            .SetTitleBarButtons(
            [
                new()
                {
                    Click = (e) => { Service.Plugin.OpenSettings(); },
                    Icon = FontAwesomeIcon.Cog,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip("Open settings"),
                },

                new()
                {
                    Click = (e) => { Service.OpenKofi(); },
                    Icon = FontAwesomeIcon.Heart,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip("Support me"),
                },
            ]);

        NoireLibMain.AddModule(new NoireHistoryLogger("MessagesLogModule",
            persistLogs: false,
            allowUserTogglePersistence: false,
            allowUserClearInMemory: true,
            allowUserClearDatabase: false));

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
    public void ToggleSettings() => ConfigWindow.Toggle();
#if DEBUG
    public void ToggleDebug() => DebugWindow.Toggle();
#endif

    public void OpenMainWindow() => MainWindow.IsOpen = true;
    public void OpenSettings() => ConfigWindow.IsOpen = true;
    public void OpenAssignHotbar(Emote emote) => _ = AssignHotbarWindow.ShowAsync(emote);
    public void OpenCreateMod() => CreateModWindow.Show();
    public void OpenCreateMod(Emote emote) => CreateModWindow.ShowFor(emote);
    public void OpenChangelog() => NoireLibMain.GetModule<NoireChangelogManager>()?.ShowWindow();
    public void OpenMessageJournal() => NoireLibMain.GetModule<NoireHistoryLogger>()?.ShowWindow();

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= HotbarDragDrop.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettings;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        SwapPromptWindow.Dispose();
        AssignHotbarWindow.Dispose();
        CreateModWindow.Dispose();
#if DEBUG
        DebugWindow.Dispose();
#endif

        Service.Dispose();

        NoireService.Condition.ConditionChange -= OnConditionChanged;

        NoireLibMain.Dispose();
    }
}
