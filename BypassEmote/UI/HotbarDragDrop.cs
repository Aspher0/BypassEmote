using BypassEmote.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI;

public static class HotbarDragDrop
{
    private const float OutlineRounding = 4f;

    private static Emote? draggedEmote;
    private static bool swallowUntilRelease;

    public static bool IsDragging => draggedEmote.HasValue || swallowUntilRelease;

    public static void BeginDrag(Emote emote)
    {
        if (IsDragging || !CommonHelper.IsEmoteAssignableToHotbar(emote))
            return;

        draggedEmote = emote;
    }

    private static void CancelDrag()
    {
        draggedEmote = null;
        swallowUntilRelease = true;
    }

    public static void Draw()
    {
        if (draggedEmote is not { } emote)
        {
            if (swallowUntilRelease && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                swallowUntilRelease = false;
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            CancelDrag();
            return;
        }

        var released = ImGui.IsMouseReleased(ImGuiMouseButton.Left);

        // The release can be lost entirely (alt-tab mid-drag)
        if (!released && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            CancelDrag();
            return;
        }

        var mousePos = ImGui.GetMousePos();

        HotbarSlotBounds hovered = default;
        var hovering = !ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) && AddonHelper.TryGetHotbarSlotAt(mousePos, out hovered);

        if (released)
        {
            if (hovering)
                AddonHelper.SetHotbarSlot(hovered.HotbarId, hovered.SlotIndex, RaptureHotbarModule.HotbarSlotType.Emote, emote.RowId);

            draggedEmote = null;
            return;
        }

        if (hovering)
            DrawSlotOutline(hovered);

        DrawDragGhost(emote, mousePos);
    }

    private static void DrawSlotOutline(HotbarSlotBounds slot)
    {
        var drawList = ImGui.GetForegroundDrawList();

        drawList.AddRectFilled(slot.Min, slot.Max, 0x3300D7FFu, OutlineRounding);
        drawList.AddRect(slot.Min, slot.Max, 0xFF00D7FFu, OutlineRounding, ImDrawFlags.None, 2f);
    }

    private static void DrawDragGhost(Emote emote, Vector2 mousePos)
    {
        try
        {
            if (IconHelper.Get(CommonHelper.GetEmoteIcon(emote)) is { } texture && texture.TryGetWrap(out var wrap, out _))
            {
                var half = new Vector2(40f * 0.5f * ImGuiHelpers.GlobalScale);
                ImGui.GetForegroundDrawList().AddImage(wrap.Handle, mousePos - half, mousePos + half, Vector2.Zero, Vector2.One, 0xD9FFFFFFu);
                return;
            }
        }
        catch
        {
            // no-op
        }

        ImGui.SetTooltip(CommonHelper.GetEmoteName(emote));
    }
}
