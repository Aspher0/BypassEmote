using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Settings;

internal sealed partial class SilkSettingsPainter
{
    private static string ModeName => L.Mode.Text;
    private static string UnsafeToggleName => L.UnsafeToggle.Text;
    private static string FaceTargetName => L.FaceTarget.Text;
    private static string LoopMatchingName => L.LoopMatching.Text;
    private static string TurnMatchingName => L.TurnMatching.Text;
    private static string SoundMatchingName => L.SoundMatching.Text;
    private static string CachedDispatchName => L.CachedDispatch.Text;
    private static string MaxTargetsName => L.MaxTargets.Text;
    private static string DispatchFidelityName => L.DispatchFidelity.Text;
    private static string ModdedTargetsName => L.ModdedTargets.Text;
    private static string IdlePoseLoopsName => L.IdlePoseLoops.Text;
    private static string LifetimeName => L.Lifetime.Text;
    private static string BehaviorName => L.Behavior.Text;
    private static string KeptSwapsName => L.KeptSwaps.Text;
    private static string AnonymizeModName => L.AnonymizeMod.Text;
    private static string AlwaysCacheBreakName => L.AlwaysCacheBreak.Text;
    private static string SwapMessagesName => L.SwapMessages.Text;
    private static string ErrorMessagesName => L.ErrorMessages.Text;
    private static string ErrorThrottleName => L.ErrorThrottle.Text;
    private static string WarningMessagesName => L.WarningMessages.Text;
    private static string WarningThrottleName => L.WarningThrottle.Text;

    private static readonly TextList LoopMatchingOptions = new(L.SilkStrict, L.SilkLenient);
    private static readonly TextList TurnMatchingOptions = new(L.SilkVeryStrict, L.SilkStrict, L.SilkLenient);
    private static readonly TurnMatchRule[] TurnMatchingOrder = [TurnMatchRule.VeryStrict, TurnMatchRule.Strict, TurnMatchRule.Lenient];
    private static readonly TextList SoundMatchingOptions = new(L.SilkStrict, L.SilkLenient, L.SilkOff);
    private static readonly TextList CachedDispatchOptions = new(L.SilkOff, L.SilkWhenNeeded, L.SilkOn);
    private static readonly TextList DispatchFidelityOptions = new(L.SilkSameRank, L.SilkOneBelow, L.SilkAny);
    private static readonly TextList ModdedTargetsOptions = new(L.SilkAllowed, L.SilkLastResort, L.SilkBlocked);
    private static readonly TextList IdlePoseLoopsOptions = new(L.SilkNever, L.SilkIfNothingFits, L.SilkAllow);
    private static readonly TextList SwapLifetimeOptions = new(L.SilkWhenItEnds, L.SilkOnTheTarget, L.SilkNever);
    private static readonly TextList SwapBehaviorOptions = new(L.SilkMultiple, L.SilkOneAtATime);

    private static readonly SilkComboItem EmoteSwapItem = new(L.EmoteSwap.Text, L.Recommended.Text, SilkPalette.Ok);
    private static readonly SilkComboItem DirectPlayItem = new(L.DirectPlay.Text, null, SilkPalette.Warn, SilkConfirms.SafeDirectPlayTooltip);
    private static readonly SilkComboItem[] ModeItems = [EmoteSwapItem, DirectPlayItem];

    private static readonly SilkParagraphList SafeNotice = new(
        (L.SafeModeLimitLine, SilkParagraphTone.Body),
        (L.SilkSafeModeNotAPromise, SilkParagraphTone.Muted));

    private static readonly SilkParagraphList UnsafeNotice = new(
        (L.UnsafeHeadline, SilkParagraphTone.Strong),
        (L.SilkUnsafeReassurance, SilkParagraphTone.Body));

    private static string ModeHelp => L.ModeHelp.Text;

    private static string UnsafeToggleHelp => L.SilkUnsafeToggleHelp.Text;

    private static string FaceTargetHelp => L.FaceTargetHelp.Text;

    private static string LoopMatchingHelp => L.SilkLoopMatchingHelp.Text;

    private static string TurnMatchingHelp => L.SilkTurnMatchingHelp.Text;

    private static string SoundMatchingHelp => L.SilkSoundMatchingHelp.Text;

    private static string CachedDispatchHelp => L.SilkCachedDispatchHelp.Text;

    private static string MaxTargetsHelp => L.MaxTargetsHelp.Text;

    private static string DispatchFidelityHelp => L.SilkDispatchFidelityHelp.Text;

