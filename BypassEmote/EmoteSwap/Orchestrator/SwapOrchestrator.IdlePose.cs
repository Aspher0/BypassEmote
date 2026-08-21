using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Animations.PapFormat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private const string IdlePoseFailureKind = "swap.idle-pose.";

    internal const uint IdlePoseTargetEmote = 0;

    private const string IdlePoseTargetLabel = "idle pose";

    internal const string IdlePoseFailureMessage = "Could not use your idle pose for this emote.";

    internal const string IdlePoseIntroDroppedMessage =
        "Your idle 0 pose has no intro, so this emote's intro will not play. Try changing pose.";

    internal const string TargetIntroDroppedMessage =
        "This emote landed on a loop only target with no intro. You will not see the intro play.";

    internal const string OneShotTargetIntroDroppedMessage =
        "This emote landed on a one time target with no intro. You will not see the intro play.";

    internal static bool TargetDropsSourceIntro(EmoteAttributes source, EmoteAttributes target)
        => source.Intro == IntroKind.Pap && target.Intro != IntroKind.Pap;

    internal static string TargetIntroDroppedMessageFor(EmoteAttributes target)
        => target.LoopKind == EmotePlayType.Looped
            ? TargetIntroDroppedMessage
            : OneShotTargetIntroDroppedMessage;

    internal static bool IdlePoseDropsSourceIntro(string? poseStartRelativePapPath, IntroKind sourceIntro,
        string? sourceIntroRequestedPath)
        => poseStartRelativePapPath == null
        && sourceIntro == IntroKind.Pap
        && sourceIntroRequestedPath != null;

    internal enum IdlePoseFailure
    {
        StillInAnotherEmote,
        MountedOrRiding,
        PoseHasNoRedirectablePap,
        PosePapNotFound,
        SourceHasNoVariant,
        SourcePapNotFound,
        PosePapCouldNotBeBuilt,
        CollectionUnavailable,
        ModCouldNotBeApplied,
        RedrawFailed,
    }

    internal static string IdlePoseCauseFor(IdlePoseFailure reason) => reason switch
    {
        IdlePoseFailure.StillInAnotherEmote => "Your character is still in another emote. Move, or change pose, then try again.",
        IdlePoseFailure.MountedOrRiding => "Your character is mounted, so there is no idle pose to borrow.",
        IdlePoseFailure.PoseHasNoRedirectablePap => "This pose cannot be changed.",
        IdlePoseFailure.PosePapNotFound => "Your pose animation could not be found.",
        IdlePoseFailure.SourceHasNoVariant => "That emote has no animation to lend.",
        IdlePoseFailure.SourcePapNotFound => "That emote's animation could not be found.",
        IdlePoseFailure.PosePapCouldNotBeBuilt => "Your pose animation could not be rebuilt.",
        IdlePoseFailure.CollectionUnavailable => "Penumbra could not say which collection your character uses.",
        IdlePoseFailure.ModCouldNotBeApplied => "The swap mod could not be turned on.",
        IdlePoseFailure.RedrawFailed => "Your character could not be refreshed.",
        _ => "Something went wrong.",
    };

    internal static string IdlePoseFailureLine(IdlePoseFailure reason)
        => $"{IdlePoseFailureMessage} {IdlePoseCauseFor(reason)}";

    internal static EmoteController.PoseType? StanceFromMode(CharacterModes mode, byte modeParam)
        => IdlePoseData.StanceFromMode(mode, modeParam);

    internal static IdlePoseFailure StanceRefusal(CharacterModes mode)
        => mode is CharacterModes.Mounted or CharacterModes.RidingPillion
            ? IdlePoseFailure.MountedOrRiding
            : IdlePoseFailure.StillInAnotherEmote;

    internal static byte PoseIndexFor(EmoteController.PoseType stance, EmoteController.PoseType reportedStance, byte reportedIndex)
        => IdlePoseData.PoseIndexFor(stance, reportedStance, reportedIndex);

    private static bool IdlePoseFailed(IdlePoseFailure reason, string debugDetail)
    {
        NoireLogger.LogDebug($"Idle-pose fallback failed ({reason}): {debugDetail}", LogPrefix);
        FeedbackHelper.Error(IdlePoseFailureLine(reason), IdlePoseFailureKind + reason);
        return false;
    }

    internal static bool ShouldAttemptIdlePoseFallback(EmoteAttributes source, MatchResult match,
        IdlePoseFallback mode, bool poolHasLoop)
        => mode != IdlePoseFallback.Never
        && (mode == IdlePoseFallback.Allowed || !poolHasLoop)
        && source.LoopKind == EmotePlayType.Looped
        && !source.IsPoseFamily
        && (match.Target == null || match.Target.LoopKind != EmotePlayType.Looped);

    private bool TryIdlePoseSwap(EmoteAttributes source, ICharacter localPlayer, string skeleton,
        Stopwatch swapClock, long elapsedAtMatch)
    {
        var poseState = CharacterPoseState.Read(localPlayer);

        if (poseState.Stance is not { } poseType)
        {
            return IdlePoseFailed(StanceRefusal(poseState.Mode),
                $"mode {poseState.Mode}({(byte)poseState.Mode})/{poseState.ModeParam} is not a stance with a pose to borrow "
                + $"(the emote controller reports {poseState.ReportedPoseType}({(byte)poseState.ReportedPoseType})/{poseState.ReportedPoseIndex}).");
        }

        var poseIndex = poseState.Index;

        if (IdlePoseData.IdlePosePathsFor(poseType, poseIndex) is not { } posePaths)
        {
            return IdlePoseFailed(IdlePoseFailure.PoseHasNoRedirectablePap,
                $"pose {poseType}({(byte)poseType}) index {poseIndex} has no redirectable pap.");
        }

        var fallbackOrder = EmotePathHelper.GetFallbackOrder(skeleton);

        if (SelectRequestedPath(posePaths.LoopRelativePapPath, fallbackOrder, ForeignModProvides, VanillaExists) is not { } loopTargetPath)
        {
            return IdlePoseFailed(IdlePoseFailure.PosePapNotFound,
                $"'{posePaths.LoopRelativePapPath}' resolves on no skeleton in the chain [{string.Join(", ", fallbackOrder)}].");
        }

        var stancePosture = PostureFromMode(poseState.Mode, poseState.ModeParam);
        var sourceVariant = source.Variants.FirstOrDefault(variant => variant.Posture == stancePosture)
            ?? source.Variants.FirstOrDefault(variant => variant.Posture == PostureFlags.Standing)
            ?? source.Variants.FirstOrDefault();

        if (sourceVariant == null)
        {
            return IdlePoseFailed(IdlePoseFailure.SourceHasNoVariant,
                $"/{source.Command} has no variant at all to lend.");
        }

        if (SelectRequestedPath(sourceVariant.RelativePapPath, fallbackOrder, ForeignModProvides, VanillaExists) is not { } sourceRequestedPath)
        {
            return IdlePoseFailed(IdlePoseFailure.SourcePapNotFound,
                $"/{source.Command}'s variant '{sourceVariant.RelativePapPath}' resolves on no skeleton in the chain [{string.Join(", ", fallbackOrder)}].");
        }

        var resolvedSourcePath = ResolveOutsideOwnMod(sourceRequestedPath);

        var sourceFaceLibrary = source.FaceLibraryFor(sourceVariant.RelativePapPath);

        var sourceIntroRelativePath = source.IntroRelativePapPath;
        var sourceIntroRequestedPath = sourceIntroRelativePath == null
            ? null
            : SelectRequestedPath(sourceIntroRelativePath, fallbackOrder, ForeignModProvides, VanillaExists);

        var files = new Dictionary<string, byte[]>(2);

        if (BuildIdlePosePap(sourceRequestedPath, resolvedSourcePath, loopTargetPath, posePaths.LoopRelativePapPath, sourceFaceLibrary) is not { } loopBytes)
        {
            return IdlePoseFailed(IdlePoseFailure.PosePapCouldNotBeBuilt,
                $"the pose pap for '{loopTargetPath}' could not be built from '{sourceRequestedPath}' (see the line above).");
        }

        files[loopTargetPath] = loopBytes;

        if (posePaths.StartRelativePapPath is { } startRelativePath
            && SelectRequestedPath(startRelativePath, fallbackOrder, ForeignModProvides, VanillaExists) is { } startTargetPath)
        {
            var startSourceRequestedPath = sourceRequestedPath;
            var startResolvedSourcePath = resolvedSourcePath;
            var startSourceFaceLibrary = sourceFaceLibrary;

            if (sourceIntroRelativePath != null && sourceIntroRequestedPath is { } introRequestedPath)
            {
                startSourceRequestedPath = introRequestedPath;
                startResolvedSourcePath = ResolveOutsideOwnMod(introRequestedPath);
                startSourceFaceLibrary = source.FaceLibraryFor(sourceIntroRelativePath);
            }

            if (BuildIdlePosePap(startSourceRequestedPath, startResolvedSourcePath, startTargetPath, startRelativePath, startSourceFaceLibrary) is { } startBytes)
                files[startTargetPath] = startBytes;
        }

        var elapsedAtRetarget = swapClock.ElapsedMilliseconds;

        var stampTicks = StampFor(sourceRequestedPath, resolvedSourcePath);

        if (_penumbra.GetPlayerCollection() is not { } collection)
        {
            return IdlePoseFailed(IdlePoseFailure.CollectionUnavailable,
                "the player's Penumbra collection is unavailable.");
        }

        var raceInput = new RaceSourceInput(skeleton, resolvedSourcePath, stampTicks,
            string.Join(";", files.Keys.Order(StringComparer.Ordinal)));

        var contentKey = SwapContentKey.For(EmoteAttributeCatalog.RulesVersion, IdlePoseTargetEmote, source.RowId,
            [raceInput]);

        var sourceKey = SwapContentKey.ForSource(EmoteAttributeCatalog.RulesVersion, source.RowId, [raceInput]);

        _swapMods.ApplyRulesPlan(SwapRulesStamp.Current(), sourceKey, source.RowId, IdlePoseTargetEmote);

        var entry = _swapMods.FindReusable(contentKey);

        var reused = entry != null && _swapMods.SelectExisting(entry);

        if (!reused)
        {
            entry = IdlePoseEntryFor(contentKey, sourceKey, source, skeleton, files);

            if (!_swapMods.AddAndSelect(entry, files, skeleton))
            {
                return IdlePoseFailed(IdlePoseFailure.ModCouldNotBeApplied,
                    $"the swap mod could not be applied for [{string.Join(", ", files.Keys)}] in collection {collection.Id}.");
            }
        }

        _generations.TakeOwnership();

        var elapsedAtApply = swapClock.ElapsedMilliseconds;

        if (!_penumbra.RedrawLocalPlayer())
        {
            _swapMods.DeselectEntry(entry!);
            return IdlePoseFailed(IdlePoseFailure.RedrawFailed,
                "the redraw could not be requested.");
        }

        var elapsedAtRedraw = swapClock.ElapsedMilliseconds;

        FeedbackHelper.SwapLine(source.Command, IdlePoseTargetLabel);

        if (IdlePoseDropsSourceIntro(posePaths.StartRelativePapPath, source.Intro, sourceIntroRequestedPath))
            FeedbackHelper.Notice(IdlePoseIntroDroppedMessage);

        if (Configuration.SwapLifetime == SwapLifetime.WhenEmoteEnds)
            _endWatcher.ArmIdlePose(entry!, () => _penumbra.RedrawLocalPlayer());
        else
            _endWatcher.StopWatching();

        NoireLogger.LogDebug(
            $"Swap timings (idle pose): match {elapsedAtMatch}ms, retarget {elapsedAtRetarget - elapsedAtMatch}ms, " +
            $"apply {elapsedAtApply - elapsedAtRetarget}ms, redraw {elapsedAtRedraw - elapsedAtApply}ms, " +
            $"total {elapsedAtRedraw}ms.", LogPrefix);

        return true;
    }

    private SwapOptionEntry IdlePoseEntryFor(string contentKey, string sourceKey, EmoteAttributes source, string skeleton,
        IReadOnlyDictionary<string, byte[]> files)
    {
        var redirectedPaths = new Dictionary<string, string>(files.Count, StringComparer.Ordinal);

        foreach (var (gamePath, bytes) in files)
        {
            redirectedPaths[gamePath] = SwapModManager.RedirectedPathValue(
                SwapModManager.DeriveFileName(bytes, SwapModManager.FileExtensionFor(gamePath)));
        }

        var groupName = OptionNaming.IdlePoseGroupName;

        var filesByRace = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [skeleton] = redirectedPaths,
        };

        return new SwapOptionEntry(contentKey, groupName,
            OptionNaming.OptionNameFor(source.Command, null, _swapMods.TakenOptionNames(groupName)),
            source.RowId, IdlePoseTargetEmote, IsIdlePoseSwap: true, filesByRace,
            RulesStamp: SwapRulesStamp.Current(),
            SourceKey: sourceKey);
    }

    private static byte[]? BuildIdlePosePap(string sourceRequestedPath, string resolvedSourcePath,
        string targetRequestedPath, string targetRelativePapPath, string? sourceFaceLibrary)
    {
        if (ReadVanillaPap(targetRequestedPath) is not { } targetVanillaBytes)
        {
            NoireLogger.LogDebug($"No vanilla pose pap at '{targetRequestedPath}' to read required names from.", LogPrefix);
            return null;
        }

        var requiredNames = IdlePoseData.IdlePoseRequiredNames(targetRelativePapPath, PapAnimationNames.Read(targetVanillaBytes));
        if (requiredNames.Count == 0)
        {
            NoireLogger.LogDebug($"The vanilla pose pap at '{targetRequestedPath}' declares no usable animation.", LogPrefix);
            return null;
        }

        return BuildRetargetedPap(
            new VariantPair(sourceRequestedPath, targetRequestedPath, SourceFaceLibrary: sourceFaceLibrary),
            resolvedSourcePath, requiredNames);
    }
}
