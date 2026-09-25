using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using BypassEmote.Safety;
using BypassEmote.UI.Skins;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using NoireLib.UpdateTracker;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI.Classic;

internal sealed class ClassicSettingsPainter
{
    private static readonly TextList SwapLifetimeOptions =
        new(L.ClassicLifetimeEnds, L.ClassicLifetimeTarget, L.ClassicNever);

    private static readonly TextList SwapBehaviorOptions = new(L.ClassicBehaviorMultiple, L.ClassicBehaviorOne);

    private static readonly TextList LoopMatchingOptions = new(L.ClassicStrict, L.ClassicLenient);
    private static readonly TextList SoundMatchingOptions = new(L.ClassicStrict, L.ClassicLenient, L.ClassicOff);
    private static readonly TextList TurnMatchingOptions = new(L.ClassicVeryStrict, L.ClassicStrict, L.ClassicLenient);
    private static readonly TurnMatchRule[] TurnMatchingOrder = [TurnMatchRule.VeryStrict, TurnMatchRule.Strict, TurnMatchRule.Lenient];

    private static readonly TextList ModdedTargetsOptions = new(L.ClassicAllowed, L.ClassicLastResort, L.ClassicBlocked);
    private static readonly TextList IdlePoseLoopsOptions = new(L.ClassicNever, L.ClassicNothingElseFits, L.ClassicAllow);

    private static readonly TextList CachedDispatchOptions = new(L.ClassicOff, L.ClassicWhenNecessary, L.ClassicOn);

    private static readonly TextList DispatchFidelityOptions = new(L.ClassicSameRank, L.ClassicOneBelow, L.ClassicAnything);

    private static string EmoteSwapLabel => L.EmoteSwap.Text;
    private static string DirectPlayLabel => L.DirectPlay.Text;
    private static readonly TextList ModeOptions = new(L.EmoteSwap, L.DirectPlay);

    private static string ModeName => L.Mode.Text;
    private static string LifetimeName => L.Lifetime.Text;
    private static string BehaviorName => L.Behavior.Text;
    private static string KeptSwapsName => L.KeptSwaps.Text;
    private static string AnonymizeModName => L.AnonymizeMod.Text;
    private static string AlwaysCacheBreakName => L.AlwaysCacheBreak.Text;
    private static string LoopMatchingName => L.LoopMatching.Text;
    private static string TurnMatchingName => L.TurnMatching.Text;
    private static string SoundMatchingName => L.SoundMatching.Text;
    private static string CachedDispatchName => L.CachedDispatch.Text;
    private static string MaxTargetsName => L.MaxTargets.Text;
    private static string DispatchFidelityName => L.DispatchFidelity.Text;
    private static string ErrorThrottleName => L.ErrorThrottle.Text;
    private static string WarningThrottleName => L.WarningThrottle.Text;

    private static string UnsafeToggleName => L.UnsafeToggle.Text;

    private static string ModdedTargetsName => L.ModdedTargets.Text;
    private static string IdlePoseLoopsName => L.IdlePoseLoops.Text;
    private static string SwapMessagesName => L.SwapMessages.Text;
    private static string ErrorMessagesName => L.ErrorMessages.Text;
    private static string WarningMessagesName => L.WarningMessages.Text;
    private static string FaceTargetName => L.FaceTarget.Text;

    private static readonly TextList SwapNames =
        new(L.Mode, L.Lifetime, L.Behavior, L.KeptSwaps, L.AnonymizeMod, L.LoopMatching, L.TurnMatching, L.SoundMatching,
         L.CachedDispatch, L.MaxTargets, L.DispatchFidelity, L.ErrorThrottle, L.WarningThrottle,
         L.ModdedTargets, L.IdlePoseLoops, L.AlwaysCacheBreak,
         L.SwapMessages, L.ErrorMessages, L.WarningMessages, L.FaceTarget, L.UnsafeToggle);

