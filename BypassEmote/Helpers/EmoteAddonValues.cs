using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NoireLib.Helpers;
using System.Collections.Generic;

namespace BypassEmote.Helpers;

internal static class EmoteAddonValues
{
    private const int CountIndex = 20;
    private const int FirstRow = 21;
    private const int ValuesPerRow = 6;

    internal static unsafe void ShowLockedRowsAsAvailable(AtkValue* values, uint valueCount)
    {
        if (values == null)
            return;

        for (var at = FirstRow; at + ValuesPerRow <= valueCount; at += ValuesPerRow)
        {
            if (!IsRow(values, at) || !Service.IsEmoteLocked(values[at + 0].UInt))
                continue;

            values[at + 2].UInt &= ~1u << 10;
        }
    }

    internal static unsafe void AddMissingEmotes(AtkValue* values, uint valueCount, IReadOnlyList<uint> lockedByOrder,
        IReadOnlyDictionary<uint, nint> searchText, bool showAsAvailable)
    {
        if (values == null || valueCount <= CountIndex || values[CountIndex].Type != AtkValueType.UInt)
            return;

        var agent = AgentEmote.Instance();

        if (agent == null)
            return;

        var totalRows = (int)values[CountIndex].UInt;

        if (totalRows <= 0)
            return;

        var listRows = CountListRows(values, valueCount, totalRows);

        if (listRows <= 0)
            return;

        var present = new HashSet<uint>();
        var rows = new Row[listRows];

        for (var row = 0; row < listRows; row++)
        {
            var at = FirstRow + row * ValuesPerRow;
            var emoteId = values[at + 0].UInt;

            present.Add(emoteId);
            rows[row] = new Row(values[at + 3].UInt, OrderOf(emoteId), NameAt(values, at));
        }

        var missing = new List<uint>();

        foreach (var emoteId in lockedByOrder)
        {
            if (!present.Contains(emoteId))
                missing.Add(emoteId);
        }

        if (missing.Count == 0)
            return;

        var newListRows = listRows + missing.Count;
        var newTotal = totalRows + missing.Count;

        if (FirstRow + newTotal * ValuesPerRow > valueCount)
            return;

        var plan = Plan(rows, missing);

        if (plan.Count != newListRows)
            return;

        var saved = new AtkValue[listRows * ValuesPerRow];

        for (var value = 0; value < saved.Length; value++)
            saved[value] = values[FirstRow + value];

        ShiftRows(values, listRows, totalRows - listRows, missing.Count);

        var history = EmoteHistoryModule.Instance();

        for (var row = 0; row < plan.Count; row++)
        {
            var target = FirstRow + row * ValuesPerRow;
            var source = plan[row].Source;

            if (source < 0)
            {
                WriteRow(values, target, plan[row].Emote, history, searchText, showAsAvailable);
                continue;
            }

            for (var value = 0; value < ValuesPerRow; value++)
                values[target + value] = saved[source * ValuesPerRow + value];
        }

        values[CountIndex].UInt = (uint)newTotal;

        *(ushort*)((byte*)agent + 0x34) = (ushort)newListRows;
        *(ushort*)((byte*)agent + 0x36) = (ushort)(newListRows + 26);
    }