    private static string ModdedTargetsHelp => L.SilkModdedTargetsHelp.Text;

    private static string IdlePoseLoopsHelp => L.SilkIdlePoseLoopsHelp.Text;

    private static string LifetimeHelp => L.SilkLifetimeHelp.Text;

    private static string BehaviorHelp => L.SilkBehaviorHelp.Text;

    private static string KeptSwapsHelp => L.SilkKeptSwapsHelp.Text;

    private static string AnonymizeModHelp => L.AnonymizeModHelp.Text;

    private static string AlwaysCacheBreakHelp => L.SilkCacheBreakHelp.Text;

    private static string SwapMessagesHelp => L.SwapMessagesHelp.Text;

    private static string ErrorMessagesHelp => L.ErrorMessagesHelp.Text;

    private static string ErrorThrottleHelp => L.SilkErrorThrottleHelp.Text;

    private static string WarningMessagesHelp => L.SilkWarningMessagesHelp.Text;

    private static string WarningThrottleHelp => L.SilkWarningThrottleHelp.Text;

    private string? cacheBreakAlarm;
    private int alarmRevision = -1;
    private string? cacheBreakAlarmFault;
    private bool cacheBreakAlarmHolds;

    private string? dispatchAlarm;
    private string? dispatchAlarmFault;
    private bool dispatchAlarmSpreads;

    private static SelfBypassMode CurrentMode => Configuration.SelfBypassMode;

    private static bool UnsafeOn => Configuration.DirectPlayUnsafe;

    private float DrawMode(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var y = origin.Y;
        var direct = CurrentMode == SelfBypassMode.DirectPlay;

        EmoteSwapItem.Label = L.EmoteSwap.Text;
        EmoteSwapItem.Note = L.Recommended.Text;
        DirectPlayItem.Label = L.DirectPlay.Text;
        DirectPlayItem.Tooltip = UnsafeOn ? SilkConfirms.UnsafeDirectPlayTooltip : SilkConfirms.SafeDirectPlayTooltip;

        y += SilkSettingsKit.Section(ModeName, new Vector2(origin.X, y), width);

        var rows = new SilkSettingRows(new Vector2(origin.X, y), width);
        var modeRow = rows.Row(ModeName, ModeHelp, SilkControls.ComboHeight);
        var mode = direct ? 1 : 0;

        if (SilkControls.Combo("##silkmodecombo", ref mode, ModeItems, modeRow.ControlMin, modeRow.ControlWidth))
        {
            if (mode == 1)
                _ = SilkConfirms.SwitchToDirectPlayAsync(this, UnsafeOn, false);
            else
                ModeSwitcher.Apply(SelfBypassMode.EmoteSwap);
        }

        y += rows.End();

        y += direct ? DrawDirectPlay(new Vector2(origin.X, y), width) : DrawEmoteSwap(new Vector2(origin.X, y), width);
        y += DrawChatMessages(new Vector2(origin.X, y), width, direct);

        return y - origin.Y;
    }

    private float DrawDirectPlay(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var y = origin.Y;
        var attention = DateTime.UtcNow < UnsafeAttentionUntil;

        y += SilkSettingsKit.Section(L.SectionSafety.Text, new Vector2(origin.X, y), width);

        var rows = new SilkSettingRows(new Vector2(origin.X, y), width);
        var row = rows.Row(UnsafeToggleName, UnsafeToggleHelp, SilkControls.SwitchHeight);
        var unsafeOn = UnsafeOn;
        var switchPos = row.RightAligned(SilkControls.SwitchWidth);

        if (attention)
        {
            var pulse = NoireAttention.Pulse();
            var switchMax = switchPos + new Vector2(SilkControls.SwitchWidth * scale, SilkControls.SwitchHeight * scale);
            SilkPaint.Glow(switchPos, switchMax, 18f * scale, 2f * scale, SilkPalette.Fade(SilkPalette.Bad, pulse * 0.8f), SilkControls.SwitchHeight * scale * 0.5f);
        }

        if (SilkControls.Switch("##silkunsafe", ref unsafeOn, switchPos, true))
        {
            UnsafeAttentionUntil = DateTime.MinValue;

            if (unsafeOn)
                _ = SilkConfirms.EnableUnsafeAsync(this, false);
            else
                Configuration.DirectPlayUnsafe = false;
        }

        y += rows.End();

        var noticeAlpha = attention ? NoireAttention.Pulse() : 1f;
        y += 10f * scale;
        y += SilkSettingsKit.Notice(UnsafeOn ? SilkNoticeKind.Bad : SilkNoticeKind.Warn, UnsafeOn ? UnsafeNotice.Array : SafeNotice.Array,
            new Vector2(origin.X, y), width, noticeAlpha);

        y += SilkSettingsKit.Section(L.SectionDirectPlay.Text, new Vector2(origin.X, y), width);

        rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var face = Configuration.AutoFaceTargetDirectPlay;
        if (SwitchRow(ref rows, "##silkfacetarget", FaceTargetName, FaceTargetHelp, ref face))
            Configuration.AutoFaceTargetDirectPlay = face;

        y += rows.End();

        return y - origin.Y;
    }

