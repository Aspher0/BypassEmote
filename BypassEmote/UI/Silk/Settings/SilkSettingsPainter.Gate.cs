using BypassEmote.Localization;
using BypassEmote.Safety;
using Dalamud.Bindings.ImGui;
using NoireLib.Localizer;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI.Silk.Settings;

internal sealed partial class SilkSettingsPainter
{
    private static string GateWaitingTitle => L.GateWaitingTitle.Text;

    private static string GateWaitingText => L.GateWaitingText.Text;

    private static string GateUntestedTitle => L.GateUntestedTitle.Text;

    private static string CheckNowLabel => L.CheckNow.Text;

    private static string ForceApprovalLabel => L.ForceApproval.Text;

    private static string ForcedApprovalText => L.ForceApprovalOn.Text;

    private static string DisableForceApprovalLabel => L.DisableForceApproval.Text;

    private static string[] checkNowLabels = [];
    private static int checkNowRevision = -1;

    private static string[] CheckNowLabels
    {
        get
        {
            if (checkNowRevision != NoireLanguages.Revision)
            {
                checkNowLabels = BuildCheckNowLabels();
                checkNowRevision = NoireLanguages.Revision;
            }

            return checkNowLabels;
        }
    }

    private static bool checkingApproval;

    private string? gateText;
    private string? gateReason;
    private string? gateNotice;
    private DateTime? gateCheckedAt;
    private int gateRevision = -1;

    private bool GateVisible
    {
        get
        {
            var gate = Service.PatchApproval;

            if (gate == null)
                return false;

#if DEBUG
            if (gate.ForcedApproval)
                return true;
#endif

            return gate.Untested || (gate.Governs && !gate.Approved);
        }
    }

    private float DrawGate(Vector2 pos, float width)
    {
        var gate = Service.PatchApproval;

#if DEBUG
        if (gate is { ForcedApproval: true })
        {
            if (SilkControls.Approval("##silkforced", ForcedApprovalText, DisableForceApprovalLabel, pos, width, out var forcedHeight))
                gate.ForceApproval(false);

            return forcedHeight;
        }
#endif

        if (gate is { Untested: true })
        {
            DrawGateBox(pos, width, GateUntestedTitle, UntestedText(gate), null, null, false, out var untestedHeight);
            return untestedHeight;
        }

        var cooldown = gate?.ManualCooldownSeconds ?? 0;
        var checkLabel = CheckNowLabels[Math.Clamp(cooldown, 0, CheckNowLabels.Length - 1)];
        var disabled = checkingApproval || cooldown > 0;
        string? second = null;

#if DEBUG
        second = ForceApprovalLabel;
#endif

        var clicked = DrawGateBox(pos, width, GateWaitingTitle, WaitingText(gate), checkLabel, second, disabled, out var height);

        if (clicked == 0 && gate != null && !disabled)
            _ = CheckApprovalAsync(gate);

#if DEBUG
        if (clicked == 1)
            gate?.ForceApproval(true);
#endif

        return height;
    }