    internal static unsafe void AddSearchMatches(AtkValue* values, uint valueCount, IReadOnlyList<uint> matches,
        IReadOnlyDictionary<uint, nint> searchText, bool showAsAvailable)
    {
        if (values == null || matches.Count == 0 || valueCount <= CountIndex)
            return;

        if (values[0].Type != AtkValueType.Int || values[0].Int != 2)
            return;

        var agent = AgentEmote.Instance();

        if (agent == null)
            return;

        var first = FirstRow + *(ushort*)((byte*)agent + 0x36) * ValuesPerRow;

        if (first + 10 * ValuesPerRow > valueCount)
            return;

        var kind = values[first + 3];

        if (kind.Type != AtkValueType.UInt || (kind.UInt & 0xFF) != 6)
            return;

        var shown = new List<(ushort Order, int Source, uint Emote)>();
        var present = new HashSet<uint>();

        for (var row = 0; row < 10; row++)
        {
            var at = first + row * ValuesPerRow;

            if (!IsRow(values, at))
                break;

            var emoteId = values[at + 0].UInt;

            present.Add(emoteId);
            shown.Add((OrderOf(emoteId), row, emoteId));
        }

        foreach (var emoteId in matches)
        {
            if (present.Add(emoteId))
                shown.Add((OrderOf(emoteId), -1, emoteId));
        }

        shown.Sort((left, right) => left.Order != right.Order
            ? left.Order.CompareTo(right.Order)
            : left.Emote.CompareTo(right.Emote));

        var saved = new AtkValue[10 * ValuesPerRow];

        for (var value = 0; value < saved.Length; value++)
            saved[value] = values[first + value];

        var history = EmoteHistoryModule.Instance();
        var rows = System.Math.Min(shown.Count, 10);
        var moved = new HashSet<int>();

        for (var row = 0; row < rows; row++)
        {
            if (shown[row].Source >= 0)
                moved.Add(shown[row].Source);
        }

        for (var row = 0; row < rows; row++)
        {
            if (moved.Contains(row))
                continue;

            for (var value = 0; value < ValuesPerRow; value++)
                (values + first + row * ValuesPerRow + value)->Dtor();
        }

        for (var row = 0; row < rows; row++)
        {
            var target = first + row * ValuesPerRow;
            var source = shown[row].Source;

            if (source < 0)
            {
                WriteRow(values, target, shown[row].Emote, history, searchText, showAsAvailable, inSearch: true);
                continue;
            }

            for (var value = 0; value < ValuesPerRow; value++)
                values[target + value] = saved[source * ValuesPerRow + value];
        }
    }

    private static unsafe bool IsRow(AtkValue* values, int at)
        => values[at + 0].Type == AtkValueType.UInt
        && values[at + 2].Type == AtkValueType.UInt
        && values[at + 0].UInt != 0;

    private static List<(int Source, uint Emote)> Plan(Row[] rows, List<uint> missing)
    {
        var runs = new List<(uint Kind, int Start, int Count)>();

        for (var row = 0; row < rows.Length; row++)
        {
            if (runs.Count > 0 && runs[^1].Kind == rows[row].Kind)
            {
                var last = runs[^1];
                runs[^1] = (last.Kind, last.Start, last.Count + 1);
                continue;
            }

            runs.Add((rows[row].Kind, row, 1));
        }

        var buckets = new Dictionary<uint, List<Added>>();
        var homeless = new List<uint>();

        foreach (var emoteId in missing)
        {
            var kind = KindOf(emoteId);

            if (!runs.Exists(run => run.Kind == kind))
            {
                homeless.Add(emoteId);
                continue;
            }

            if (!buckets.TryGetValue(kind, out var bucket))
                buckets[kind] = bucket = [];

            bucket.Add(new Added(emoteId, NameOf(emoteId), OrderOf(emoteId)));
        }

        var plan = new List<(int Source, uint Emote)>(rows.Length + missing.Count);

        foreach (var run in runs)
        {
            buckets.Remove(run.Kind, out var bucket);
            MergeRun(plan, rows, run.Start, run.Count, bucket);
        }

        foreach (var emoteId in homeless)
            plan.Add((-1, emoteId));

        return plan;
    }

    private static void MergeRun(List<(int Source, uint Emote)> plan, Row[] rows, int start, int count,
        List<Added>? bucket)
    {
        if (bucket == null || bucket.Count == 0)
        {
            for (var row = 0; row < count; row++)
                plan.Add((start + row, 0u));

            return;
        }

        var byName = !IsSortedByOrder(rows, start, count);

        if (byName)
            bucket.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        else
            bucket.Sort((left, right) => left.Order.CompareTo(right.Order));

        var kept = start;
        var end = start + count;
        var next = 0;

        while (kept < end || next < bucket.Count)
        {
            var takeAdded = kept >= end
                || (next < bucket.Count && (byName
                    ? string.CompareOrdinal(bucket[next].Name, rows[kept].Name) <= 0
                    : bucket[next].Order <= rows[kept].Order));

            if (takeAdded)
            {
                plan.Add((-1, bucket[next].Emote));
                next++;
                continue;
            }

            plan.Add((kept, 0u));
            kept++;
        }
    }

    private static bool IsSortedByOrder(Row[] rows, int start, int count)
    {
        for (var row = start + 1; row < start + count; row++)
        {
            if (rows[row].Order < rows[row - 1].Order)
                return false;
        }

        return true;
    }

