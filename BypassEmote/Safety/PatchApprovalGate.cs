using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Hooking;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BypassEmote.Safety;

public sealed class PatchApprovalGate : IDisposable
{
    private const string LogPrefix = "[PatchApprovalGate] ";

    internal const string EmoteReaderHookName = "OnEmote";

    internal const string ApprovalListUrl =
        "https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/patch-approval.json";

    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(10);

    internal static readonly TimeSpan ManualCheckCooldown = TimeSpan.FromSeconds(10);

    internal const string ApprovedNowMessage =
        "The plugin has been approved for this patch. If you noticed weird behaviors prior to this message, "
        + "try again and it should be fixed now.";

    private static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(15);

    private static readonly Dictionary<string, string> NoCacheHeaders = new() { ["Cache-Control"] = "no-cache" };

    private readonly CancellationTokenSource _tokens = new();

    private readonly List<INoireHook> _held = [];

    private readonly object _pollLock = new();

    private Reading _reading;
    private int _seenHookVersion = -1;
    private bool _frameworkAttached;
    private bool _seenGoverns;
    private CancellationTokenSource? _pollTokens;
    private Task? _polling;
    private DateTime? _manualCheckRequestedUtc;

    private sealed record Reading(PatchApprovalStatus Status, string Reason, string? Notice, DateTime? CheckedUtc);

    public PatchApprovalGate()
    {
        GameVersion = GameVersionHelper.CurrentGameVersion(string.Empty);
        PluginVersion = Assembly.GetExecutingAssembly().GetName().Version;

        _reading = RememberedApproval()
            ? new Reading(PatchApprovalStatus.Approved, $"Game build {GameVersion} was approved earlier.", null, null)
            : new Reading(PatchApprovalStatus.Checking, "Reading the approval list.", null, null);
    }

    public string GameVersion { get; }

    public Version? PluginVersion { get; }

    public PatchApprovalStatus Status => Volatile.Read(ref _reading).Status;

    public string Reason => Volatile.Read(ref _reading).Reason;

    public string? Notice => Volatile.Read(ref _reading).Notice;

    public DateTime? LastCheckedUtc => Volatile.Read(ref _reading).CheckedUtc;

    public bool Approved => Status == PatchApprovalStatus.Approved;

    public bool Governs => Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap;

    public int ManualCooldownSeconds
        => PatchApproval.CooldownSeconds(_manualCheckRequestedUtc, DateTime.UtcNow, ManualCheckCooldown);

    public int HeldCount => _held.Count;

    public void Start()
    {
        Apply();

        if (!_frameworkAttached)
        {
            NoireService.Framework.Update += OnFrameworkUpdate;
            _frameworkAttached = true;
        }

        if (!Governs)
            return;

        if (Approved)
        {
            NoireLogger.LogDebug($"Game build {GameVersion} was approved before.", LogPrefix);
        }
        else
        {
            NoireLogger.LogWarning($"Game build '{GameVersion}' is not approved: {Reason}.", LogPrefix);
        }

        Resume();
    }

    public async Task CheckNowAsync()
    {
        if (!Governs || ManualCooldownSeconds > 0)
            return;

        _manualCheckRequestedUtc = DateTime.UtcNow;

        await CheckAsync(_tokens.Token).ConfigureAwait(false);
    }

    public void OnModeChanged()
    {
        if (Governs)
            Resume();
        else
            StopPolling();
    }

    private void Resume()
    {
        lock (_pollLock)
        {
            if (_tokens.IsCancellationRequested || _polling is { IsCompleted: false })
                return;

            _pollTokens?.Dispose();
            _pollTokens = CancellationTokenSource.CreateLinkedTokenSource(_tokens.Token);
            _polling = PollAsync(_pollTokens.Token);
        }
    }

    private void StopPolling()
    {
        lock (_pollLock)
        {
            _pollTokens?.Cancel();
            _pollTokens?.Dispose();
            _pollTokens = null;
            _polling = null;
        }
    }

    private bool RememberedApproval()
        => !string.IsNullOrEmpty(GameVersion)
            && string.Equals(Configuration.ApprovedGameVersion, GameVersion, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Configuration.ApprovedPluginVersion, PluginVersion?.ToString() ?? string.Empty,
                StringComparison.Ordinal);