    private static string PluginEnabledName => L.PluginEnabled.Text;
    private static string HotbarBypassName => L.HotbarBypass.Text;
    private static string LockedEmotesInWindowName => L.ClassicLockedInWindow.Text;
    private static string LockedEmotesName => L.LockedAsUsable.Text;
    private static string StopOnMoveName => L.StopOnMove.Text;
    private static string UpdateNotificationName => L.UpdateNotification.Text;
    private static string ChangelogName => L.ChangelogOnUpdate.Text;
    private static string NewInterfaceName => L.ClassicNewInterface.Text;
    private static string GposeWindowsName => L.ClassicGposeWindows.Text;
    private static string HiddenUiWindowsName => L.ClassicHiddenUiWindows.Text;

    private static string LanguageName => L.Language.Text;
    private static string TranslationName => L.Translation.Text;

    private static readonly TextList GeneralNames =
        new(L.ClassicNewInterface, L.Language, L.Translation, L.PluginEnabled, L.HotbarBypass, L.ClassicLockedInWindow, L.LockedAsUsable,
            L.StopOnMove, L.UpdateNotification, L.ChangelogOnUpdate,
            L.ClassicGposeWindows, L.ClassicHiddenUiWindows);

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

    private const string GeneralTabId = "general";
    private const string ModeTabId = "mode";
    private const string OverridesTabId = "overrides";

    private const float SettingsContentWidth = 485f;

    private const float WarningCountdownSeconds = 5f;

    private static string SyncServicesLine => L.SyncServicesLine.Text;

    private static string SafeModeLimitLine => L.SafeModeLimitLine.Text;

    private static string SafeModeIsNotAPromiseLine => L.ClassicSafeModeNotAPromise.Text;

    private static string UnsafeHeadline => L.UnsafeHeadline.Text;

    private static string UnsafeReassurance => L.ClassicUnsafeReassurance.Text;

    private static string UnsafeToggleHelp => L.ClassicUnsafeToggleHelp.Text;

    private static string SafeDirectPlayTooltip => L.SafeDirectPlayTooltip.With("limit", SafeModeLimitLine);

    private static string UnsafeDirectPlayTooltip => L.UnsafeDirectPlayTooltip.Text;

    private static string ModeHelp => L.ModeHelp.Text;

    private static readonly NoireTabBar Tabs = new("BypassEmoteConfig")
    {
        Tabs =
        {
            new UiTab(GeneralTabId, L.PageGeneral.Text, () => DrawTabBody("##BypassEmoteGeneralBody", DrawGeneralSettings)),
            new UiTab(ModeTabId, L.PageBypassMode.Text, () => DrawTabBody("##BypassEmoteModeBody", DrawBypassMode)),
            new UiTab(OverridesTabId, L.PageOverrides.Text, () => DrawWideTabBody("##BypassEmoteOverridesBody", OverridesTab.Draw)),
        },
    };

    private static SettingsWindow window = null!;

    private static DateTime unsafeAttentionUntil => window.UnsafeAttentionUntil;

    private static bool confirmingUnsafe;

    internal void Draw(SettingsWindow target)
    {
        window = target;
        TakeRequests();

        DrawPatchApproval();

        Tabs.Tabs[0].Label = L.PageGeneral.Text;
        Tabs.Tabs[1].Label = L.PageBypassMode.Text;
        Tabs.Tabs[2].Label = L.PageOverrides.Text;
        Tabs.Draw();
    }

    private static void TakeRequests()
    {
        if (window.TakeRequestedOverride() is { } source)
            SwitchToOverrides(source);

        if (window.TakeRequestedPage() is { } page)
            Tabs.SwitchTab(page switch
            {
                SettingsPage.BypassMode => ModeTabId,
                SettingsPage.Overrides => OverridesTabId,
                _ => GeneralTabId,
            });
    }

    public static void SwitchToOverrides(uint sourceRowId)
    {
        OverridesTab.ShowFor(sourceRowId);
        Tabs.SwitchTab(OverridesTabId);
    }

    private static readonly Vector4 PatchWarningColor = ColorHelper.HexToVector4("#E81313");
    private static readonly Vector4 PatchNoticeColor = ColorHelper.HexToVector4("#FF8C1A");

    private static bool checkingApproval;

    private static string CheckNowLabel => L.CheckNow.Text;

