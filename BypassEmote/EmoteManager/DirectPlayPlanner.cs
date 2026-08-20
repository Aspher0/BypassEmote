using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;
using NoireLib.Animations.Helpers;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;

namespace BypassEmote;

internal sealed record DirectPlayPlan(int SlotIndex, ushort TimelineId, ushort IntroTimelineId = 0)
{
    public bool PlaysIntro => IntroTimelineId != 0;
}

internal readonly record struct PlayerState(EmoteCondition Condition, string? OrnamentName, byte? OrnamentKind);

internal static class DirectPlayPlanner
{
    internal const int ConditionTimelineSlot = -1;

    internal static readonly Dictionary<(uint EmoteRowId, EmoteCondition Condition), (ushort Intro, ushort Loop)>
        ConditionTimelines = new()
        {
            [(143u, EmoteCondition.Swimming)] = (5795, 5796), // /playdead -> emote/swim_on_emot02_start/_loop
        };

    internal static EmoteCondition PlayableAsFor(EmoteCondition condition)
        => ActionTimelineSlots.PlayableAsFor(condition);

    internal static EmoteCondition ConditionTimelineKeyFor(EmoteCondition condition)
        => condition == EmoteCondition.Diving ? EmoteCondition.Swimming : condition;

    internal static DirectPlayPlan ReplaceForDiving(DirectPlayPlan plan, Func<ushort, ushort> replace)
        => plan with
        {
            TimelineId = replace(plan.TimelineId),
            IntroTimelineId = plan.IntroTimelineId == 0 ? (ushort)0 : replace(plan.IntroTimelineId),
        };

    internal static EmoteCondition ConditionFrom(
        CharacterModes mode,
        byte modeParam,
        EmoteController.PoseType poseType,
        bool isDiving,
        bool isSwimming,
        bool isFishing,
        byte? ornamentKind)
        => EmoteHelper.ConditionFrom(mode, modeParam, poseType, isDiving, isSwimming, isFishing, ornamentKind);

    internal static bool RefusedByMode(
        bool isNpc, bool isInBypassedLoop, bool isSleeping, CharacterModes mode, EmotePlayType playType,
        bool isFishing)
    {
        if (isNpc || isInBypassedLoop)
            return false;

        if (mode == CharacterModes.Normal && !isSleeping)
            return false;

        if (isSleeping)
            return true;

        var inAllowedMode = mode is CharacterModes.EmoteLoop or CharacterModes.InPositionLoop
            or CharacterModes.Mounted or CharacterModes.RidingPillion;

        if (!inAllowedMode && !isFishing)
            return true;

        return playType != EmotePlayType.OneShot;
    }

    internal static EmoteCondition ConditionForOrnamentKind(byte ornamentKind)
        => OrnamentHelper.ConditionForOrnamentKind(ornamentKind);

    internal static DirectPlayPlan? TryPlanFor(
        ICharacter character, Emote emote, EmotePlayType playType, out PlayerState state)
    {
        state = ReadState(character);

        ConditionTimelines.TryGetValue(
            (emote.RowId, ConditionTimelineKeyFor(state.Condition)), out var conditionTimelines);

        var plan = TryPlan(
            state.Condition,
            EmoteHelper.GetEmoteConditions(emote),
            SlotTimelineIds(emote),
            playType,
            OverrideSlotFor(emote, playType),
            conditionTimelines == default ? null : conditionTimelines);

        return plan != null && state.Condition == EmoteCondition.Diving
            ? ReplaceForDiving(plan, DivingReplacementFor)
            : plan;
    }

    internal static ushort DivingReplacementFor(ushort timelineId)
        => ActionTimelineHelper.GetReplacement(timelineId);

    internal static PlayerState ReadState(ICharacter character)
    {
        var ornamentKind = OrnamentHelper.GetOrnamentKind(character);
        var ornamentName = ornamentKind == null ? null : OrnamentHelper.GetOrnamentName(character);

        return new PlayerState(EmoteHelper.ConditionOf(character), ornamentName, ornamentKind);
    }

    private static ushort[] SlotTimelineIds(Emote emote)
    {
        var slots = new ushort[ActionTimelineSlots.SlotCount];

        for (var index = 0; index < slots.Length && index < emote.ActionTimeline.Count; index++)
            slots[index] = (ushort)emote.ActionTimeline[index].RowId;

        return slots;
    }

    private static int? OverrideSlotFor(Emote emote, EmotePlayType playType)
    {
        var specification = CommonHelper.TryGetEmoteSpecification(emote);

        if (specification == null)
            return null;

        return playType == EmotePlayType.Looped
            ? specification.SpecificLoopActionTimelineSlot
            : specification.SpecificOneShotActionTimelineSlot;
    }

    internal static string RefusalMessageFor(Emote emote, PlayerState state)
        => RefusalMessage(
            (emote.TextCommand.ValueNullable?.Command.ExtractText() ?? string.Empty).TrimStart('/'),
            state.Condition,
            state.OrnamentName);

    internal static string RefusalMessage(string command, EmoteCondition condition, string? ornamentName = null)
        => $"/{command} cannot be played {Describe(condition, ornamentName)}.";

    private static string Describe(EmoteCondition condition, string? ornamentName) => condition switch
    {
        EmoteCondition.HoldingUmbrella or EmoteCondition.HoldingTorch
            when !string.IsNullOrWhiteSpace(ornamentName) => $"while carrying your {ornamentName}",

        EmoteCondition.Standing => "while standing",
        EmoteCondition.Swimming => "while swimming",
        EmoteCondition.Diving => "while diving",
        EmoteCondition.SittingOnGround => "while sitting on the ground",
        EmoteCondition.SittingInChair => "while sitting in a chair",
        EmoteCondition.Mounted => "while mounted",
        EmoteCondition.HoldingUmbrella => "while holding an umbrella",
        EmoteCondition.HoldingTorch => "while holding a torch",
        EmoteCondition.WearingFashionAccessory => "while wearing a fashion accessory",
        EmoteCondition.Fishing => "while fishing",
        _ => "right now",
    };

    internal static IReadOnlyList<int> SlotPreferenceFor(EmoteCondition condition)
        => ActionTimelineSlots.SlotPreferenceFor(condition);

    internal static DirectPlayPlan? TryPlan(
        EmoteCondition condition,
        EmoteCondition allowedConditions,
        IReadOnlyList<ushort> slotTimelineIds,
        EmotePlayType playType,
        int? overrideSlot = null,
        (ushort Intro, ushort Loop)? conditionTimelines = null)
    {
        var effective = PlayableAsFor(condition);

        if ((allowedConditions & effective) != effective)
            return null;

        if (conditionTimelines is { } supplied)
            return new DirectPlayPlan(ConditionTimelineSlot, supplied.Loop, supplied.Intro);

        IReadOnlyList<int> preference = overrideSlot is { } pinned ? [pinned] : SlotPreferenceFor(effective);

        foreach (var slotIndex in preference)
        {
            if (slotIndex < 0 || slotIndex >= slotTimelineIds.Count)
                continue;

            if (slotTimelineIds[slotIndex] is not (var timelineId and not 0))
                continue;

            var playsIntro = slotIndex == 0 || overrideSlot is not null;
            var introTimelineId = playsIntro && slotTimelineIds.Count > 1 ? slotTimelineIds[1] : (ushort)0;

            return new DirectPlayPlan(slotIndex, timelineId, introTimelineId);
        }

        return null;
    }
}
