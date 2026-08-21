using BypassEmote.Helpers;
using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal readonly record struct DispatchRank(EmotePlayType LoopKind, TurnClass Turn, SoundClass Sound);

    private sealed record DispatchAssignment(uint Target, DispatchRank Rank, long LastUseStamp);

    private readonly Dictionary<uint, DispatchAssignment> _dispatchFor = new();

    private long _dispatchClock;

    private bool _dispatchLoaded;

    internal static DispatchRank RankOf(EmoteAttributes emote) => new(emote.LoopKind, emote.Turn, emote.Sound);

    private static int RankBudget() => Math.Max(1, Configuration.MaxTargetsPerRank);

    private bool Spreads(bool staleVulnerable) => Configuration.CachedDispatch switch
    {
        CachedDispatchMode.On => true,
        CachedDispatchMode.WhenNecessary => staleVulnerable && !_residency.CacheBreakIntact,
        _ => false,
    };

    private MatchResult ResolveDispatchedTarget(EmoteAttributes source, IReadOnlyList<EmoteAttributes> pool,
        MatchConfig matchConfig, PostureFlags posture)
    {
        LoadDispatchOnce();

        var budget = RankBudget();

        var (first, tier) = BestMatchResolver.ResolveSameTier(source, pool, matchConfig, posture,
            wantedCount: budget, maxScoreDistance: BestMatchResolver.DistanceFor(Configuration.DispatchFidelity));

        if (first.Target is null || tier.Count == 0)
            return first;

        var rank = RankOf(source);

        if (_dispatchFor.TryGetValue(source.RowId, out var existing))
        {
            foreach (var candidate in tier)
            {
                if (candidate.RowId != existing.Target)
                    continue;

                Remember(source.RowId, candidate.RowId, rank);
                return first with { Target = candidate };
            }

            _dispatchFor.Remove(source.RowId);
        }

        var heldInRank = TargetsHeldInRank(rank, source.RowId);

        var picked = PickDispatchTarget(tier,
            targetRowId => heldInRank.TryGetValue(targetRowId, out var stamp) ? stamp : null,
            budget,
            heldInRank.Keys.ToHashSet());

        if (picked == null)
            return first;

        Remember(source.RowId, picked.RowId, rank);

        if (picked.RowId != first.Target.RowId)
        {
            FeedbackHelper.DebugLine(heldInRank.ContainsKey(picked.RowId)
                ? $">   dispatch: every target of this rank is taken; /{source.Command} shares /{picked.Command}"
                : $">   dispatch: /{first.Target.Command} is held by another emote; /{source.Command} gets /{picked.Command}");
        }

        return first with { Target = picked };
    }

    private Dictionary<uint, long> TargetsHeldInRank(DispatchRank rank, uint exceptSource)
    {
        var held = new Dictionary<uint, long>();

        foreach (var (holder, assignment) in _dispatchFor)
        {
            if (holder == exceptSource || assignment.Rank != rank)
                continue;

            if (!held.TryGetValue(assignment.Target, out var stamp) || assignment.LastUseStamp > stamp)
                held[assignment.Target] = assignment.LastUseStamp;
        }

        return held;
    }

    private void Remember(uint sourceRowId, uint targetRowId, DispatchRank rank)
    {
        _dispatchFor[sourceRowId] = new DispatchAssignment(targetRowId, rank, ++_dispatchClock);
        SaveDispatch();
    }

    public void ResetDispatchMemory()
    {
        _dispatchFor.Clear();
        _dispatchClock = 0;
        _dispatchLoaded = true;

        SaveDispatch();
    }

    private void SaveDispatch()
        => _swapMods.SaveDispatch(_dispatchFor
            .Select(entry => new DispatchRecord(entry.Key, entry.Value.Target, entry.Value.LastUseStamp))
            .ToList());

    private void LoadDispatchOnce()
    {
        if (_dispatchLoaded || !_catalog.Ready)
            return;

        _dispatchLoaded = true;

        foreach (var record in _swapMods.Registry.Dispatch ?? [])
        {
            if (_catalog.Get(record.SourceEmote) is not { } source)
                continue;

            _dispatchFor[record.SourceEmote] = new DispatchAssignment(record.TargetEmote, RankOf(source), record.LastUseStamp);
            _dispatchClock = Math.Max(_dispatchClock, record.LastUseStamp);
        }
    }

    internal static EmoteAttributes? PickDispatchTarget(IReadOnlyList<EmoteAttributes> tierCandidates,
        Func<uint, long?> holderLastUse, int budget = 0, IReadOnlyCollection<uint>? targetsInUse = null)
    {
        if (budget > 0 && targetsInUse is { Count: > 0 } && targetsInUse.Count >= budget)
        {
            var withinBudget = tierCandidates.Where(candidate => targetsInUse.Contains(candidate.RowId)).ToList();

            if (withinBudget.Count > 0)
                tierCandidates = withinBudget;
        }

        EmoteAttributes? oldestHeld = null;
        var oldestStamp = long.MaxValue;

        foreach (var candidate in tierCandidates)
        {
            if (holderLastUse(candidate.RowId) is not { } stamp)
                return candidate;

            if (stamp < oldestStamp)
            {
                oldestStamp = stamp;
                oldestHeld = candidate;
            }
        }

        return oldestHeld;
    }
}
