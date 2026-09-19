using BypassEmote.Enums;
using Dalamud.Plugin.Services;
using NoireLib;
using System;

namespace BypassEmote.EmoteSwap;

public sealed class SkeletonWatcher : IDisposable
{
    private const string LogPrefix = "[SkeletonWatcher] ";

    private readonly SwapModManager _swapMods;

    private bool _subscribed;
    private string? _drawnSkeleton;
    private string? _requestedFor;

    public SkeletonWatcher(SwapModManager swapMods)
    {
        _swapMods = swapMods;

        NoireService.Framework.Update += OnFrameworkUpdate;
        _subscribed = true;
    }

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
            Log.Error(ex, "Could not read the drawn body. Frame skipped.", LogPrefix);
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

        if (SwapOrchestrator.DrawnBodyFor(localPlayer) is not { } body)
            return;

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

        Log.Debug(
            $"The local player is drawn as {skeleton}. Kept swaps serve {servedSkeleton ?? "no recorded body"}.",
            LogPrefix);

        Service.Orchestrator?.CorrectForDrawnSkeleton(skeleton);
    }

    private void Forget()
    {
        _drawnSkeleton = null;
        _requestedFor = null;
    }
}
