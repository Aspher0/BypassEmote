using BypassEmote.Models;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.EmoteSwap;

public static class BestMatchResolver
{
    private const int CurrentPostureFit = 400;   // candidate.Postures contains currentPosture
    private const int SameKindWhenLenient = 300; // only under AllowLoopOnOneShot: candidate.LoopKind == source.LoopKind
    private const int IntroBothMatch = 250;      // candidate.HasIntro == source.HasIntro
    private const int TurnExact = 200;           // candidate.Turn == source.Turn (both known)
    private const int SoundSilent = 150;         // candidate.Sound == Silent
    private const int TurnEyesNone = 100;        // turn pair {Eyes,None}: nearly identical on screen
    private const int PostureSetEqual = 100;     // candidate.Postures == source.Postures
    private const int SoundSfx = 50;             // candidate.Sound == Sfx
    private const int TurnHeadEyes = 50;         // turn pair {Head,Eyes}
    private const int TurnHeadNone = 35;         // turn pair {Head,None}
    private const int IntroSourceOnly = 25;      // source.HasIntro && !candidate.HasIntro
    private const int TurnBodyHead = 25;         // turn pair {Body,Head}
    private const int TurnBodyEyes = 10;         // turn pair {Body,Eyes}
    private const int PostureShared = 10;        // per shared posture flag (only when sets not equal)
    private const int TurnBodyNone = 0;          // turn pair {Body,None}
    private const int SoundVoiceline = 0;        // candidate.Sound == Voiceline
    private const int TurnUnknownPenalty = -50;  // turn: either side Unknown (worst rank)
    private const int LenientSilentSourceSfxPenalty = -200;       // lenient sound, silent source, Sfx candidate
    private const int LenientSilentSourceVoicelinePenalty = -400; // lenient sound, silent source, Voiceline candidate

    // Deliberately large negative scores to keep the tiering separate from the soft scores above.

    private const int CancelsOnRotateTierPenalty = -100_000;

    private const int SourceIntroDroppedTierPenalty = -50_000;

    // Slot 1 populated with no pap behind it, so its windup plays over any swap.
    private const int TmbOnlyIntroTierPenalty = -75_000;

    // Needs the shared-pap machinery, so it goes last of the intro cases.
    private const int PapIntroOnPaplessSourceTierPenalty = -25_000;

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

            // Every candidate here already passed the player's rules; the distance only says how far below the best
            // one this emote scores, so it decides how far the rank may widen, never whether a rule may be broken.
            if (bestScore - ComputeScore(source, next, config, currentPosture) >= maxScoreDistance)
                break;

            tier.Add(next);
            remaining.RemoveAll(candidate => candidate.RowId == next.RowId);
        }

        return (first, tier);
    }

    // Sits above the soft scores (+/-1200) and below the smallest tier step (25k)
    internal const int SameTierScoreBand = 10_000;

    // What one rank is worth
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
    {
        if (rule == SoundMatchRule.Strict && source.Sound == SoundClass.Silent)
            return candidate.Sound == SoundClass.Silent;

        return true;
    }

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
            score += CurrentPostureFit;

        if (config.Loop == LoopMatchRule.AllowLoopOnOneShot && candidate.LoopKind == source.LoopKind)
            score += SameKindWhenLenient;

        score += ScoreTurn(source.Turn, candidate.Turn);
        score += ScoreSound(config.Sound, source.Sound, candidate.Sound);
        score += ScorePosture(source.Postures, candidate.Postures);
        score += ScoreIntro(HasPapIntro(source), HasPapIntro(candidate));

        if (candidate.CancelsOnRotate)
            score += CancelsOnRotateTierPenalty;

        if (HasPapIntro(source) && candidate.Intro == IntroKind.None)
            score += SourceIntroDroppedTierPenalty;

        if (HasPapIntro(source) && candidate.Intro == IntroKind.TmbOnly)
            score += TmbOnlyIntroTierPenalty;

        if (!HasPapIntro(source) && HasPapIntro(candidate))
            score += PapIntroOnPaplessSourceTierPenalty;

        return score;
    }

    internal static int ScoreTurn(TurnClass sourceTurn, TurnClass candidateTurn)
    {
        if (sourceTurn == TurnClass.Unknown || candidateTurn == TurnClass.Unknown)
            return TurnUnknownPenalty;

        if (sourceTurn == candidateTurn)
            return TurnExact;

        return (sourceTurn, candidateTurn) switch
        {
            (TurnClass.Eyes, TurnClass.None) or (TurnClass.None, TurnClass.Eyes) => TurnEyesNone,
            (TurnClass.Head, TurnClass.Eyes) or (TurnClass.Eyes, TurnClass.Head) => TurnHeadEyes,
            (TurnClass.Head, TurnClass.None) or (TurnClass.None, TurnClass.Head) => TurnHeadNone,
            (TurnClass.Body, TurnClass.Head) or (TurnClass.Head, TurnClass.Body) => TurnBodyHead,
            (TurnClass.Body, TurnClass.Eyes) or (TurnClass.Eyes, TurnClass.Body) => TurnBodyEyes,
            (TurnClass.Body, TurnClass.None) or (TurnClass.None, TurnClass.Body) => TurnBodyNone,
            _ => TurnUnknownPenalty,
        };
    }

    private static int ScoreSound(SoundMatchRule rule, SoundClass sourceSound, SoundClass candidateSound)
    {
        var score = candidateSound switch
        {
            SoundClass.Silent => SoundSilent,
            SoundClass.Sfx => SoundSfx,
            SoundClass.Voiceline => SoundVoiceline,
            _ => 0,
        };

        if (rule == SoundMatchRule.Lenient && sourceSound == SoundClass.Silent)
        {
            score += candidateSound switch
            {
                SoundClass.Sfx => LenientSilentSourceSfxPenalty,
                SoundClass.Voiceline => LenientSilentSourceVoicelinePenalty,
                _ => 0,
            };
        }

        return score;
    }

    private static int ScorePosture(PostureFlags sourcePostures, PostureFlags candidatePostures)
    {
        if (candidatePostures == sourcePostures)
            return PostureSetEqual;

        var shared = candidatePostures & sourcePostures;
        return BitOperations.PopCount((uint)shared) * PostureShared;
    }

    private static int ScoreIntro(bool sourceHasIntro, bool candidateHasIntro)
    {
        if (sourceHasIntro == candidateHasIntro)
            return IntroBothMatch;

        return sourceHasIntro ? IntroSourceOnly : 0;
    }

    private static bool HasPapIntro(EmoteAttributes emote) => emote.Intro == IntroKind.Pap;
}
