using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.IPC;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator : IDisposable
{
    private const string LogPrefix = "[SwapOrchestrator] ";

    private const string CatalogLoadingMessage = "Still loading emote data. Try again in a moment.";
    private const string GenericFailureMessage = "Something went wrong. Emote not swapped.";
    private const string NoCollectionMessage = "No Penumbra collection is assigned to your character. Emote not swapped.";

    private string PenumbraUnavailableMessage => $"{_penumbra.UnavailableReason} Emote not swapped.";

    private const string NoCharacterMessage =
        "Penumbra could not say which collection your character uses. Emote not swapped.";

    private readonly IPCCaller_Penumbra _penumbra;
    private readonly EmoteAttributeCatalog _catalog;
    private readonly SwapModManager _swapMods;
    private readonly SwapEndWatcher _endWatcher;
    private readonly GenerationTracker _generations = new();

    private volatile bool _disposed;

    public SwapOrchestrator(IPCCaller_Penumbra penumbra, EmoteAttributeCatalog catalog, SwapModManager swapMods,
        SwapEndWatcher endWatcher)
    {
        _penumbra = penumbra;
        _catalog = catalog;
        _swapMods = swapMods;
        _endWatcher = endWatcher;

        penumbra.ExternalModChanged += ForgetChangedTargets;
    }

    public void Dispose()
    {
        _disposed = true;
        _penumbra.ExternalModChanged -= ForgetChangedTargets;
        ClearExecuteRetry();
    }

    public bool IsExecutingSwap { get; private set; }

    public void TrySwap(Emote sourceEmote)
    {
        try
        {
            RunPipeline(sourceEmote);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Swapping emote {sourceEmote.RowId} failed.", LogPrefix);
            LogHelper.Error(GenericFailureMessage);
        }
    }

    private void RunPipeline(Emote sourceEmote)
    {
        if (OpenPipeline(sourceEmote) is not { } start)
            return;

        var (localPlayer, source, condition, collectionId) = start;

        var swapClock = Stopwatch.StartNew();
        var skeleton = SkeletonFor(localPlayer);
        var posture = PostureForCondition(condition);
        var fallbackOrder = EmotePathHelper.GetFallbackOrder(skeleton);

        Log.Debug($"Serving for the drawn skeleton '{skeleton}', chain [{string.Join(", ", fallbackOrder)}].", LogPrefix);

        source = WithMotionFolder(source, MotionFolderFor(source, localPlayer, fallbackOrder));

        var pool = BuildPool(localPlayer, source, condition);

        if (OverrideFor(source.RowId, sourceEmote.RowId) is { } configured
            && PlayOverride(localPlayer, source, configured, pool, skeleton, fallbackOrder, collectionId, swapClock))
        {
            return;
        }

        var matchConfig = new MatchConfig(Configuration.LoopMatching, Configuration.TurnMatching,
            Configuration.SoundMatching, BlockedTargets());

        (matchConfig, pool) = ApplyModdedRule(source, pool, matchConfig, posture, skeleton, fallbackOrder, collectionId);

        var poolHasLoop = PoolOffersALoop(pool, matchConfig);

        var loopsFirst = source.LoopKind == EmotePlayType.Looped
            && matchConfig.Loop == LoopMatchRule.AllowLoopOnOneShot;

        var choice = ChooseTarget(source, pool,
            loopsFirst ? matchConfig with { Loop = LoopMatchRule.Strict } : matchConfig, posture, fallbackOrder);

        var elapsedAtMatch = swapClock.ElapsedMilliseconds;

        if (ShouldAttemptIdlePoseFallback(source, choice.Match, Configuration.IdlePoseLoops, poolHasLoop)
            && TryIdlePoseSwap(source, localPlayer, skeleton, swapClock, elapsedAtMatch))
        {
            return;
        }

        if (choice.Match.Target == null && loopsFirst)
            choice = ChooseTarget(source, pool, matchConfig, posture, fallbackOrder);

        if (choice.Match.Target is not { } target)
        {
            ReportNoMatch(source, choice.Match.Diagnostics, skeleton, fallbackOrder);
            return;
        }

        LogHelper.DebugLine($"> {source.Command} -> {target.Command}"
            + (choice.StaleVulnerable ? " | stale-guarded shape" : " | free shape")
            + (choice.PlainBest != null && choice.PlainBest != target.RowId ? " | dispatched off the plain best" : ""));

        if (Configuration.ModdedTargets == ModdedTargetRule.LastResort
            && ChangedByAnotherMod(target, skeleton, fallbackOrder) is { Length: > 0 } changedBy)
        {
            ReportChangedTarget(target, changedBy);
        }

        BuildAndPlay(localPlayer, source, target, skeleton, swapClock, elapsedAtMatch);
    }

    private bool PlayOverride(ICharacter localPlayer, EmoteAttributes source, EmoteOverride configured,
        List<EmoteAttributes> pool, string skeleton, IReadOnlyList<string> fallbackOrder, Guid collectionId,
        Stopwatch swapClock)
    {
        if (ChooseOverrideTarget(source, configured, pool, skeleton, fallbackOrder, collectionId) is { } target)
        {
            LogHelper.DebugLine($"> {source.Command} -> {target.Command} | override");

            if (Configuration.ModdedTargets == ModdedTargetRule.LastResort
                && ChangedByAnotherMod(target, skeleton, fallbackOrder) is { Length: > 0 } changedBy)
            {
                ReportChangedTarget(target, changedBy);
            }

            BuildAndPlay(localPlayer, source, target, skeleton, swapClock, swapClock.ElapsedMilliseconds);
            return true;
        }

        if (configured.LimitedToTargets)
        {
            ReportNoOverrideTarget(source, configured, pool, skeleton, fallbackOrder);
            return true;
        }

        Log.Debug($"No override target of /{source.Command} can be played here. The usual matching is used.", LogPrefix);

        return false;
    }

    private void BuildAndPlay(ICharacter localPlayer, EmoteAttributes source, EmoteAttributes target,
        string skeleton, Stopwatch swapClock, long elapsedAtMatch)
    {
        var raceInputs = RaceInputsFor(source, target, skeleton);
        var elapsedAtPair = swapClock.ElapsedMilliseconds;

        if (raceInputs.Count == 0 || raceInputs[0].Race != skeleton)
        {
            Log.Debug($"/{source.Command} and /{target.Command} share no usable posture variant on {skeleton}.", LogPrefix);
            LogHelper.Error(NoMatchMessage(source, []), "swap.no-match");
            return;
        }

        var resolvedPairs = raceInputs[0].Pairs;

        Log.Debug($"Reading /{source.Command} onto /{target.Command} on {skeleton}: "
            + string.Join("; ", resolvedPairs.Select(entry =>
                $"'{entry.Pair.SourceRequestedPath}' -> '{entry.ResolvedSourcePath}' onto '{entry.Pair.TargetRequestedPath}'")),
            LogPrefix);

        Log.Debug($"This swap is built for {raceInputs.Count} bod(y/ies): "
            + $"{string.Join(", ", raceInputs.Select(race => race.Race))}.", LogPrefix);

        var elapsedAtResolve = swapClock.ElapsedMilliseconds;

        var sourceKey = SourceKeyFor(source, raceInputs);

        DropSwapsTheRulesNoLongerMake(source, sourceKey, target.RowId);

        var contentKey = ContentKeyFor(source, target, raceInputs);

        if (_swapMods.FindReusable(contentKey) is { } kept
            && OnDiskShapeMatches(kept)
            && TryReuseAndExecute(kept, source, target, new SwapTimings(swapClock, elapsedAtMatch, elapsedAtPair,
                AtRetarget: elapsedAtResolve, AtPrepare: elapsedAtResolve, AtApply: 0)))
        {
            return;
        }

        StartBackgroundBuild(new SwapBuildRequest(source, target, _generations.TakeOwnership(), raceInputs,
            skeleton, contentKey, sourceKey, _swapMods.BeginPrepare(), ModServingAnimation(source, skeleton),
            new SwapTimings(swapClock, elapsedAtMatch, elapsedAtPair, AtRetarget: 0, AtPrepare: 0, AtApply: 0),
            HoldOffHand: WeaponHoldFor(source, localPlayer)));
    }

    private readonly record struct PipelineStart(
        ICharacter LocalPlayer, EmoteAttributes Source, EmoteCondition Condition, Guid CollectionId);

    private PipelineStart? OpenPipeline(Emote sourceEmote)
    {
        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap || NoireService.ClientState.IsGPosing)
            return null;

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
            return null;

        if (!_penumbra.Available)
            return Refuse(PenumbraUnavailableMessage);

        if (!_catalog.Ready)
            return Refuse(CatalogLoadingMessage);

        if (ResolveSource(localPlayer, sourceEmote.RowId) is not { } source)
            return null;

        if (source.IsPoseFamily)
        {
            Log.Debug($"/{source.Command} is a pose-family emote; handing it to the game untouched.", LogPrefix);
            TryExecuteEmote(source.RowId);
            return null;
        }

        var playerState = DirectPlayPlanner.ReadState(localPlayer);
        var condition = DirectPlayPlanner.PlayableAsFor(playerState.Condition);

        if (!AllowedIn(source.RowId, condition))
        {
            return Refuse(DirectPlayPlanner.RefusalMessage(
                source.Command, playerState.Condition, playerState.OrnamentName));
        }

        if (!EmoteHelper.MeetsEnvironmentFor(localPlayer, source.RowId))
            return Refuse($"/{source.Command} needs {EmoteHelper.EnvironmentRequirementFor(source.RowId)}.");

        if (GameEmoteCooldownActive())
        {
            Log.Debug($"/{source.Command} pressed inside the game's emote cooldown; ignored.", LogPrefix);
            return null;
        }

        if (_penumbra.GetPlayerCollection() is not { } collection)
            return Refuse(_penumbra.Available ? NoCharacterMessage : PenumbraUnavailableMessage);

        if (IsUnassignedCollection(collection.Id))
            return Refuse(NoCollectionMessage);

        return new PipelineStart(localPlayer, WithConditionVariant(source, condition), condition, collection.Id);

        static PipelineStart? Refuse(string message)
        {
            LogHelper.Error(message);
            return null;
        }
    }

    private List<EmoteAttributes> BuildPool(ICharacter localPlayer, EmoteAttributes source, EmoteCondition condition)
        => _catalog.All
            .Where(candidate => candidate.EligibleTarget
                             && candidate.RowId != source.RowId
                             && EmoteHelper.IsEmoteUnlocked(candidate.RowId)
                             && PoolExclusionFor(localPlayer, candidate, condition, askTheGame: true) is null)
            .ToList();

    private (MatchConfig Config, List<EmoteAttributes> Pool) ApplyModdedRule(EmoteAttributes source,
        List<EmoteAttributes> pool, MatchConfig config, PostureFlags posture, string skeleton,
        IReadOnlyList<string> fallbackOrder, Guid collectionId)
    {
        var rule = Configuration.ModdedTargets;

        if (rule == ModdedTargetRule.Allowed)
            return (config, pool);

        ForgetChangedTargetsOfAnotherCollection(collectionId);

        return rule == ModdedTargetRule.Blocked
            ? (config with { ModdedTargets = ChangedTargetRowIds(pool, skeleton, fallbackOrder) }, pool)
            : (config, PoolAvoidingChangedTargets(source, pool, config, posture, skeleton, fallbackOrder));
    }

    internal static string ContentKeyFor(EmoteAttributes source, EmoteAttributes target,
        IReadOnlyList<RaceBuildInput> races)
        => SwapContentKey.For(EmoteAttributeCatalog.RulesVersion, target.RowId, source.RowId,
            [.. races.Select(race => race.Source)]);

    private static EmoteAttributes WithConditionVariant(EmoteAttributes source, EmoteCondition condition)
    {
        var key = (source.RowId, DirectPlayPlanner.ConditionTimelineKeyFor(condition));

        if (!DirectPlayPlanner.ConditionTimelines.TryGetValue(key, out var timelines))
            return source;

        if (condition == EmoteCondition.Diving)
        {
            timelines = (DirectPlayPlanner.DivingReplacementFor(timelines.Intro),
                DirectPlayPlanner.DivingReplacementFor(timelines.Loop));
        }

        if (PapPathForTimeline(timelines.Loop) is not { } loopPath)
            return source;

        var posture = PostureForCondition(condition);

        var variants = source.Variants.Where(variant => variant.Posture != posture).ToList();
        variants.Add(new VariantPaths(posture, loopPath));

        var introPath = PapPathForTimeline(timelines.Intro);

        return source with
        {
            Variants = variants,
            Postures = source.Postures | posture,
            HasIntro = introPath != null || source.HasIntro,
            Intro = introPath != null ? IntroKind.Pap : source.Intro,
            IntroRelativePapPath = introPath ?? source.IntroRelativePapPath,
        };
    }

    private static bool AllowedIn(uint emoteRowId, EmoteCondition condition)
        => (EmoteHelper.GetEmoteConditions(emoteRowId) & condition) == condition;

    private static string? PoolExclusionFor(ICharacter character, EmoteAttributes candidate,
        EmoteCondition condition, bool askTheGame)
    {
        if (!AllowedIn(candidate.RowId, condition))
            return "Condition";

        if (askTheGame && !EmoteHelper.CanUseEmote(candidate.RowId))
            return "Game gate";

        if (CommonHelper.ResolveTargetedEmote(character, candidate.RowId) != candidate.RowId)
            return "Targeted variant";

        return null;
    }

    private readonly record struct TargetChoice(MatchResult Match, uint? PlainBest, bool StaleVulnerable);

    private TargetChoice ChooseTarget(EmoteAttributes source, IReadOnlyList<EmoteAttributes> pool,
        MatchConfig matchConfig, PostureFlags posture, IReadOnlyList<string> fallbackOrder)
    {
        var match = BestMatchResolver.Resolve(source, pool, matchConfig, posture);
        var plainBest = match.Target?.RowId;
        var staleVulnerable = match.Target is { } bestTarget
            && IsStaleVulnerableShape(bestTarget.LoopKind, bestTarget.Intro,
                SourceCarriesOwnDistinctIntroFile(source, fallbackOrder));

        if (Spreads(staleVulnerable))
            match = ResolveDispatchedTarget(source, pool, matchConfig, posture);

        return new TargetChoice(match, plainBest, staleVulnerable);
    }

    // For /dote and its like the game plays a second row when nothing is targeted. The swap has to serve the
    // animation that row would have played. Null when the row is in no catalog at all.
    private EmoteAttributes? ResolveSource(ICharacter localPlayer, uint sourceRowId)
    {
        var asked = _catalog.Get(sourceRowId);
        var resolvedRowId = CommonHelper.ResolveTargetedEmote(localPlayer, sourceRowId);

        if (resolvedRowId == sourceRowId || _catalog.Get(resolvedRowId) is not { } resolved)
            return asked;

        return string.IsNullOrEmpty(resolved.Command) && asked != null
            ? resolved with { Command = asked.Command }
            : resolved;
    }

    internal static bool IsUnassignedCollection(Guid effectiveCollectionId)
        => effectiveCollectionId == Guid.Empty;

    internal static IReadOnlySet<uint>? BlockedTargets()
    {
        var blocked = Configuration.BlockedTargetEmotesEmoteSwap;
        return blocked is { Count: > 0 } ? blocked.ToHashSet() : null;
    }
}
