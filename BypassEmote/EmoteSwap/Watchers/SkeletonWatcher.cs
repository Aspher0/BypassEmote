using BypassEmote.Models;
using Dalamud.Plugin.Services;
using NoireLib;
using System;

namespace BypassEmote.EmoteSwap;

public sealed class SkeletonWatcher : IDisposable
{
    private const string LogPrefix = "[SkeletonWatcher] ";

    private readonly SwapModManager _swapMods;

    private bool _subscribed;
    private nint _readFrom;
    private string? _drawnSkeleton;
    private string? _requestedFor;

    public SkeletonWatcher(SwapModManager swapMods)
    {
        _swapMods = swapMods;

        NoireService.Framework.Update += OnFrameworkUpdate;
        _subscribed = true;
    }

    public string? DrawnSkeleton => _drawnSkeleton;

    public void Dispose()
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
            NoireLogger.LogError(ex, "Could not read the drawn body; this frame is skipped.", LogPrefix);
        }
    }

    private void Evaluate()
    {
        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
            return;

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
        {
            Forget();
            return;
        }

        // Nothing human is drawn
        if (SwapOrchestrator.DrawnBodyFor(localPlayer) is not { } body)
            return;

        if (body.DrawObject == _readFrom && body.SkeletonId == _drawnSkeleton)
        {
            ReportIfChanged();
            return;
        }

        _readFrom = body.DrawObject;
        _drawnSkeleton = body.SkeletonId;

        ReportIfChanged();
    }

    private void ReportIfChanged()
    {
        if (_drawnSkeleton is not { } skeleton)
            return;

        var servedSkeleton = _swapMods.Registry.Skeleton;

        if (servedSkeleton == skeleton)
        {
            _requestedFor = null;
            return;
        }

        if (_requestedFor == skeleton)
            return;

        _requestedFor = skeleton;

        NoireLogger.LogDebug(
            $"The local player is drawn as {skeleton}, and the kept swaps serve {servedSkeleton ?? "an unrecorded body"}.",
            LogPrefix);

        Service.Orchestrator?.CorrectForDrawnSkeleton(skeleton);
    }

    private void Forget()
    {
        _readFrom = 0;
        _drawnSkeleton = null;
        _requestedFor = null;
    }
}
