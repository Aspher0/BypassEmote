using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NoireLib.Helpers;
using System;

namespace BypassEmote.Helpers;

internal static unsafe class EmoteListDimmer
{
    internal static void Paint(AtkUnitBase* addon, bool lit)
    {
        var agent = AgentEmote.Instance();

        if (agent == null)
            return;

        switch (*((byte*)agent + 0x31))
        {
            case 0:
                PaintSlotLists(addon, lit);
                break;

            case 4:
                if (AddonHelper.TryGetComponent(addon, 47u, ComponentType.TreeList, out var searchList))
                    PaintNamedRows((AtkComponentList*)searchList, lit);
                break;

            default:
                if (AddonHelper.TryGetComponentList(addon, 4u, out var categoryList))
                    PaintNamedRows(categoryList, lit);
                break;
        }
    }

    private static void PaintNamedRows(AtkComponentList* list, bool lit)
    {
        if (list->ListLength <= 0)
            return;

        var multiply = lit ? (byte)100 : (byte)50;

        for (var row = 0; row < list->ListLength; row++)
        {
            var renderer = list->GetItemRenderer(row);

            if (renderer == null || renderer->AtkComponentButton.AtkComponentBase.OwnerNode == null)
                continue;

            var locked = ShowsLockedEmote(&renderer->AtkComponentButton.AtkComponentBase);

            Paint(&renderer->AtkComponentButton.AtkComponentBase.OwnerNode->AtkResNode, locked ? multiply : (byte)100);
        }
    }

    private static void PaintSlotLists(AtkUnitBase* addon, bool lit)
    {
        var module = EmoteHistoryModule.Instance();

        if (module == null)
            return;

        PaintSlotList(addon, 20u, module->History, lit);
        PaintSlotList(addon, 25u, module->Favorites, lit);
    }

    private static void PaintSlotList(AtkUnitBase* addon, uint nodeId, Span<ushort> slots, bool lit)
    {
        if (!AddonHelper.TryGetComponentList(addon, nodeId, out var list))
            return;

        var multiply = lit ? (byte)100 : (byte)50;
        var rows = Math.Min(list->ListLength, slots.Length);

        for (var row = 0; row < rows; row++)
        {
            var renderer = list->GetItemRenderer(row);

            if (renderer == null || renderer->AtkComponentButton.AtkComponentBase.OwnerNode == null)
                continue;

            var emoteId = (uint)(slots[row] & 0xFFF);
            var locked = emoteId != 0 && Service.IsEmoteLocked(emoteId);

            Paint(&renderer->AtkComponentButton.AtkComponentBase.OwnerNode->AtkResNode, locked ? multiply : (byte)100);
        }
    }

    private static bool ShowsLockedEmote(AtkComponentBase* renderer)
    {
        foreach (var text in AddonHelper.ReadComponentTexts(renderer))
        {
            if (Service.IsLockedEmoteName(text))
                return true;
        }

        return false;
    }

    private static void Paint(AtkResNode* node, byte multiply)
    {
        if (node->MultiplyRed == multiply && node->MultiplyGreen == multiply && node->MultiplyBlue == multiply)
            return;

        node->MultiplyRed = multiply;
        node->MultiplyGreen = multiply;
        node->MultiplyBlue = multiply;
    }
}
