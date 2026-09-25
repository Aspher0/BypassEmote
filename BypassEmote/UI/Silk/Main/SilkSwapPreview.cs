using BypassEmote.Enums;
using BypassEmote.Localization;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

internal enum SilkPreviewKind
{
    Target,
    IdlePose,
    None,
}

internal sealed record SilkSwapPlan(
    SilkPreviewKind Kind,
    EmoteAttributes? Source,
    EmoteAttributes? Target,
    bool FromOverride,
    byte PoseIndex,
    uint PoseRowId,
    string Headline,
    string[] Details);

public sealed partial class SwapOrchestrator
{
    private static readonly string[] NoDetails = [];

    internal SilkSwapPlan SilkPreviewTarget(uint sourceRowId)
    {
        try
        {
            return SilkPreviewCore(sourceRowId);
        }
        catch (Exception ex)
        {
            Log.Debug($"Swap preview of {sourceRowId} failed: {ex.Message}", LogPrefix);
            return Nothing(ShortLine(GenericFailureMessage.Display));
        }
    }

    private static SilkSwapPlan Nothing(string headline, List<string>? details = null)
        => new(SilkPreviewKind.None, null, null, false, 0, 0, headline,
            details is { Count: > 0 } ? details.ToArray() : NoDetails);

    private SilkSwapPlan SilkPreviewCore(uint sourceRowId)
    {
        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
            return Nothing(L.PreviewSwapOff.Text);

        if (NoireService.ClientState.IsGPosing)
            return Nothing(L.PreviewGroupPose.Text);

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
            return Nothing(ShortLine(NoCharacterMessage.Display));

        if (!_catalog.Ready)
            return Nothing(ShortLine(CatalogLoadingMessage.Display));

        if (!_penumbra.Available)
            return Nothing(ShortLine(PenumbraUnavailableMessage.Display));

        if (ResolveSource(localPlayer, sourceRowId) is not { } source)
            return Nothing(L.NotInEmoteSwap.Text);

        if (source.IsPoseFamily)
            return Nothing(L.PosesCannotSwap.Text);

        if (Service.LeaveToTheGame(sourceRowId))
            return Nothing(L.PreviewOwned.Text);

        var playerState = DirectPlayPlanner.ReadState(localPlayer);
        var condition = DirectPlayPlanner.PlayableAsFor(playerState.Condition);

        if (!AllowedIn(source.RowId, condition))
            return Nothing(DirectPlayPlanner.RefusalMessage(source.Command, playerState.Condition, playerState.OrnamentName).Display);

        if (!EmoteHelper.MeetsEnvironmentFor(localPlayer, source.RowId))
            return Nothing(L.NeedsEnvironment.With("command", source.Command, "requirement", EmoteHelper.EnvironmentRequirementFor(source.RowId) ?? string.Empty));

        if (_penumbra.GetPlayerCollection() is not { } collection)
            return Nothing(ShortLine(NoCharacterMessage.Display));

        if (IsUnassignedCollection(collection.Id))
            return Nothing(ShortLine(NoCollectionMessage.Display));

        source = WithConditionVariant(source, condition);

        var skeleton = SkeletonFor(localPlayer);
        var posture = PostureForCondition(condition);
        var fallbackOrder = EmotePathHelper.GetFallbackOrder(skeleton);

        source = WithMotionFolder(source, MotionFolderFor(source, localPlayer, fallbackOrder));

        var leftOut = new Dictionary<string, int>(StringComparer.Ordinal);
        var pool = BuildPool(localPlayer, source, condition, leftOut);

        if (pool.Count == 0 && leftOut.ContainsKey(GameGateReason))
        {
            leftOut.Clear();
            pool = SilkPoolIgnoringGameGate(localPlayer, source, condition, leftOut);
        }

        if (OverrideFor(source.RowId, sourceRowId) is { } configured)
        {
            var eligible = OverrideResolver.Eligible(configured, pool);
            var rule = Configuration.ModdedTargets;

            if (eligible.Count > 0 && rule != ModdedTargetRule.Allowed)
            {
                ForgetChangedTargetsOfAnotherCollection(collection.Id);
                PrimeChangedTargets(eligible, skeleton, fallbackOrder);
                eligible = OverrideResolver.ApplyModdedRule(eligible, rule,
                    candidate => ChangedByAnotherMod(candidate, skeleton, fallbackOrder) != null);
            }

            if (eligible.Count > 0)
            {
                var picked = RememberedAmong(source.RowId, eligible) ?? eligible[0];
                return new SilkSwapPlan(SilkPreviewKind.Target, source, picked, true, 0, 0, string.Empty, NoDetails);
            }

            if (configured.LimitedToTargets)
                return OverrideExhausted(source, configured, pool, skeleton, fallbackOrder);
        }

        var matchConfig = new MatchConfig(Configuration.LoopMatching, Configuration.TurnMatching,
            Configuration.SoundMatching, BlockedTargets());

        var fullPool = pool;
        (matchConfig, pool) = ApplyModdedRule(source, pool, matchConfig, posture, skeleton, fallbackOrder, collection.Id);

        var poolHasLoop = PoolOffersALoop(pool, matchConfig);

        var loopsFirst = source.LoopKind == EmotePlayType.Looped
            && matchConfig.Loop == LoopMatchRule.AllowLoopOnOneShot;

        var rules = loopsFirst ? matchConfig with { Loop = LoopMatchRule.Strict } : matchConfig;
        var match = BestMatchResolver.Resolve(source, pool, rules, posture);

        if (match.Target == null && loopsFirst)
        {
            rules = matchConfig;
            match = BestMatchResolver.Resolve(source, pool, rules, posture);
        }

        if (ShouldAttemptIdlePoseFallback(source, match, Configuration.IdlePoseLoops, poolHasLoop)
            && SilkIdlePose(localPlayer) is { } pose)
        {
            return new SilkSwapPlan(SilkPreviewKind.IdlePose, source, null, false, pose.Index, pose.RowId, string.Empty, NoDetails);
        }

        if (match.Target is { } best)
        {
            var target = best;
            var staleVulnerable = IsStaleVulnerableShape(best.LoopKind, best.Intro,
                SourceCarriesOwnDistinctIntroFile(source, fallbackOrder));

            if (Spreads(staleVulnerable))
            {
                var tier = BestMatchResolver.ResolveSameTier(source, pool, rules, posture,
                    wantedCount: RankBudget(),
                    maxScoreDistance: BestMatchResolver.DistanceFor(Configuration.DispatchFidelity)).Tier;

                target = RememberedAmong(source.RowId, tier) ?? best;
            }

            return new SilkSwapPlan(SilkPreviewKind.Target, source, target, false, 0, 0, string.Empty, NoDetails);
        }

        return NoMatchReasons(localPlayer, source, fullPool, pool, matchConfig, match, leftOut, skeleton, fallbackOrder);
    }

