using BypassEmote.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
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
            MinimumSize = new Vector2(350, 200),
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

        ImGui.SameLine();
        ImGuiComponents.HelpMarker("This only affects vanilla emote commands such as /beesknees, /tea, etc.\nYou will still be able to use the main UI or the /be command to bypass emotes.");

        var bypassOnHotbarSlotTriggered = Configuration.BypassOnHotbarSlotTriggered;
        if (ImGui.Checkbox("Enable Bypassing Emotes on Locked Hotbar Slot", ref bypassOnHotbarSlotTriggered))
        {
            Configuration.BypassOnHotbarSlotTriggered = bypassOnHotbarSlotTriggered;
            Service.ExecuteHotbarSlotHook.SetEnabled(bypassOnHotbarSlotTriggered);
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker("This will allow you to bypass locked emotes when pressing a locked emote hotbar slots.\nYou will be able to assign emotes to hotbar slots in the main UI by right clicking an emote.");

        var autoFaceTarget = Configuration.AutoFaceTarget;
        if (ImGui.Checkbox("Automatically Face Target On Emote", ref autoFaceTarget))
            Configuration.AutoFaceTarget = autoFaceTarget;

        var stopCompanionEmoteOnCompanionMove = Configuration.StopOwnedObjectEmoteOnMove;
        if (ImGui.Checkbox("Stop Companion/Pet/Chocobo Emote on Move", ref stopCompanionEmoteOnCompanionMove))
        {
            Configuration.StopOwnedObjectEmoteOnMove = stopCompanionEmoteOnCompanionMove;
            IpcHelper.NotifyConfigChanged();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Will stop your pet/companion/chocobo looped emote when they move.");

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
    }

    public void Dispose() { }
}