    private static unsafe string NameAt(AtkValue* values, int at)
    {
        var value = values[at + 4];

        return value.String.Value == null ? string.Empty : value.String.ToString();
    }

    private static string NameOf(uint emoteId)
        => EmoteHelper.GetEmoteById(emoteId) is { } emote ? emote.Name.ExtractText() ?? string.Empty : string.Empty;

    private static unsafe uint KindOf(uint emoteId)
    {
        EmoteController.EmoteDetails details;

        return EmoteController.TryGetEmoteDetails(emoteId, &details) ? details.EmoteCategory : 0u;
    }

    private static unsafe int CountListRows(AtkValue* values, uint valueCount, int totalRows)
    {
        for (var row = 0; row < totalRows; row++)
        {
            var at = FirstRow + row * ValuesPerRow;

            if (at + ValuesPerRow > valueCount)
                return row;

            if (values[at + 3].Type != AtkValueType.UInt || values[at + 3].UInt > 3)
                return row;
        }

        return totalRows;
    }

    private static unsafe void ShiftRows(AtkValue* values, int firstMovedRow, int movedRows, int by)
    {
        for (var row = movedRows - 1; row >= 0; row--)
        {
            var from = FirstRow + (firstMovedRow + row) * ValuesPerRow;
            var to = FirstRow + (firstMovedRow + by + row) * ValuesPerRow;

            for (var value = 0; value < ValuesPerRow; value++)
                values[to + value] = values[from + value];
        }
    }

    private static unsafe void WriteRow(AtkValue* values, int at, uint emoteId, EmoteHistoryModule* history,
        IReadOnlyDictionary<uint, nint> searchText, bool showAsAvailable, bool inSearch = false)
    {
        EmoteController.EmoteDetails details;

        if (!EmoteController.TryGetEmoteDetails(emoteId, &details) || details.Name.Value == null)
        {
            SetUInt(values, at + 0, 0);
            return;
        }

        var name = details.Name.Value;
        var trailer = name;

        while (*trailer != 0)
            trailer++;

        if (searchText.TryGetValue(emoteId, out var commands) && commands != 0)
            trailer = (byte*)commands;

        var conditions = details.EmoteCategory == 3 ? 0x3FF : ConditionFlags(&details);
        var flags = showAsAvailable || inSearch ? conditions : conditions | 1u << 10;

        if (history != null && history->IsUnseen((ushort)emoteId))
            flags |= 1u << 11;

        var kind = inSearch ? 6 | ((uint)details.EmoteCategory << 8) : details.EmoteCategory;

        SetUInt(values, at + 0, emoteId);
        SetUInt(values, at + 1, details.Icon);
        SetUInt(values, at + 2, flags);
        SetUInt(values, at + 3, kind);
        SetConstString(values, at + 4, name);
        SetConstString(values, at + 5, trailer);
    }

    private static unsafe uint ConditionFlags(EmoteController.EmoteDetails* details)
    {
        var flags = 0u;

        if (details->SittingOnGround) flags |= 1u << 0;
        if (details->SittingInChair) flags |= 1u << 1;
        if (details->Mounted) flags |= 1u << 2;
        if (details->Fishing) flags |= 1u << 3;
        if (details->Standing) flags |= 1u << 4;
        if (details->Swimming) flags |= 1u << 5;
        if (details->Diving) flags |= 1u << 6;
        if (details->HoldingTorch) flags |= 1u << 7;
        if (details->WearingFashionAccessory) flags |= 1u << 8;
        if (details->HoldingUmbrella) flags |= 1u << 9;

        return flags;
    }

    private static ushort OrderOf(uint emoteId)
        => EmoteHelper.GetEmoteById(emoteId) is { } emote ? emote.Order : ushort.MaxValue;

    private static unsafe void SetUInt(AtkValue* values, int at, uint value)
    {
        values[at].Type = AtkValueType.UInt;
        values[at].UInt = value;
    }

    private static unsafe void SetConstString(AtkValue* values, int at, byte* value)
    {
        values[at].Type = AtkValueType.ConstString;
        values[at].String = value;
    }

    private readonly record struct Row(uint Kind, ushort Order, string Name);

    private readonly record struct Added(uint Emote, string Name, ushort Order);
}
