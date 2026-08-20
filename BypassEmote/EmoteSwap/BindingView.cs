using System;
using System.Text;

namespace BypassEmote.EmoteSwap;

// Field offsets of an animation binding, and the decoding of a raw dump of its head. Offsets read off the game's
// own member table: originalSkeletonName 0x10, animation 0x18, transformTrackToBoneIndices 0x20,
// floatTrackToFloatSlotIndices 0x30, partitionIndices 0x40, blendHint 0x50. An index array is a pointer, a count
// and a capacity, which puts that count at 0x28.
internal static class BindingView
{
    internal const int SkeletonNameOffset = 0x10;
    internal const int TrackIndicesPointerOffset = 0x20;
    internal const int TrackIndicesCountOffset = 0x28;
    internal const int BlendHintOffset = 0x50;

    // How many bytes of the object must be dumped for every field above to be present.
    internal const int HeadSize = 0x58;
    internal const int MissingValue = -1;

    // Pointer to the track-to-bone index array. 0 when the dump is short or the array is empty.
    internal static long TrackIndicesPointer(byte[] head, int read)
        => read >= TrackIndicesPointerOffset + sizeof(long) && head.Length >= TrackIndicesPointerOffset + sizeof(long)
            ? BitConverter.ToInt64(head, TrackIndicesPointerOffset)
            : 0;

    // How many transform tracks the binding drives, or MissingValue.
    internal static int TrackCount(byte[] head, int read)
        => read >= TrackIndicesCountOffset + sizeof(int) && head.Length >= TrackIndicesCountOffset + sizeof(int)
            ? BitConverter.ToInt32(head, TrackIndicesCountOffset)
            : MissingValue;

    internal static int BlendHint(byte[] head, int read)
        => read > BlendHintOffset && head.Length > BlendHintOffset ? head[BlendHintOffset] : MissingValue;

    internal static string FormatIndices(byte[] raw, int read, int headCount, int tailCount)
    {
        var available = Math.Min(read, raw.Length) / sizeof(short);
        if (available <= 0)
            return string.Empty;

        var head = Math.Clamp(headCount, 0, available);
        var tail = Math.Max(tailCount, 0);
        var text = new StringBuilder();

        for (var i = 0; i < head; i++)
            Append(text, raw, i);

        if (available > head + tail)
            Append(text, "...");

        for (var i = Math.Max(head, available - tail); i < available; i++)
            Append(text, raw, i);

        return text.ToString();
    }

    private static void Append(StringBuilder text, byte[] raw, int index)
        => Append(text, BitConverter.ToInt16(raw, index * sizeof(short)).ToString());

    private static void Append(StringBuilder text, string value)
    {
        if (text.Length > 0)
            text.Append(' ');

        text.Append(value);
    }
}
