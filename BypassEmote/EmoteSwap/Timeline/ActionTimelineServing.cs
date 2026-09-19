using NoireLib.Animations.PapFormat;
using NoireLib.Animations.PapFormat.Tmb;
using NoireLib.Animations.PapFormat.Tmb.Entries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

internal static class ActionTimelineServing
{
    private const string LogPrefix = "[ActionTimelineServing] ";

    internal sealed record ServedActionTimeline(byte[] Bytes, IReadOnlyList<string> BodyNames);

    internal static ServedActionTimeline? Serve(byte[] sourceBytes, IReadOnlyCollection<string> sourcePapNames,
        IReadOnlyList<string> targetNames, string sourceLabel, string targetLabel)
    {
        var timeline = new TmbFile(new BinaryReader(new MemoryStream(sourceBytes)));

        var sourceNames = AnimationNames(timeline)
            .Where(name => sourcePapNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (sourceNames.Count == 0)
        {
            Log.Debug($"Tmb not served on '{targetLabel}': '{sourceLabel}' asks for no animation of its pap", LogPrefix);

            return null;
        }

        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = PapSharing.Match(sourceNames, targetNames);

        for (var index = 0; index < targetNames.Count; index++)
        {
            if (matches[index] >= 0)
                renames.TryAdd(sourceNames[matches[index]], targetNames[index]);
        }

        if (renames.Count == 0)
        {
            Log.Debug($"Tmb not served on '{targetLabel}': no animation match with '{sourceLabel}'", LogPrefix);
            return null;
        }

        foreach (var entry in timeline.AllEntries)
        {
            var path = entry switch
            {
                C010 animation => animation.Path,
                C009 animation => animation.Path,
                _ => null,
            };

            if (path?.Value is { } name && renames.TryGetValue(name, out var renamed))
                path.Value = renamed;
        }

        var bodyNames = renames.Values.Distinct(StringComparer.Ordinal).ToList();
        var bytes = timeline.ToBytes();

        Verify(bytes, timeline.AllEntries.Select(entry => entry.Magic).ToList(), bodyNames);
        VerifyOnlyNamesChanged(sourceBytes, bytes, renames);

        Log.Debug($"Tmb '{sourceLabel}' served on '{targetLabel}', "
            + $"commands: {string.Join(", ", timeline.AllEntries.Select(entry => entry.Magic).Distinct())}, "
            + $"renamed: {string.Join(", ", renames.Select(rename => $"{rename.Key} -> {rename.Value}"))}", LogPrefix);

        return new ServedActionTimeline(bytes, bodyNames);
    }

    private static List<string> AnimationNames(TmbFile timeline)
        => timeline.AllEntries
            .Select(entry => entry switch
            {
                C010 animation => animation.Path.Value,
                C009 animation => animation.Path.Value,
                _ => null,
            })
            .OfType<string>()
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static void Verify(byte[] bytes, IReadOnlyList<string> expectedCommands, IReadOnlyList<string> bodyNames)
    {
        TmbFile reread;

        try
        {
            reread = new TmbFile(new BinaryReader(new MemoryStream(bytes)));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Served tmb is unreadable", ex);
        }

        if (!reread.AllEntries.Select(entry => entry.Magic).SequenceEqual(expectedCommands, StringComparer.Ordinal))
            throw new InvalidDataException("Command mismatch with the mod tmb");

        var asked = AnimationNames(reread);

        if (bodyNames.FirstOrDefault(name => !asked.Contains(name, StringComparer.Ordinal)) is { } missing)
            throw new InvalidDataException($"Served tmb is missing {missing}");
    }

    private const int SizeFieldOffset = 4;

    private const int ItemCountOffset = 8;

    private const int FirstItemOffset = 12;

    private const int ItemHeaderLength = 8;

    private sealed record StringField(int Position, int Target);

    internal static void VerifyOnlyNamesChanged(byte[] source, byte[] served,
        IReadOnlyDictionary<string, string> renames)
    {
        var sourceFields = StringFieldsOf(source);
        var servedFields = StringFieldsOf(served);

        if (sourceFields.Count != servedFields.Count)
            throw new InvalidDataException("Item mismatch with the mod tmb");

        var ignored = new HashSet<int>();
        for (var index = 0; index < SizeFieldOffset + sizeof(int); index++)
            ignored.Add(index);

        var sourceStrings = source.Length;

        for (var index = 0; index < sourceFields.Count; index++)
        {
            var (magic, sourceField) = sourceFields[index];
            var (servedMagic, servedField) = servedFields[index];

            if (magic != servedMagic || sourceField.Position != servedField.Position)
                throw new InvalidDataException("Item mismatch with the mod tmb");

            for (var offset = 0; offset < sizeof(int); offset++)
                ignored.Add(sourceField.Position + offset);

            var before = sourceField.Target < 0 ? string.Empty : ReadString(source, sourceField.Target);
            var after = servedField.Target < 0 ? string.Empty : ReadString(served, servedField.Target);

            var renamedBody = (magic == C010.MAGIC || magic == C009.MAGIC)
                && renames.TryGetValue(before, out var expected) && expected == after;

            if (before != after && !renamedBody)
                throw new InvalidDataException($"{magic} string changed: {before} -> {after}");

            if (sourceField.Target >= 0)
                sourceStrings = Math.Min(sourceStrings, sourceField.Target);
        }

        if (served.Length < sourceStrings)
            throw new InvalidDataException("Served tmb is truncated");

        var covered = new bool[source.Length];

        foreach (var (_, field) in sourceFields)
        {
            if (field.Target < 0)
                continue;

            var end = Array.IndexOf(source, (byte)0, field.Target);
            for (var index = field.Target; index <= (end < 0 ? source.Length - 1 : end); index++)
                covered[index] = true;
        }

        for (var index = sourceStrings; index < source.Length; index++)
        {
            if (!covered[index] && source[index] != 0)
                throw new InvalidDataException($"Unreferenced data in the mod tmb at 0x{index:X}");
        }

        for (var index = 0; index < sourceStrings; index++)
        {
            if (!ignored.Contains(index) && source[index] != served[index])
                throw new InvalidDataException($"Byte mismatch with the mod tmb at 0x{index:X}");
        }
    }

    private static List<(string Magic, StringField Field)> StringFieldsOf(byte[] bytes)
    {
        var fields = new List<(string, StringField)>();
        var count = BitConverter.ToInt32(bytes, ItemCountOffset);
        var item = FirstItemOffset;

        for (var index = 0; index < count; index++)
        {
            var magic = System.Text.Encoding.ASCII.GetString(bytes, item, 4);
            var size = BitConverter.ToInt32(bytes, item + SizeFieldOffset);

            if (size < ItemHeaderLength || item + size > bytes.Length)
                throw new InvalidDataException("Tmb item out of range");

            if (TmbFile.StringFieldOffsets.TryGetValue(magic, out var fieldOffset) && fieldOffset + sizeof(int) <= size)
            {
                var relative = BitConverter.ToInt32(bytes, item + fieldOffset);
                var target = relative == 0 ? -1 : item + ItemHeaderLength + relative;

                if (target >= bytes.Length)
                    throw new InvalidDataException($"{magic} string offset out of range");

                fields.Add((magic, new StringField(item + fieldOffset, target)));
            }

            item += size;
        }

        return fields;
    }

    private static string ReadString(byte[] bytes, int start)
    {
        var end = Array.IndexOf(bytes, (byte)0, start);
        return System.Text.Encoding.UTF8.GetString(bytes, start, (end < 0 ? bytes.Length : end) - start);
    }
}
