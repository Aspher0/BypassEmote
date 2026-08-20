using BypassEmote.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI;

/// <summary> Drag-and-drop from the emote window onto the game's mouse hotbars. </summary>
public static class HotbarDragDrop
{
    // Hotbars 1-10. cross hotbars not supported
    private static readonly string[] BarAddonNames =
    [
        "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04",
        "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09",
    ];

    private const float GhostSize = 40f;
    private const float OutlineRounding = 4f;
    private const float OutlineThickness = 2f;
    private const uint OutlineColor = 0xFF00D7FF;
    private const uint OutlineFillColor = 0x3300D7FF;
    private const uint GhostTint = 0xD9FFFFFF;

    private static Emote? draggedEmote;
    private static bool swallowUntilRelease;
    private static readonly List<HotbarSlotCandidate> candidates = new(120);

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

        // A hotbar behind one of the plugin's windows must not light up nor catch the drop
        HotbarSlotCandidate? hovered = null;
        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow))
        {
            CollectSlotCandidates(candidates);
            hovered = HotbarSlotPicker.Pick(mousePos, candidates);
        }

        if (released)
        {
            if (hovered is { } target)
                CommonHelper.AssignEmoteToHotbarSlot(target.BarId, target.SlotIndex, emote.RowId);

            draggedEmote = null;
            return;
        }

        if (hovered is { } slot)
            DrawSlotOutline(slot.Rect);

        DrawDragGhost(emote, mousePos);
    }

    /// <summary> Screen rects of every droppable slot on the visible mouse hotbars. </summary>
    private static unsafe void CollectSlotCandidates(List<HotbarSlotCandidate> into)
    {
        into.Clear();

        if (!AddonHelper.IsNativeUiVisible())
            return;

        foreach (var addonName in BarAddonNames)
        {
            try
            {
                if (!AddonHelper.TryGetReadyAddon<AddonActionBarBase>(addonName, out var bar) || bar == null)
                    continue;

                var unit = (AtkUnitBase*)bar;
                var addonScale = unit->Scale;

                int barId = bar->RaptureHotbarId;
                if (barId > 9)
                    continue;

                var slotCount = Math.Min((int)bar->SlotCount, bar->ActionBarSlotVector.Count);
                for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    ref var slot = ref bar->ActionBarSlotVector[slotIndex];

                    var node = slot.ComponentDragDrop != null && slot.ComponentDragDrop->AtkComponentBase.OwnerNode != null
                        ? &slot.ComponentDragDrop->AtkComponentBase.OwnerNode->AtkResNode
                        : slot.Icon != null ? &slot.Icon->AtkResNode : null;
                    if (node == null)
                        continue;

                    var width = node->Width * node->ScaleX * addonScale;
                    var height = node->Height * node->ScaleY * addonScale;
                    if (width <= 1 || height <= 1)
                        continue;

                    into.Add(new HotbarSlotCandidate(barId, slotIndex, new Vector4(
                        node->ScreenX, node->ScreenY, node->ScreenX + width, node->ScreenY + height)));
                }
            }
            catch
            {
                // A faulting read skips this bar
            }
        }
    }

    private static void DrawSlotOutline(Vector4 rect)
    {
        var drawList = ImGui.GetForegroundDrawList();
        var min = new Vector2(rect.X, rect.Y);
        var max = new Vector2(rect.Z, rect.W);

        drawList.AddRectFilled(min, max, OutlineFillColor, OutlineRounding);
        drawList.AddRect(min, max, OutlineColor, OutlineRounding, ImDrawFlags.None, OutlineThickness);
    }

    private static void DrawDragGhost(Emote emote, Vector2 mousePos)
    {
        try
        {
            var texture = NoireService.TextureProvider.GetFromGameIcon(CommonHelper.GetEmoteIcon(emote));
            if (texture.TryGetWrap(out var wrap, out _))
            {
                var half = new Vector2(GhostSize * 0.5f * ImGuiHelpers.GlobalScale);
                ImGui.GetForegroundDrawList().AddImage(wrap.Handle, mousePos - half, mousePos + half, Vector2.Zero, Vector2.One, GhostTint);
                return;
            }
        }
        catch
        {
            // Icon lookup failed
        }

        ImGui.SetTooltip(CommonHelper.GetEmoteName(emote));
    }
}
