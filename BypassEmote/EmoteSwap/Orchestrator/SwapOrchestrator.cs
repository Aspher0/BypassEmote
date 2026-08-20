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

    private const string PenumbraUnavailableMessage = "Penumbra is not available. Emote not swapped.";
    private const string CatalogLoadingMessage = "Still loading emote data. Try again in a moment.";
    private const string GenericFailureMessage = "Something went wrong. Emote not swapped.";
    private const string NoCollectionMessage = "No Penumbra collection is assigned to your character. Emote not swapped.";

    private static readonly IReadOnlyDictionary<string, string> RedirectsComputedByApply =
        new Dictionary<string, string>();

    private readonly IPCCaller_Penumbra _penumbra;
    private readonly EmoteAttributeCatalog _catalog;
    private readonly SwapModManager _swapMods;
    private readonly SwapEndWatcher _endWatcher;
    private readonly SchedulerResidencyProbe _residency;
    private readonly GenerationTracker _generations = new();

    private volatile bool _disposed;

    public SwapOrchestrator(IPCCaller_Penumbra penumbra, EmoteAttributeCatalog catalog, SwapModManager swapMods,
        SwapEndWatcher endWatcher, SchedulerResidencyProbe residency)
    {
        _penumbra = penumbra;
        _catalog = catalog;
        _swapMods = swapMods;
        _endWatcher = endWatcher;
        _residency = residency;

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
            NoireLogger.LogError(ex, $"Swapping emote {sourceEmote.RowId} failed.", LogPrefix);
            FeedbackHelper.Error(GenericFailureMessage);
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

        NoireLogger.LogDebug($"Serving for the drawn skeleton '{skeleton}', chain [{string.Join(", ", fallbackOrder)}].", LogPrefix);

        var pool = BuildPool(localPlayer, source, condition);
        var poolHasLoop = pool.Any(candidate => candidate.LoopKind == EmotePlayType.Looped);

        var matchConfig = new MatchConfig(Configuration.LoopMatching, Configuration.TurnMatching,
            Configuration.SoundMatching, BlockedTargets());

        (matchConfig, pool) = ApplyModdedRule(source, pool, matchConfig, posture, skeleton, fallbackOrder, collectionId);

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

        FeedbackHelper.DebugLine($"> {source.Command} -> {target.Command}"
            + (choice.StaleVulnerable ? " | stale-guarded shape" : " | free shape")
            + (choice.PlainBest != null && choice.PlainBest != target.RowId ? " | dispatched off the plain best" : ""));

        if (Configuration.ModdedTargets == ModdedTargetRule.LastResort
            && ChangedByAnotherMod(target, skeleton, fallbackOrder) is { Length: > 0 } changedBy)
        {
            ReportChangedTarget(target, changedBy);
        }

        var pairs = PairVariants(source, target, skeleton);
        var elapsedAtPair = swapClock.ElapsedMilliseconds;

        if (pairs.Count == 0)
        {
            NoireLogger.LogDebug($"/{source.Command} and /{target.Command} share no usable posture variant on {skeleton}.", LogPrefix);
            FeedbackHelper.Error(NoMatchMessage(source, []), NoMatchKind);
            return;
        }

        var resolvedPairs = pairs
            .Select(pair => new ResolvedVariantPair(pair, ResolveOutsideOwnMod(pair.SourceRequestedPath)))
            .ToList();

        NoireLogger.LogWarning($"Reading /{source.Command} onto /{target.Command} on {skeleton}: "
            + string.Join("; ", resolvedPairs.Select(entry =>
                $"'{entry.Pair.SourceRequestedPath}' -> '{entry.ResolvedSourcePath}' onto '{entry.Pair.TargetRequestedPath}'")),
            LogPrefix);

        var elapsedAtResolve = swapClock.ElapsedMilliseconds;
        var mainCandidate = resolvedPairs[0];
        var composeUniqueNames = ComposeUniqueNamesFor(target, out var reading);

        NoireLogger.LogDebug($"/{target.Command}: {reading}, so this swap "
            + (composeUniqueNames ? "loads under a composed name." : "is served on its own path."), LogPrefix);

        FeedbackHelper.DebugLine((composeUniqueNames ? ">   composed name" : ">   vanilla path") + $" | {reading}");

        if (OnDiskShapeMatches(_swapMods.Current, composeUniqueNames)
            && _swapMods.CanReuse(source.RowId, target.RowId, mainCandidate.ResolvedSourcePath,
                StampFor(mainCandidate.Pair.SourceRequestedPath, mainCandidate.ResolvedSourcePath),
                collectionId, skeleton)
            && TryReuseAndExecute(source, target, new SwapTimings(swapClock, elapsedAtMatch, elapsedAtPair,
                AtRetarget: elapsedAtResolve, AtPrepare: elapsedAtResolve, AtApply: 0)))
        {
            return;
        }

        const bool publishInternalNames = true;

        StartBackgroundBuild(new SwapBuildRequest(source, target, _generations.TakeOwnership(), resolvedPairs,
            fallbackOrder, skeleton, PathSignatureFor(pairs), _swapMods.BeginPrepare(), composeUniqueNames,
            publishInternalNames,
            new SwapTimings(swapClock, elapsedAtMatch, elapsedAtPair, AtRetarget: 0, AtPrepare: 0, AtApply: 0)));
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
            NoireLogger.LogDebug($"/{source.Command} is a pose-family emote; handing it to the game untouched.", LogPrefix);
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
            NoireLogger.LogDebug($"/{source.Command} pressed inside the game's emote cooldown; ignored.", LogPrefix);
            return null;
        }

        if (_penumbra.GetPlayerCollection() is not { } collection)
            return Refuse(PenumbraUnavailableMessage);

        if (IsUnassignedCollection(collection.Id))
            return Refuse(NoCollectionMessage);

        return new PipelineStart(localPlayer, WithConditionVariant(source, condition), condition, collection.Id);

        static PipelineStart? Refuse(string message)
        {
            FeedbackHelper.Error(message);
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

    private bool ComposeUniqueNamesFor(EmoteAttributes target, out string reading)
    {
        var knownNames = _swapMods.Current?.TargetEmote == target.RowId ? _swapMods.Current?.InternalNames : null;
        var packResident = _residency.AnyPackNameResident(knownNames ?? [], out var packTrace);

        var residencyIds = target.AnimationTimelineIds is { Count: > 0 } ids
            ? ids
            : EmoteHelper.GetActionTimelineIds(target.RowId);

        var consumers = residencyIds.Select(id => (Id: id, Count: _residency.ConsumersOf(id))).ToList();

        reading = $"timeline [{string.Join(" ", consumers.Select(entry => $"{entry.Id}:{entry.Count}"))}], packs "
            + packResident switch
            {
                true => $"hold [{string.Join(", ", knownNames!)}]",
                false => $"dropped [{string.Join(", ", knownNames!)}]",
                null => "nothing of ours to look for",
            }
            + $" ({packTrace})";

        return SwapLayers.AlwaysComposePaths || consumers.Any(entry => entry.Count > 0);
    }

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

        if (staleVulnerable && Alternates())
            match = ResolveDispatchedTarget(source, pool, matchConfig, posture, maxDistinctTargets: 2);

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
