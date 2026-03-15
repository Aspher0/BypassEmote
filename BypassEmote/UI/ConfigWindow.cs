using BypassEmote.Helpers;
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
            MinimumSize = new Vector2(350, 170),
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
        var pluginEnabled = Configuration.PluginEnabled;
        if (ImGui.Checkbox("Enable Bypassing Emote Commands", ref pluginEnabled))
            Configuration.PluginEnabled = pluginEnabled;

        var autoFaceTarget = Configuration.AutoFaceTarget;
        if (ImGui.Checkbox("Automatically Face Target On Emote", ref autoFaceTarget))
            Configuration.AutoFaceTarget = autoFaceTarget;

        var showUpdateNotification = Configuration.ShowUpdateNotification;
        if (ImGui.Checkbox("Show Update Notifications", ref showUpdateNotification))
        {
            Configuration.ShowUpdateNotification = showUpdateNotification;
            var updateTracker = NoireLibMain.GetModule<NoireUpdateTracker>();
            updateTracker?.SetShouldShowNotificationOnUpdate(Configuration.ShowUpdateNotification);
            updateTracker?.SetShouldPrintMessageInChatOnUpdate(Configuration.ShowUpdateNotification);
        }

        var showChangelogOnUpdate = Configuration.ShowChangelogOnUpdate;
        if (ImGui.Checkbox("Show Changelog on Updates", ref showChangelogOnUpdate))
        {
            Configuration.ShowChangelogOnUpdate = showChangelogOnUpdate;
            var changelogManager = NoireLibMain.GetModule<NoireChangelogManager>();
            changelogManager?.SetAutomaticallyShowChangelog(Configuration.ShowChangelogOnUpdate);
        }

        var stopCompanionEmoteOnCompanionMove = Configuration.StopOwnedObjectEmoteOnMove;
        if (ImGui.Checkbox("Stop Companion/Pet Emote on Move", ref stopCompanionEmoteOnCompanionMove))
        {
            Configuration.StopOwnedObjectEmoteOnMove = stopCompanionEmoteOnCompanionMove;
            IpcHelper.NotifyConfigChanged();
        }
    }

    public void Dispose() { }
}
