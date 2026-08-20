using BypassEmote.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI;

/// <summary> The "Assign emote to hotbar" picker. </summary>
public class AssignHotbarWindow : IDisposable
{
    private const string DragHintText = "You can also drag and drop an emote from the main window onto any visible hotbar slot.";

    private const float SlotSize = 36f;
    private const float GridCellPadding = 2f;
    private const float GridSpacing = 4f;

    private int hotbarSlot;

    public async Task ShowAsync(Emote emoteToAssign)
    {
        var options = new ModalOptions
        {
            ConfirmLabel = "Assign",
            Width = DialogWidth(),
        };

        var content = new NoireContent().AddCustom(() => DrawBody(options));

        if (!await NoireModal.ConfirmAsync($"Assign {CommonHelper.GetEmoteName(emoteToAssign)} to hotbar...", content, options))
            return;

        await AsyncHelper.RunOnFrameworkThreadAsync(() =>
            CommonHelper.AssignEmoteToHotbarSlot(Math.Clamp(Configuration.AssignModalHotbar, 0, 17), hotbarSlot, emoteToAssign.RowId));
    }

    private static int GridColumns()
        => Math.Clamp(Configuration.AssignModalHotbar, 0, 17) < 10 ? 12 : 8;

    private static float DialogWidth()
    {
        var columns = GridColumns();
        var gridWidth = columns * (SlotSize + GridCellPadding * 2f) + (columns - 1) * GridSpacing;

        return (gridWidth + ImGui.GetStyle().WindowPadding.X * 2f) / NoireUI.Scale;
    }

    private void DrawBody(ModalOptions options)
    {
        options.Width = DialogWidth();

        var hotbar = Math.Clamp(Configuration.AssignModalHotbar, 0, 17);
        if (ImGui.Combo("Hotbar", ref hotbar, "1\02\03\04\05\06\07\08\09\010\0XHB 1\0XHB 2\0XHB 3\0XHB 4\0XHB 5\0XHB 6\0XHB 7\0XHB 8"))
            Configuration.AssignModalHotbar = hotbar;

        var slotCount = hotbar < 10 ? 12 : 16;
        if (hotbarSlot >= slotCount)
            hotbarSlot = slotCount - 1;

        ImGui.Separator();

        DrawSlotGrid(hotbar, slotCount);

        ImGui.Separator();

        DrawSlotStatus(hotbar);

        ImGui.TextColoredWrapped(ColorHelper.HexToVector4("#B3B3B3"), DragHintText);
    }

    private unsafe void DrawSlotStatus(int hotbar)
    {
        var slot = CommonHelper.GetHotbarSlot(hotbar, hotbarSlot);
        if (slot != null && !slot->IsEmpty)
            ImGui.TextColoredWrapped(ColorHelper.HexToVector4("#ff0000"), $"Currently assigned: {slot->PopUpHelp}");
        else
            ImGui.Text("This slot is empty. You can safely assign an emote.");
    }

    private unsafe void DrawSlotGrid(int hotbar, int slotCount)
    {
        var slotSize = SlotSize;
        var columns = slotCount == 12 ? 12 : 8;
        var selectedColor = ColorHelper.HexToVector4("#FFD700");
        var emptyBg = new Vector4(0.22f, 0.22f, 0.22f, 1f);
        var emptyBgHovered = new Vector4(0.32f, 0.32f, 0.32f, 1f);

        using var framePadding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(GridCellPadding, GridCellPadding));
        using var itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(GridSpacing, GridSpacing));

        for (var i = 0; i < slotCount; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            var slot = CommonHelper.GetHotbarSlot(hotbar, i);
            var isEmpty = slot == null || slot->IsEmpty;

            var drewIcon = false;
            if (!isEmpty)
            {
                var iconId = slot->IconId;
                if (iconId == 0)
                    iconId = (uint)Math.Max(0, slot->GetIconIdForSlot(slot->CommandType, slot->CommandId));

                if (iconId != 0)
                {
                    try
                    {
                        var texture = NoireService.TextureProvider.GetFromGameIcon(iconId);
                        if (texture.TryGetWrap(out var wrap, out _))
                        {
                            using var id = ImRaii.PushId(i);
                            using var buttonBg = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
                            if (ImGui.ImageButton(wrap.Handle, new Vector2(slotSize, slotSize)))
                                hotbarSlot = i;
                            drewIcon = true;
                        }
                    }
                    catch
                    {
                        // Icon lookup failed
                    }
                }
            }

            if (!drewIcon)
            {
                using var buttonBg = ImRaii.PushColor(ImGuiCol.Button, emptyBg)
                    .Push(ImGuiCol.ButtonHovered, emptyBgHovered)
                    .Push(ImGuiCol.ButtonActive, emptyBgHovered);
                if (ImGui.Button($"##assign_slot_{i}", new Vector2(slotSize + GridCellPadding * 2f, slotSize + GridCellPadding * 2f)))
                    hotbarSlot = i;
            }

            if (hotbarSlot == i)
                ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                    ImGui.ColorConvertFloat4ToU32(selectedColor), 3f, ImDrawFlags.None, 2f);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip(isEmpty ? $"Slot {i + 1} - Empty" : $"Slot {i + 1} - {slot->PopUpHelp}");
            }
        }
    }

    public void Dispose() { }
}
