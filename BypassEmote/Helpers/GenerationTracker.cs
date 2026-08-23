using System.Threading;

namespace BypassEmote.Helpers;

public sealed class GenerationTracker
{
    private int _current;

    public int Current => Volatile.Read(ref _current);

    public int TakeOwnership() => Interlocked.Increment(ref _current);

    public bool IsCurrent(int generation) => Volatile.Read(ref _current) == generation;

    public bool Relinquish(int generation)
        => Interlocked.CompareExchange(ref _current, generation - 1, generation) == generation;
}
