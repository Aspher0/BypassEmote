using Dalamud.Bindings.ImGui;
using NoireLib.Enums;
using NoireLib.Helpers;
using System.Numerics;

namespace BypassEmote.UI;

// The ten condition icons the game's own emote window shows
internal static class ConditionIcons
{
    private static readonly Vector4 Lit = Vector4.One;
    private static readonly Vector4 Unlit = new(1f, 1f, 1f, 0.18f);

    public static void Draw(EmoteCondition conditions, float height)
    {
        var drawn = false;

        foreach (var condition in EmoteHelper.ConditionIconOrder)
        {
            if (EmoteHelper.GetConditionIcon(condition) is not { } part)
                continue;

            if (drawn)
                ImGui.SameLine(0, 2f);

            var size = new Vector2(height * part.Size.X / part.Size.Y, height);

            ImGui.Image(part.Texture.Handle, size, part.Uv0, part.Uv1,
                (conditions & condition) == condition ? Lit : Unlit);

            drawn = true;
        }
    }
}