    private const string GameGateReason = "game gate";

    private List<EmoteAttributes> SilkPoolIgnoringGameGate(ICharacter localPlayer, EmoteAttributes source, EmoteCondition condition,
        Dictionary<string, int> leftOut)
    {
        var pool = new List<EmoteAttributes>();

        foreach (var candidate in _catalog.All)
        {
            if (candidate.RowId == source.RowId)
                continue;

            var reason = !candidate.EligibleTarget ? "not a target"
                : !EmoteHelper.IsEmoteUnlocked(candidate.RowId) ? "locked"
                : PoolExclusionFor(localPlayer, candidate, condition, askTheGame: false)?.ToLowerInvariant();

            if (reason != null)
                leftOut[reason] = leftOut.GetValueOrDefault(reason) + 1;
            else
                pool.Add(candidate);
        }

        return pool;
    }

    private static (byte Index, uint RowId)? SilkIdlePose(ICharacter localPlayer)
    {
        var poseState = CharacterPoseState.Read(localPlayer);

        if (poseState.Stance is not { } stance || IdlePoseData.IdlePosePathsFor(stance, poseState.Index) == null)
            return null;

        return (poseState.Index, SilkPoseRow(stance, poseState.Index));
    }

    private static uint SilkPoseRow(EmoteController.PoseType stance, byte index)
    {
        foreach (var rowId in IdlePoseData.PoseFamilyRowIds)
        {
            if (IdlePoseData.PoseFamilyFor(rowId) is { } family && family.PoseType == stance && family.Index == index)
                return rowId;
        }

        return 0;
    }

    private SilkSwapPlan OverrideExhausted(EmoteAttributes source, EmoteOverride configured,
        IReadOnlyList<EmoteAttributes> pool, string skeleton, IReadOnlyList<string> fallbackOrder)
    {
        var usableHere = new HashSet<uint>();

        foreach (var candidate in pool)
            usableHere.Add(candidate.RowId);

        var rule = Configuration.ModdedTargets;
        var details = new List<string>();
        var skipped = 0;

        foreach (var rowId in configured.Targets)
        {
            var attributes = _catalog.Get(rowId);
            var unlocked = EmoteHelper.IsEmoteUnlocked(rowId);
            var playableHere = usableHere.Contains(rowId);

            var changed = attributes != null && unlocked && playableHere && rule != ModdedTargetRule.Allowed
                && ChangedByAnotherMod(attributes, skeleton, fallbackOrder) != null;

            var refusal = OverrideResolver.RefusalFor(attributes, unlocked, playableHere, changed, rule);

            if (refusal == OverrideResolver.Refusal.None)
                continue;

            skipped++;

            if (details.Count < MaxRefusalsReported)
                details.Add(L.PreviewDetail.With("emote", NameOf(rowId), "reason", SilkRefusalText(refusal)));
        }

        if (skipped > MaxRefusalsReported)
            details.Add(L.Count(L.PreviewMoreSkipped, skipped - MaxRefusalsReported));

        return Nothing(L.PreviewNoOverrideTarget.With("command", source.Command), details);
    }

