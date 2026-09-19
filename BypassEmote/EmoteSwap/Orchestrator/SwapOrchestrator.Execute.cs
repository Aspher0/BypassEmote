using BypassEmote.Enums;
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
        SwapTimings timings, SwapTrace trace)
    {
        Log.Debug($"Reuse route for /{source.Command} onto /{target.Command}.", LogPrefix);

        if (!_swapMods.SelectExisting(kept))
        {
            Log.Debug($"The existing swap could not be put back. Rebuilding /{source.Command}.", LogPrefix);
            LogHelper.DebugLine(">   reuse refused, rebuilding");
            return false;
        }

        LogHelper.DebugLine(">   reuse");

        var generation = _generations.TakeOwnership();

        ExecuteSwapTail(source, target, generation, timings with { AtApply = timings.Clock.ElapsedMilliseconds }, trace);
        return true;
    }

    private void ExecuteSwapTail(EmoteAttributes source, EmoteAttributes target, int generation, SwapTimings timings,
        SwapTrace? trace)
    {
        var elapsedAtTail = timings.Clock.ElapsedMilliseconds;

        if (!_generations.IsCurrent(generation))
        {
            Log.Debug("Dropped the deferred execute of a superseded swap.", LogPrefix);
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
            CompleteSwapTail(source, target, timings, elapsedAtTail, trace);
            return;
        }

        _pendingExecute = new PendingExecute(source, target, generation, timings, elapsedAtTail, trace);

        SubscribeExecuteRetry();

        Log.Debug(
            $"The game refused /{target.Command} for the /{source.Command} swap. Retrying for up to " +
            $"{ExecuteRetryPolicy.MaxWaitMilliseconds}ms.", LogPrefix);
    }

    private bool AttemptExecute(uint targetRowId)
    {
        if (_catalog.Get(targetRowId) is { } target)
            Service.Rebinder?.Arm(target);

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

    private void CompleteSwapTail(EmoteAttributes source, EmoteAttributes target, SwapTimings timings, long elapsedAtTail,
        SwapTrace? trace)
    {
        var elapsedAtExecute = timings.Clock.ElapsedMilliseconds;

        LogHelper.SwapLine(source.Command, target.Command);

        LogSwapPlayed(source, target, trace, elapsedAtExecute);

        Service.RecordEmoteHistory(source.RowId);

        if (TargetDropsSourceIntro(source, target))
            LogHelper.Notice(TargetIntroDroppedMessageFor(target));

        if (Configuration.SwapLifetime == SwapLifetime.WhenEmoteEnds && _swapMods.ArmedFor(target.RowId) is { } armed)
            _endWatcher.Arm(armed);
        else
            _endWatcher.StopWatching();

        Log.Debug(
            $"Swap timings: match {timings.AtMatch}ms, pair {timings.AtPair - timings.AtMatch}ms, " +
            $"retarget {timings.AtRetarget - timings.AtPair}ms, prepare {timings.AtPrepare - timings.AtRetarget}ms, " +
            $"apply {timings.AtApply - timings.AtPrepare}ms"
            + (timings.AtFrame > 0
                ? $" (frame wait {timings.AtFrame - timings.AtPrepare}ms, naming {timings.AtEntry - timings.AtFrame}ms)"
                : string.Empty)
            + $", gate {elapsedAtTail - timings.AtApply}ms, " +
            $"execute {elapsedAtExecute - elapsedAtTail}ms, total {elapsedAtExecute}ms.", LogPrefix);

        LogHelper.DebugLine(
            $">   executed | gate {elapsedAtTail - timings.AtApply}ms, total {elapsedAtExecute}ms");

        LogHelper.DebugLine(
            $">   shapes | /{source.Command} {ShapeOf(source)} -> /{target.Command} {ShapeOf(target)}");
    }

    internal static string ShapeOf(EmoteAttributes emote)
        => $"{(emote.Intro == IntroKind.Pap ? "intro" : "no intro")}"
        + $" + {(emote.LoopKind == EmotePlayType.Looped ? "loop" : "one shot")}";

    private void FailSwapTail(PendingExecute pending, string debugDetail)
    {
        Log.Debug(debugDetail, LogPrefix);

        LogSwapNotPlayed(pending.Source, pending.Target, pending.Trace,
            $"the game kept refusing the target for {pending.RetryClock.ElapsedMilliseconds}ms");

        if (_generations.IsCurrent(pending.Generation))
            DeselectArmed(pending.Target.RowId);
        else
            Log.Debug("The failed execute's swap was already superseded. Mod left to the newer swap.", LogPrefix);

        LogHelper.Error(GenericFailureMessage);
    }

    private sealed record PendingExecute(EmoteAttributes Source, EmoteAttributes Target, int Generation,
        SwapTimings Timings, long ElapsedAtTail, SwapTrace? Trace)
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
            Log.Debug($"Dropped the pending retry of /{pending.Target.Command}.", LogPrefix);

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
                Log.Debug("Stopped retrying the refused execute of a superseded swap.", LogPrefix);
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
                    FailSwapTail(pending,
                        $"The game kept refusing /{pending.Target.Command} for {elapsed}ms. Swap not played.");
                }

                return;
            }

            pending.LastAttemptMs = elapsed;

            if (AttemptExecute(pending.Target.RowId))
            {
                ClearExecuteRetry();
                CompleteSwapTail(pending.Source, pending.Target, pending.Timings, pending.ElapsedAtTail, pending.Trace);
                return;
            }

            if (!ExecuteRetryPolicy.ShouldKeepTrying(pending.RetryClock.ElapsedMilliseconds))
            {
                ClearExecuteRetry();
                FailSwapTail(pending,
                    $"The game kept refusing /{pending.Target.Command} for {pending.RetryClock.ElapsedMilliseconds}ms. Swap not played.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "The refused-execute retry failed. Dropped.", LogPrefix);
            ClearExecuteRetry();
        }
    }

    private void AbandonUnexecutedSwap(int generation, uint targetRowId)
    {
        if (!_generations.IsCurrent(generation))
        {
            Log.Debug("The execute never ran. The mod belongs to a newer swap.", LogPrefix);
            return;
        }

        Log.Debug("The execute never ran. Deselecting its swap.", LogPrefix);
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
            Log.Error("EmoteManager is unavailable. Target emote not executed.", LogPrefix);
            return ExecuteSucceeded(managerAvailable: false, gameAccepted: false);
        }

        var option = CommonHelper.LocalPlayerEmoteOption();
        var accepted = manager->ExecuteEmote((ushort)emoteRowId, &option);

        if (!accepted)
            Log.Debug($"The game refused to execute emote {emoteRowId} right now.", LogPrefix);

        return ExecuteSucceeded(managerAvailable: true, accepted);
    }

    private static bool GameEmoteCooldownActive() => EmoteHelper.IsEmoteCooldownActive();
}
