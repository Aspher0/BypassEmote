using BypassEmote.Models;
using Dalamud.Plugin.Services;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace BypassEmote.EmoteSwap;

public sealed class SwapEndWatcher
{
    private const string LogPrefix = "[SwapEndWatcher] ";

    private readonly SwapModManager _swapMods;

    private bool _armed;
    private bool _subscribed;
    private SwapOptionEntry? _armedEntry;
    private Vector3 _armedPosition;

    private ushort _watchedEmote;

    private bool _isIdlePoseWatch;
    private bool _playerAway;
    private bool _idlePoseRedrawsOnEnd;
    private Action? _idlePoseRedraw;

    public SwapEndWatcher(SwapModManager swapMods)
        => _swapMods = swapMods;

    public void Arm(SwapOptionEntry entry)
    {
        _armed = true;
        _armedEntry = entry;
        _isIdlePoseWatch = false;
        _playerAway = false;
        _idlePoseRedrawsOnEnd = false;
        _idlePoseRedraw = null;
        _armedPosition = Vector3.Zero;
        _watchedEmote = 0;

        if (NoireService.ObjectTable.LocalPlayer is { } localPlayer)
        {
            _armedPosition = localPlayer.Position;

            _watchedEmote = EmoteHelper.GetPlayingEmoteId(localPlayer);
        }

        if (_watchedEmote == 0)
            NoireLogger.LogDebug("Armed while the character is playing nothing; this watch ends next frame.", LogPrefix);

        EnsureSubscribed();
    }

    public void ArmIdlePose(SwapOptionEntry entry, Action redrawLocalPlayer)
    {
        _armed = true;
        _armedEntry = entry;
        _isIdlePoseWatch = true;
        _playerAway = false;
        _idlePoseRedrawsOnEnd = SwapOrchestrator.IdlePoseNeedsRedrawOnEnd(entry.IdlePoseIndex);
        _idlePoseRedraw = redrawLocalPlayer;
        _armedPosition = Vector3.Zero;
        _watchedEmote = 0;

        if (NoireService.ObjectTable.LocalPlayer is { } localPlayer)
            _armedPosition = localPlayer.Position;

        EnsureSubscribed();
    }

    // Stops watching and turns the swap off.
    public void Disarm() => Disarm(forceIdlePoseRedraw: true);

    private void Disarm(bool forceIdlePoseRedraw)
    {
        var wasArmed = _armed;
        var armedEntry = _armedEntry;

        var idlePoseRedraw = _isIdlePoseWatch && (forceIdlePoseRedraw || _idlePoseRedrawsOnEnd)
            ? _idlePoseRedraw
            : null;

        StopWatching();

        if (!wasArmed)
            return;

        if (armedEntry != null)
            _swapMods.DeselectEntry(armedEntry);

        idlePoseRedraw?.Invoke();
    }

    public bool StopWatchingIdlePose()
    {
        if (!_armed || !_isIdlePoseWatch)
            return false;

        StopWatching();
        return true;
    }

    // Stops watching without touching the swap, for lingering swaps
    public void StopWatching()
    {
        Unsubscribe();

        _armed = false;
        _armedEntry = null;
        _watchedEmote = 0;
        _isIdlePoseWatch = false;
        _playerAway = false;
        _idlePoseRedrawsOnEnd = false;
        _idlePoseRedraw = null;
    }

    private void EnsureSubscribed()
    {
        if (_subscribed)
            return;

        NoireService.Framework.Update += OnFrameworkUpdate;
        _subscribed = true;
    }

    private void Unsubscribe()
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
            NoireLogger.LogError(ex, "Could not evaluate the swap end conditions; ending the swap.", LogPrefix);
            Disarm();
        }
    }

    private void Evaluate()
    {
        if (!_armed)
        {
            StopWatching();
            return;
        }

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
        {
            _playerAway = true;
            return;
        }

        if (_playerAway)
        {
            _playerAway = false;
            _armedPosition = localPlayer.Position;

            NoireLogger.LogDebug("The character was redrawn. Watcher picking up where it left off.",
                LogPrefix);

            return;
        }

        if (localPlayer.Position != _armedPosition)
        {
            End("the player moved");
            return;
        }

        if (_isIdlePoseWatch)
            return;

        var playing = EmoteHelper.GetPlayingEmoteId(localPlayer);

        if (playing == _watchedEmote)
            return;

        End(playing == 0
            ? "the emote finished playing"
            : $"the player started emote {playing} instead");
    }

    private void End(string reason)
    {
        NoireLogger.LogDebug($"Ending the swap: {reason}"
            + (_isIdlePoseWatch
                ? _idlePoseRedrawsOnEnd ? ", with a redraw." : ", leaving the character as it is."
                : "."),
            LogPrefix);

        Disarm(forceIdlePoseRedraw: false);
    }
}
