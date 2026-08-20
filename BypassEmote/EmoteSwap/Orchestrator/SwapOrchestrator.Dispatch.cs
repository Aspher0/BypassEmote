using BypassEmote.Helpers;
using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private sealed record DispatchAssignment(uint Target, long LastUseStamp);

    private readonly Dictionary<uint, DispatchAssignment> _dispatchFor = new();

    private long _dispatchClock;

    private bool Alternates() => Configuration.AlternateTargets switch
    {
        AlternateTargetsMode.TwoEmotes => true,
        AlternateTargetsMode.TwoEmotesWhenCacheBreakDown => !_residency.CacheBreakIntact,
        _ => false,
    };

    private MatchResult ResolveDispatchedTarget(EmoteAttributes source, IReadOnlyList<EmoteAttributes> pool,
        MatchConfig matchConfig, PostureFlags posture, int maxDistinctTargets = 0)
    {
        var (first, tier) = BestMatchResolver.ResolveSameTier(source, pool, matchConfig, posture, maxDistinctTargets);

        if (first.Target is null || tier.Count == 0)
            return first;

        if (_dispatchFor.TryGetValue(source.RowId, out var existing))
        {
            foreach (var candidate in tier)
            {
                if (candidate.RowId == existing.Target)
                {
                    _dispatchFor[source.RowId] = existing with { LastUseStamp = ++_dispatchClock };
                    return first with { Target = candidate };
                }
            }

            _dispatchFor.Remove(source.RowId);
        }

        var holderByTarget = new Dictionary<uint, (uint Source, long LastUseStamp)>();

        foreach (var (holder, assignment) in _dispatchFor)
        {
            if (holder != source.RowId)
                holderByTarget[assignment.Target] = (holder, assignment.LastUseStamp);
        }

        var picked = PickDispatchTarget(tier,
            targetRowId => holderByTarget.TryGetValue(targetRowId, out var holder) ? holder.LastUseStamp : null,
            maxDistinctTargets,
            _dispatchFor.Values.Select(assignment => assignment.Target).ToHashSet());

        if (picked == null)
            return first;

        if (holderByTarget.TryGetValue(picked.RowId, out var evictedHolder))
        {
            _dispatchFor.Remove(evictedHolder.Source);
            FeedbackHelper.DebugLine($">   dispatch: every same-tier target is held; /{source.Command} takes /{picked.Command} from its oldest holder");
        }
        else if (picked.RowId != first.Target.RowId)
        {
            FeedbackHelper.DebugLine($">   dispatch: /{first.Target.Command} is held by another source; /{source.Command} gets /{picked.Command}");
        }

        foreach (var duplicateHolder in _dispatchFor
                     .Where(entry => entry.Key != source.RowId && entry.Value.Target == picked.RowId)
                     .Select(entry => entry.Key)
                     .ToList())
        {
            _dispatchFor.Remove(duplicateHolder);
        }

        _dispatchFor[source.RowId] = new DispatchAssignment(picked.RowId, ++_dispatchClock);
        return first with { Target = picked };
    }

    public void ResetDispatchMemory()
    {
        _dispatchFor.Clear();
        _dispatchClock = 0;
    }

    internal static EmoteAttributes? PickDispatchTarget(IReadOnlyList<EmoteAttributes> tierCandidates,
        Func<uint, long?> holderLastUse, int maxDistinctTargets = 0,
        IReadOnlyCollection<uint>? targetsInUse = null)
    {
        if (maxDistinctTargets > 0 && targetsInUse is { Count: > 0 }
            && targetsInUse.Count >= maxDistinctTargets)
        {
            var capped = tierCandidates.Where(candidate => targetsInUse.Contains(candidate.RowId)).ToList();
            if (capped.Count > 0)
                tierCandidates = capped;
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
