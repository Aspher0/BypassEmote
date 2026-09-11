using BypassEmote.Models;
using NoireLib;
using NoireLib.Hooking;
using System;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

// Breaks the game's animation cache. The character keeps its animation bound in two
// trees on its draw object, keyed by a hash of the animation name
public sealed unsafe class AnimationRebinder : IDisposable
{
    private const string LogPrefix = "[AnimationRebinder] ";

    private const string HookGroup = "BypassEmote.Scheduler";

    // Client::System::Scheduler MotionPackBinding::UnloadFromCharacter
    private const string UnbindSignature = "40 53 48 83 EC 20 80 79 ?? ?? 48 8B D9 74 ?? 48 8B 49 ?? 48 89 7C 24";

    // The pack get-or-create
    private const string PackRequestSignature =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 30 41 8B F1 49 8B E8 44 8B F2 "
        + "48 8B F9 E8";

    private const int OwnerPackListOffset = 0x38;
    private const int NodeNextOffset = 0x10;
    private const int NodeBindingOffset = 0x18;
    private const int NodeStateOffset = 0x28;
    private const int BindingTypeOffset = 0x18;
    private const int BindingBoundOffset = 0x58;
    private const int BindingRowIdOffset = 0x5C;

    private const int MaxNodes = 512;

    private static readonly TimeSpan ArmLifetime = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();

    private readonly Dictionary<int, ArmedGroup> _armed = [];

    public delegate void UnbindDelegate(nint binding);

    public delegate nint PackRequestDelegate(nint owner, nint type, nint name, nint variant, nint useCache,
        nint a6, nint a7, nint a8);

    public NoireHook<UnbindDelegate>? UnbindHook { get; }

    public NoireHook<PackRequestDelegate>? PackRequestHook { get; }

    private sealed record ArmedGroup(uint EmoteRowId, IReadOnlyList<ushort> TimelineIds, DateTime ExpiresUtc);

    public readonly record struct BoundView(nint Node, nint Binding, int Type, int RowId, bool Bound);

    public AnimationRebinder()
    {
        try
        {
            UnbindHook = new NoireHook<UnbindDelegate>(
                UnbindSignature,
                binding => UnbindHook!.Original(binding),
                autoEnable: false, name: "UnbindMotionPack")
            {
                Group = HookGroup,
            };

            Log.Debug("Resolved the motion pack unbind entry", LogPrefix);
        }
        catch (Exception ex)
        {
            UnbindHook = null;
            Log.Warning($"Could not resolve the motion pack unbind entry ({ex.Message}).", LogPrefix);
        }

        try
        {
            PackRequestHook = new NoireHook<PackRequestDelegate>(
                PackRequestSignature, PackRequestDetour, autoEnable: true, name: "GetOrCreatePackRequest")
            {
                Group = HookGroup,
            };

            Log.Debug("Hooked the pack-request get-or-create", LogPrefix);
        }
        catch (Exception ex)
        {
            PackRequestHook = null;
            Log.Warning($"Could not hook the pack-request get-or-create ({ex.Message})", LogPrefix);
        }
    }

    private nint PackRequestDetour(nint owner, nint type, nint name, nint variant, nint useCache,
        nint a6, nint a7, nint a8)
    {
        if (TakeFresh(owner, (int)variant))
            useCache = 0;

        return PackRequestHook!.Original(owner, type, name, variant, useCache, a6, a7, a8);
    }

    public void Dispose()
    {
        PackRequestHook?.Dispose();
        UnbindHook?.Dispose();

        lock (_gate)
            _armed.Clear();
    }

    public bool Ready => UnbindHook is { IsDisposed: false }
        && PackRequestHook is { IsDisposed: false, IsEnabled: true };

    public string? Fault
    {
        get
        {
            if (UnbindHook is not { IsDisposed: false })
                return "the animation unbind hook was not found";

            if (PackRequestHook is not { IsDisposed: false })
                return "the pack request hook was not found";

            return PackRequestHook.IsEnabled ? null : "the 'GetOrCreatePackRequest' hook is switched off";
        }
    }

    public void Arm(EmoteAttributes emote)
    {
        if (!Ready || emote.AnimationTimelineIds is not { Count: > 0 } timelineIds)
            return;

        var group = new ArmedGroup(emote.RowId, timelineIds, DateTime.UtcNow + ArmLifetime);

        lock (_gate)
        {
            DropExpired();

            foreach (var timelineId in timelineIds)
                _armed[timelineId] = group;
        }

        Log.Debug($"Armed a fresh bind for {NameOf(emote)} ({string.Join(", ", timelineIds)}).",
            LogPrefix);
    }

    private static string NameOf(EmoteAttributes emote)
        => string.IsNullOrEmpty(emote.Command) ? $"emote {emote.RowId}" : $"/{emote.Command}";

    public void ArmEach(EmoteAttributes emote, TimeSpan lifetime)
    {
        if (!Ready || emote.AnimationTimelineIds is not { Count: > 0 } timelineIds)
            return;

        var expiresUtc = DateTime.UtcNow + lifetime;

        lock (_gate)
        {
            DropExpired();

            foreach (var timelineId in timelineIds)
                _armed[timelineId] = new ArmedGroup(emote.RowId, [timelineId], expiresUtc);
        }

        Log.Debug($"Armed a fresh bind for each of {NameOf(emote)}'s timelines "
            + $"({string.Join(", ", timelineIds)}), for {lifetime.TotalSeconds:0}s.", LogPrefix);
    }

    public bool TakeFresh(nint owner, int timelineId)
    {
        ArmedGroup? group;

        lock (_gate)
        {
            DropExpired();

            if (!_armed.TryGetValue(timelineId, out group))
                return false;

            foreach (var armedId in group.TimelineIds)
                _armed.Remove(armedId);
        }

        if (UnbindHook is not { IsDisposed: false } unbind || owner == 0)
            return false;

        var evicted = 0;

        try
        {
            foreach (var view in Read(owner))
            {
                if (view.RowId != timelineId || !view.Bound)
                    continue;

                unbind.Original(view.Binding);
                evicted++;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not evict timeline {timelineId}.", LogPrefix);
            return false;
        }

        Log.Debug($"Timeline {timelineId}: evicted {evicted} bound "
            + $"entr{(evicted == 1 ? "y" : "ies")}. Pack cache skipped once.", LogPrefix);

        return true;
    }

    private void DropExpired()
    {
        if (_armed.Count == 0)
            return;

        var now = DateTime.UtcNow;
        List<int>? stale = null;

        foreach (var (timelineId, group) in _armed)
        {
            if (group.ExpiresUtc > now)
                continue;

            stale ??= [];
            stale.Add(timelineId);
        }

        if (stale == null)
            return;

        foreach (var timelineId in stale)
            _armed.Remove(timelineId);
    }

    private static List<BoundView> Read(nint owner)
    {
        var bound = new List<BoundView>();
        var node = *(nint*)(owner + OwnerPackListOffset);

        for (var seen = 0; node > 0x10000 && seen < MaxNodes; seen++)
        {
            var binding = *(nint*)(node + NodeBindingOffset);

            if (binding > 0x10000 && *(int*)(node + NodeStateOffset) == 1)
            {
                bound.Add(new BoundView(node, binding, *(int*)(binding + BindingTypeOffset),
                    *(int*)(binding + BindingRowIdOffset), *(byte*)(binding + BindingBoundOffset) != 0));
            }

            node = *(nint*)(node + NodeNextOffset);
        }

        return bound;
    }
}
