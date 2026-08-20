using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.Helpers;

public readonly record struct HotbarSlotCandidate(int BarId, int SlotIndex, Vector4 Rect);

public static class HotbarSlotPicker
{
    public static HotbarSlotCandidate? Pick(Vector2 point, IReadOnlyList<HotbarSlotCandidate> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var rect = candidates[i].Rect;
            if (point.X >= rect.X && point.X < rect.Z && point.Y >= rect.Y && point.Y < rect.W)
                return candidates[i];
        }

        return null;
    }
}
