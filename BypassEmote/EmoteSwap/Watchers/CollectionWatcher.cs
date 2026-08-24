using BypassEmote.IPC;
using Dalamud.Plugin.Services;
using NoireLib;
using System;

namespace BypassEmote.EmoteSwap;

public sealed class CollectionWatcher : IDisposable
{
    private const string LogPrefix = "[CollectionWatcher] ";

    private const long PollIntervalMilliseconds = 1000;

    private readonly IPCCaller_Penumbra _penumbra;
    private readonly SwapModManager _swapMods;

    private bool _subscribed;
    private long _lastPollAt;

    public CollectionWatcher(IPCCaller_Penumbra penumbra, SwapModManager swapMods)
    {
        _penumbra = penumbra;
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
        var now = Environment.TickCount64;

        if (now - _lastPollAt < PollIntervalMilliseconds)
            return;

        _lastPollAt = now;

        try
        {
            Evaluate();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not find the player's collection.", LogPrefix);
        }
    }

    private void Evaluate()
    {
        if (!_penumbra.Available || NoireService.ObjectTable.LocalPlayer == null)
            return;

        if (_penumbra.GetPlayerCollection() is not { } collection)
            return;

        _swapMods.HandleCollectionChanged(collection.Id);
    }
}
