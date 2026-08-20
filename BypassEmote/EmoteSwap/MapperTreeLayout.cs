using NoireLib.Helpers.Memory;

namespace BypassEmote.EmoteSwap;

internal enum MapperSourceOutcome
{
    // A guarded read refused, or the descent ran deeper than a real tree can be.
    Unreadable,

    // The animation belongs to this skeleton, so nothing needs retargeting.
    Native,

    Missing,

    Found,
}

internal readonly struct MapperSource
{
    private MapperSource(MapperSourceOutcome outcome, long value)
    {
        Outcome = outcome;
        Value = value;
    }

    internal MapperSourceOutcome Outcome { get; }

    // The retarget object, meaningful only when the outcome is Found.
    internal long Value { get; }

    internal static MapperSource Unreadable() => new(MapperSourceOutcome.Unreadable, 0);

    internal static MapperSource Native() => new(MapperSourceOutcome.Native, 0);

    internal static MapperSource Missing() => new(MapperSourceOutcome.Missing, 0);

    internal static MapperSource Found(long value) => new(MapperSourceOutcome.Found, value);
}

internal enum MapperAction
{
    KeepGameAnswer,

    // Hand back the game's answer and stop correcting for the rest of the session.
    Disarm,

    InstallNone,
    InstallFound,
}

// Address arithmetic for the retarget maps a skeleton keeps, and the walk that reads one. A skeleton keeps two
// ordered maps from a pack's skeleton key to the retarget an animation off that pack is sampled through. The game
// takes the key off the first pack in the set declaring the name, which is not always the pack that is playing.
// Only the arithmetic is here; the reads stay behind the caller's guard.
internal static class MapperTreeLayout
{
    // The skeleton a pack was authored for.
    internal const int PackSkeletonKeyOffset = 0xDC;

    // The skeleton's own key, compared against a pack's before any map is consulted.
    internal const int OwnerSkeletonKeyOffset = 0x11C;

    // The keys the skeleton's maps were built from.
    internal const int SkeletonChainOffset = 0x120;

    internal const int SkeletonChainCount = 4;

    // Head node of the map consulted first, and only when the caller's flag is set.
    internal const int PrimaryMapOffset = 0xD8;

    // Head node of the map consulted otherwise, and after a miss in the primary one.
    internal const int FallbackMapOffset = 0xE8;

    internal static MapperSource Find(IGuardedMemory memory, long head, uint key)
        => StdMapReader.TryFind(memory, head, key, out var value) switch
        {
            StdMapLookup.Found => MapperSource.Found(value),
            StdMapLookup.Missing => MapperSource.Missing(),
            _ => MapperSource.Unreadable(),
        };

    // The retarget a skeleton holds for a pack's key. None when the pack is the skeleton's own.
    internal static MapperSource Select(IGuardedMemory memory, long owner, uint ownerKey, uint packKey, byte flag)
    {
        if (packKey == ownerKey)
            return MapperSource.Native();

        if (flag != 0)
        {
            var primary = FindInMap(memory, owner + PrimaryMapOffset, packKey);
            if (primary.Outcome is MapperSourceOutcome.Found or MapperSourceOutcome.Unreadable)
                return primary;
        }

        return FindInMap(memory, owner + FallbackMapOffset, packKey);
    }

    private static MapperSource FindInMap(IGuardedMemory memory, long headField, uint key)
        => memory.IsReadable(headField, sizeof(long))
            ? Find(memory, memory.ReadInt64(headField), key)
            : MapperSource.Unreadable();

    // What to do with the game's answer, given the retarget the pack that is playing calls for
    internal static MapperAction Decide(MapperSource computed, long original, bool boundIsGameChoice)
    {
        if (computed.Outcome == MapperSourceOutcome.Unreadable)
            return MapperAction.KeepGameAnswer;

        var value = computed.Outcome == MapperSourceOutcome.Found ? computed.Value : 0;

        if (boundIsGameChoice)
            return value == original ? MapperAction.KeepGameAnswer : MapperAction.Disarm;

        if (value == original)
            return MapperAction.KeepGameAnswer;

        return computed.Outcome == MapperSourceOutcome.Found
            ? MapperAction.InstallFound
            : MapperAction.InstallNone;
    }
}