    private int DrawGateBox(Vector2 pos, float width, string title, string text, string? first, string? second, bool firstDisabled, out float height)
    {
        var scale = SilkUi.Scale;
        var padX = 14f * scale;
        var gap = 12f * scale;
        var firstWidth = first == null ? 0f : SilkControls.SmallButtonWidth(first, 12f, SilkWeight.Bold);
        var secondWidth = second == null ? 0f : SilkControls.SmallButtonWidth(second, 12f, SilkWeight.Bold);
        var buttons = firstWidth + secondWidth + (first != null && second != null ? gap : 0f);
        var textX = pos.X + padX + (8f * scale) + gap;
        var textRight = pos.X + width - padX;
        var stacked = buttons > 0f;
        var textWidth = MathF.Max(1f, textRight - textX);
        var titleHeight = SilkText.ParagraphHeight(title, 12.5f, SilkWeight.Bold, textWidth, 1.45f);
        var bodyHeight = SilkText.ParagraphHeight(text, 12f, SilkWeight.Medium, textWidth, 1.45f);
        var buttonRow = stacked ? (10f * scale) + (30f * scale) : 0f;
        var content = stacked ? titleHeight + bodyHeight + buttonRow : MathF.Max(titleHeight + bodyHeight, 30f * scale);

        height = content + (22f * scale);

        var max = pos + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(pos, max, SilkPalette.GateBg, radius);
        SilkPaint.InsetRing(pos, max, SilkPalette.GateRing, radius, scale);

        var centreY = (pos.Y + max.Y) * 0.5f;
        var textTop = centreY - (content * 0.5f);
        var dot = new Vector2(pos.X + padX + (4f * scale), stacked ? textTop + ((titleHeight + bodyHeight) * 0.5f) : centreY);
        SilkPaint.Glow(dot - new Vector2(4f * scale, 4f * scale), dot + new Vector2(4f * scale, 4f * scale), 10f * scale, 0f, SilkPalette.Warn, 4f * scale);
        SilkPaint.Circle(dot, 4f * scale, SilkPalette.Warn);

        SilkText.DrawParagraph(new Vector2(textX, textTop), title, 12.5f, SilkWeight.Bold, SilkPalette.WarnText, textWidth, 1.45f);
        SilkText.DrawParagraph(new Vector2(textX, textTop + titleHeight), text, 12f, SilkWeight.Medium, SilkPalette.Ink2, textWidth, 1.45f);

        var clicked = -1;
        var x = stacked ? textX : max.X - padX - buttons;
        var buttonCentreY = stacked ? textTop + titleHeight + bodyHeight + (10f * scale) + (15f * scale) : centreY;

        if (first != null)
        {
            if (firstDisabled)
                ImGui.BeginDisabled();

            if (SilkControls.SmallButton(first, new Vector2(x, buttonCentreY - (15f * scale)), firstWidth, 30f, 12f, SilkWeight.Bold, SilkPalette.Ink, SilkPalette.Ink, SilkPalette.HoverWashStrong) && !firstDisabled)
                clicked = 0;

            if (firstDisabled)
                ImGui.EndDisabled();

            x += firstWidth + gap;
        }

        if (second != null && SilkControls.SmallButton(second, new Vector2(x, buttonCentreY - (15f * scale)), secondWidth, 30f, 12f, SilkWeight.Bold, SilkPalette.Ink, SilkPalette.Ink, SilkPalette.HoverWashStrong))
            clicked = 1;

        return clicked;
    }

    private string WaitingText(PatchApprovalGate? gate)
    {
        if (gate == null)
            return GateWaitingText;

        if (gateText != null && ReferenceEquals(gateReason, gate.Reason) && ReferenceEquals(gateNotice, gate.Notice) && gateCheckedAt == gate.LastCheckedUtc
            && gateRevision == NoireLanguages.Revision)
            return gateText;

        gateRevision = NoireLanguages.Revision;
        gateReason = gate.Reason;
        gateNotice = gate.Notice;
        gateCheckedAt = gate.LastCheckedUtc;

        var checkedAt = gate.LastCheckedUtc is { } utc ? utc.ToLocalTime().ToString("HH:mm:ss") : L.NotYet.Text;
        var notice = string.IsNullOrEmpty(gate.Notice) ? string.Empty : "\n" + gate.Notice;

        gateText = L.Fill(L.GateDetail, "reason", gate.Reason, "notice", notice, "time", checkedAt);

        return gateText;
    }

    private string UntestedText(PatchApprovalGate gate)
    {
        if (gateText != null && ReferenceEquals(gateReason, gate.Reason) && ReferenceEquals(gateNotice, gate.Notice)
            && gateRevision == NoireLanguages.Revision)
            return gateText;

        gateRevision = NoireLanguages.Revision;
        gateReason = gate.Reason;
        gateNotice = gate.Notice;
        gateCheckedAt = gate.LastCheckedUtc;
        gateText = string.IsNullOrEmpty(gate.Notice) ? gate.Reason : gate.Reason + "\n" + gate.Notice;

        return gateText;
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

    private static string[] BuildCheckNowLabels()
    {
        var seconds = (int)MathF.Ceiling((float)PatchApprovalGate.ManualCheckCooldown.TotalSeconds);
        var labels = new string[seconds + 1];
        labels[0] = CheckNowLabel;

        for (var i = 1; i <= seconds; i++)
            labels[i] = L.CountdownLabel.With("label", CheckNowLabel, "seconds", i.ToString());

        return labels;
    }
}
