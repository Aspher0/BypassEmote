namespace BypassEmote.Models;

public class EmoteSpecification
{
    public uint? SingleId { get; }
    public uint? RangeStart { get; }
    public uint? RangeEnd { get; }

    public EmotePlayType PlayType { get; }
    public string? Name { get; }
    public ushort? Icon { get; }
    public int? SpecificOneShotActionTimelineSlot { get; }
    public int? SpecificLoopActionTimelineSlot { get; }
    public byte Cpose { get; } = 0;

    public EmoteSpecification(
        uint id,
        EmotePlayType playType = EmotePlayType.OneShot,
        string? name = null,
        ushort? icon = null,
        int? specificOneShotActionTimelineSlot = null,
        int? specificLoopActionTimelineSlot = null,
        byte cpose = 0)
    {
        SingleId = id;
        PlayType = playType;
        Name = name;
        Icon = icon;
        SpecificOneShotActionTimelineSlot = specificOneShotActionTimelineSlot;
        SpecificLoopActionTimelineSlot = specificLoopActionTimelineSlot;
        Cpose = cpose;
    }

    public EmoteSpecification(
        uint startId,
        uint endId,
        EmotePlayType playType = EmotePlayType.DoNotPlay,
        string? name = null,
        ushort? icon = null,
        int? specificOneShotActionTimelineSlot = null,
        int? specificLoopActionTimelineSlot = null,
        byte cpose = 0)
    {
        RangeStart = startId;
        RangeEnd = endId;
        PlayType = playType;
        Name = name;
        Icon = icon;
        SpecificOneShotActionTimelineSlot = specificOneShotActionTimelineSlot;
        SpecificLoopActionTimelineSlot = specificLoopActionTimelineSlot;
        Cpose = cpose;
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
