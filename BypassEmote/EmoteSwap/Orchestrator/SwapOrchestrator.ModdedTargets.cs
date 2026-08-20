using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib;
using NoireLib.Animations.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed partial class SwapOrchestrator
{
    internal const string ChangedTargetKind = "swap.changed-target";

    private readonly Dictionary<(string Skeleton, uint RowId), string?> _changedByAnotherMod = new();

    private Guid _changedByAnotherModCollection;

    private void ForgetChangedTargets(Guid _) => _changedByAnotherMod.Clear();

    private void ForgetChangedTargetsOfAnotherCollection(Guid collectionId)
    {
        if (_changedByAnotherModCollection == collectionId)
            return;

        _changedByAnotherModCollection = collectionId;
        _changedByAnotherMod.Clear();
    }

    private string? ChangedByAnotherMod(EmoteAttributes candidate, string skeleton, IReadOnlyList<string> fallbackOrder)
    {
        var key = (skeleton, candidate.RowId);

        if (_changedByAnotherMod.TryGetValue(key, out var known))
            return known;

        var modRoot = _penumbra.GetModRootDirectory();
        string? changedBy = null;
        var changed = false;
        var certain = true;

        try
        {
            foreach (var variant in candidate.Variants)
            {
                foreach (var step in fallbackOrder)
                {
                    var path = EmotePathHelper.GetSkeletonPath(step, variant.RelativePapPath);
                    var resolved = _penumbra.ResolvePlayerPath(path);

                    if (resolved == path)
                        continue;

                    if (_swapMods.IsOwnPath(resolved))
                    {
                        certain = false;
                        continue;
                    }

                    changedBy = SwapModManager.ModDirectoryFromDiskPath(resolved, modRoot) ?? string.Empty;
                    changed = true;
                    break;
                }

                if (changed)
                    break;
            }
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not tell whether /{candidate.Command} is already modded; treating it as clean.", LogPrefix);
            return null;
        }

        if (certain || changed)
            _changedByAnotherMod[key] = changedBy;

        return changedBy;
    }

    private IReadOnlySet<uint>? ChangedTargetRowIds(IReadOnlyList<EmoteAttributes> pool, string skeleton,
        IReadOnlyList<string> fallbackOrder)
    {
        var changed = new HashSet<uint>();

        foreach (var candidate in pool)
        {
            if (ChangedByAnotherMod(candidate, skeleton, fallbackOrder) != null)
                changed.Add(candidate.RowId);
        }

        return changed.Count > 0 ? changed : null;
    }

    internal string? ModServingAnimation(EmoteAttributes emote, string skeleton)
        => ModNameFor(ChangedByAnotherMod(emote, skeleton, EmotePathHelper.GetFallbackOrder(skeleton)));

    private string? ModNameFor(string? modDirectory)
    {
        if (string.IsNullOrEmpty(modDirectory))
            return null;

        return _penumbra.GetModNames() is { } names && names.TryGetValue(modDirectory, out var name) && name.Length > 0
            ? name
            : modDirectory;
    }

    private List<EmoteAttributes> PoolAvoidingChangedTargets(EmoteAttributes source, List<EmoteAttributes> pool,
        MatchConfig matchConfig, PostureFlags posture, string skeleton, IReadOnlyList<string> fallbackOrder)
        => PoolAvoidingChangedTargetsCore(source, pool, matchConfig, posture,
            candidate => ChangedByAnotherMod(candidate, skeleton, fallbackOrder) != null);

    internal static List<EmoteAttributes> PoolAvoidingChangedTargetsCore(EmoteAttributes source,
        List<EmoteAttributes> pool, MatchConfig matchConfig, PostureFlags posture,
        Func<EmoteAttributes, bool> changedByAnotherMod)
    {
        var clean = pool.Where(candidate => !changedByAnotherMod(candidate)).ToList();

        if (clean.Count == pool.Count)
            return pool;

        if (BestMatchResolver.Resolve(source, clean, matchConfig, posture).Target == null)
        {
            NoireLogger.LogDebug($"Every emote that fits /{source.Command} is changed by another mod; none is avoided.", LogPrefix);
            return pool;
        }

        NoireLogger.LogDebug($"{pool.Count - clean.Count} emote(s) left out of the running: another mod changes them.", LogPrefix);
        return clean;
    }

    internal static string ChangedTargetMessage(EmoteAttributes target, string modName)
        => $"This emote landed on /{target.Command}, which your mod \"{modName}\" changes. Players around you may "
        + "briefly see that mod's animation before yours reaches them. If you don't want this to happen, head over to the configuration " +
        "window and block emotes that are changed by other mods.";

    private void ReportChangedTarget(EmoteAttributes target, string modDirectory)
    {
        var modName = ModNameFor(modDirectory) ?? modDirectory;
        var message = ChangedTargetMessage(target, modName);

        var chat = NoireLogger.CreateChatMessageBuilder();
        chat.AddText(message, NoticeColor);
        ModActionChatPayloads.Append(chat, modDirectory, modName);

        FeedbackHelper.Notice(message, ChangedTargetKind, chat);
    }
}
