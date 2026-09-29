using BypassEmote.Enums;
using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.EmoteSwap;

public static class BestMatchResolver
{
    private const int TurnUnknownPenalty = -50;

    public static MatchResult Resolve(
        EmoteAttributes source,
        IReadOnlyList<EmoteAttributes> pool,
        MatchConfig config,
        PostureFlags currentPosture)
    {
        EmoteAttributes? winner = null;
        var winnerScore = int.MinValue;

        var refused = new List<(NearMiss Miss, int Score)>();

        foreach (var candidate in pool)
        {
            var score = ComputeScore(source, candidate, config, currentPosture);
            var blockedBy = FirstFailingFilter(source, candidate, config);

            if (blockedBy is null)
            {
                if (winner is null || score > winnerScore || (score == winnerScore && candidate.RowId < winner.RowId))
                {
                    winner = candidate;
                    winnerScore = score;
                }

                continue;
            }

            refused.Add((new NearMiss(candidate, blockedBy), score));
        }

        return new MatchResult(winner, RankDiagnostics(refused));
    }

    internal const int MaxDiagnostics = 3;

    internal static IReadOnlyList<NearMiss> RankDiagnostics(List<(NearMiss Miss, int Score)> refused)
    {
        if (refused.Count == 0)
            return [];

        refused.Sort(Nearest);

        var ranked = new List<NearMiss>(MaxDiagnostics);
        var reasonsTaken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (miss, _) in refused)
        {
            if (ranked.Count == MaxDiagnostics)
                break;

            if (reasonsTaken.Add(miss.BlockedBy))
                ranked.Add(miss);
        }

        foreach (var (miss, _) in refused)
        {
            if (ranked.Count == MaxDiagnostics)
                break;

            if (!ranked.Contains(miss))
                ranked.Add(miss);
        }

