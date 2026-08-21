using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Diagnostics;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private sealed record SwapTimings(Stopwatch Clock, long AtMatch, long AtPair, long AtRetarget, long AtPrepare,
        long AtApply, long AtFrame = 0, long AtEntry = 0);

    private bool TryReuseAndExecute(SwapOptionEntry kept, EmoteAttributes source, EmoteAttributes target,
        SwapTimings timings)
    {
        NoireLogger.LogDebug($"Reuse route for /{source.Command} onto /{target.Command}.", LogPrefix);

        if (!_swapMods.SelectExisting(kept))
        {
            NoireLogger.LogDebug($"The existing swap could not be put back, so /{source.Command} is built again.", LogPrefix);
            FeedbackHelper.DebugLine(">   reuse refused, rebuilding");
            return false;
        }

        FeedbackHelper.DebugLine(">   reuse");

        var generation = _generations.TakeOwnership();

        ExecuteSwapTail(source, target, generation, timings with { AtApply = timings.Clock.ElapsedMilliseconds });
        return true;
    }

    private void ExecuteSwapTail(EmoteAttributes source, EmoteAttributes target, int generation, SwapTimings timings)
    {
        var elapsedAtTail = timings.Clock.ElapsedMilliseconds;

        if (!_generations.IsCurrent(generation))
        {
            NoireLogger.LogDebug("A superseded swap's deferred execute was dropped; a newer swap owns the mod.", LogPrefix);
            return;
        }

        if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
        {
            AbandonUnexecutedSwap(generation, target.RowId);
            return;
        }

        ClearExecuteRetry();

        if (AttemptExecute(target.RowId))
        {
            CompleteSwapTail(source, target, timings, elapsedAtTail);
            return;
        }

        _pendingExecute = new PendingExecute(source, target, generation, timings, elapsedAtTail);

        SubscribeExecuteRetry();

        NoireLogger.LogDebug(
            $"The game refused /{target.Command} for the /{source.Command} swap; retrying for up to " +
            $"{ExecuteRetryPolicy.MaxWaitMilliseconds}ms.", LogPrefix);
    }

    private bool AttemptExecute(uint targetRowId)
    {
        _residency.ArmNameSubstitution();

        IsExecutingSwap = true;
        try
        {
            return TryExecuteEmote(targetRowId);
        }
        finally
        {
            IsExecutingSwap = false;
        }
    }

    private void CompleteSwapTail(EmoteAttributes source, EmoteAttributes target, SwapTimings timings, long elapsedAtTail)
    {
        var elapsedAtExecute = timings.Clock.ElapsedMilliseconds;

        FeedbackHelper.SwapLine(source.Command, target.Command);

        if (TargetDropsSourceIntro(source, target))
            FeedbackHelper.Notice(TargetIntroDroppedMessageFor(target));

        if (Configuration.SwapLifetime == SwapLifetime.WhenEmoteEnds && _swapMods.ArmedFor(target.RowId) is { } armed)
            _endWatcher.Arm(armed);
        else
            _endWatcher.StopWatching();

        NoireLogger.LogDebug(
            $"Swap timings: match {timings.AtMatch}ms, pair {timings.AtPair - timings.AtMatch}ms, " +
            $"retarget {timings.AtRetarget - timings.AtPair}ms, prepare {timings.AtPrepare - timings.AtRetarget}ms, " +
            $"apply {timings.AtApply - timings.AtPrepare}ms"
            + (timings.AtFrame > 0
                ? $" (frame wait {timings.AtFrame - timings.AtPrepare}ms, naming {timings.AtEntry - timings.AtFrame}ms)"
                : string.Empty)
            + $", gate {elapsedAtTail - timings.AtApply}ms, " +
            $"execute {elapsedAtExecute - elapsedAtTail}ms, total {elapsedAtExecute}ms.", LogPrefix);

        FeedbackHelper.DebugLine(
            $">   executed | gate {elapsedAtTail - timings.AtApply}ms, total {elapsedAtExecute}ms");

        FeedbackHelper.DebugLine(
            $">   shapes | /{source.Command} {ShapeOf(source)} -> /{target.Command} {ShapeOf(target)}");
    }

    internal static string ShapeOf(EmoteAttributes emote)
        => $"{(emote.Intro == IntroKind.Pap ? "intro" : "no intro")}"
        + $" + {(emote.LoopKind == EmotePlayType.Looped ? "loop" : "one shot")}";

    private void FailSwapTail(int generation, uint targetRowId, string debugDetail)
    {
        NoireLogger.LogDebug(debugDetail, LogPrefix);

        if (_generations.IsCurrent(generation))
            DeselectArmed(targetRowId);
        else
            NoireLogger.LogDebug("The failed execute's swap was already superseded; the mod is left to its new owner.", LogPrefix);

        FeedbackHelper.Error(GenericFailureMessage);
    }

    private sealed record PendingExecute(EmoteAttributes Source, EmoteAttributes Target, int Generation,
        SwapTimings Timings, long ElapsedAtTail)
    {
        public Stopwatch RetryClock { get; } = Stopwatch.StartNew();

        public long LastAttemptMs { get; set; }
    }

    internal static class ExecuteRetryPolicy
    {
        internal const long MaxWaitMilliseconds = 600;

        internal const long AttemptIntervalMilliseconds = 50;

        internal static bool ShouldKeepTrying(long elapsedMilliseconds)
            => elapsedMilliseconds < MaxWaitMilliseconds;

        internal static bool ShouldAttemptNow(long elapsedMilliseconds, long lastAttemptMilliseconds)
            => elapsedMilliseconds - lastAttemptMilliseconds >= AttemptIntervalMilliseconds;
    }

    private PendingExecute? _pendingExecute;

    private bool _subscribedToExecuteRetry;

    private void SubscribeExecuteRetry()
    {
        if (_subscribedToExecuteRetry)
            return;

        NoireService.Framework.Update += OnExecuteRetryUpdate;
        _subscribedToExecuteRetry = true;
    }

    public void CancelPendingExecute()
    {
        if (_pendingExecute is { } pending)
            NoireLogger.LogDebug($"Dropped the pending retry of /{pending.Target.Command}.", LogPrefix);

        ClearExecuteRetry();
    }

    private void ClearExecuteRetry()
    {
        _pendingExecute = null;

        if (!_subscribedToExecuteRetry)
            return;

        NoireService.Framework.Update -= OnExecuteRetryUpdate;
        _subscribedToExecuteRetry = false;
    }

    private void OnExecuteRetryUpdate(IFramework framework)
    {
        if (_pendingExecute is not { } pending)
        {
            ClearExecuteRetry();
            return;
        }

        try
        {
            if (!_generations.IsCurrent(pending.Generation))
            {
                NoireLogger.LogDebug("A superseded swap's refused execute stopped retrying; a newer swap owns the mod.", LogPrefix);
                ClearExecuteRetry();
                return;
            }

            if (Configuration.SelfBypassMode != SelfBypassMode.EmoteSwap)
            {
                ClearExecuteRetry();
                AbandonUnexecutedSwap(pending.Generation, pending.Target.RowId);
                return;
            }

            var elapsed = pending.RetryClock.ElapsedMilliseconds;

            if (!ExecuteRetryPolicy.ShouldAttemptNow(elapsed, pending.LastAttemptMs))
            {
                if (!ExecuteRetryPolicy.ShouldKeepTrying(elapsed))
                {
                    ClearExecuteRetry();
                    FailSwapTail(pending.Generation, pending.Target.RowId,
                        $"The game kept refusing /{pending.Target.Command} for {elapsed}ms; the swap did not play.");
                }

                return;
            }

            pending.LastAttemptMs = elapsed;

            if (AttemptExecute(pending.Target.RowId))
            {
                ClearExecuteRetry();
                CompleteSwapTail(pending.Source, pending.Target, pending.Timings, pending.ElapsedAtTail);
                return;
            }

            if (!ExecuteRetryPolicy.ShouldKeepTrying(pending.RetryClock.ElapsedMilliseconds))
            {
                ClearExecuteRetry();
                FailSwapTail(pending.Generation, pending.Target.RowId,
                    $"The game kept refusing /{pending.Target.Command} for {pending.RetryClock.ElapsedMilliseconds}ms; the swap did not play.");
            }
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "The refused-execute retry failed; dropping it.", LogPrefix);
            ClearExecuteRetry();
        }
    }

    private void AbandonUnexecutedSwap(int generation, uint targetRowId)
    {
        if (!_generations.IsCurrent(generation))
        {
            NoireLogger.LogDebug("The execute never ran, but a newer swap owns the mod; leaving it untouched.", LogPrefix);
            return;
        }

        NoireLogger.LogDebug("The execute never ran; turning off the swap that was selected for it.", LogPrefix);
        DeselectArmed(targetRowId);
    }

    private void DeselectArmed(uint targetRowId)
    {
        if (_swapMods.ArmedFor(targetRowId) is { } armed)
            _swapMods.DeselectEntry(armed);
    }

    internal static bool ExecuteSucceeded(bool managerAvailable, bool gameAccepted)
        => managerAvailable && gameAccepted;

    private static unsafe bool TryExecuteEmote(uint emoteRowId)
    {
        var manager = EmoteManager.Instance();
        if (manager == null)
        {
            NoireLogger.LogError("EmoteManager is unavailable; the target emote cannot be executed.", LogPrefix);
            return ExecuteSucceeded(managerAvailable: false, gameAccepted: false);
        }

        var option = CommonHelper.LocalPlayerEmoteOption();
        var accepted = manager->ExecuteEmote((ushort)emoteRowId, &option);

        if (!accepted)
            NoireLogger.LogDebug($"The game refused to execute emote {emoteRowId} right now.", LogPrefix);

        return ExecuteSucceeded(managerAvailable: true, accepted);
    }

    private static bool GameEmoteCooldownActive() => EmoteHelper.IsEmoteCooldownActive();
}
