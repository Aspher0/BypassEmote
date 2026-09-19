using Dalamud.Plugin.Services;
using NoireLib;
using System;
using System.Collections.Generic;
using System.Linq;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace BypassEmote.EmoteSwap;

public sealed unsafe class RedrawTimelineRestart : IDisposable
{
    private const string LogPrefix = "[RedrawTimelineRestart] ";

    private const string SharedPapFolder = "bt_common/";
    private const string PapExtension = ".pap";

    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(15);

    private bool _subscribed;
    private bool _sawHidden;
    private DateTime _armedAtUtc;
    private DateTime? _backInViewAtUtc;
    private ushort _baseTimeline;
    private HashSet<string> _timelineKeys = new(StringComparer.OrdinalIgnoreCase);
    private string _reason = string.Empty;

    public void Arm(string reason, IEnumerable<string> redirectedRelativePapPaths)
    {
        _armedAtUtc = DateTime.UtcNow;
        _backInViewAtUtc = null;
        _sawHidden = false;
        _reason = reason;
        _timelineKeys = redirectedRelativePapPaths
            .Select(TimelineKeyFor)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _baseTimeline = NoireService.ObjectTable.LocalPlayer is { Address: not 0 } player
            ? ((NativeCharacter*)player.Address)->Timeline.TimelineSequencer.TimelineIds[0]
            : (ushort)0;

        if (_subscribed)
            return;

        NoireService.Framework.Update += OnFrameworkUpdate;
        _subscribed = true;
    }

    public void Dispose() => Stop();

    private static string? TimelineKeyFor(string relativePapPath)
        => relativePapPath.StartsWith(SharedPapFolder, StringComparison.OrdinalIgnoreCase)
            && relativePapPath.EndsWith(PapExtension, StringComparison.OrdinalIgnoreCase)
                ? relativePapPath[SharedPapFolder.Length..^PapExtension.Length]
                : null;

    private void Stop()
    {
        if (!_subscribed)
            return;

        NoireService.Framework.Update -= OnFrameworkUpdate;
        _subscribed = false;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            Evaluate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not restart the timeline after the redraw.", LogPrefix);
            Stop();
        }
    }

    private bool Wanted(uint slot, ushort timelineId, string? key)
        => slot == 0 && (timelineId == _baseTimeline || (key != null && _timelineKeys.Contains(key)));

    private void Evaluate()
    {
        var now = DateTime.UtcNow;
        var waited = now - _armedAtUtc;

        if (waited > GiveUpAfter)
        {
            Stop();

            var slots = NoireService.ObjectTable.LocalPlayer is { } lastSeen
                ? TimelineRestarter.DescribeSlots(lastSeen)
                : "no character";

            Log.Warning(_backInViewAtUtc == null
                ? $"Character never came back into view after the redraw ({_reason}). Timeline not restarted. {slots}."
                : $"Pose never started after the redraw ({_reason}). Sounds not restarted. Expected timeline "
                    + $"{_baseTimeline} or [{string.Join(", ", _timelineKeys)}], got {slots}.", LogPrefix);

            return;
        }

        if (NoireService.ObjectTable.LocalPlayer is not { } player || !TimelineRestarter.IsDrawnAndVisible(player))
        {
            _sawHidden = true;
            return;
        }

        if (!_sawHidden)
            return;

        _backInViewAtUtc ??= now;

        var restarts = TimelineRestarter.Restart(player, Wanted);

        if (restarts.Count == 0)
            return;

        Stop();
    }
}