    private float DrawEmoteSwap(Vector2 origin, float width)
    {
        var y = origin.Y;

        y += SilkSettingsKit.Section(L.SectionMatching.Text, new Vector2(origin.X, y), width);

        var rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var loop = (int)Configuration.LoopMatching;
        if (SegmentedRow(ref rows, LoopMatchingName, LoopMatchingHelp, "##silkloop", ref loop, LoopMatchingOptions))
            Configuration.LoopMatching = (LoopMatchRule)loop;

        var turn = Array.IndexOf(TurnMatchingOrder, Configuration.TurnMatching);
        if (SegmentedRow(ref rows, TurnMatchingName, TurnMatchingHelp, "##silkturn", ref turn, TurnMatchingOptions))
            Configuration.TurnMatching = TurnMatchingOrder[turn];

        var sound = (int)Configuration.SoundMatching;
        if (SegmentedRow(ref rows, SoundMatchingName, SoundMatchingHelp, "##silksound", ref sound, SoundMatchingOptions))
            Configuration.SoundMatching = (SoundMatchRule)sound;

        var dispatch = (int)Configuration.CachedDispatch;
        if (SegmentedRow(ref rows, CachedDispatchName, CachedDispatchHelp, "##silkdispatch", ref dispatch, CachedDispatchOptions, CachedDispatchAlarm()))
            Configuration.CachedDispatch = (CachedDispatchMode)dispatch;

        var maxTargets = Configuration.MaxTargetsPerRank;
        if (StepperRow(ref rows, MaxTargetsName, MaxTargetsHelp, "##silkmaxtargets", ref maxTargets, 1, 50))
            Configuration.MaxTargetsPerRank = maxTargets;

        var fidelity = (int)Configuration.DispatchFidelity;
        if (SegmentedRow(ref rows, DispatchFidelityName, DispatchFidelityHelp, "##silkfidelity", ref fidelity, DispatchFidelityOptions))
            Configuration.DispatchFidelity = (DispatchFidelity)fidelity;

        var modded = (int)Configuration.ModdedTargets;
        if (SegmentedRow(ref rows, ModdedTargetsName, ModdedTargetsHelp, "##silkmodded", ref modded, ModdedTargetsOptions))
            Configuration.ModdedTargets = (ModdedTargetRule)modded;

        var idle = (int)Configuration.IdlePoseLoops;
        if (SegmentedRow(ref rows, IdlePoseLoopsName, IdlePoseLoopsHelp, "##silkidle", ref idle, IdlePoseLoopsOptions))
            Configuration.IdlePoseLoops = (IdlePoseFallback)idle;

        y += rows.End();

        y += SilkSettingsKit.Section(L.SectionPenumbra.Text, new Vector2(origin.X, y), width);

        rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var lifetime = (int)Configuration.SwapLifetime;
        if (SegmentedRow(ref rows, LifetimeName, LifetimeHelp, "##silklifetime", ref lifetime, SwapLifetimeOptions))
            Configuration.SwapLifetime = (SwapLifetime)lifetime;

        var behavior = (int)Configuration.SwapBehavior;
        if (SegmentedRow(ref rows, BehaviorName, BehaviorHelp, "##silkbehavior", ref behavior, SwapBehaviorOptions))
            Configuration.SwapBehavior = (SwapBehavior)behavior;

        var kept = Configuration.MaxKeptSwapsPerTarget;
        if (StepperRow(ref rows, KeptSwapsName, KeptSwapsHelp, "##silkkept", ref kept, 0, 100))
            Configuration.MaxKeptSwapsPerTarget = kept;

        var anonymize = Configuration.AnonymizeModName;
        if (SwitchRow(ref rows, "##silkanonymize", AnonymizeModName, AnonymizeModHelp, ref anonymize))
            Configuration.AnonymizeModName = anonymize;

        var cacheBreak = Configuration.AlwaysCacheBreak;
        if (SwitchRow(ref rows, "##silkcachebreak", AlwaysCacheBreakName, AlwaysCacheBreakHelp, ref cacheBreak, AlwaysCacheBreakAlarm()))
            Configuration.AlwaysCacheBreak = cacheBreak;

        y += rows.End();

        return y - origin.Y;
    }

