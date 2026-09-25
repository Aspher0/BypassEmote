using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI.Classic;

internal sealed class ClassicHotbarPrompt
{
    private static string DragHintText => L.ClassicDragHint.Text;

    private static string hotbarItems = string.Empty;
    private static int hotbarItemsRevision = -1;

    private const float SlotSize = 36f;
    private const float GridCellPadding = 2f;
    private const float GridSpacing = 4f;

    private static readonly Vector4 HintColor = ColorHelper.HexToVector4("#B3B3B3");
    private static readonly Vector4 AssignedColor = ColorHelper.HexToVector4("#ff0000");
    private static readonly Vector4 SelectedColor = ColorHelper.HexToVector4("#FFD700");
    private static readonly string[] SlotButtonIds = BuildSlotButtonIds(16);

    private int hotbarSlot;

    private string statusText = string.Empty;
    private (int Hotbar, int Slot, int Type, uint Command, int Revision) statusKey = (-1, -1, -1, 0, -1);
    private readonly string[] tipTexts = new string[16];
    private readonly (int Hotbar, int Type, uint Command, int Revision)[] tipKeys = new (int, int, uint, int)[16];

    private static string[] BuildSlotButtonIds(int count)
    {
        var ids = new string[count];

        for (var i = 0; i < count; i++)
            ids[i] = "##assign_slot_" + i;

        return ids;
    }

    public async Task ShowAsync(Emote emoteToAssign)
    {
        var options = new ModalOptions
        {
            ConfirmLabel = L.Assign.Text,
            Width = DialogWidth(),
        };

        var content = new NoireContent().AddCustom(() => DrawBody(options));

        if (!await NoireModal.ConfirmAsync(L.ClassicAssignTitle.With("emote", CommonHelper.GetEmoteName(emoteToAssign)), content, options))
            return;

        await AsyncHelper.RunOnFrameworkThreadAsync(() =>
            AddonHelper.SetHotbarSlot(Math.Clamp(Configuration.AssignModalHotbar, 0, AddonHelper.HotbarCount - 1), hotbarSlot,
                RaptureHotbarModule.HotbarSlotType.Emote, emoteToAssign.RowId));
    }

    private static readonly IdLabel HotbarLabel = new(L.HotbarLabel, "###BypassEmoteAssignHotbar");

    private static string HotbarItems()
    {
        if (hotbarItemsRevision == NoireLanguages.Revision)
            return hotbarItems;

        var items = new System.Text.StringBuilder();

        for (var i = 1; i <= 10; i++)
            items.Append(i).Append('\0');

        for (var i = 1; i <= 8; i++)
        {
            items.Append(L.CrossHotbarShort.With("number", i.ToString()));

            if (i < 8)
                items.Append('\0');
        }

        hotbarItems = items.ToString();
        hotbarItemsRevision = NoireLanguages.Revision;
        return hotbarItems;
    }

    private static int GridColumns()
        => Math.Clamp(Configuration.AssignModalHotbar, 0, AddonHelper.HotbarCount - 1) < AddonHelper.StandardHotbarCount ? 12 : 8;

    private static float DialogWidth()
    {
        var columns = GridColumns();
        var gridWidth = columns * (SlotSize + GridCellPadding * 2f) + (columns - 1) * GridSpacing;

        return (gridWidth + ImGui.GetStyle().WindowPadding.X * 2f) / NoireUI.Scale;
    }

    private void DrawBody(ModalOptions options)
    {
        options.Width = DialogWidth();

        var hotbar = Math.Clamp(Configuration.AssignModalHotbar, 0, AddonHelper.HotbarCount - 1);
        if (ImGui.Combo(HotbarLabel.Text, ref hotbar, HotbarItems()))
            Configuration.AssignModalHotbar = hotbar;

        var slotCount = AddonHelper.HotbarSlotCount(hotbar);
        if (hotbarSlot >= slotCount)
            hotbarSlot = slotCount - 1;

        ImGui.Separator();

        DrawSlotGrid(hotbar, slotCount);

        ImGui.Separator();

        DrawSlotStatus(hotbar);

        ImGui.TextColoredWrapped(HintColor, DragHintText);
    }

    private unsafe void DrawSlotStatus(int hotbar)
    {
        var slot = AddonHelper.GetHotbarSlot(hotbar, hotbarSlot);

        if (slot == null || slot->IsEmpty)
        {
            ImGui.TextUnformatted(L.ClassicSlotEmpty.Text);
            return;
        }

        var key = (hotbar, hotbarSlot, (int)slot->CommandType, slot->CommandId, NoireLanguages.Revision);

        if (key != statusKey)
        {
            statusKey = key;
            statusText = L.CurrentlyAssigned.With("name", slot->PopUpHelp.ToString());
        }

        ImGui.TextColoredWrapped(AssignedColor, statusText);
    }

    private unsafe void DrawSlotGrid(int hotbar, int slotCount)
    {
        var slotSize = SlotSize;
        var columns = slotCount == 12 ? 12 : 8;
        var emptyBg = new Vector4(0.22f, 0.22f, 0.22f, 1f);
        var emptyBgHovered = new Vector4(0.32f, 0.32f, 0.32f, 1f);

        using var framePadding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(GridCellPadding, GridCellPadding));
        using var itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(GridSpacing, GridSpacing));

        for (var i = 0; i < slotCount; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            var slot = AddonHelper.GetHotbarSlot(hotbar, i);
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
                        if (IconHelper.Get(iconId) is { } texture && texture.TryGetWrap(out var wrap, out _))
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
                    }
                }
            }

            if (!drewIcon)
            {
                using var buttonBg = ImRaii.PushColor(ImGuiCol.Button, emptyBg)
                    .Push(ImGuiCol.ButtonHovered, emptyBgHovered)
                    .Push(ImGuiCol.ButtonActive, emptyBgHovered);
                if (ImGui.Button(SlotButtonIds[i], new Vector2(slotSize + GridCellPadding * 2f, slotSize + GridCellPadding * 2f)))
                    hotbarSlot = i;
            }

            if (hotbarSlot == i)
                ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                    ImGui.ColorConvertFloat4ToU32(SelectedColor), 3f, ImDrawFlags.None, 2f);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                var key = (hotbar, isEmpty ? -1 : (int)slot->CommandType, isEmpty ? 0u : slot->CommandId, NoireLanguages.Revision);

                if (tipTexts[i] == null || key != tipKeys[i])
                {
                    tipKeys[i] = key;
                    tipTexts[i] = isEmpty ? L.SlotEmpty.With("slot", (i + 1).ToString()) : L.SlotHolds.With("slot", (i + 1).ToString(), "name", slot->PopUpHelp.ToString());
                }

                ImGui.SetTooltip(tipTexts[i]);
            }
        }
    }
}
