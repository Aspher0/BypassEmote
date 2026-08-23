using NoireLib;
using NoireLib.Helpers.Memory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed unsafe partial class SchedulerResidencyProbe
{
    private const long ReleaseWindowMs = 500;

    private const long CacheBreakWindowMs = 1500;

    private readonly object _releaseLock = new();

    private HashSet<string> _releaseKeys = new(StringComparer.Ordinal);
    private HashSet<string> _releaseNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, nint> _freshReleasePacks = new(StringComparer.Ordinal);

    private volatile string? _releasedSource;

    private long _releaseArmedTick;

    private Dictionary<string, nint>? _cacheBreakNameMap;
    private HashSet<string> _cacheBreakAliases = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cacheBreakSubstitutedThisWindow = new(StringComparer.Ordinal);
    private readonly Dictionary<string, nint> _freshPackByAlias = new(StringComparer.Ordinal);

    private long _cacheBreakArmedTick;
    private long _freshLoadStamp;

    private bool ReleaseOpen
    {
        get
        {
            var armed = _releaseArmedTick;
            var since = Environment.TickCount64 - armed;
            return armed != 0 && since >= 0 && since <= ReleaseWindowMs;
        }
    }

    private bool CacheBreakOpen
    {
        get
        {
            var armed = _cacheBreakArmedTick;
            var since = Environment.TickCount64 - armed;
            return armed != 0 && since >= 0 && since <= CacheBreakWindowMs;
        }
    }

    private bool CacheBreakSubstituteOpen
    {
        get
        {
            var armed = _cacheBreakArmedTick;
            var since = Environment.TickCount64 - armed;
            return armed != 0 && since >= 0 && since <= SubstituteWindowMs;
        }
    }

    public void ArmRelease(IReadOnlyCollection<string> keys, IReadOnlyCollection<string> names,
        string? releasedSource)
    {
        if (keys.Count == 0 || !SwapLayers.ReleaseCachedPacks)
            return;

        var avoid = releasedSource == null ? null : ShortSourceName(releasedSource);

        lock (_releaseLock)
        {
            _releaseKeys = new HashSet<string>(keys, StringComparer.Ordinal);
            _releaseNames = new HashSet<string>(names, StringComparer.Ordinal);
            _freshReleasePacks.Clear();
            _releasedSource = avoid;
            _releaseArmedTick = Environment.TickCount64;
        }

        NoireLogger.LogDebug($"Release armed for [{string.Join(", ", keys)}]"
            + (avoid == null ? "." : $", moving off [{avoid}]."), LogPrefix);
    }

    public void ArmCacheBreak(IReadOnlyDictionary<string, string> aliasByKey, IReadOnlyCollection<string> names)
    {
        if (aliasByKey.Count == 0 || !SwapLayers.ReleaseCachedPacks)
            return;

        var map = new Dictionary<string, nint>(aliasByKey.Count, StringComparer.Ordinal);

        foreach (var (key, alias) in aliasByKey)
        {
            lock (IssuedNamesGate)
                IssuedUniqueNames.Add(alias);

            map[key] = NamePool.Pin(alias);
        }

        lock (_releaseLock)
        {
            var aliases = new HashSet<string>(aliasByKey.Values, StringComparer.Ordinal);

            foreach (var stale in _freshPackByAlias.Keys.Where(alias => !aliases.Contains(alias)).ToList())
                _freshPackByAlias.Remove(stale);

            _cacheBreakAliases = aliases;
            _cacheBreakNameMap = map;
            _cacheBreakSubstitutedThisWindow.Clear();
            _cacheBreakArmedTick = Environment.TickCount64;
        }

        ArmRelease(aliasByKey.Keys.ToList(), names, releasedSource: null);

        NoireLogger.LogDebug("Cache break armed: "
            + string.Join(", ", aliasByKey.Select(entry => $"'{entry.Key}' -> '{entry.Value}'")) + ".", LogPrefix);
    }

    private nint? CacheBreakSubstitute(string door, nint a1, string requested)
    {
        Dictionary<string, nint>? map;
        lock (_releaseLock)
            map = _cacheBreakNameMap;

        if (map == null || Environment.TickCount64 - _cacheBreakArmedTick > SubstituteWindowMs)
            return null;

        if (!map.TryGetValue(requested, out var aliasBuffer))
            return null;

        lock (_releaseLock)
        {
            if (Environment.TickCount64 - _cacheBreakArmedTick > SubstituteWindowMs)
                return null;

            if (!_cacheBreakSubstitutedThisWindow.Add(requested) && (a1 == 0 || a1 != _ourPackOwner))
                return null;
        }

        if (a1 != 0)
            _ourPackOwner = a1;

        NoireLogger.LogDebug(
            $"MotionPack name substituted [cache break, {door}]: '{requested}' -> '{ReadCString(aliasBuffer)}' (a1 0x{a1:X}).",
            LogPrefix);
        return aliasBuffer;
    }

    private void NoteFreshLoad(nint namePointer, nint pack)
    {
        try
        {
            if (pack == 0 || namePointer == 0)
                return;

            if (_republishing && Environment.CurrentManagedThreadId == _republishThreadId)
                return;

            var first = *(byte*)namePointer;
            if (first < 0x20 || first > 0x7E)
                return;

            var name = ReadCString(namePointer);

            lock (_releaseLock)
            {
                if (_cacheBreakNameMap != null && _cacheBreakAliases.Contains(name))
                {
                    _freshPackByAlias[name] = pack;
                    _freshLoadStamp = _cacheBreakArmedTick;
                }
                else if (ReleaseOpen && _releaseKeys.Contains(name))
                {
                    _freshReleasePacks[name] = pack;
                }
                else
                {
                    return;
                }
            }

            NoireLogger.LogDebug($"Fresh load of '{name}' as pack 0x{pack:X}.", LogPrefix);
        }
        catch
        {
        }
    }

    private nint[] FreshPacksSnapshot()
    {
        lock (_releaseLock)
        {
            var cacheBreak = CacheBreakOpen && _freshPackByAlias.Count > 0;
            var release = ReleaseOpen && _freshReleasePacks.Count > 0;

            if (!cacheBreak && !release)
                return [];

            var packs = new List<nint>((cacheBreak ? _freshPackByAlias.Count : 0)
                + (release ? _freshReleasePacks.Count : 0));

            if (cacheBreak)
                packs.AddRange(_freshPackByAlias.Values);

            if (release)
                packs.AddRange(_freshReleasePacks.Values);

            return packs.ToArray();
        }
    }

    private bool CacheBreakDeclares(string text)
    {
        foreach (var candidate in FreshPacksSnapshot())
        {
            if (candidate != 0 && IndexOfPackNamePerEntry(candidate, text) >= 0)
                return true;
        }

        return false;
    }

    private nint? CacheBreakBoundPack(nint packSet, string text, nint chosen)
    {
        foreach (var candidate in FreshPacksSnapshot())
        {
            if (candidate == 0 || IndexOfPackNamePerEntry(candidate, text) < 0)
                continue;

            RememberBoundPack(packSet, text, candidate, chosen);

            if (candidate != chosen && TakeBindingLogSlot())
                NoireLogger.LogDebug($"Fresh binding for '{text}': pack 0x{chosen:X} -> 0x{candidate:X}.", LogPrefix);

            return candidate;
        }

        return null;
    }

    private bool FreshLoadThisWindow
    {
        get
        {
            lock (_releaseLock)
                return CacheBreakOpen && _freshLoadStamp == _cacheBreakArmedTick && _freshLoadStamp != 0;
        }
    }

    private bool ReleasedThisWindow(string name)
    {
        if (!SwapLayers.ReleaseBindNewest || !ReleaseOpen)
            return false;

        lock (_releaseLock)
            return _releaseNames.Contains(name);
    }

    private nint PackDeclaringOtherThanReleased(nint packSet, string text, nint chosen)
    {
        var avoid = _releasedSource;
        if (avoid == null && !FreshLoadThisWindow)
            return chosen;

        var newest = (nint)0;

        for (var group = 0; group < PackScanLayout.Groups; group++)
        {
            for (var slot = 0; slot < PackScanLayout.SlotsPerGroup; slot++)
            {
                var vector = (nint)PackScanLayout.VectorAddress(packSet, group, slot);

                if (!GuardedMemory.TryReadPointer(vector, out var begin)
                    || !GuardedMemory.TryReadPointer(vector + 8, out var end))
                {
                    continue;
                }

                if (!PackScanLayout.IsPlausibleVector(begin, end))
                    continue;

                var count = PackScanLayout.PackCount(begin, end);

                for (var index = 0; index < count; index++)
                {
                    if (!GuardedMemory.TryReadPointer(begin + (index * 8), out var candidate) || candidate == 0)
                        continue;

                    if (IndexOfPackNamePerEntry(candidate, text) < 0)
                        continue;

                    if (avoid != null
                        && string.Equals(SourceOfPackContent(candidate, text), avoid, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    newest = candidate;
                }
            }
        }

        if (newest == 0 || newest == chosen)
        {
            if (TakeBindingLogSlot())
            {
                NoireLogger.LogDebug($"Release binding for '{text}': nothing resident to move to"
                    + (avoid == null ? string.Empty : $" off [{avoid}]") + $"; pack 0x{chosen:X} kept.", LogPrefix);
            }

            return chosen;
        }

        if (TakeBindingLogSlot())
        {
            NoireLogger.LogDebug($"Release binding for '{text}': pack 0x{chosen:X} -> 0x{newest:X}"
                + (avoid == null ? " (newest after a fresh load)." : $", moving off [{avoid}]."), LogPrefix);
        }

        return newest;
    }
}
