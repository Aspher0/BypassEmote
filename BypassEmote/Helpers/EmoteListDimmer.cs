using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace BypassEmote.Helpers;

internal static unsafe class EmoteListDimmer
{
    internal static void Paint(AtkUnitBase* addon, bool lit)
    {
        if (addon == null || !addon->IsVisible)
            return;

        var agent = AgentEmote.Instance();

        if (agent == null)
            return;

        if (*((byte*)agent + 0x31) == 0)
            PaintSlotLists(addon, lit);
        else
            PaintCategoryList(addon, lit);
    }

    private static void PaintCategoryList(AtkUnitBase* addon, bool lit)
    {
        var list = FindList(addon, 4);

        if (list == null || list->ListLength <= 0)
            return;

        var multiply = lit ? (byte)100 : (byte)50;

        for (var row = 0; row < list->ListLength; row++)
        {
            var renderer = list->GetItemRenderer(row);

            if (renderer == null)
                continue;

            var owner = renderer->AtkComponentButton.AtkComponentBase.OwnerNode;

            if (owner == null)
                continue;

            var locked = ShowsLockedEmote(&renderer->AtkComponentButton.AtkComponentBase, 0);

            Paint(&owner->AtkResNode, locked ? multiply : (byte)100);
        }
    }

    private static void PaintSlotLists(AtkUnitBase* addon, bool lit)
    {
        var module = EmoteHistoryModule.Instance();

        if (module == null)
            return;

        PaintSlotList(addon, 20, module->History, lit);
        PaintSlotList(addon, 25, module->Favorites, lit);
    }

    private static void PaintSlotList(AtkUnitBase* addon, uint nodeId, Span<ushort> slots, bool lit)
    {
        if (!lit)
            return;

        var list = FindList(addon, nodeId);

        if (list == null)
            return;

        var rows = Math.Min(list->ListLength, slots.Length);

        for (var row = 0; row < rows; row++)
        {
            var renderer = list->GetItemRenderer(row);

            if (renderer == null)
                continue;

            var owner = renderer->AtkComponentButton.AtkComponentBase.OwnerNode;

            if (owner == null)
                continue;

            var emoteId = (uint)(slots[row] & 0xFFF);

            if (emoteId != 0 && Service.IsEmoteLocked(emoteId))
                Paint(&owner->AtkResNode, 100);
        }
    }

    private static bool ShowsLockedEmote(AtkComponentBase* component, int depth)
    {
        if (component == null || depth > 3)
            return false;

        for (var index = 0; index < component->UldManager.NodeListCount; index++)
        {
            var node = component->UldManager.NodeList[index];

            if (node == null)
                continue;

            if (node->Type == NodeType.Text)
            {
                if (Service.IsLockedEmoteName(((AtkTextNode*)node)->NodeText.ToString()))
                    return true;

                continue;
            }

            if ((ushort)node->Type >= 1000 && ShowsLockedEmote(((AtkComponentNode*)node)->Component, depth + 1))
                return true;
        }

        return false;
    }

    private static AtkComponentList* FindList(AtkUnitBase* addon, uint nodeId)
    {
        for (var index = 0; index < addon->UldManager.NodeListCount; index++)
        {
            var node = addon->UldManager.NodeList[index];

            if (node == null || node->NodeId != nodeId || (ushort)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;

            return component == null ? null : (AtkComponentList*)component;
        }

        return null;
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
