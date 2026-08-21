using BypassEmote.Models;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
#if DEBUG
    internal sealed record SwapPreview
    {
        public EmoteAttributes? Source { get; init; }

        public uint? ResolvedFrom { get; init; }

        public string? Refusal { get; init; }

        public string? PipelineBlocked { get; init; }

        public bool HandedToGame { get; init; }

        public EmoteCondition Condition { get; init; }

        public PostureFlags Posture { get; init; }

        public string Skeleton { get; init; } = string.Empty;

        public List<EmoteAttributes> Pool { get; init; } = [];

        public List<(EmoteAttributes Candidate, string Reason)> Excluded { get; init; } = [];

        public bool GameGateApplied { get; init; }

        public MatchConfig? Config { get; init; }

        public MatchResult? Match { get; init; }

        public List<EmoteAttributes> Tier { get; init; } = [];

        public bool LoopsFirstFailed { get; init; }

        public bool TriesIdlePose { get; init; }

        public bool WouldAlternate { get; init; }

        public bool NoUsablePair { get; init; }
    }

    // Debug tab only for now
    internal SwapPreview Preview(uint sourceRowId, EmoteCondition rawCondition)
        => PreviewCore(sourceRowId, rawCondition) with { PipelineBlocked = PipelineBlockedBy() };

    private string? PipelineBlockedBy()
    {
        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
            return "Emote Swap is off, so no press reaches this pipeline.";

        if (NoireService.ClientState.IsGPosing)
            return "The swap stands down in gpose.";

        if (!_penumbra.Available)
            return PenumbraUnavailableMessage;

        if (_penumbra.GetPlayerCollection() is not { } collection)
            return PenumbraUnavailableMessage;

        return IsUnassignedCollection(collection.Id) ? NoCollectionMessage : null;
    }

    private SwapPreview PreviewCore(uint sourceRowId, EmoteCondition rawCondition)
    {
        var condition = DirectPlayPlanner.PlayableAsFor(rawCondition);

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
            return new SwapPreview { Condition = condition, Refusal = "There is no local player to read." };

        if (!_catalog.Ready)
            return new SwapPreview { Condition = condition, Refusal = CatalogLoadingMessage };

        if (ResolveSource(localPlayer, sourceRowId) is not { } source)
        {
            return new SwapPreview
            {
                Condition = condition,
                Refusal = "This emote is not in the catalog, so it can never be swapped.",
            };
        }

        var resolvedFrom = source.RowId != sourceRowId ? sourceRowId : (uint?)null;

        if (source.IsPoseFamily)
        {
            return new SwapPreview
            {
                Source = source,
                ResolvedFrom = resolvedFrom,
                Condition = condition,
                HandedToGame = true,
            };
        }

        if (!AllowedIn(source.RowId, condition))
        {
            return new SwapPreview
            {
                Source = source,
                ResolvedFrom = resolvedFrom,
                Condition = condition,
                Refusal = DirectPlayPlanner.RefusalMessage(source.Command, rawCondition),
            };
        }

        if (!EmoteHelper.MeetsEnvironmentFor(localPlayer, source.RowId))
        {
            return new SwapPreview
            {
                Source = source,
                ResolvedFrom = resolvedFrom,
                Condition = condition,
                Refusal = $"/{source.Command} needs {EmoteHelper.EnvironmentRequirementFor(source.RowId)}.",
            };
        }

        source = WithConditionVariant(source, condition);

        var skeleton = SkeletonFor(localPlayer);
        var posture = PostureForCondition(condition);
        var fallbackOrder = EmotePathHelper.GetFallbackOrder(skeleton);

        var askTheGame = EmoteHelper.ConditionOf(localPlayer) == rawCondition;

        var pool = new List<EmoteAttributes>();
        var excluded = new List<(EmoteAttributes Candidate, string Reason)>();

        foreach (var candidate in _catalog.All)
        {
            if (!candidate.EligibleTarget || candidate.RowId == source.RowId
                || !EmoteHelper.IsEmoteUnlocked(candidate.RowId))
            {
                continue;
            }

            if (PoolExclusionFor(localPlayer, candidate, condition, askTheGame) is { } reason)
                excluded.Add((candidate, reason));
            else
                pool.Add(candidate);
        }

        var poolHasLoop = pool.Any(candidate => candidate.LoopKind == EmotePlayType.Looped);

        var matchConfig = new MatchConfig(Configuration.LoopMatching, Configuration.TurnMatching,
            Configuration.SoundMatching, BlockedTargets());

        var moddedRule = Configuration.ModdedTargets;

        if (moddedRule != ModdedTargetRule.Allowed)
        {
            if (_penumbra.GetPlayerCollection() is { } collection)
                ForgetChangedTargetsOfAnotherCollection(collection.Id);

            if (moddedRule == ModdedTargetRule.Blocked)
            {
                matchConfig = matchConfig with { ModdedTargets = ChangedTargetRowIds(pool, skeleton, fallbackOrder) };
            }
            else
            {
                var kept = PoolAvoidingChangedTargets(source, pool, matchConfig, posture, skeleton, fallbackOrder);

                if (kept.Count != pool.Count)
                {
                    var keptIds = kept.Select(candidate => candidate.RowId).ToHashSet();

                    foreach (var candidate in pool)
                    {
                        if (!keptIds.Contains(candidate.RowId))
                            excluded.Add((candidate, "Changed by another mod"));
                    }

                    pool = kept;
                }
            }
        }

        var loopsFirst = source.LoopKind == EmotePlayType.Looped
            && matchConfig.Loop == LoopMatchRule.AllowLoopOnOneShot;

        var config = loopsFirst ? matchConfig with { Loop = LoopMatchRule.Strict } : matchConfig;
        var match = BestMatchResolver.Resolve(source, pool, config, posture);

        var triesIdlePose = ShouldAttemptIdlePoseFallback(source, match, Configuration.IdlePoseLoops, poolHasLoop);
        var loopsFirstFailed = false;

        if (match.Target == null && loopsFirst)
        {
            loopsFirstFailed = true;
            config = matchConfig;
            match = BestMatchResolver.Resolve(source, pool, config, posture);
        }

        var staleVulnerable = match.Target is { } best
            && IsStaleVulnerableShape(best.LoopKind, best.Intro,
                SourceCarriesOwnDistinctIntroFile(source, fallbackOrder));

        var spreads = Spreads(staleVulnerable);

        // The tier is what a dispatched repeat picks from. Asking for the pick itself is what must not happen
        // here: ResolveDispatchedTarget writes the memory the next real swap reads.
        var tier = spreads
            ? BestMatchResolver.ResolveSameTier(source, pool, config, posture,
                wantedCount: Math.Max(1, Configuration.MaxTargetsPerRank),
                maxScoreDistance: BestMatchResolver.DistanceFor(Configuration.DispatchFidelity)).Tier
            : BestMatchResolver.ResolveSameTier(source, pool, config, posture).Tier;

        return new SwapPreview
        {
            Source = source,
            ResolvedFrom = resolvedFrom,
            Condition = condition,
            Posture = posture,
            Skeleton = skeleton,
            Pool = pool,
            Excluded = excluded,
            GameGateApplied = askTheGame,
            Config = config,
            Match = match,
            Tier = tier,
            LoopsFirstFailed = loopsFirstFailed,
            TriesIdlePose = triesIdlePose,
            WouldAlternate = spreads,
            NoUsablePair = match.Target is { } target && PairVariants(source, target, skeleton).Count == 0,
        };
    }
#endif
}
