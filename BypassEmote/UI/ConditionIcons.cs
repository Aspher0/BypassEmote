using Dalamud.Bindings.ImGui;
using NoireLib.Enums;
using NoireLib.Helpers;
using System.Numerics;

namespace BypassEmote.UI;

// The ten condition icons the game's own emote window shows
internal static class ConditionIcons
{
    private const string UldPath = "ui/uld/emote.uld";
    private const uint PartListId = 15;

    private static readonly (EmoteCondition Condition, int Part)[] Order =
    [
        (EmoteCondition.Standing, 4),
        (EmoteCondition.Swimming, 5),
        (EmoteCondition.Diving, 6),
        (EmoteCondition.SittingOnGround, 0),
        (EmoteCondition.SittingInChair, 1),
        (EmoteCondition.Mounted, 2),
        (EmoteCondition.HoldingUmbrella, 7),
        (EmoteCondition.HoldingTorch, 9),
        (EmoteCondition.WearingFashionAccessory, 8),
        (EmoteCondition.Fishing, 3),
    ];

    private static readonly Vector4 Lit = Vector4.One;
    private static readonly Vector4 Unlit = new(1f, 1f, 1f, 0.18f);

    public static void Draw(EmoteCondition conditions, float height)
    {
        var drawn = false;

        foreach (var (condition, partIndex) in Order)
        {
            if (UldHelper.PartTexture(UldPath, PartListId, partIndex) is not { } part)
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