    private float DrawChatMessages(Vector2 origin, float width, bool direct)
    {
        var y = origin.Y;

        y += SilkSettingsKit.Section(L.SectionChatMessages.Text, new Vector2(origin.X, y), width);

        var rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        if (!direct)
        {
            var swapMessages = Configuration.ShowSwapMessages;
            if (SwitchRow(ref rows, "##silkswapmessages", SwapMessagesName, SwapMessagesHelp, ref swapMessages))
                Configuration.ShowSwapMessages = swapMessages;
        }

        var errors = Configuration.ShowErrorMessages;
        if (SwitchRow(ref rows, "##silkerrormessages", ErrorMessagesName, ErrorMessagesHelp, ref errors))
            Configuration.ShowErrorMessages = errors;

        var errorThrottle = Configuration.ThrottleTimeErrors;
        if (DurationRow(ref rows, ErrorThrottleName, ErrorThrottleHelp, "##silkerrorthrottle", ref errorThrottle, Configuration.ShowErrorMessages))
            Configuration.ThrottleTimeErrors = errorThrottle;

        var warnings = Configuration.ShowWarningMessages;
        if (SwitchRow(ref rows, "##silkwarnmessages", WarningMessagesName, WarningMessagesHelp, ref warnings))
            Configuration.ShowWarningMessages = warnings;

        var warningThrottle = Configuration.ThrottleTimeWarnings;
        if (DurationRow(ref rows, WarningThrottleName, WarningThrottleHelp, "##silkwarnthrottle", ref warningThrottle, Configuration.ShowWarningMessages))
            Configuration.ThrottleTimeWarnings = warningThrottle;

        y += rows.End();

        return y - origin.Y;
    }

    private static bool SegmentedRow(ref SilkSettingRows rows, string name, string help, string id, ref int value, TextList options, string? alarm = null)
    {
        var row = rows.Row(name, help, SilkControls.SegmentedHeight, alarm);
        return SilkControls.Segmented(id, ref value, options.Array, row.ControlMin, row.ControlWidth);
    }

    private static bool StepperRow(ref SilkSettingRows rows, string name, string help, string id, ref int value, int min, int max)
    {
        var row = rows.Row(name, help, SilkControls.StepperHeight);
        return SilkControls.Stepper(id, ref value, row.ControlMin, row.ControlWidth, min, max);
    }

    private static bool DurationRow(ref SilkSettingRows rows, string name, string help, string id, ref TimeSpan value, bool enabled)
    {
        var row = rows.Row(name, help, SilkControls.StepperHeight);

        if (!enabled)
            ImGui.BeginDisabled();

        var changed = SilkDuration.Stepper(id, ref value, row.ControlMin, row.ControlWidth, enabled);

        if (!enabled)
            ImGui.EndDisabled();

        return changed && enabled;
    }

    private string? AlwaysCacheBreakAlarm()
    {
        var holds = Service.PatchApproval is { HoldsHooks: true };
        var fault = Service.Rebinder?.Fault;

        if (alarmRevision != NoireLanguages.Revision)
        {
            alarmRevision = NoireLanguages.Revision;
            cacheBreakAlarm = null;
            dispatchAlarm = null;
        }

        if (holds)
        {
            if (!cacheBreakAlarmHolds || cacheBreakAlarm == null)
            {
                cacheBreakAlarmHolds = true;
                cacheBreakAlarmFault = null;
                cacheBreakAlarm = L.CacheBreakNotApproved.Text;
            }

            return cacheBreakAlarm;
        }

        if (fault == null)
        {
            cacheBreakAlarmHolds = false;
            cacheBreakAlarmFault = null;
            cacheBreakAlarm = null;
            return null;
        }

        if (cacheBreakAlarmHolds || !ReferenceEquals(fault, cacheBreakAlarmFault) || cacheBreakAlarm == null)
        {
            cacheBreakAlarmHolds = false;
            cacheBreakAlarmFault = fault;
            cacheBreakAlarm = L.NotRunning.With("fault", fault);
        }

        return cacheBreakAlarm;
    }

