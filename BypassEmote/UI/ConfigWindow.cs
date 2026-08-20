using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Helpers;
using NoireLib.Helpers.ObjectExtensions;
using NoireLib.UI;
using NoireLib.UpdateTracker;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI;

public class ConfigWindow : Window, IDisposable
{
    private static readonly string[] SwapModFlavorOptions = ["Visible mod", "Hidden temporary mod"];
    private static readonly string[] SwapLifetimeOptions = ["Turn the swap off", "Keep it until next swap"];

    private static readonly string[] LoopMatchingOptions = ["Strict", "Lenient"];
    private static readonly string[] SoundMatchingOptions = ["Strict", "Lenient"];
    private static readonly string[] TurnMatchingOptions = ["Very strict", "Strict", "Lenient"];
    private static readonly TurnMatchRule[] TurnMatchingOrder = [TurnMatchRule.VeryStrict, TurnMatchRule.Strict, TurnMatchRule.Lenient];

    private static readonly string[] ModdedTargetsOptions = ["Allowed", "Last resort", "Blocked"];
    private static readonly string[] IdlePoseLoopsOptions = ["Never", "Only when nothing else fits", "Allow"];

    private static readonly string[] AlternateTargetsOptions = ["Off", "2 emotes if needed", "Always 2 emotes"];

    private const string EmoteSwapLabel = "Emote Swap";
    private const string DirectPlayLabel = "Direct Play";
    private static readonly string[] ModeOptions = [EmoteSwapLabel, DirectPlayLabel];

    private const string ModeName = "Mode";
    private const string PenumbraModName = "Penumbra mod";
    private const string LifetimeName = "After the emote ends";
    private const string LoopMatchingName = "Loop matching";
    private const string TurnMatchingName = "Turn matching";
    private const string SoundMatchingName = "Sound matching";
    private const string AlternateName = "Alternate equally good emotes";
    private const string ErrorThrottleName = "Repeat an error at most every";
    private const string WarningThrottleName = "Repeat a warning at most every";

    private const string ModdedTargetsName = "Emotes your mods change";
    private const string IdlePoseLoopsName = "Loops on idle poses";
    private const string SwapMessagesName = "Show swap messages";
    private const string ErrorMessagesName = "Show error messages";
    private const string WarningMessagesName = "Show warning messages";
    private const string FaceTargetName = "Face your target automatically";

    private static readonly string[] SwapNames =
        [ModeName, PenumbraModName, LifetimeName, LoopMatchingName, TurnMatchingName, SoundMatchingName,
         AlternateName, ErrorThrottleName, WarningThrottleName, ModdedTargetsName, IdlePoseLoopsName,
         SwapMessagesName, ErrorMessagesName, WarningMessagesName, FaceTargetName];

    private const string PluginEnabledName = "Enable the plugin";
    private const string HotbarBypassName = "Bypass emotes from locked hotbar slots";
    private const string StopOnMoveName = "Stop companion emotes when they move";
    private const string UpdateNotificationName = "Show update notifications";
    private const string ChangelogName = "Show the changelog after an update";
    private const string GposeWindowsName = "Show windows in GPose";
    private const string HiddenUiWindowsName = "Show windows while the game UI is hidden";

    private static readonly string[] GeneralNames =
        [PluginEnabledName, HotbarBypassName, StopOnMoveName, UpdateNotificationName, ChangelogName, GposeWindowsName, HiddenUiWindowsName];

    private static readonly DurationStyle WarningThrottleStyle = new()
    {
        Hint = "5m",
        BareUnit = DurationUnit.Seconds,
        Min = TimeSpan.Zero,
        Max = TimeSpan.FromHours(1),
        Default = TimeSpan.FromMinutes(5),
        Width = 0f,
        ShowPreview = false,
        Focus = new FocusStyle { Shape = FocusShape.None },
    };