    private static void DrawPatchApproval()
    {
        var gate = Service.PatchApproval;

        if (gate == null)
            return;

#if DEBUG
        if (gate.ForcedApproval)
        {
            DrawForcedApproval(gate);
            return;
        }
#endif

        if (gate.Untested)
        {
            DrawUntestedClient(gate);
            return;
        }

        if (!gate.Governs || gate.Approved)
            return;

        ImGui.TextColoredWrapped(PatchWarningColor, L.ClassicNotApproved.Text);
        ImGui.TextWrapped(gate.Reason);

        ImGui.TextWrapped(L.ClassicNotApprovedText.Text);

        if (gate.Notice is { Length: > 0 } notice)
            ImGui.TextColoredWrapped(PatchNoticeColor, notice);

        if (checkedAtFor != gate.LastCheckedUtc || checkedAtRevision != NoireLanguages.Revision || checkedAtText.Length == 0)
        {
            checkedAtFor = gate.LastCheckedUtc;
            checkedAtRevision = NoireLanguages.Revision;
            var checkedAt = gate.LastCheckedUtc is { } utc ? utc.ToLocalTime().ToString("HH:mm:ss") : L.NotYet.Text;
            checkedAtText = L.ClassicCheckedAt.With("time", checkedAt);
        }

        ImGui.TextDisabled(checkedAtText);

        DrawCheckNowButton(gate);

#if DEBUG
        ImGui.SameLine();

        if (ImGui.Button(L.ClassicEnableForceApproval.Text + "##BypassEmoteForceApproval"))
            gate.ForceApproval(true);
#endif

        ImGui.Separator();
    }

#if DEBUG
    private static readonly Vector4 ForcedApprovalColor = ColorHelper.HexToVector4("#3FBF7F");

    private static void DrawForcedApproval(PatchApprovalGate gate)
    {
        ImGui.TextColoredWrapped(ForcedApprovalColor, L.ClassicForceApprovalOn.Text);

        if (ImGui.Button(L.ClassicDisableForceApproval.Text + "##BypassEmoteForceApproval"))
            gate.ForceApproval(false);

        ImGui.Separator();
    }
#endif

    private static void DrawUntestedClient(PatchApprovalGate gate)
    {
        ImGui.TextColoredWrapped(PatchWarningColor, L.ClassicUntested.Text);

        ImGui.TextWrapped(gate.Reason);

        if (gate.Notice is { Length: > 0 } notice)
            ImGui.TextColoredWrapped(PatchNoticeColor, notice);

        ImGui.Separator();
    }

    private static DateTime? checkedAtFor;
    private static int checkedAtRevision = -1;
    private static string checkedAtText = string.Empty;

    private static string[] checkNowLabels = [];
    private static int checkNowRevision = -1;

    private static string[] CheckNowLabels()
    {
        if (checkNowRevision == NoireLanguages.Revision)
            return checkNowLabels;

        var seconds = (int)Math.Ceiling(PatchApprovalGate.ManualCheckCooldown.TotalSeconds);
        var labels = new string[seconds + 1];
        labels[0] = CheckNowLabel + "##BypassEmotePatchApproval";

        for (var i = 1; i <= seconds; i++)
            labels[i] = L.ClassicCountdown.With("label", CheckNowLabel, "seconds", i.ToString()) + "##BypassEmotePatchApproval";

        checkNowLabels = labels;
        checkNowRevision = NoireLanguages.Revision;
        return labels;
    }

