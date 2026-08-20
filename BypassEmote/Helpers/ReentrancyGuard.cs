using System;
using System.Threading;

namespace BypassEmote.Helpers;

/// <summary>
/// Marks the window during which self-issued writes are in flight, so the change notifications they provoke
/// can be told apart from external ones. The depth counter nests, so a write made inside another write does
/// not close the window early.
/// </summary>
public sealed class ReentrancyGuard
{
    private int _depth;

    /// <summary> Gets a value indicating whether a self-issued write is in flight. </summary>
    public bool IsInside => Volatile.Read(ref _depth) > 0;

    /// <summary> Gets the number of nested writes currently in flight. </summary>
    public int Depth => Volatile.Read(ref _depth);

    /// <summary>
    /// Opens the window, closing it when the returned scope is disposed.
    /// </summary>
    /// <returns>The scope to dispose once the write completes.</returns>
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
            // Latched so a doubly-disposed scope cannot decrement past the depth it added.
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref owner._depth);
        }
    }
}