        return LastWordToTheMod(ranked);
    }

    private static IReadOnlyList<NearMiss> LastWordToTheMod(List<NearMiss> ranked)
    {
        var ordered = new List<NearMiss>(ranked.Count);

        foreach (var miss in ranked)
        {
            if (miss.BlockedBy != BlockedByModdedTarget)
                ordered.Add(miss);
        }

        foreach (var miss in ranked)
        {
            if (miss.BlockedBy == BlockedByModdedTarget)
                ordered.Add(miss);
        }

        return ordered;
    }

    private static int Nearest((NearMiss Miss, int Score) left, (NearMiss Miss, int Score) right)
    {
        var leftIsRule = IsConfigured(left.Miss.BlockedBy);
        var rightIsRule = IsConfigured(right.Miss.BlockedBy);

        if (leftIsRule != rightIsRule)
            return leftIsRule ? -1 : 1;

        if (left.Score != right.Score)
            return right.Score.CompareTo(left.Score);

        return left.Miss.Candidate.RowId.CompareTo(right.Miss.Candidate.RowId);
    }

    private static bool IsConfigured(string blockedBy)
        => blockedBy is BlockedByRules or BlockedByModdedTarget;

    public const string BlockedByRules = "Rules";

    public const string BlockedByModdedTarget = "Modded";

    public static (MatchResult First, List<EmoteAttributes> Tier) ResolveSameTier(
        EmoteAttributes source,
        IReadOnlyList<EmoteAttributes> pool,
        MatchConfig config,
        PostureFlags currentPosture,
        int wantedCount = 0,
        int maxScoreDistance = SameTierScoreBand)
    {
        var first = Resolve(source, pool, config, currentPosture);
        var tier = new List<EmoteAttributes>();

        if (first.Target is not { } best)
            return (first, tier);

        tier.Add(best);

        var bestScore = ComputeScore(source, best, config, currentPosture);
        var remaining = new List<EmoteAttributes>(pool.Count);

        foreach (var candidate in pool)
        {
            if (candidate.RowId != best.RowId)
                remaining.Add(candidate);
        }

        while (remaining.Count > 0)
        {
            if (wantedCount > 0 && tier.Count >= wantedCount)
                break;

            if (Resolve(source, remaining, config, currentPosture).Target is not { } next)
                break;

            if (bestScore - ComputeScore(source, next, config, currentPosture) >= maxScoreDistance)
                break;

            tier.Add(next);
            remaining.RemoveAll(candidate => candidate.RowId == next.RowId);
        }

        return (first, tier);
    }

    internal const int SameTierScoreBand = 10_000;

    internal const int RankStep = 25_000;

    internal static int DistanceFor(DispatchFidelity fidelity) => fidelity switch
    {
        DispatchFidelity.OneRankBelow => SameTierScoreBand + RankStep,
        DispatchFidelity.AnythingAllowed => int.MaxValue,
        _ => SameTierScoreBand,
    };

    private static string? FirstFailingFilter(EmoteAttributes source, EmoteAttributes candidate, MatchConfig config)
    {
        if (!PassesLoopFilter(source, candidate, config.Loop))
            return "Loop";

        if (!PassesSoundFilter(source, candidate, config.Sound))
            return "Sound";

        if (!PassesTurnFilter(source, candidate, config.Turn))
            return "Turn";

        if (config.BlockedTargets?.Contains(candidate.RowId) == true)
            return BlockedByRules;

        if (config.ModdedTargets?.Contains(candidate.RowId) == true)
            return BlockedByModdedTarget;

        return null;
    }

    private static bool PassesLoopFilter(EmoteAttributes source, EmoteAttributes candidate, LoopMatchRule rule)
    {
        if (candidate.LoopKind == source.LoopKind)
            return true;

        return rule == LoopMatchRule.AllowLoopOnOneShot
            && source.LoopKind == EmotePlayType.Looped
            && candidate.LoopKind == EmotePlayType.OneShot;
    }

    private static bool PassesSoundFilter(EmoteAttributes source, EmoteAttributes candidate, SoundMatchRule rule)
        => rule switch
        {
            SoundMatchRule.Strict => candidate.Sound == SoundClass.Silent,
            SoundMatchRule.Lenient => source.Sound != SoundClass.Silent || candidate.Sound == SoundClass.Silent,
            _ => true,
        };

    private static bool PassesTurnFilter(EmoteAttributes source, EmoteAttributes candidate, TurnMatchRule rule)
    {
        if (rule == TurnMatchRule.Lenient)
            return true;

        if (source.Turn == TurnClass.Unknown || candidate.Turn == TurnClass.Unknown)
            return false;

        if (rule == TurnMatchRule.VeryStrict)
            return candidate.Turn == source.Turn;

        return StrictTurnBucket(candidate.Turn) == StrictTurnBucket(source.Turn);
    }

    private static TurnClass StrictTurnBucket(TurnClass turn) => turn == TurnClass.Eyes ? TurnClass.None : turn;

    private static int ComputeScore(EmoteAttributes source, EmoteAttributes candidate, MatchConfig config, PostureFlags currentPosture)
    {
        var score = 0;

        if ((candidate.Postures & currentPosture) == currentPosture)
            score += 400;

        if (config.Loop == LoopMatchRule.AllowLoopOnOneShot && candidate.LoopKind == source.LoopKind)
            score += 300;

        score += ScoreTurn(source.Turn, candidate.Turn);
        score += ScoreSound(config.Sound, candidate.Sound);
        score += ScorePosture(source.Postures, candidate.Postures);
        score += ScoreIntro(HasPapIntro(source), HasPapIntro(candidate));

        if (candidate.CancelsOnRotate)
            score -= 100_000;

        if (HasPapIntro(source) && candidate.Intro == IntroKind.None)
            score -= 50_000;

        if (HasPapIntro(source) && candidate.Intro == IntroKind.TmbOnly)
            score -= 75_000;

        if (!HasPapIntro(source) && HasPapIntro(candidate))
            score -= 25_000;

        return score;
    }

    internal static int ScoreTurn(TurnClass sourceTurn, TurnClass candidateTurn)
    {
        if (sourceTurn == TurnClass.Unknown || candidateTurn == TurnClass.Unknown)
            return TurnUnknownPenalty;

        if (sourceTurn == candidateTurn)
            return 200;

        return (sourceTurn, candidateTurn) switch
        {
            (TurnClass.Eyes, TurnClass.None) or (TurnClass.None, TurnClass.Eyes) => 100,
            (TurnClass.Head, TurnClass.Eyes) or (TurnClass.Eyes, TurnClass.Head) => 50,
            (TurnClass.Head, TurnClass.None) or (TurnClass.None, TurnClass.Head) => 35,
            (TurnClass.Body, TurnClass.Head) or (TurnClass.Head, TurnClass.Body) => 25,
            (TurnClass.Body, TurnClass.Eyes) or (TurnClass.Eyes, TurnClass.Body) => 10,
            (TurnClass.Body, TurnClass.None) or (TurnClass.None, TurnClass.Body) => 0,
            _ => TurnUnknownPenalty,
        };
    }

    private static int ScoreSound(SoundMatchRule rule, SoundClass candidateSound)
    {
        var score = candidateSound switch
        {
            SoundClass.Silent => 150,
            SoundClass.Sfx => 50,
            SoundClass.Voiceline => 0,
            _ => 0,
        };

        if (rule != SoundMatchRule.Strict && candidateSound != SoundClass.Silent)
            score -= 10_000;

        return score;
    }

    private static int ScorePosture(PostureFlags sourcePostures, PostureFlags candidatePostures)
    {
        if (candidatePostures == sourcePostures)
            return 100;

        var shared = candidatePostures & sourcePostures;
        return BitOperations.PopCount((uint)shared) * 10;
    }

    private static int ScoreIntro(bool sourceHasIntro, bool candidateHasIntro)
    {
        if (sourceHasIntro == candidateHasIntro)
            return 250;

        return sourceHasIntro ? 25 : 0;
    }

    private static bool HasPapIntro(EmoteAttributes emote) => emote.Intro == IntroKind.Pap;
}