    private static void DrawCheckNowButton(PatchApprovalGate gate)
    {
        var labels = CheckNowLabels();
        var cooldown = Math.Clamp(gate.ManualCooldownSeconds, 0, labels.Length - 1);
        var width = ImGui.CalcTextSize(labels[^1], true).X + (ImGui.GetStyle().FramePadding.X * 2f);

        using (ImRaii.Disabled(checkingApproval || cooldown > 0))
        {
            if (ImGui.Button(labels[cooldown], new Vector2(width, 0f)))
                _ = CheckApprovalAsync(gate);
        }
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
        var avail = ImGui.GetContentRegionAvail();
        var width = MathF.Min(avail.X, NoireUI.Scaled(SettingsContentWidth));

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (avail.X - width) * 0.5f));

        using var child = ImRaii.Child(id, new Vector2(width, 0f), false);

        if (child)
            body();
    }

    private static void DrawWideTabBody(string id, Action body)
    {
        using var child = ImRaii.Child(id, Vector2.Zero, false);

        if (child)
            body();
    }

    private static void DrawGeneralSettings()
    {
        var names = SettingsLayout.NameColumn(GeneralNames.Sources);
        var controls = SettingsLayout.CheckboxColumn();

        SettingsLayout.Heading(L.SectionInterface.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteInterfaceRows", names, controls))
        {
            if (rows)
            {
                var useNewInterface = !BeSkins.ClassicActive;
                if (CheckRow(NewInterfaceName, ref useNewInterface, L.ClassicNewInterfaceHelp.Text))
                {
                    Service.Plugin.SetClassicInterface(!useNewInterface);
                }
            }
        }

        DrawLanguageRows(names);

        SettingsLayout.Heading(L.SectionPlugin.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmotePluginRows", names, controls))
        {
            if (rows)
            {
                var pluginEnabled = Configuration.PluginEnabled;
                if (CheckRow(PluginEnabledName, ref pluginEnabled, L.PluginEnabledHelp.Text))
                {
                    Configuration.PluginEnabled = pluginEnabled;
                }

                var bypassOnHotbarSlotTriggered = Configuration.BypassOnHotbarSlotTriggered;
                if (CheckRow(HotbarBypassName, ref bypassOnHotbarSlotTriggered, L.HotbarBypassHelp.Text))
                {
                    Configuration.BypassOnHotbarSlotTriggered = bypassOnHotbarSlotTriggered;
                }

                var showLockedEmotesInGameWindow = Configuration.ShowLockedEmotesInGameWindow;
                if (CheckRow(LockedEmotesInWindowName, ref showLockedEmotesInGameWindow, L.LockedInWindowHelp.Text))
                {
                    Configuration.ShowLockedEmotesInGameWindow = showLockedEmotesInGameWindow;
                }

                var showLockedEmotesAsUsable = Configuration.ShowLockedEmotesAsUsable;
                if (CheckRow(LockedEmotesName, ref showLockedEmotesAsUsable, L.LockedAsUsableHelp.Text))
                {
                    Configuration.ShowLockedEmotesAsUsable = showLockedEmotesAsUsable;
                }

                var stopCompanionEmoteOnCompanionMove = Configuration.StopOwnedObjectEmoteOnMove;
                if (CheckRow(StopOnMoveName, ref stopCompanionEmoteOnCompanionMove, L.StopOnMoveHelp.Text))
                {
                    Configuration.StopOwnedObjectEmoteOnMove = stopCompanionEmoteOnCompanionMove;
                    IpcHelper.NotifyConfigChanged();
                }
            }
        }

        SettingsLayout.Heading(L.ClassicSectionWindows.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteWindowRows", names, controls))
        {
            if (rows)
            {
                var showWindowsInGpose = EveryWindow(WindowMenuToggle.StayInGpose);
                if (CheckRow(GposeWindowsName, ref showWindowsInGpose, L.ClassicGposeWindowsHelp.Text))
                {
                    SetEveryWindow(WindowMenuToggle.StayInGpose, showWindowsInGpose);
                }

                var showWindowsWhenUiHidden = EveryWindow(WindowMenuToggle.StayWhenUiHidden);
                if (CheckRow(HiddenUiWindowsName, ref showWindowsWhenUiHidden, L.ClassicHiddenUiWindowsHelp.Text))
                {
                    SetEveryWindow(WindowMenuToggle.StayWhenUiHidden, showWindowsWhenUiHidden);
                }
            }
        }

        SettingsLayout.Heading(L.SectionUpdates.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteUpdateRows", names, controls))
        {
            if (rows)
            {
                var showUpdateNotification = Configuration.ShowUpdateNotification;
                if (CheckRow(UpdateNotificationName, ref showUpdateNotification, L.UpdateNotificationHelp.Text))
                {
                    Configuration.ShowUpdateNotification = showUpdateNotification;
                    var updateTracker = NoireLibMain.GetModule<NoireUpdateTracker>();
                    updateTracker?.SetShouldShowNotificationOnUpdate(Configuration.ShowUpdateNotification);
                    updateTracker?.SetShouldPrintMessageInChatOnUpdate(Configuration.ShowUpdateNotification);
                }

                var showChangelogOnUpdate = Configuration.ShowChangelogOnUpdate;
                if (CheckRow(ChangelogName, ref showChangelogOnUpdate, L.ChangelogOnUpdateHelp.Text))
                {
                    Configuration.ShowChangelogOnUpdate = showChangelogOnUpdate;
                    var changelogManager = NoireLibMain.GetModule<NoireChangelogManager>();
                    changelogManager?.SetAutomaticallyShowChangelog(Configuration.ShowChangelogOnUpdate);
                }
            }
        }
    }

    private static void DrawLanguageRows(float names)
    {
        DrawLanguageTable(names);

        if (!showCredits || NoireLanguages.CreditLines is not { Count: > 0 } credits)
            return;

        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);

        foreach (var line in credits)
            ImGui.TextWrapped(line);

        ImGui.PopStyleColor();
    }

    private static void DrawLanguageTable(float names)
    {
        var languages = LanguageChoice.Names;
        var controls = SettingsLayout.ControlColumn(languages);

        using var rows = SettingsLayout.Rows("##BypassEmoteLanguageRows", names, controls);

        if (!rows)
            return;

        SettingsLayout.Name(LanguageName);

        var active = LanguageChoice.Active;

        if (ImGui.BeginCombo("##BypassEmoteLanguage", languages.Length > 0 ? languages[active] : string.Empty))
        {
            for (var i = 0; i < languages.Length; i++)
            {
                if (ImGui.Selectable(languages[i], i == active) && i != active)
                    LanguageChoice.Pick(i);

                if (i == active)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        SettingsLayout.Help(L.LanguageHelp.Text);

        SettingsLayout.Name(TranslationName);

        if (ImGui.Button(TranslateLabel.Text, new Vector2(ImGui.GetContentRegionAvail().X, 0f)))
            NoireTranslationEditor.Open();

        SettingsLayout.Help(L.TranslationHelp.Text);

        var credits = NoireLanguages.CreditLines;

        if (credits.Count == 0)
            return;

        SettingsLayout.Name(NoireStrings.TranslationCredits.Text);

        if (ImGui.Button((showCredits ? NoireStrings.Hide.Text : NoireStrings.Show.Text) + "##BypassEmoteCredits", new Vector2(ImGui.GetContentRegionAvail().X, 0f)))
            showCredits = !showCredits;
    }

    private static bool showCredits;

    private static readonly IdLabel TranslateLabel = new(L.Translate, "##BypassEmoteTranslate");

    private static void DrawBypassMode()
    {
        var names = SettingsLayout.NameColumn(SwapNames.Sources);
        var controls = SettingsLayout.ControlColumn(
            ModeOptions.Array, SwapLifetimeOptions.Array, SwapBehaviorOptions.Array, LoopMatchingOptions.Array, TurnMatchingOptions.Array,
            SoundMatchingOptions.Array, DispatchFidelityOptions.Array, ModdedTargetsOptions.Array, IdlePoseLoopsOptions.Array);

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

        DrawDirectPlaySettings(names, controls);
    }

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
            ImGui.TextUnformatted(Configuration.DirectPlayUnsafe ? UnsafeDirectPlayTooltip : SafeDirectPlayTooltip);
            ImGui.EndTooltip();
        }

        if (directPlayClicked && !directPlayActive)
            _ = ConfirmSwitchToDirectPlayAsync();

        if (directPlayActive)
            ImGui.SetItemDefaultFocus();

        ImGui.EndCombo();
    }

    private static void DrawDirectPlaySettings(float names, float controls)
    {
        SettingsLayout.Heading(L.SectionSafety.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteDirectPlaySafetyRows", names, controls))
        {
            if (rows)
                DrawUnsafeToggleRow();
        }

        DrawSafetyNotice();

        SettingsLayout.Heading(L.SectionDirectPlay.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteDirectPlayRows", names, controls))
        {
            if (!rows)
                return;

            var autoFaceTarget = Configuration.AutoFaceTargetDirectPlay;
            if (CheckRow(FaceTargetName, ref autoFaceTarget, L.FaceTargetHelp.Text))
                Configuration.AutoFaceTargetDirectPlay = autoFaceTarget;
        }
    }

    private static void DrawUnsafeToggleRow()
    {
        var unsafeEnabled = Configuration.DirectPlayUnsafe;

        if (SettingsLayout.Check(UnsafeToggleName, ref unsafeEnabled))
        {
            if (unsafeEnabled)
                _ = ConfirmEnableUnsafeAsync();
            else
                Configuration.DirectPlayUnsafe = false;
        }

        NoireAttention.Glow(DateTime.UtcNow < unsafeAttentionUntil);

        SettingsLayout.Help(UnsafeToggleHelp);
    }

    private static void DrawSafetyNotice()
    {
        ImGui.Spacing();

        if (!Configuration.DirectPlayUnsafe)
        {
            var warningColor = NoireTheme.Current.Resolve(ThemeColor.Warning);

            ImGui.TextColoredWrapped(
                ColorHelper.ScaleAlpha(warningColor, NoireAttention.Pulse()), DirectPlayGate.SafeModeMessage.Text);

            ImGui.Spacing();

            ImGui.TextColoredWrapped(
                NoireTheme.Current.Resolve(ThemeColor.TextMuted), SafeModeIsNotAPromiseLine);

            return;
        }

        var danger = ColorHelper.ScaleAlpha(NoireTheme.Current.Resolve(ThemeColor.Danger), NoireAttention.Pulse());
        var icon = FontAwesomeIcon.ExclamationTriangle.ToIconString();
        var top = ImGui.GetCursorPosY();

        Vector2 iconSize;

        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(icon);

        ImGui.SetCursorPosY(top + MathF.Max(0f, NoireText.CenterOffset(TextSize.Heading) - (iconSize.Y * 0.5f)));

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(danger, icon);

        ImGui.SameLine();
        ImGui.SetCursorPosY(top);

        using (ImRaii.PushColor(ImGuiCol.Text, danger))
            NoireText.Wrapped(ImGui.GetContentRegionAvail().X, UnsafeHeadline, TextSize.Heading);

        ImGui.Spacing();
        ImGui.TextWrapped(UnsafeReassurance);
    }

    private static void DrawEmoteSwapSettings(float names, float controls)
    {
        SettingsLayout.Heading(L.SectionMatching.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteMatchingRows", names, controls))
        {
            if (rows)
            {
                var loopMatching = (int)Configuration.LoopMatching;
                if (ComboRow(LoopMatchingName, "##BypassEmoteLoopMatching", ref loopMatching, LoopMatchingOptions, L.ClassicLoopMatchingHelp.Text))
                {
                    Configuration.LoopMatching = (LoopMatchRule)loopMatching;
                }

                var turnMatching = Array.IndexOf(TurnMatchingOrder, Configuration.TurnMatching);
                if (ComboRow(TurnMatchingName, "##BypassEmoteTurnMatching", ref turnMatching, TurnMatchingOptions, L.ClassicTurnMatchingHelp.Text))
                {
                    Configuration.TurnMatching = TurnMatchingOrder[turnMatching];
                }

                var soundMatching = (int)Configuration.SoundMatching;
                if (ComboRow(SoundMatchingName, "##BypassEmoteSoundMatching", ref soundMatching, SoundMatchingOptions, L.ClassicSoundMatchingHelp.Text))
                {
                    Configuration.SoundMatching = (SoundMatchRule)soundMatching;
                }

                var cachedDispatch = (int)Configuration.CachedDispatch;
                if (ComboRow(CachedDispatchName, "##BypassEmoteCachedDispatch", ref cachedDispatch, CachedDispatchOptions, L.ClassicCachedDispatchHelp.Text,
                    CachedDispatchAlarm()))
                {
                    Configuration.CachedDispatch = (CachedDispatchMode)cachedDispatch;
                }

                SettingsLayout.Name(MaxTargetsName);

                var maxTargets = Configuration.MaxTargetsPerRank;
                if (NoireInputs.Number("###BypassEmoteMaxTargets", ref maxTargets, MaxTargetsStyle))
                    Configuration.MaxTargetsPerRank = maxTargets;

                SettingsLayout.Help(L.MaxTargetsHelp.Text);

                var dispatchFidelity = (int)Configuration.DispatchFidelity;
                if (ComboRow(DispatchFidelityName, "##BypassEmoteDispatchFidelity", ref dispatchFidelity,
                    DispatchFidelityOptions, L.ClassicDispatchFidelityHelp.Text))
                {
                    Configuration.DispatchFidelity = (DispatchFidelity)dispatchFidelity;
                }

                var moddedTargets = (int)Configuration.ModdedTargets;
                if (ComboRow(ModdedTargetsName, "##BypassEmoteModdedTargets", ref moddedTargets, ModdedTargetsOptions, L.ClassicModdedTargetsHelp.Text))
                {
                    Configuration.ModdedTargets = (ModdedTargetRule)moddedTargets;
                }

                var idlePoseLoops = (int)Configuration.IdlePoseLoops;
                if (ComboRow(IdlePoseLoopsName, "##BypassEmoteIdlePoseLoops", ref idlePoseLoops, IdlePoseLoopsOptions, L.ClassicIdlePoseLoopsHelp.Text))
                {
                    Configuration.IdlePoseLoops = (IdlePoseFallback)idlePoseLoops;
                }
            }
        }

        SettingsLayout.Heading(L.SectionPenumbra.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmotePenumbraRows", names, controls))
        {
            if (rows)
            {
                var swapLifetime = (int)Configuration.SwapLifetime;
                if (ComboRow(LifetimeName, "##BypassEmoteLifetime", ref swapLifetime, SwapLifetimeOptions, L.ClassicLifetimeHelp.Text))
                {
                    Configuration.SwapLifetime = (SwapLifetime)swapLifetime;
                }

                var swapBehavior = (int)Configuration.SwapBehavior;
                if (ComboRow(BehaviorName, "##BypassEmoteSwapBehavior", ref swapBehavior, SwapBehaviorOptions, L.ClassicBehaviorHelp.Text))
                {
                    Configuration.SwapBehavior = (SwapBehavior)swapBehavior;
                }

                SettingsLayout.Name(KeptSwapsName);

                var maxKeptSwaps = Configuration.MaxKeptSwapsPerTarget;
                if (NoireInputs.Number("###BypassEmoteMaxKeptSwaps", ref maxKeptSwaps, KeptSwapsStyle))
                    Configuration.MaxKeptSwapsPerTarget = maxKeptSwaps;

                SettingsLayout.Help(L.ClassicKeptSwapsHelp.Text);

                var anonymizeModName = Configuration.AnonymizeModName;
                if (CheckRow(AnonymizeModName, ref anonymizeModName, L.AnonymizeModHelp.Text))
                {
                    Configuration.AnonymizeModName = anonymizeModName;
                }

                var alwaysCacheBreak = Configuration.AlwaysCacheBreak;
                if (CheckRow(AlwaysCacheBreakName, ref alwaysCacheBreak, L.ClassicCacheBreakHelp.Text,
                    AlwaysCacheBreakAlarm()))
                {
                    Configuration.AlwaysCacheBreak = alwaysCacheBreak;
                }
            }
        }

        SettingsLayout.Heading(L.SectionChatMessages.Text);

        using (var rows = SettingsLayout.Rows("##BypassEmoteMessageRows", names, controls))
        {
            if (rows)
            {
                var showSwapMessages = Configuration.ShowSwapMessages;
                if (CheckRow(SwapMessagesName, ref showSwapMessages, L.SwapMessagesHelp.Text))
                    Configuration.ShowSwapMessages = showSwapMessages;

                var showErrorMessages = Configuration.ShowErrorMessages;
                if (CheckRow(ErrorMessagesName, ref showErrorMessages, L.ErrorMessagesHelp.Text))
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

                SettingsLayout.Help(L.ClassicErrorThrottleHelp.Text);

                var showWarningMessages = Configuration.ShowWarningMessages;
                if (CheckRow(WarningMessagesName, ref showWarningMessages, L.ClassicWarningMessagesHelp.Text))
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

                SettingsLayout.Help(L.ClassicWarningThrottleHelp.Text);
            }
        }
    }

    private static bool EveryWindow(WindowMenuToggle toggle)
    {
        foreach (var window in NoireSkinnedWindowBase.All)
        {
            if (!window.Options.Get(toggle))
                return false;
        }

        return true;
    }

    private static void SetEveryWindow(WindowMenuToggle toggle, bool value)
    {
        foreach (var window in NoireSkinnedWindowBase.All)
        {
            window.Options.Set(toggle, value);
            window.SaveOptions();
        }
    }

    private static string? AlwaysCacheBreakAlarm()
    {
        if (Service.PatchApproval is { HoldsHooks: true })
            return L.CacheBreakNotApproved.Text;

        return Service.Rebinder?.Fault is { } fault ? L.NotRunning.With("fault", fault) : null;
    }

    private static string? CachedDispatchAlarm()
    {
        var fault = Service.Rebinder?.Fault;
        var spreads = Configuration.CachedDispatch != CachedDispatchMode.Off;

        if (spreads && fault == null)
            return null;

        var message = L.ClassicCachedDispatchAlarm.Text;

        if (fault == null)
            return message;

        return (spreads ? string.Empty : message + "\n\n")
            + L.NotRunning.With("fault", fault);
    }

    private static bool ComboRow(string name, string id, ref int index, TextList options, string help, string? alarm = null)
    {
        SettingsLayout.Name(name, alarm);
        var changed = ImGui.Combo(id, ref index, options.Array, options.Length);
        SettingsLayout.Help(help);

        return changed;
    }

    private static bool CheckRow(string name, ref bool value, string help, string? alarm = null)
    {
        var changed = SettingsLayout.Check(name, ref value, alarm);
        SettingsLayout.Help(help);

        return changed;
    }

    private static NoireContent UnsafeWarningContent(bool withSyncLine)
    {
        var danger = NoireTheme.Current.Resolve(ThemeColor.Danger);

        var content = new NoireContent()
            .AddIcon(FontAwesomeIcon.ExclamationTriangle, danger)
            .AddSpacing(6f)
            .AddText(UnsafeHeadline, danger)
            .AddNewLine()
            .AddNewLine()
            .AddText(UnsafeReassurance);

        if (withSyncLine)
            content.AddNewLine().AddNewLine().AddText(SyncServicesLine);

        return content;
    }

    private static async Task ConfirmSwitchToDirectPlayAsync()
    {
        var liftsTheLimit = Configuration.DirectPlayUnsafe;

        var message = liftsTheLimit
            ? UnsafeWarningContent(true)
            : new NoireContent()
                .AddText(SyncServicesLine)
                .AddNewLine()
                .AddNewLine()
                .AddText(SafeModeLimitLine)
                .AddNewLine()
                .AddNewLine()
                .AddText(SafeModeIsNotAPromiseLine, NoireTheme.Current.Resolve(ThemeColor.TextMuted));

        var confirmed = await NoireModal.ConfirmAsync(L.SwitchToDirectPlayTitle.Text, message, new ModalOptions
        {
            ConfirmLabel = L.SwitchToDirectPlay.Text,
            CancelLabel = L.Cancel.Text,
            Danger = liftsTheLimit,
            EnableAfterSeconds = liftsTheLimit ? WarningCountdownSeconds : 0f,
        });

        if (confirmed)
            ModeSwitcher.Apply(SelfBypassMode.DirectPlay);
    }

    private static async Task ConfirmEnableUnsafeAsync()
    {
        if (confirmingUnsafe)
            return;

        confirmingUnsafe = true;

        try
        {
            var confirmed = await NoireModal.ConfirmAsync(L.EnableUnsafeTitle.Text, UnsafeWarningContent(false), new ModalOptions
            {
                ConfirmLabel = L.EnableUnsafe.Text,
                CancelLabel = L.Cancel.Text,
                Danger = true,
                EnableAfterSeconds = WarningCountdownSeconds,
            });

            if (!confirmed)
                return;

            await AsyncHelper.RunOnFrameworkThreadAsync(() => Configuration.DirectPlayUnsafe = true);
        }
        finally
        {
            confirmingUnsafe = false;
        }
    }

}
