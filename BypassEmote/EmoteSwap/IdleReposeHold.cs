#if DEBUG
using NoireLib;
using NoireLib.Hooking;
using System;
using System.Threading;

namespace BypassEmote.EmoteSwap;

public sealed class IdleReposeHold : IDisposable
{
    private const string IdlePoseUpdaterSignature = "40 53 56 57 41 56 48 83 EC 38 48 8B 1D ?? ?? ?? ?? 33 F6";

    private delegate byte IdlePoseUpdaterDelegate(nint state);

    private readonly NoireHook<IdlePoseUpdaterDelegate>? _hook;

    private static int _holdCount;

    private static bool _announcedThisHold;

    public static bool Holding => Volatile.Read(ref _holdCount) > 0;

    public IdleReposeHold()
    {
        try
        {
            _hook = new NoireHook<IdlePoseUpdaterDelegate>(IdlePoseUpdaterSignature, Detour, false, "IdlePoseUpdater").SetGroup("BypassEmote.IdlePose");
            NoireLogger.LogDebug("Resolved the idle-pose updater; parked, so gate pauses no longer hold the automatic re-pose.");
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the idle-pose updater; gate pauses will keep the pre-hold behavior.");
            _hook = null;
        }
    }

    public static void Hold()
    {
        _announcedThisHold = false;
        Interlocked.Increment(ref _holdCount);
    }

    public static void Release()
    {
        if (Interlocked.Decrement(ref _holdCount) < 0)
            Interlocked.Exchange(ref _holdCount, 0);
    }

    private byte Detour(nint state)
    {
        try
        {
            if (Holding)
            {
                if (!_announcedThisHold)
                {
                    _announcedThisHold = true;
                    NoireLogger.LogDebug("Intercepted the automatic idle re-pose (timer held for this gate pause).");
                }

                return 0;
            }
        }
        catch
        {
            // An unreadable flag must never break the game's own updater.
        }

        return _hook!.Original(state);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _holdCount, 0);
        _hook?.Dispose();
    }
}
#endif