    private void Remember(bool approved)
    {
        Configuration.ApprovedGameVersion = approved ? GameVersion : string.Empty;
        Configuration.ApprovedPluginVersion = approved ? PluginVersion?.ToString() ?? string.Empty : string.Empty;
    }

    private void RememberAnnouncement(bool approved)
        => Configuration.AnnouncedApprovalGameVersion = approved ? GameVersion : string.Empty;

    private async Task PollAsync(CancellationToken token)
    {
        var wait = PatchApproval.TimeUntilNextCheck(LastCheckedUtc, DateTime.UtcNow, RetryInterval);

        while (!token.IsCancellationRequested)
        {
            if (wait > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(wait, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            await CheckAsync(token).ConfigureAwait(false);

            if (Approved || token.IsCancellationRequested)
                return;

            wait = RetryInterval;
        }
    }

    private async Task CheckAsync(CancellationToken token)
    {
        PatchApprovalDocument? document = null;

        try
        {
            document = await HttpHelper.GetJsonAsync<PatchApprovalDocument>(ApprovalListUrl, token,
                headers: NoCacheHeaders).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            NoireLogger.LogDebug($"Could not read the approval list ({ex.Message}).", LogPrefix);
        }

        if (token.IsCancellationRequested)
            return;

        if (document == null && RememberedApproval())
        {
            NoireLogger.LogDebug("The approval list could not be reached. The approval already recorded for "
                + $"game build {GameVersion} stands.", LogPrefix);

            Volatile.Write(ref _reading, Volatile.Read(ref _reading) with { CheckedUtc = DateTime.UtcNow });

            return;
        }

        var verdict = PatchApproval.Decide(document, GameVersion, PluginVersion);
        var wasApproved = Approved;

        Volatile.Write(ref _reading,
            new Reading(verdict.Status, verdict.Reason, verdict.Notice, DateTime.UtcNow));

        Remember(verdict.Status == PatchApprovalStatus.Approved && !string.IsNullOrEmpty(GameVersion));

        var announce = PatchApproval.ShouldAnnounce(verdict.Status, wasApproved, GameVersion,
            Configuration.AnnouncedApprovalGameVersion);

        RememberAnnouncement(verdict.Status == PatchApprovalStatus.Approved);

        if (announce)
            NoireLogger.LogDebug($"Game build {GameVersion} is now approved.", LogPrefix);

        await AsyncHelper.RunOnFrameworkThreadAsync(() =>
        {
            Apply();

            if (announce)
                AnnounceApproval();
        }).ConfigureAwait(false);
    }

    private static void AnnounceApproval()
    {
        FeedbackHelper.Success(ApprovedNowMessage);

        NoireService.NotificationManager.AddNotification(new Notification
        {
            Title = "Bypass Emote",
            Content = ApprovedNowMessage,
            InitialDuration = NotificationDuration,
            Type = NotificationType.Success,
        });
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (NoireHook.Version == _seenHookVersion && Governs == _seenGoverns)
            return;

        Apply();
    }

    private void Apply()
    {
        if (Approved || !Governs)
            Release();
        else
            Hold();

        _seenHookVersion = NoireHook.Version;
        _seenGoverns = Governs;
    }

    private void Hold()
    {
        foreach (var hook in NoireHook.All)
        {
            if (!IsGated(hook) || !hook.IsEnabled)
                continue;

            hook.Disable();

            if (!_held.Contains(hook))
                _held.Add(hook);

            NoireLogger.LogDebug($"'{hook.Name}' ({hook.Target.Describe()}) is switched off until the build is "
                + "approved.", LogPrefix);
        }
    }

    private void Release()
    {
        if (_held.Count == 0)
            return;

        foreach (var hook in _held)
        {
            if (hook.IsDisposed)
                continue;

            hook.Enable();
            NoireLogger.LogDebug($"'{hook.Name}' is switched back on.", LogPrefix);
        }

        _held.Clear();
    }

    private static bool IsGated(INoireHook hook)
        => hook.Target.Kind != HookTargetKind.ClientStructs
            && !string.Equals(hook.Name, EmoteReaderHookName, StringComparison.Ordinal);

    public void Dispose()
    {
        if (_frameworkAttached)
        {
            NoireService.Framework.Update -= OnFrameworkUpdate;
            _frameworkAttached = false;
        }

        StopPolling();

        _tokens.Cancel();
        _tokens.Dispose();
        _held.Clear();
    }
}
