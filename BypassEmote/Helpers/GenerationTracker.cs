using System.Threading;

namespace BypassEmote.Helpers;

/// <summary>
/// Decides which of several overlapping runs still owns a shared resource, so a run superseded while it was
/// away cannot tear down its successor's work. A run that claims ownership and then applies nothing must call
/// <see cref="Relinquish"/>, or older runs still in flight read themselves as superseded by a run that no
/// longer exists.
/// </summary>
public sealed class GenerationTracker
{
    private int _current;

    /// <summary> Gets the generation currently owning the resource. </summary>
    public int Current => Volatile.Read(ref _current);

    /// <summary>
    /// Claims the resource for the calling run.
    /// </summary>
    /// <returns>The generation the run must capture and hand to its own cleanups.</returns>
    public int TakeOwnership() => Interlocked.Increment(ref _current);

    /// <summary>
    /// Reports whether a generation still owns the resource.
    /// </summary>
    /// <param name="generation">The generation a run captured from <see cref="TakeOwnership"/>.</param>
    /// <returns>True when the generation is still current.</returns>
    public bool IsCurrent(int generation) => Volatile.Read(ref _current) == generation;

    /// <summary>
    /// Undoes a <see cref="TakeOwnership"/> that applied nothing, doing nothing when a newer run has since
    /// claimed the resource.
    /// </summary>
    /// <param name="generation">The generation to hand back.</param>
    /// <returns>True when the claim was handed back, false when a newer run already holds it.</returns>
    public bool Relinquish(int generation)
        => Interlocked.CompareExchange(ref _current, generation - 1, generation) == generation;
}
