using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.UpdateTracker;
using System;
using System.Numerics;

namespace BypassEmote.UI;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow() : base("Bypass Emote##BypassEmoteConfig", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        TitleBarButtons.Add(new()
        {
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.OpenKofi(); },
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Support me"),
        });
    }

    public override void Draw()
    {
        var pluginEnabled = Service.Configuration!.PluginEnabled;
        ImGui.Checkbox("Enable Bypassing Emote Commands", ref pluginEnabled);
        Service.Configuration.UpdateConfiguration(() => Service.Configuration.PluginEnabled = pluginEnabled);

        var autoFaceTarget = Service.Configuration!.AutoFaceTarget;
        ImGui.Checkbox("Automatically Face Target On Emote", ref autoFaceTarget);
        Service.Configuration.UpdateConfiguration(() => Service.Configuration.AutoFaceTarget = autoFaceTarget);

        var showUpdateNotification = Service.Configuration!.ShowUpdateNotification;
        if (ImGui.Checkbox("Show Update Notifications", ref showUpdateNotification))
        {
            Service.Configuration.UpdateConfiguration(() => Service.Configuration.ShowUpdateNotification = showUpdateNotification);
            var updateTracker = NoireLibMain.GetModule<NoireUpdateTracker>();
            updateTracker?.SetShouldShowNotificationOnUpdate(Service.Configuration.ShowUpdateNotification);
            updateTracker?.SetShouldPrintMessageInChatOnUpdate(Service.Configuration.ShowUpdateNotification);
        }

        var showChangelogOnUpdate = Service.Configuration!.ShowChangelogOnUpdate;
        if (ImGui.Checkbox("Show Changelog on Updates", ref showChangelogOnUpdate))
        {
            Service.Configuration.UpdateConfiguration(() => Service.Configuration.ShowChangelogOnUpdate = showChangelogOnUpdate);
            var changelogManager = NoireLibMain.GetModule<NoireChangelogManager>();
            changelogManager?.SetAutomaticallyShowChangelog(Service.Configuration.ShowChangelogOnUpdate);
        }
    }

    public void Dispose() { }
}