    private string? CachedDispatchAlarm()
    {
        var fault = Service.Rebinder?.Fault;
        var spreads = Configuration.CachedDispatch != CachedDispatchMode.Off;

        if (spreads && fault == null)
        {
            dispatchAlarm = null;
            dispatchAlarmFault = null;
            dispatchAlarmSpreads = true;
            return null;
        }

        if (dispatchAlarm != null && dispatchAlarmSpreads == spreads && ReferenceEquals(dispatchAlarmFault, fault)
            && alarmRevision == NoireLanguages.Revision)
            return dispatchAlarm;

        alarmRevision = NoireLanguages.Revision;
        var message = L.SilkCachedDispatchAlarm.Text;

        dispatchAlarmSpreads = spreads;
        dispatchAlarmFault = fault;
        dispatchAlarm = fault == null
            ? message
            : (spreads ? string.Empty : message + "\n\n") + L.NotRunning.With("fault", fault);

        return dispatchAlarm;
    }
}

internal static class SilkDuration
{
    private static readonly int[] Ladder =
        [0, 1, 2, 3, 5, 10, 15, 20, 30, 45, 60, 90, 120, 180, 240, 300, 600, 900, 1200, 1800, 2700, 3600];

    private static readonly System.Collections.Generic.Dictionary<int, string> Labels = new();

    internal static bool Stepper(string id, ref TimeSpan value, Vector2 pos, float width, bool enabled)
    {
        var scale = SilkUi.Scale;
        var height = SilkControls.StepperHeight * scale;
        var max = pos + new Vector2(width, height);
        var button = 28f * scale;
        var seconds = (int)Math.Clamp(value.TotalSeconds, 0d, 3600d);
        var changed = false;
        var alpha = enabled ? 1f : 0.4f;

        SilkPaint.Fill(pos, max, SilkPalette.Fade(SilkPalette.Sunken, alpha), 9f * scale);
        SilkPaint.InsetRing(pos, max, SilkPalette.Fade(SilkPalette.Line2, alpha), 9f * scale, scale);

        ImGui.PushID(id);
        ImGui.SetCursorScreenPos(pos);

        if (ImGui.InvisibleButton("-", new Vector2(button, height)))
        {
            var next = Previous(seconds);

            if (next != seconds)
            {
                value = TimeSpan.FromSeconds(next);
                seconds = next;
                changed = true;
            }
        }

        var minusHovered = ImGui.IsItemHovered();

        ImGui.SetCursorScreenPos(new Vector2(max.X - button, pos.Y));

        if (ImGui.InvisibleButton("+", new Vector2(button, height)))
        {
            var next = Next(seconds);

            if (next != seconds)
            {
                value = TimeSpan.FromSeconds(next);
                seconds = next;
                changed = true;
            }
        }

        var plusHovered = ImGui.IsItemHovered();
        ImGui.PopID();

        if (enabled && (minusHovered || plusHovered))
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        SilkText.DrawInBox(pos, new Vector2(pos.X + button, max.Y), "-", 13f, SilkWeight.Regular,
            SilkPalette.Fade(minusHovered && enabled ? SilkPalette.Ink : SilkPalette.Ink3, alpha), UiAlign.Center);
        SilkText.DrawInBox(new Vector2(max.X - button, pos.Y), max, "+", 13f, SilkWeight.Regular,
            SilkPalette.Fade(plusHovered && enabled ? SilkPalette.Ink : SilkPalette.Ink3, alpha), UiAlign.Center);
        SilkText.DrawInBox(new Vector2(pos.X + button, pos.Y), new Vector2(max.X - button, max.Y), Label(seconds), 12f, SilkWeight.Medium,
            SilkPalette.Fade(SilkPalette.Ink, alpha), UiAlign.Center, 0f, true);

        return changed;
    }

    internal static string Label(int seconds)
    {
        if (Labels.TryGetValue(seconds, out var cached))
            return cached;

        string text;

        if (seconds < 60)
            text = seconds + " s";
        else if (seconds % 3600 == 0)
            text = (seconds / 3600) + " h";
        else if (seconds % 60 == 0)
            text = (seconds / 60) + " min";
        else
            text = (seconds / 60) + "m " + (seconds % 60) + "s";

        Labels[seconds] = text;
        return text;
    }

    private static int Next(int seconds)
    {
        foreach (var step in Ladder)
        {
            if (step > seconds)
                return step;
        }

        return Ladder[^1];
    }

    private static int Previous(int seconds)
    {
        var best = Ladder[0];

        foreach (var step in Ladder)
        {
            if (step < seconds)
                best = step;
        }

        return best;
    }
}