    private static string SilkRefusalText(OverrideResolver.Refusal refusal) => refusal switch
    {
        OverrideResolver.Refusal.Locked => L.PreviewNotUnlocked.Text,
        OverrideResolver.Refusal.NeverATarget => L.PreviewNeverTarget.Text,
        OverrideResolver.Refusal.NotHere => L.PreviewNotHere.Text,
        OverrideResolver.Refusal.ChangedByAMod => L.PreviewChangedByMod.Text,
        OverrideResolver.Refusal.NotConfigured => L.PreviewNoAnimation.Text,
        _ => L.PreviewUnavailable.Text,
    };

    private SilkSwapPlan NoMatchReasons(ICharacter localPlayer, EmoteAttributes source,
        IReadOnlyList<EmoteAttributes> fullPool, IReadOnlyList<EmoteAttributes> pool, MatchConfig matchConfig,
        MatchResult match, IReadOnlyDictionary<string, int> leftOut, string skeleton, IReadOnlyList<string> fallbackOrder)
    {
        var details = new List<string>();
        string headline;

        if (pool.Count == 0)
        {
            headline = fullPool.Count == 0
                ? L.PreviewOwnNone.Text
                : L.PreviewAllUnavailable.Text;

            foreach (var (reason, count) in leftOut)
                details.Add(SilkPoolReason(reason, count));
        }
        else
        {
            var blocked = 0;
            var modded = 0;

            foreach (var candidate in pool)
            {
                if (matchConfig.BlockedTargets?.Contains(candidate.RowId) == true)
                    blocked++;

                if (matchConfig.ModdedTargets?.Contains(candidate.RowId) == true)
                    modded++;
            }

            if (blocked == pool.Count)
            {
                headline = L.PreviewAllBlocked.Text;
            }
            else if (modded == pool.Count)
            {
                headline = L.PreviewAllModded.Text;
            }
            else if (source.LoopKind == EmotePlayType.Looped && !PoolOffersALoop(pool, matchConfig)
                && Configuration.IdlePoseLoops == IdlePoseFallback.Never)
            {
                headline = L.PreviewNoLoop.Text;
            }
            else
            {
                headline = L.PreviewNoMatch.With("command", source.Command);
            }

            foreach (var miss in match.Diagnostics)
                details.Add(L.PreviewMiss.With("command", miss.Candidate.Command, "reason", SilkMissText(miss, skeleton, fallbackOrder)));

            if (blocked > 0 && blocked != pool.Count)
                details.Add(L.Count(L.PreviewBlockedFit, blocked));

            if (modded > 0 && modded != pool.Count)
                details.Add(L.Count(L.PreviewModdedFit, modded));
        }

        if (source.LoopKind == EmotePlayType.Looped && Configuration.IdlePoseLoops != IdlePoseFallback.Never
            && SilkIdlePose(localPlayer) == null)
        {
            details.Add(IdlePoseCauseFor(StanceRefusal(CharacterPoseState.Read(localPlayer).Mode)).Text);
        }

        return Nothing(headline, details);
    }

    private string SilkMissText(NearMiss miss, string skeleton, IReadOnlyList<string> fallbackOrder)
    {
        if (miss.BlockedBy == BestMatchResolver.BlockedByRules)
            return L.MissBlocked.Text;

        if (miss.BlockedBy == BestMatchResolver.BlockedByModdedTarget)
        {
            return SilkModNameFor(miss, skeleton, fallbackOrder) is { Length: > 0 } mod
                ? L.MissNamedMod.With("mod", mod)
                : L.PreviewChangedByMod.Text;
        }

        return miss.BlockedBy switch
        {
            "Loop" => L.MissLoop.Text,
            "Turn" => L.MissTurn.Text,
            "Sound" => L.MissSound.Text,
            _ => L.MissOther.With("rule", miss.BlockedBy.ToLowerInvariant()),
        };
    }

    private string? SilkModNameFor(NearMiss miss, string skeleton, IReadOnlyList<string> fallbackOrder)
        => ChangedByAnotherMod(miss.Candidate, skeleton, fallbackOrder) is { Length: > 0 } directory
            ? ModNameFor(directory)
            : null;

    private static string SilkPoolReason(string reason, int count) => reason switch
    {
        "locked" => L.Count(L.PoolLocked, count),
        "not a target" => L.Count(L.PoolNeverTarget, count),
        "condition" => L.Count(L.PoolNotHere, count),
        "game gate" => L.Count(L.PoolGameGate, count),
        "targeted variant" => L.Count(L.PoolTargeted, count),
        _ => L.Count(L.PoolOther, count).Replace("{reason}", reason),
    };

    private static string ShortLine(string message)
    {
        var stop = message.IndexOf(". ", StringComparison.Ordinal);
        return stop > 0 ? message[..(stop + 1)] : message;
    }

    private EmoteAttributes? RememberedAmong(uint sourceRowId, IReadOnlyList<EmoteAttributes> candidates)
    {
        if (!_dispatchFor.TryGetValue(sourceRowId, out var existing) || existing.RulesStamp != SwapRulesStamp.Current())
            return null;

        foreach (var candidate in candidates)
        {
            if (candidate.RowId == existing.Target)
                return candidate;
        }

        return null;
    }
}
