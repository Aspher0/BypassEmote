using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using NoireLib;
using NoireLib.Animations.Helpers;
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
    private Vector3 _armedPosition;

    private ushort _watchedEmote;

    private bool _isIdlePoseWatch;
    private Action? _idlePoseRedraw;
    private CharacterPoseState _armedPoseState;

    public SwapEndWatcher(SwapModManager swapMods)
        => _swapMods = swapMods;

    public void Arm()
    {
        _armed = true;
        _isIdlePoseWatch = false;
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

    public void ArmIdlePose(Action redrawLocalPlayer)
    {
        _armed = true;
        _isIdlePoseWatch = true;
        _idlePoseRedraw = redrawLocalPlayer;
        _armedPosition = Vector3.Zero;
        _watchedEmote = 0;

        if (NoireService.ObjectTable.LocalPlayer is { } localPlayer)
        {
            _armedPosition = localPlayer.Position;
            SnapshotPoseState(localPlayer);
        }

        EnsureSubscribed();
    }

    // Stops watching and turns the swap off.
    public void Disarm()
    {
        var wasArmed = _armed;
        var idlePoseRedraw = _isIdlePoseWatch ? _idlePoseRedraw : null;

        StopWatching();

        if (!wasArmed)
            return;

        _swapMods.Deactivate();
        idlePoseRedraw?.Invoke();
    }

    // Stops watching without touching the swap, for lingering swaps
    public void StopWatching()
    {
        Unsubscribe();

        _armed = false;
        _watchedEmote = 0;
        _isIdlePoseWatch = false;
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
            End("the local player is gone");
            return;
        }

        if (localPlayer.Position != _armedPosition)
        {
            End("the player moved");
            return;
        }

        var playing = EmoteHelper.GetPlayingEmoteId(localPlayer);

        if (_isIdlePoseWatch)
        {
            if (playing != 0)
                End($"the player started emote {playing}");
            else if (LeftArmedPoseState(localPlayer))
                End("the player left the pose the swap was applied to");

            return;
        }

        if (playing == _watchedEmote)
            return;

        End(playing == 0
            ? "the emote finished playing"
            : $"the player started emote {playing} instead");
    }

    private void End(string reason)
    {
        NoireLogger.LogDebug($"Ending the swap: {reason}.", LogPrefix);
        Disarm();
    }

    private void SnapshotPoseState(ICharacter character)
        => _armedPoseState = CharacterPoseState.Read(character);

    private bool LeftArmedPoseState(ICharacter character)
        => IdlePoseWatchEnded(_armedPoseState, CharacterPoseState.Read(character));

    internal static bool IdlePoseWatchEnded(CharacterPoseState armed, CharacterPoseState now)
        => armed != now;
}
