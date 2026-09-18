using BypassEmote.Enums;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    private sealed record SwapContext(string Route, string Pool, string Character);

    private sealed record SwapTrace(string Skeleton, SwapContext Context, IReadOnlyList<string> Details,
        bool Reused = false);

    private static string LabelFor(EmoteAttributes emote)
    {
        var name = DisplayNameFor(emote);

        return $"{CommandFor(emote) ?? "no command"} ({(string.IsNullOrWhiteSpace(name) ? "unnamed" : name)}, #{emote.RowId})";
    }

    private string ServedBy(string requestedPath, string resolvedPath)
        => ServedPath.Describe(requestedPath, resolvedPath, _penumbra.GetModRootDirectory(), _swapMods.IsOwnPath,
            ModNameFor);

    private string? ModNameServing(string requestedPath, string resolvedPath)
        => resolvedPath == requestedPath || _swapMods.IsOwnPath(resolvedPath)
            ? null
            : ModNameFor(SwapModManager.ModDirectoryFromDiskPath(resolvedPath, _penumbra.GetModRootDirectory()));

    private string SourceServedByFor(EmoteAttributes target, IReadOnlyList<ResolvedVariantPair> pairs)
        => string.Join("; ", pairs.Select(pair =>
            $"{RoleOf(target, pair.Pair.TargetRequestedPath)}: {ServedBy(pair.Pair.SourceRequestedPath, pair.ResolvedSourcePath)}"));

    private static string MatchRoute(MatchResult match, uint? plainBest, Func<uint, EmoteAttributes?> emoteOf)
    {
        if (plainBest is not { } best || match.Target is not { } target || best == target.RowId)
            return "best match";

        return $"best match, dispatched off {(emoteOf(best) is { } plain ? LabelFor(plain) : $"#{best}")}";
    }

    private static string IdlePoseRoute(MatchResult match, bool poolHasLoop)
        => $"idle pose ({Configuration.IdlePoseLoops}), "
        + (match.Target is { } best ? $"the best target {LabelFor(best)} is a one shot" : "no target fits")
        + $", {(poolHasLoop ? "the pool has a loop" : "the pool has no loop")}";

    internal static string? TargetRefusal(ICharacter character, EmoteAttributes candidate, EmoteCondition condition)
    {
        if (!candidate.EligibleTarget)
            return "not a target";

        if (!EmoteHelper.IsEmoteUnlocked(candidate.RowId))
            return "locked";

        return PoolExclusionFor(character, candidate, condition, askTheGame: true)?.ToLowerInvariant();
    }

    private static string PoolLine(int catalogCount, IReadOnlyDictionary<string, int> leftOut,
        IReadOnlyList<EmoteAttributes> pool, IReadOnlySet<uint>? blocked, string? modded)
    {
        var line = new StringBuilder($"pool: {pool.Count} target(s) out of {catalogCount} emotes, "
            + $"{pool.Count(candidate => candidate.LoopKind == EmotePlayType.Looped)} loop(s)");

        if (leftOut.Count > 0)
        {
            line.Append("; left out: ").Append(string.Join(", ", leftOut
                .OrderByDescending(reason => reason.Value)
                .Select(reason => $"{reason.Value} {reason.Key}")));
        }

        if (blocked != null && pool.Count(candidate => blocked.Contains(candidate.RowId)) is > 0 and var count)
            line.Append($"; {count} on your blocked list");

        if (modded != null)
            line.Append("; ").Append(modded);

        return line.ToString();
    }

    private string ModdedLine(IReadOnlyList<EmoteAttributes> before, IReadOnlyList<EmoteAttributes> after,
        MatchConfig config, string skeleton, IReadOnlyList<string> fallbackOrder)
    {
        switch (Configuration.ModdedTargets)
        {
            case ModdedTargetRule.Allowed:
                return "modded targets allowed";

            case ModdedTargetRule.Blocked:
                return $"{config.ModdedTargets?.Count ?? 0} changed by another mod, blocked";
        }

        if (after.Count < before.Count)
            return $"{before.Count - after.Count} changed by another mod, left out";

        var kept = after.Count(candidate => ChangedByAnotherMod(candidate, skeleton, fallbackOrder) != null);

        return kept > 0
            ? $"{kept} changed by another mod, kept since nothing clean fits"
            : "none changed by another mod";
    }

    private static string CharacterLine(ICharacter localPlayer, EmoteCondition playedAs)
    {
        var state = DirectPlayPlanner.ReadState(localPlayer);
        var pose = CharacterPoseState.Read(localPlayer);

        return $"character: {state.Condition}"
            + (state.Condition != playedAs ? $" played as {playedAs}" : string.Empty)
            + $", mode {pose.Mode}/{pose.ModeParam}"
            + (pose.Stance is { } stance ? $", pose {stance} {pose.Index}" : ", no pose")
            + $", weapon {(CharacterHelper.IsCharacterWeaponDrawn(localPlayer.Address) ? "drawn" : "sheathed")}"
            + $", weapon motion {ReadWeaponMotionFolder(localPlayer) ?? "unreadable"}"
            + (state.OrnamentName is { } ornament ? $", ornament {ornament}" : string.Empty);
    }

    private SwapTrace TraceFor(EmoteAttributes target, string skeleton, SwapContext context,
        IReadOnlyList<ResolvedVariantPair> pairs, IReadOnlyList<string> fallbackOrder)
    {
        var details = new List<string>(pairs.Count + 1);

        foreach (var (pair, resolvedSourcePath) in pairs)
        {
            details.Add($"{RoleOf(target, pair.TargetRequestedPath)}: {pair.SourceRequestedPath} -> "
                + $"{ServedBy(pair.SourceRequestedPath, resolvedSourcePath)}, onto {pair.TargetRequestedPath}");
        }

        details.Add("target emote: " + (ChangedByAnotherMod(target, skeleton, fallbackOrder) switch
        {
            null => "not changed by another mod",
            { Length: 0 } => "changed by a file outside the mod folder",
            var directory => $"changed by mod \"{ModNameFor(directory) ?? directory}\"",
        }));

        return new SwapTrace(skeleton, context, details);
    }

    private static string RoleOf(EmoteAttributes target, string targetRequestedPath)
    {
        if (target.IntroRelativePapPath is { } intro && targetRequestedPath.EndsWith(intro, StringComparison.Ordinal))
            return "intro";

        if (target.AdjustRelativePapPath is { } adjust && targetRequestedPath.EndsWith(adjust, StringComparison.Ordinal))
            return "adjust";

        return target.Variants.FirstOrDefault(variant =>
                targetRequestedPath.EndsWith(variant.RelativePapPath, StringComparison.Ordinal)) is { } matched
            ? matched.Posture.ToString()
            : "pap";
    }

    private static IEnumerable<string> ContextLines(SwapContext context, bool reused)
        => [$"route: {context.Route}, {(reused ? "reused option" : "new option")}", context.Pool, context.Character];

    private void LogSwapPlayed(EmoteAttributes source, EmoteAttributes target, SwapTrace? trace, long totalMilliseconds)
    {
        if (trace == null)
            return;

        var option = _swapMods.ArmedFor(target.RowId) is { } armed
            ? $"'{armed.GroupName}' / '{armed.OptionName}'"
            : "none armed";

        Log.Info(TraceText($"Swap played: {LabelFor(source)} -> {LabelFor(target)} on {trace.Skeleton} in {totalMilliseconds}ms",
            [.. ContextLines(trace.Context, trace.Reused), $"option: {option}", .. trace.Details]), LogPrefix);
    }

    private static void LogSwapNotPlayed(EmoteAttributes source, EmoteAttributes target, SwapTrace? trace, string reason)
    {
        if (trace == null)
            return;

        Log.Info(TraceText($"Swap did not play: {LabelFor(source)} -> {LabelFor(target)} on {trace.Skeleton}, {reason}",
            [.. ContextLines(trace.Context, trace.Reused), .. trace.Details]), LogPrefix);
    }

    private static string TraceText(string header, IEnumerable<string> lines)
    {
        var text = new StringBuilder(header);

        foreach (var line in lines)
            text.Append(Environment.NewLine).Append("    ").Append(line);

        return text.ToString();
    }
}
