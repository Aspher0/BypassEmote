using BypassEmote.IPC;
using BypassEmote.Models;
using NoireLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace BypassEmote.EmoteSwap;

public sealed class CacheBreaker : IDisposable
{
    private const string LogPrefix = "[CacheBreaker] ";

    private const string TemporaryModTag = "BypassEmote.CacheBreak";
    private const int TemporaryModPriority = int.MaxValue;
    internal const string VanillaCopyFolder = "cache-break";
    private const int IdentityHexLength = 10;

    private readonly IPCCaller_Penumbra _penumbra;
    private readonly SchedulerResidencyProbe _residency;
    private readonly string _vanillaCopyDirectory;

    private readonly Dictionary<string, string> _vanillaCopies = new(StringComparer.Ordinal);

    private Guid _appliedCollection = Guid.Empty;
    private Dictionary<string, string>? _appliedRedirects;

    public CacheBreaker(IPCCaller_Penumbra penumbra, SchedulerResidencyProbe residency, string configDirectory)
    {
        _penumbra = penumbra;
        _residency = residency;
        _vanillaCopyDirectory = Path.Combine(configDirectory, VanillaCopyFolder);

        _penumbra.AvailabilityChanged += OnPenumbraAvailabilityChanged;
    }

    public void Dispose()
    {
        _penumbra.AvailabilityChanged -= OnPenumbraAvailabilityChanged;

        if (_appliedCollection == Guid.Empty)
            return;

        _penumbra.ClearTemporaryRedirects(TemporaryModTag, _appliedCollection, TemporaryModPriority);
        _appliedCollection = Guid.Empty;
        _appliedRedirects = null;
    }

    private void OnPenumbraAvailabilityChanged(bool available)
    {
        _appliedCollection = Guid.Empty;
        _appliedRedirects = null;
    }

    public void BreakFor(EmoteAttributes emote, IReadOnlyList<string> fallbackOrder, IReadOnlyList<string> names)
    {
        try
        {
            if (!_penumbra.Available)
                return;

            if (_penumbra.GetPlayerCollection() is not { } collection
                || SwapOrchestrator.IsUnassignedCollection(collection.Id))
            {
                return;
            }

            var plan = Plan(RequestedPathsFor(emote, fallbackOrder), fallbackOrder, ContentFor);

            if (plan.AliasByKey.Count == 0)
            {
                NoireLogger.LogDebug($"No alias found for /{emote.Command}.", LogPrefix);
                return;
            }

            if (!ApplyRedirects(collection.Id, plan.Redirects))
                return;

            _residency.ArmCacheBreak(plan.AliasByKey, names);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Cache break for /{emote.Command} failed.", LogPrefix);
        }
    }

    internal sealed record CacheBreakPlan(
        IReadOnlyDictionary<string, string> AliasByKey, IReadOnlyDictionary<string, string> Redirects);

    internal static CacheBreakPlan Plan(IReadOnlyList<string> requestedPaths, IReadOnlyList<string> fallbackOrder,
        Func<string, (string File, string Identity)?> contentFor)
    {
        var aliasByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var redirects = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var requested in requestedPaths)
        {
            if (UniqueNamePlanner.TimelineKeyFromPapPath(requested) is not { } key || aliasByKey.ContainsKey(key))
                continue;

            if (contentFor(requested) is not { } content)
                continue;

            if (UniqueNamePlanner.UniqueNameFor(key, content.Identity) is not { } alias)
                continue;

            if (UniqueNamePlanner.ComposedPapPath(requested, key, alias) is not { } composed)
                continue;

            foreach (var chainPath in UniqueNamePlanner.ComposedPapPathsForChain(composed, fallbackOrder))
                redirects[chainPath] = content.File;

            aliasByKey[key] = alias;
        }

        return new CacheBreakPlan(aliasByKey, redirects);
    }

    private List<string> RequestedPathsFor(EmoteAttributes emote, IReadOnlyList<string> fallbackOrder)
    {
        var paths = new List<string>(emote.Variants.Count + 1);

        void Add(string relative)
        {
            if (SwapOrchestrator.SelectRequestedPath(relative, fallbackOrder, ModProvides, VanillaExists) is { } path
                && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        foreach (var variant in emote.Variants)
            Add(variant.RelativePapPath);

        if (emote.IntroRelativePapPath is { } intro)
            Add(intro);

        return paths;
    }

    private bool ModProvides(string requestedPath)
        => !string.Equals(_penumbra.ResolvePlayerPath(requestedPath), requestedPath, StringComparison.Ordinal);

    private static bool VanillaExists(string gamePath) => NoireService.DataManager.FileExists(gamePath);

    private (string File, string Identity)? ContentFor(string requestedPath)
    {
        var resolved = _penumbra.ResolvePlayerPath(requestedPath);

        if (!string.Equals(resolved, requestedPath, StringComparison.Ordinal))
            return (resolved, $"{resolved}|{StampOf(resolved)}");

        return VanillaCopyFor(requestedPath) is { } copy ? (copy, $"vanilla|{requestedPath}") : null;
    }

    private static long StampOf(string filePath)
    {
        try
        {
            return File.GetLastWriteTimeUtc(filePath).Ticks;
        }
        catch
        {
            return 0;
        }
    }

    private string? VanillaCopyFor(string gamePath)
    {
        if (_vanillaCopies.TryGetValue(gamePath, out var existing))
        {
            if (File.Exists(existing))
                return existing;

            _vanillaCopies.Remove(gamePath);
        }

        try
        {
            if (NoireService.DataManager.GetFile(gamePath)?.Data is not { Length: > 0 } bytes)
                return null;

            var name = "vanilla_" + Convert.ToHexString(SHA1.HashData(bytes))[..IdentityHexLength].ToLowerInvariant() + ".pap";
            var path = Path.Combine(_vanillaCopyDirectory, name);

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(_vanillaCopyDirectory);
                File.WriteAllBytes(path, bytes);
                NoireLogger.LogDebug($"Copied vanilla '{gamePath}' to '{name}' ({bytes.Length} bytes).", LogPrefix);
            }

            _vanillaCopies[gamePath] = path;
            return path;
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Could not copy vanilla '{gamePath}' ({ex.Message}).", LogPrefix);
            return null;
        }
    }

    private bool ApplyRedirects(Guid collectionId, IReadOnlyDictionary<string, string> redirects)
    {
        if (_appliedCollection == collectionId && SameRedirects(_appliedRedirects, redirects))
            return true;

        if (_appliedCollection != Guid.Empty && _appliedCollection != collectionId)
            _penumbra.ClearTemporaryRedirects(TemporaryModTag, _appliedCollection, TemporaryModPriority);

        if (!_penumbra.SetTemporaryRedirects(TemporaryModTag, collectionId, redirects, TemporaryModPriority))
        {
            NoireLogger.LogDebug("Penumbra did not take the alias redirects.", LogPrefix);
            return false;
        }

        _appliedCollection = collectionId;
        _appliedRedirects = new Dictionary<string, string>(redirects, StringComparer.Ordinal);
        return true;
    }

    internal static bool SameRedirects(IReadOnlyDictionary<string, string>? applied,
        IReadOnlyDictionary<string, string> wanted)
    {
        if (applied == null || applied.Count != wanted.Count)
            return false;

        foreach (var (gamePath, file) in wanted)
        {
            if (!applied.TryGetValue(gamePath, out var current) || !string.Equals(current, file, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
