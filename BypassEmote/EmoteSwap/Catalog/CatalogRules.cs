using BypassEmote.Enums;
using NoireLib.Animations.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public static class CatalogRules
{
    private static readonly HashSet<string> VfxMagics = new(StringComparer.Ordinal) { "C012", "C173" };

    public static SoundClass ClassifySound(IEnumerable<TmbEntryInfo> entries, Func<string, bool>? vfxPlaysSound = null)
    {
        var hasSfx = false;

        foreach (var entry in entries)
        {
            if (entry.Magic == "C053")
                return SoundClass.Voiceline;

            if (hasSfx || entry.Path is not { Length: > 0 } path)
                continue;

            if (entry.Magic == "C063" && path.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                hasSfx = true;
            else if (vfxPlaysSound != null && VfxMagics.Contains(entry.Magic)
                && path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase) && vfxPlaysSound(path))
            {
                hasSfx = true;
            }
        }

        return hasSfx ? SoundClass.Sfx : SoundClass.Silent;
    }

    public static TurnClass ClassifyTurn(int slot0SlotValue) => slot0SlotValue switch
    {
        0 => TurnClass.Body,
        2 => TurnClass.Head,
        3 => TurnClass.Eyes,
        1 => TurnClass.None,
        _ => TurnClass.Unknown,
    };

    // /waterfloat carries its Pause flag in slot 4, not slot 0.
    public static EmotePlayType ClassifyLoop(IEnumerable<bool> populatedSlotPauseFlags) =>
        populatedSlotPauseFlags.Any(flag => flag) ? EmotePlayType.Looped : EmotePlayType.OneShot;

    public static PostureFlags PostureForSlot(int slotIndex) => ActionTimelineSlots.PostureForSlot(slotIndex);
}
