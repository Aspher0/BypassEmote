using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.Models;
using BypassEmote.Safety;
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
    private static readonly string[] SwapLifetimeOptions =
        ["When the emote ends", "When you play the target emote", "Never"];

    private static readonly string[] SwapBehaviorOptions = ["Multiple swaps", "One swap at a time"];

    private static readonly string[] LoopMatchingOptions = ["Strict", "Lenient"];
    private static readonly string[] SoundMatchingOptions = ["Strict", "Lenient", "Off"];
    private static readonly string[] TurnMatchingOptions = ["Very strict", "Strict", "Lenient"];
    private static readonly TurnMatchRule[] TurnMatchingOrder = [TurnMatchRule.VeryStrict, TurnMatchRule.Strict, TurnMatchRule.Lenient];

    private static readonly string[] ModdedTargetsOptions = ["Allowed", "Last resort", "Blocked"];
    private static readonly string[] IdlePoseLoopsOptions = ["Never", "Only when nothing else fits", "Allow"];

    private static readonly string[] CachedDispatchOptions = ["Off", "Only when necessary", "On"];

    private static readonly string[] DispatchFidelityOptions = ["Same rank only", "One rank below", "Anything allowed"];

    private const string EmoteSwapLabel = "Emote Swap";
    private const string DirectPlayLabel = "Direct Play";
    private static readonly string[] ModeOptions = [EmoteSwapLabel, DirectPlayLabel];

    private const string ModeName = "Mode";
    private const string LifetimeName = "Turn the swap off";
    private const string BehaviorName = "Swaps at the same time";
    private const string KeptSwapsName = "Kept swaps per emote";
    private const string AnonymizeModName = "Hide your name on the mod";
    private const string LoopMatchingName = "Loop matching";
    private const string TurnMatchingName = "Turn matching";
    private const string SoundMatchingName = "Sound matching";
    private const string CachedDispatchName = "Spread swaps over several emotes";
    private const string MaxTargetsName = "Emotes used per behaviour";
    private const string DispatchFidelityName = "How close a spread emote must fit";
    private const string ErrorThrottleName = "Repeat an error at most every";
    private const string WarningThrottleName = "Repeat a warning at most every";

    private const string ModdedTargetsName = "Emotes your mods change";
    private const string IdlePoseLoopsName = "Loops on idle poses";
    private const string SwapMessagesName = "Show swap messages";
    private const string ErrorMessagesName = "Show error messages";
    private const string WarningMessagesName = "Show warning messages";
    private const string FaceTargetName = "Face your target automatically";

    private static readonly string[] SwapNames =
        [ModeName, LifetimeName, BehaviorName, KeptSwapsName, AnonymizeModName, LoopMatchingName, TurnMatchingName, SoundMatchingName,
         CachedDispatchName, MaxTargetsName, DispatchFidelityName, ErrorThrottleName, WarningThrottleName,
         ModdedTargetsName, IdlePoseLoopsName,
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

    private static readonly NumberStyle MaxTargetsStyle = new()
    {
        Step = 1f,
        FastStep = 5f,
        Min = 1f,
        Max = 50f,
        Default = 3f,
        Width = 0f,
        Focus = new FocusStyle { Shape = FocusShape.None },
    };

    private static readonly NumberStyle KeptSwapsStyle = new()
    {
        Step = 1f,
        FastStep = 5f,
        Min = 0f,
        Max = 100f,
        Default = 5f,
        Width = 0f,
        Focus = new FocusStyle { Shape = FocusShape.None },
    };

    private const string DirectPlayTooltip =
        "Not recommended. Not all sync services support it and Emote Swap is safer.";

    private const string ModeHelp =
        "\"Emote Swap plays\" your emote over one your character owns, through a Penumbra mod. Other players see it "
        + "over any sync service."
        + "\n\"Direct Play\" sends the emote to the game itself, which is less safe and not every sync service supports it.";

    // The window never scrolls: the tab bar stays put and each tab scrolls its own body.
    public ConfigWindow() : base("Bypass Emote##BypassEmoteConfig",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(495, 400),
            MaximumSize = new Vector2(495, float.MaxValue)
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
        DrawPatchApproval();

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

    private static readonly Vector4 PatchWarningColor = ColorHelper.HexToVector4("#E81313");
    private static readonly Vector4 PatchNoticeColor = ColorHelper.HexToVector4("#FF8C1A");

    private static bool checkingApproval;

    private static void DrawPatchApproval()
    {
        var gate = Service.PatchApproval;

        if (gate == null || gate.Approved)
            return;

        ImGui.TextColoredWrapped(PatchWarningColor, "Bypass Emote has not been approved for this game build.");
        ImGui.TextWrapped(gate.Reason);

        ImGui.TextWrapped("Emote swaps may behave oddly until the build is approved.\nThe plugin will automatically fetch updates every 10 minutes to check if it was approved.");

        if (gate.Notice is { Length: > 0 } notice)
            ImGui.TextColoredWrapped(PatchNoticeColor, notice);

        var checkedAt = gate.LastCheckedUtc is { } utc
            ? utc.ToLocalTime().ToString("HH:mm:ss")
            : "not yet";

        ImGui.TextDisabled($"Checked at {checkedAt}.");

        using (ImRaii.Disabled(checkingApproval))
        {
            if (ImGui.Button("Check now##BypassEmotePatchApproval"))
                _ = CheckApprovalAsync(gate);
        }

        ImGui.Separator();
    }

    private static async Task CheckApprovalAsync(PatchApprovalGate gate)
    {
        checkingApproval = true;

        try
        {
            await gate.CheckNowAsync();
        }
        finally
        {
            checkingApproval = false;
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
            ModeOptions, SwapLifetimeOptions, SwapBehaviorOptions, LoopMatchingOptions, TurnMatchingOptions,
            SoundMatchingOptions, DispatchFidelityOptions, ModdedTargetsOptions, IdlePoseLoopsOptions);

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
                    "\"Strict\" only puts a looping emote on another looping one."
                    + "\n\"Lenient\" lets a looping emote play once on a one time emote when no better match exists."
                    + "\n\nRecommended: \"Strict\", or \"Lenient\" if you really don't have many emotes."))
                {
                    Configuration.LoopMatching = (LoopMatchRule)loopMatching;
                }

                var turnMatching = Array.IndexOf(TurnMatchingOrder, Configuration.TurnMatching);
                if (ComboRow(TurnMatchingName, "##BypassEmoteTurnMatching", ref turnMatching, TurnMatchingOptions,
                    "\"Very strict\" only picks emotes that look at your target the exact same way."
                    + "\n\"Strict\" allows eye following differences but keeps emotes head and body turn behaviors."
                    + "\n\"Lenient\" allows any turn behavior."
                    + "\n\nThe plugin will still always try to find the best match first, regardless of the selected rule."
                    + "\n\nRecommended: \"Lenient\"."))
                {
                    Configuration.TurnMatching = TurnMatchingOrder[turnMatching];
                }

                var soundMatching = (int)Configuration.SoundMatching;
                if (ComboRow(SoundMatchingName, "##BypassEmoteSoundMatching", ref soundMatching, SoundMatchingOptions,
                    "\"Strict\" never puts an emote on one that makes sound."
                    + "\n\"Lenient\" only matches emotes that make sounds together."
                    + "\n\"Off\" will let emotes play regardless of sound."
                    + "\n\nThis is to prevent vanilla people from seeing you play fume which could annoy other vanilla players, for example."
                    + "\n\nRecommended: \"Lenient\"."))
                {
                    Configuration.SoundMatching = (SoundMatchRule)soundMatching;
                }

                // Asked every frame: a hook can stop resolving mid-session.
                var cachedDispatch = (int)Configuration.CachedDispatch;
                if (ComboRow(CachedDispatchName, "##BypassEmoteCachedDispatch", ref cachedDispatch, CachedDispatchOptions,
                    "Gives each bypassed emote a target emote of its own. This is useful when you want to bypass multiple emotes quickly."
                    + "\n\n\"Off\" would make it so other people "
                    + "on your sync service would see you redraw constantly."
                    + "\n\"Only when necessary\" spreads nothing while the game plays fresh content on its own, and "
                    + "steps in for the emotes that would show a stale frame once a game patch breaks that."
                    + "\n\"On\" always spreads emotes."
                    + "\n\nRecommended: \"On\" if you want other people on your sync service to always see you properly without "
                    + "redrawing all the time, otherwise highly recommended to leave it on \"Only when necessary\" and not \"Off\".",
                    CachedDispatchAlarm()))
                {
                    Configuration.CachedDispatch = (CachedDispatchMode)cachedDispatch;
                }

                SettingsLayout.Name(MaxTargetsName);

                var maxTargets = Configuration.MaxTargetsPerRank;
                if (NoireInputs.Number("###BypassEmoteMaxTargets", ref maxTargets, MaxTargetsStyle))
                    Configuration.MaxTargetsPerRank = maxTargets;

                SettingsLayout.Help("How many different target emotes one kind of emote may swap to.");

                var dispatchFidelity = (int)Configuration.DispatchFidelity;
                if (ComboRow(DispatchFidelityName, "##BypassEmoteDispatchFidelity", ref dispatchFidelity,
                    DispatchFidelityOptions,
                    "How far the plugin may look for a target emote nothing else is using."
                    + "\n\n\"Same rank only\" keeps to the emotes that fit your emote just as well as the very best one."
                    + "\n\"One rank below\" also accepts the rank under it, which is what lets an emote have a target to "
                    + "itself when its own rank holds only one."
                    + "\n\"Anything allowed\" takes any emote at all, however far it fits."
                    + "\n\nNone of them ever breaks your matching rules above."
                    + "\n\nRecommended: \"One rank below\"."))
                {
                    Configuration.DispatchFidelity = (DispatchFidelity)dispatchFidelity;
                }

                var moddedTargets = (int)Configuration.ModdedTargets;
                if (ComboRow(ModdedTargetsName, "##BypassEmoteModdedTargets", ref moddedTargets, ModdedTargetsOptions,
                    "Determines whether to block unlocked emotes from being picked when they are modified by at least one of your mods. "
                    + "This prevents other people from seeing other modded emotes you might have before the swap takes place."
                    + "\nAs an example, you have a mod on beesknees, and you try to bypass /conduct which happens to land on beesknees: "
                    + "other players might see the modded beesknees for a moment."
                    + "\n\n\"Allowed\" allows using unlocked emotes that are modified by one of your mods."
                    + "\n\"Last resort\" uses one only when nothing else fits."
                    + "\n\"Blocked\" never uses one, and show an error message in the chat with options to disable or open the mod in penumbra."
                    + "\n\nRecommended: \"Last resort\", or \"Blocked\" if you absolutely don't want your modded emotes to accidentaly be seen."))
                {
                    Configuration.ModdedTargets = (ModdedTargetRule)moddedTargets;
                }

                var idlePoseLoops = (int)Configuration.IdlePoseLoops;
                if (ComboRow(IdlePoseLoopsName, "##BypassEmoteIdlePoseLoops", ref idlePoseLoops, IdlePoseLoopsOptions,
                    "When no unlocked looped emote fits as a target, your current idle pose may be eligible instead. "
                    + "The emote you try to bypass will then be targeted onto your current idle pose. This will cause a redraw of your character when triggered."
                    + "\n\n\"Never\" blocks idle poses from being used as targets."
                    + "\n\"Only when nothing else fits\" uses the pose only when literally no unlocked looped emote could have played here at all. "
                    + "This will not use your idle pose if any other emote would have been available if it wasn't blocked."
                    + "\n\"Allow\" always falls back to your idle pose when no other options are available."
                    + "\n\nRecommended: \"Only when nothing else fits\"."))
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
                var swapLifetime = (int)Configuration.SwapLifetime;
                if (ComboRow(LifetimeName, "##BypassEmoteLifetime", ref swapLifetime, SwapLifetimeOptions,
                    "\"When the emote ends\" puts your real emote back as soon as the animation stops."
                    + "\n\"When you play the target emote\" keeps the swap live until you play that emote for real."
                    + "\n\"Never\" keeps the swap live until another swap claims the same target emote."
                    + "\n\nRecommended: \"When you play the target emote\"."))
                {
                    Configuration.SwapLifetime = (SwapLifetime)swapLifetime;
                }

                var swapBehavior = (int)Configuration.SwapBehavior;
                if (ComboRow(BehaviorName, "##BypassEmoteSwapBehavior", ref swapBehavior, SwapBehaviorOptions,
                    "\"Multiple swaps\" lets swaps on different target emotes stay live together."
                    + "\n\"One swap at a time\" turns the previous one off as soon as a new one starts."
                    + "\n\nRecommended: \"Multiple swaps\"."))
                {
                    Configuration.SwapBehavior = (SwapBehavior)swapBehavior;
                }

                SettingsLayout.Name(KeptSwapsName);

                var maxKeptSwaps = Configuration.MaxKeptSwapsPerTarget;
                if (NoireInputs.Number("###BypassEmoteMaxKeptSwaps", ref maxKeptSwaps, KeptSwapsStyle))
                    Configuration.MaxKeptSwapsPerTarget = maxKeptSwaps;

                SettingsLayout.Help("How many swaps are kept per target emote. 0 keeps them all."
                    + "\n\nEach kept swap stays as an option of the generated Penumbra mod, so playing that emote "
                    + "again puts it back without rebuilding anything.");

                var anonymizeModName = Configuration.AnonymizeModName;
                if (CheckRow(AnonymizeModName, ref anonymizeModName,
                    "Names the generated mod after your initials instead of your full name and world."))
                {
                    Configuration.AnonymizeModName = anonymizeModName;
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

    private static string? CachedDispatchAlarm()
    {
        var residency = Service.ResidencyProbe;
        var isCacheBreakIntact = residency.IsDefault() || residency.CacheBreakIntact;
        var spreads = Configuration.CachedDispatch != CachedDispatchMode.Off;

        if (spreads && isCacheBreakIntact)
            return null;

        var message = "Leave this on, otherwise bypassing several animations in a row may look broken.";

        if (isCacheBreakIntact)
            return message;

        return (spreads ? string.Empty : message + "\n\n")
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

    private static bool CheckRow(string name, ref bool value, string help, string? alarm = null)
    {
        var changed = SettingsLayout.Check(name, ref value, alarm);
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
