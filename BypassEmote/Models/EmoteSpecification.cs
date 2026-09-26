using BypassEmote.Enums;
using System;

namespace BypassEmote.Models;

public class EmoteSpecification
{
    public uint? SingleId { get; }
    public uint? RangeStart { get; }
    public uint? RangeEnd { get; }

    public EmotePlayType PlayType { get; }
    private readonly Func<string>? name;

    public string? Name => name?.Invoke();
    public uint? Icon { get; }
    public int? SpecificOneShotActionTimelineSlot { get; }
    public int? SpecificLoopActionTimelineSlot { get; }
    public bool IsAssignableToHotbar { get; } = true;

    public EmoteSpecification(
        uint id,
        EmotePlayType playType = EmotePlayType.OneShot,
        Func<string>? name = null,
        uint? icon = null,
        int? specificOneShotActionTimelineSlot = null,
        int? specificLoopActionTimelineSlot = null,
        bool isAssignableToHotbar = true)
    {
        SingleId = id;
        PlayType = playType;
        this.name = name;
        Icon = icon;
        SpecificOneShotActionTimelineSlot = specificOneShotActionTimelineSlot;
        SpecificLoopActionTimelineSlot = specificLoopActionTimelineSlot;
        IsAssignableToHotbar = isAssignableToHotbar;
    }

    public EmoteSpecification(
        uint startId,
        uint endId,
        EmotePlayType playType = EmotePlayType.DoNotPlay,
        Func<string>? name = null,
        uint? icon = null,
        int? specificOneShotActionTimelineSlot = null,
        int? specificLoopActionTimelineSlot = null,
        bool isAssignableToHotbar = true)
    {
        RangeStart = startId;
        RangeEnd = endId;
        PlayType = playType;
        this.name = name;
        Icon = icon;
        SpecificOneShotActionTimelineSlot = specificOneShotActionTimelineSlot;
        SpecificLoopActionTimelineSlot = specificLoopActionTimelineSlot;
        IsAssignableToHotbar = isAssignableToHotbar;
    }

    public bool Matches(uint rowId)
    {
        if (SingleId.HasValue && SingleId.Value == rowId)
            return true;

        if (RangeStart.HasValue && RangeEnd.HasValue && rowId >= RangeStart.Value && rowId <= RangeEnd.Value)
            return true;

        return false;
    }
}
