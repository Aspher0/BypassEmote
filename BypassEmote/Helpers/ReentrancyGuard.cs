using System;
using System.Threading;

namespace BypassEmote.Helpers;

public sealed class ReentrancyGuard
{
    private int _depth;

    public bool IsInside => Volatile.Read(ref _depth) > 0;

    public int Depth => Volatile.Read(ref _depth);

    public IDisposable Enter()
    {
        Interlocked.Increment(ref _depth);
        return new Scope(this);
    }

    private sealed class Scope(ReentrancyGuard owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref owner._depth);
        }
    }
}
