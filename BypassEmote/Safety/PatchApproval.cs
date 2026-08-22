using System;
using System.Collections.Generic;

namespace BypassEmote.Safety;

public enum PatchApprovalStatus
{
    Checking,
    Approved,
    Blocked,
}

public sealed class ApprovedPatch
{
    public string? GameVersion { get; set; }
    public string? MinimumPluginVersion { get; set; }
    public string? Notice { get; set; }
}

public sealed class PatchApprovalDocument
{
    public string? Notice { get; set; }
    public List<ApprovedPatch>? Approved { get; set; }
}

public readonly record struct PatchApprovalVerdict(PatchApprovalStatus Status, string Reason, string? Notice);

public static class PatchApproval
{
    public static PatchApprovalVerdict Decide(PatchApprovalDocument? document, string? gameVersion,
        Version? pluginVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
            return new(PatchApprovalStatus.Blocked, "The installed game build could not be read.", null);

        if (document == null)
            return new(PatchApprovalStatus.Blocked, "The approval list could not be reached.", null);

        var notice = Trimmed(document.Notice);

        if (Find(document, gameVersion) is not { } entry)
        {
            return new(PatchApprovalStatus.Blocked,
                $"Game build {gameVersion} has not been approved yet.", notice);
        }

        notice = Trimmed(entry.Notice) ?? notice;

        if (Trimmed(entry.MinimumPluginVersion) is { } minimumText)
        {
            if (!Version.TryParse(minimumText, out var minimum))
            {
                return new(PatchApprovalStatus.Blocked,
                    $"Game build {gameVersion} names a plugin version the plugin cannot read.", notice);
            }

            if (pluginVersion == null || pluginVersion < minimum)
            {
                return new(PatchApprovalStatus.Blocked,
                    $"Game build {gameVersion} needs Bypass Emote {minimum} or newer; this is "
                    + $"{pluginVersion?.ToString() ?? "unknown"}.", notice);
            }
        }

        return new(PatchApprovalStatus.Approved, $"Game build {gameVersion} is approved.", notice);
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

    private static ApprovedPatch? Find(PatchApprovalDocument document, string gameVersion)
    {
        foreach (var entry in document.Approved ?? [])
        {
            if (string.Equals(Trimmed(entry.GameVersion), gameVersion, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
