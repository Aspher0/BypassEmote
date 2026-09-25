using BypassEmote.Enums;
using BypassEmote.Localization;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;

namespace BypassEmote.Safety;

public sealed class ApprovedPatch
{
    public string? GameVersion { get; set; }
    public string? Client { get; set; }
    public string? MinimumPluginVersion { get; set; }
    public string? Notice { get; set; }
}

public sealed class PatchApprovalDocument
{
    public string? Notice { get; set; }
    public List<ApprovedPatch>? Approved { get; set; }
}

public readonly record struct PatchApprovalVerdict(PatchApprovalStatus Status, LiveText Reason, string? Notice);

public static class PatchApproval
{
    public static PatchApprovalVerdict Decide(PatchApprovalDocument? document, string? gameVersion,
        Version? pluginVersion, GameClient client = GameClient.Global)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
            return new(PatchApprovalStatus.Blocked, new(L.GateBuildUnreadable), null);

        if (document == null)
            return Unapproved(client, new(L.GateListUnreachable), null);

        var notice = Trimmed(document.Notice);

        if (Find(document, gameVersion, client) is not { } entry)
            return Unapproved(client, new(L.GateNotApprovedYet, "build", gameVersion), notice);

        notice = Trimmed(entry.Notice) ?? notice;

        if (Trimmed(entry.MinimumPluginVersion) is { } minimumText)
        {
            if (!Version.TryParse(minimumText, out var minimum))
            {
                return new(PatchApprovalStatus.Blocked, new(L.GateUnreadableVersion, "build", gameVersion), notice);
            }

            if (pluginVersion == null || pluginVersion < minimum)
            {
                return new(PatchApprovalStatus.Blocked, new(L.GateNeedsVersion, "build", gameVersion, "minimum", minimum.ToString(),
                    "installed", pluginVersion?.ToString() ?? L.GateUnknownVersion.Text), notice);
            }
        }

        return new(PatchApprovalStatus.Approved, new(L.GateApproved, "build", gameVersion), notice);
    }

    internal static bool ShouldAnnounce(PatchApprovalStatus status, bool wasApproved, string? gameVersion,
        string? announcedGameVersion)
        => status == PatchApprovalStatus.Approved
            && !wasApproved
            && !string.IsNullOrWhiteSpace(gameVersion)
            && !string.Equals(Trimmed(announcedGameVersion), Trimmed(gameVersion), StringComparison.OrdinalIgnoreCase);

    internal static TimeSpan TimeUntilNextCheck(DateTime? lastCheckedUtc, DateTime nowUtc, TimeSpan interval)
    {
        if (lastCheckedUtc is not { } last)
            return TimeSpan.Zero;

        var elapsed = nowUtc - last;

        if (elapsed >= interval)
            return TimeSpan.Zero;

        return elapsed < TimeSpan.Zero ? interval : interval - elapsed;
    }

    internal static int CooldownSeconds(DateTime? lastRequestedUtc, DateTime nowUtc, TimeSpan window)
    {
        var remaining = TimeUntilNextCheck(lastRequestedUtc, nowUtc, window);

        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    public static LiveText UntestedReason(GameClient client) => client switch
    {
        GameClient.Korean or GameClient.Chinese => new(L.GateUntestedClient, "client", GameClientHelper.Name(client)),
        _ => new(L.GateUntestedOther),
    };

    private static PatchApprovalVerdict Unapproved(GameClient client, LiveText blockedReason, string? notice)
        => client == GameClient.Global
            ? new(PatchApprovalStatus.Blocked, blockedReason, notice)
            : new(PatchApprovalStatus.Untested, UntestedReason(client), notice);

    private static ApprovedPatch? Find(PatchApprovalDocument document, string gameVersion, GameClient client)
    {
        foreach (var entry in document.Approved ?? [])
        {
            if (!string.Equals(Trimmed(entry.GameVersion), gameVersion, StringComparison.OrdinalIgnoreCase))
                continue;

            if (GameClientHelper.Parse(entry.Client) == client)
                return entry;
        }

        return null;
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
