using NoireLib;
using System;

namespace BypassEmote.EmoteSwap;

// Five sibling walks over the same pack set share a byte-identical prologue. Only the binding scan reads the
// first pack straight into the call; the other four test it for null first. That difference is an instruction,
// not a displacement, so it survives a rebuild where a byte signature reaching past the alignment padding
// cannot.
internal static unsafe class BindingScanLocator
{
    private const string LogPrefix = "[BindingScanLocator] ";

    private const string SharedPrologue =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 54 41 56 41 57 48 83 EC 20 "
        + "4C 8B F2 4C 8D 61 08 45 33 FF";

    private const string TakesFirstPackUnchecked = "48 8B 0B 49 8B D6";

    internal const int BodyWindow = 0x80;

    internal static readonly byte[] Prologue = Bytes(SharedPrologue);

    internal static readonly byte[] Marker = Bytes(TakesFirstPackUnchecked);

    public static nint Resolve()
    {
        try
        {
            var scanner = NoireService.SigScanner;
            var section = scanner.TextSectionBase;
            var text = new ReadOnlySpan<byte>((void*)section, scanner.TextSectionSize);
            var offset = Locate(text, out var siblings, out var unchecked_);

            if (offset < 0)
            {
                NoireLogger.LogWarning($"The binding scan did not locate: {siblings} sibling walk(s) carry the "
                    + $"shared prologue and {unchecked_} of them read the first pack unchecked, where exactly one "
                    + "is expected. Falling back to the byte signature.", LogPrefix);

                return 0;
            }

            NoireLogger.LogDebug($"Located the binding scan {offset:X} bytes into the text section by its "
                + "unchecked first-pack read.", LogPrefix);

            return section + offset;
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Could not walk the text section ({ex.Message}); falling back to the byte "
                + "signature.", LogPrefix);

            return 0;
        }
    }

    internal static int Locate(ReadOnlySpan<byte> text)
        => Locate(text, out _, out _);

    internal static int Locate(ReadOnlySpan<byte> text, out int siblings, out int unchecked_)
    {
        var found = -1;
        var matches = 0;
        var seen = 0;
        var at = 0;

        while (at < text.Length)
        {
            var hit = text[at..].IndexOf(Prologue);

            if (hit < 0)
                break;

            var start = at + hit;
            seen++;

            if (text.Slice(start, Math.Min(BodyWindow, text.Length - start)).IndexOf(Marker) >= 0)
            {
                found = start;
                matches++;
            }

            at = start + 1;
        }

        siblings = seen;
        unchecked_ = matches;

        return matches == 1 ? found : -1;
    }

    private static byte[] Bytes(string hex)
    {
        var parts = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[parts.Length];

        for (var i = 0; i < parts.Length; i++)
            bytes[i] = Convert.ToByte(parts[i], 16);

        return bytes;
    }
}