    private static readonly DurationStyle ErrorThrottleStyle = new()
    {
        Hint = "0s",
        BareUnit = DurationUnit.Seconds,
        Min = TimeSpan.Zero,
        Max = TimeSpan.FromHours(1),
        Default = TimeSpan.Zero,
        Width = 0f,
        ShowPreview = false,
        Focus = new FocusStyle { Shape = FocusShape.None },
    };

    private const string DirectPlayTooltip =
        "Not recommended. Not all sync services support it and Emote Swap is safer.";

    private const string ModeHelp =
        "Emote Swap plays your emote over one your character owns, through a Penumbra mod. Other players see it "
        + "over any sync service."
        + "\nDirect Play sends the emote to the game itself, and not every sync service supports it.";

    // The window never scrolls: the tab bar stays put and each tab scrolls its own body.
    public ConfigWindow() : base("Bypass Emote##BypassEmoteConfig",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 400),
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
        if (ImGui.BeginTabBar("##BypassEmoteConfigTabs"))
        {
            if (ImGui.BeginTabItem("General settings"))
            {
                DrawTabBody("##BypassEmoteGeneralBody", DrawGeneralSettings);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Bypass Mode"))
            {
                DrawTabBody("##BypassEmoteModeBody", DrawBypassMode);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private static void DrawTabBody(string id, Action body)
    {
        using var child = ImRaii.Child(id, Vector2.Zero, false);

        if (child)
            body();
    }

    private static void DrawGeneralSettings()
    {
        var names = SettingsLayout.NameColumn(GeneralNames);
        var controls = SettingsLayout.CheckboxColumn();

        SettingsLayout.Heading("Plugin");

        using (var rows = SettingsLayout.Rows("##BypassEmotePluginRows", names, controls))
        {
            if (rows)
            {
                var pluginEnabled = Configuration.PluginEnabled;
                if (CheckRow(PluginEnabledName, ref pluginEnabled,
                    "Lets you bypass emotes with the vanilla emote commands (/beesknees, /tea, and so on) and with hotbar slots."
                    + "\nThe main window and the /be command work regardless of this setting."))
                {
                    Configuration.PluginEnabled = pluginEnabled;
                }

                var bypassOnHotbarSlotTriggered = Configuration.BypassOnHotbarSlotTriggered;
                if (CheckRow(HotbarBypassName, ref bypassOnHotbarSlotTriggered,
                    "Bypasses a locked emote when you press its hotbar slot."
                    + "\nRight-click an emote in the main window to assign it to a slot."))
                {
                    Configuration.BypassOnHotbarSlotTriggered = bypassOnHotbarSlotTriggered;
                }

                var stopCompanionEmoteOnCompanionMove = Configuration.StopOwnedObjectEmoteOnMove;
                if (CheckRow(StopOnMoveName, ref stopCompanionEmoteOnCompanionMove,
                    "Stops your minion, pet or chocobo's looped emote when they move."))
                {
                    Configuration.StopOwnedObjectEmoteOnMove = stopCompanionEmoteOnCompanionMove;
                    IpcHelper.NotifyConfigChanged();
                }
            }
        }

        SettingsLayout.Heading("Windows");

        using (var rows = SettingsLayout.Rows("##BypassEmoteWindowRows", names, controls))
        {
            if (rows)
            {
                var showWindowsInGpose = Configuration.ShowWindowsInGpose;
                if (CheckRow(GposeWindowsName, ref showWindowsInGpose,
                    "Keeps this plugin's windows visible while you are in GPose."))
                {
                    Configuration.ShowWindowsInGpose = showWindowsInGpose;
                    Plugin.ApplyUiHideFlags();
                }

                var showWindowsWhenUiHidden = Configuration.ShowWindowsWhenUiHidden;
                if (CheckRow(HiddenUiWindowsName, ref showWindowsWhenUiHidden,
                    "Keeps this plugin's windows visible when you hide the game UI."))
                {
                    Configuration.ShowWindowsWhenUiHidden = showWindowsWhenUiHidden;
                    Plugin.ApplyUiHideFlags();
                }
            }
        }

        SettingsLayout.Heading("Updates");

        using (var rows = SettingsLayout.Rows("##BypassEmoteUpdateRows", names, controls))
        {
            if (rows)
            {
                var showUpdateNotification = Configuration.ShowUpdateNotification;
                if (CheckRow(UpdateNotificationName, ref showUpdateNotification,
                    "Tells you in chat and on screen when a new version of the plugin has been installed."))
                {
                    Configuration.ShowUpdateNotification = showUpdateNotification;
                    var updateTracker = NoireLibMain.GetModule<NoireUpdateTracker>();
                    updateTracker?.SetShouldShowNotificationOnUpdate(Configuration.ShowUpdateNotification);
                    updateTracker?.SetShouldPrintMessageInChatOnUpdate(Configuration.ShowUpdateNotification);
                }

                var showChangelogOnUpdate = Configuration.ShowChangelogOnUpdate;
                if (CheckRow(ChangelogName, ref showChangelogOnUpdate,
                    "Opens the changelog window after an update."))
                {
                    Configuration.ShowChangelogOnUpdate = showChangelogOnUpdate;
                    var changelogManager = NoireLibMain.GetModule<NoireChangelogManager>();
                    changelogManager?.SetAutomaticallyShowChangelog(Configuration.ShowChangelogOnUpdate);
                }
            }
        }
    }

    private static void DrawBypassMode()
    {
        // Measured from every name either mode can show, so the columns stay put when the mode changes.
        var names = SettingsLayout.NameColumn(SwapNames);
        var controls = SettingsLayout.ControlColumn(
            ModeOptions, SwapModFlavorOptions, SwapLifetimeOptions, LoopMatchingOptions, TurnMatchingOptions,
            SoundMatchingOptions, AlternateTargetsOptions, ModdedTargetsOptions, IdlePoseLoopsOptions);

        using (var rows = SettingsLayout.Rows("##BypassEmoteModeRow", names, controls))
        {
            if (rows)
            {
                SettingsLayout.Name(ModeName);
                DrawModeCombo();
                SettingsLayout.Help(ModeHelp);
            }
        }

        if (Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap)
        {
            DrawEmoteSwapSettings(names, controls);
            return;
        }

        DrawDirectPlayWarning();
        DrawDirectPlaySettings(names, controls);
    }

    // Hand-rolled from BeginCombo: ImGui.Combo cannot color a single option, and Direct Play needs the danger color.
    private static void DrawModeCombo()
    {
        var current = Configuration.SelfBypassMode;
        var directPlayActive = current == SelfBypassMode.DirectPlay;
        var previewLabel = directPlayActive ? DirectPlayLabel : EmoteSwapLabel;

        if (directPlayActive)
            ImGui.PushStyleColor(ImGuiCol.Text, NoireTheme.Current.Resolve(ThemeColor.Danger));

        var comboOpen = ImGui.BeginCombo("##BypassEmoteMode", previewLabel);

        if (directPlayActive)
            ImGui.PopStyleColor();

        if (!comboOpen)
            return;

        var emoteSwapSelected = current == SelfBypassMode.EmoteSwap;
        if (ImGui.Selectable(EmoteSwapLabel, emoteSwapSelected) && !emoteSwapSelected)
            ModeSwitcher.Apply(SelfBypassMode.EmoteSwap);

        if (emoteSwapSelected)
            ImGui.SetItemDefaultFocus();

        ImGui.PushStyleColor(ImGuiCol.Text, NoireTheme.Current.Resolve(ThemeColor.Danger));
        var directPlayClicked = ImGui.Selectable(DirectPlayLabel, directPlayActive);
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGui.TextUnformatted(DirectPlayTooltip);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        if (directPlayClicked && !directPlayActive)
            _ = ConfirmSwitchToDirectPlayAsync();

        if (directPlayActive)
            ImGui.SetItemDefaultFocus();

        ImGui.EndCombo();
    }

    private static void DrawDirectPlayWarning()
    {
        var warningColor = NoireTheme.Current.Resolve(ThemeColor.Warning);

        ImGui.Spacing();

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(warningColor, FontAwesomeIcon.ExclamationTriangle.ToIconString());
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.TextColoredWrapped(warningColor, "This mode is unsafe, use the Emote Swap mode instead unless you know what you're doing.");
    }

    private static void DrawDirectPlaySettings(float names, float controls)
    {
        SettingsLayout.Heading("Direct play");

        using var rows = SettingsLayout.Rows("##BypassEmoteDirectPlayRows", names, controls);
        if (!rows)
            return;

        var autoFaceTarget = Configuration.AutoFaceTargetDirectPlay;
        if (CheckRow(FaceTargetName, ref autoFaceTarget, "Turns your character toward your target when you bypass an emote."))
            Configuration.AutoFaceTargetDirectPlay = autoFaceTarget;
    }

    /// <summary> The matching, Penumbra and message settings. Emote Swap mode only. </summary>
    private static void DrawEmoteSwapSettings(float names, float controls)
    {
        SettingsLayout.Heading("Matching");

        using (var rows = SettingsLayout.Rows("##BypassEmoteMatchingRows", names, controls))
        {
            if (rows)
            {
                var loopMatching = (int)Configuration.LoopMatching;
                if (ComboRow(LoopMatchingName, "##BypassEmoteLoopMatching", ref loopMatching, LoopMatchingOptions,
                    "Strict only puts a looping emote on another looping one."
                    + "\nLenient lets a looping emote play once on a one time emote when no better match exists."
                    + "\n\nRecommended: Strict, or Lenient if you really don't have many emotes."))
                {
                    Configuration.LoopMatching = (LoopMatchRule)loopMatching;
                }

                var turnMatching = Array.IndexOf(TurnMatchingOrder, Configuration.TurnMatching);
                if (ComboRow(TurnMatchingName, "##BypassEmoteTurnMatching", ref turnMatching, TurnMatchingOptions,
                    "Very strict only picks emotes that look at your target the exact same way."
                    + "\nStrict allows eye following differences but keeps emotes head and body turn behaviors."
                    + "\nLenient allows any turn behavior."
                    + "\n\nThe plugin will still always try to find the best match first, regardless of the selected rule."
                    + "\nRecommended: Lenient."))
                {
                    Configuration.TurnMatching = TurnMatchingOrder[turnMatching];
                }

                var soundMatching = (int)Configuration.SoundMatching;
                if (ComboRow(SoundMatchingName, "##BypassEmoteSoundMatching", ref soundMatching, SoundMatchingOptions,
                    "Strict never puts an emote on one that makes sound."
                    + "\nLenient ignores sound entirely."
                    + "\n\nThis is to prevent vanilla people from seeing you play fume, for example."
                    + "\nRecommended: Strict."))
                {
                    Configuration.SoundMatching = (SoundMatchRule)soundMatching;
                }

                // Asked every frame: a hook can stop resolving mid-session.
                var alternateTargets = (int)Configuration.AlternateTargets;
                if (ComboRow(AlternateName, "##BypassEmoteAlternateTargets", ref alternateTargets, AlternateTargetsOptions,
                    "Without going into much details, the game caches animations, so playing 2 different bypasses that somehow land on the same unlocked emote "
                    + "might cause the second bypassed emote to replay the first one. A cache-breaker is implemented in the plugin to prevent this, "
                    + "but it may break with patches."
                    + "\n\nOff: always use the best fitting emote."
                    + "\n2 emotes if needed: spread each bypassed emote over two targets, so two presses in a row."
                    + "never land on the same one. Prevents cached emotes when the cache-breaker is down."
                    + "\nAlways 2 emotes: Same as above, but always uses two emotes regardless of cache-breaker status."
                    + "\n\nRecommended: 2 emotes if needed. It will use 2 emotes only when it detects that the cache-breaker is down.",
                    AlternateTargetsAlarm()))
                {
                    var newMode = (AlternateTargetsMode)alternateTargets;

                    // Start the spread fresh, so no two sources keep the same target.
                    if (newMode != Configuration.AlternateTargets && newMode != AlternateTargetsMode.Off)
                        Service.Orchestrator?.ResetDispatchMemory();

                    Configuration.AlternateTargets = newMode;
                }

                var moddedTargets = (int)Configuration.ModdedTargets;
                if (ComboRow(ModdedTargetsName, "##BypassEmoteModdedTargets", ref moddedTargets, ModdedTargetsOptions,
                    "Determines whether to block unlocked emotes from being picked when they are modified by at least one of your mods. "
                    + "This prevents other people from seeing other modded emotes you might have before the swap takes place."
                    + "\nAs an example, you have a mod on beesknees, and you try to bypass /conduct which happens to land on beesknees: "
                    + "other players might see the modded beesknees for a moment."
                    + "\n\nAllowed: allow using unlocked emotes that are modified by one of your mods."
                    + "\nLast resort: use one only when nothing else fits."
                    + "\nBlocked: never use one, and show an error message in the chat with options to disable or open the mod in penumbra."
                    + "\n\nRecommended: Last resort, or Blocked if you absolutely don't want your modded emotes to accidentaly be seen."))
                {
                    Configuration.ModdedTargets = (ModdedTargetRule)moddedTargets;
                }

                var idlePoseLoops = (int)Configuration.IdlePoseLoops;
                if (ComboRow(IdlePoseLoopsName, "##BypassEmoteIdlePoseLoops", ref idlePoseLoops, IdlePoseLoopsOptions,
                    "When no unlocked looped emote fits as a target, your current idle pose may be eligible instead. "
                    + "The emote you try to bypass will then be targeted onto your current idle pose. This will cause a redraw of your character when triggered."
                    + "\n\nNever: block idle poses from being used as targets."
                    + "\nOnly when nothing else fits: use the pose only when literally no unlocked looped emote could have played here at all. "
                    + "This will not use your idle pose if any other emote would have been available if it wasn't blocked."
                    + "\nAllow: always falls back to your idle pose when no other options are available."
                    + "\n\nRecommended: Only when nothing else fits."))
                {
                    Configuration.IdlePoseLoops = (IdlePoseFallback)idlePoseLoops;
                }
            }
        }

        SettingsLayout.Heading("Penumbra");

        using (var rows = SettingsLayout.Rows("##BypassEmotePenumbraRows", names, controls))
        {
            if (rows)
            {
                var swapModFlavor = (int)Configuration.SwapModFlavor;
                if (ComboRow(PenumbraModName, "##BypassEmoteModFlavor", ref swapModFlavor, SwapModFlavorOptions,
                    "Visible mod shows up in your Penumbra mod list so you can see and manage it."
                    + "\nHidden temporary mod makes the mod invisible."))
                {
                    Configuration.SwapModFlavor = (SwapModFlavor)swapModFlavor;
                }

                var swapLifetime = (int)Configuration.SwapLifetime;
                if (ComboRow(LifetimeName, "##BypassEmoteLifetime", ref swapLifetime, SwapLifetimeOptions,
                    "Turn the swap off puts your real emote back once the animation ends."
                    + "\nKeep it until next swap leaves the mod enabled until you try to play the real emote next time."))
                {
                    Configuration.SwapLifetime = (SwapLifetime)swapLifetime;
                }
            }
        }

        SettingsLayout.Heading("Chat messages");

        using (var rows = SettingsLayout.Rows("##BypassEmoteMessageRows", names, controls))
        {
            if (rows)
            {
                var showSwapMessages = Configuration.ShowSwapMessages;
                if (CheckRow(SwapMessagesName, ref showSwapMessages, "Shows a message in chat when an emote is swapped."))
                    Configuration.ShowSwapMessages = showSwapMessages;

                var showErrorMessages = Configuration.ShowErrorMessages;
                if (CheckRow(ErrorMessagesName, ref showErrorMessages,
                    "Shows an error in chat when a swap could not be done."))
                {
                    Configuration.ShowErrorMessages = showErrorMessages;
                }

                SettingsLayout.Name(ErrorThrottleName);

                using (ImRaii.Disabled(!Configuration.ShowErrorMessages))
                {
                    var throttleTimeErrors = Configuration.ThrottleTimeErrors;
                    if (NoireInputs.Duration("###BypassEmoteErrorThrottle", ref throttleTimeErrors, ErrorThrottleStyle))
                        Configuration.ThrottleTimeErrors = throttleTimeErrors;
                }

                SettingsLayout.Help("How long the same error stays quiet after you have seen it."
                    + "\n\nAccepts time format: 5m, 300s, 5m10s, 1h."
                    + "\nSet to 0 to see every one of them.");

                var showWarningMessages = Configuration.ShowWarningMessages;
                if (CheckRow(WarningMessagesName, ref showWarningMessages,
                    "Shows a warning in chat when a swap happened, but not the way you would expect."))
                {
                    Configuration.ShowWarningMessages = showWarningMessages;
                }

                SettingsLayout.Name(WarningThrottleName);

                using (ImRaii.Disabled(!Configuration.ShowWarningMessages))
                {
                    var throttleTimeWarnings = Configuration.ThrottleTimeWarnings;
                    if (NoireInputs.Duration("###BypassEmoteWarningThrottle", ref throttleTimeWarnings, WarningThrottleStyle))
                        Configuration.ThrottleTimeWarnings = throttleTimeWarnings;
                }

                SettingsLayout.Help("How long the same warning stays quiet after you have seen it."
                    + "\n\nAccepts time format: 5m, 300s, 5m10s, 1h."
                    + "\nSet to 0 to disable.");
            }
        }
    }

    private static string? AlternateTargetsAlarm()
    {
        var residency = Service.ResidencyProbe;
        var isCacheBreakIntact = residency.IsDefault() || residency.CacheBreakIntact;

        if (Configuration.AlternateTargets != AlternateTargetsMode.Off && isCacheBreakIntact)
            return null;

        var message = "Set this to \"Alternate 2 emotes if needed\", otherwise bypassing multiple animations may look broken.";

        if (isCacheBreakIntact)
            return message;

        return (Configuration.AlternateTargets == AlternateTargetsMode.Off ? message + "\n\n" : "")
            + (residency.IsDefault() ? "" : $"Not running: {residency.DescribeCacheBreakFault()}.\n")
            + "A game patch probably broke those, report it to the dev.";
    }

    private static bool ComboRow(string name, string id, ref int index, string[] options, string help, string? alarm = null)
    {
        SettingsLayout.Name(name, alarm);
        var changed = ImGui.Combo(id, ref index, options, options.Length);
        SettingsLayout.Help(help);

        return changed;
    }

    private static bool CheckRow(string name, ref bool value, string help)
    {
        var changed = SettingsLayout.Check(name, ref value);
        SettingsLayout.Help(help);

        return changed;
    }

    private static async Task ConfirmSwitchToDirectPlayAsync()
    {
        var confirmed = await NoireModal.ConfirmAsync(
            "Switch to Direct Play?",
            "Not all sync services support it, and it is less safe than Emote Swap.",
            new ModalOptions { ConfirmLabel = "Switch", CancelLabel = "Cancel" });

        if (confirmed)
            ModeSwitcher.Apply(SelfBypassMode.DirectPlay);
    }

    public void Dispose() { }
}
