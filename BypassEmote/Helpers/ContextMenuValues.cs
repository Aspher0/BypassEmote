using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NoireLib.Helpers;
using AtkEventInterface = FFXIVClientStructs.FFXIV.Component.GUI.AtkModuleInterface.AtkEventInterface;

namespace BypassEmote.Helpers;

internal static unsafe class ContextMenuValues
{
    internal static void EnableExecute(AtkValue* values, uint valueCount)
    {
        if (!TryFindExecute(out var item))
            return;

        ClearFlag(values, valueCount, item);

        AgentContext.Instance()->CurrentContextMenu->ContextItemDisabledMask &= ~(1u << item);
    }

    internal static void EnableExecute(AtkUnitBase* menu)
    {
        if (!TryFindExecute(out var item))
            return;

        ClearFlag(menu->AtkValues, menu->AtkValuesCount, item);

        if (!AddonHelper.TryFindComponentList(menu, out var list) || item >= list->ListLength)
            return;

        if (list->GetItemDisabledState(item))
            list->SetItemDisabledState(item, false);
    }

    internal static bool TryFindExecute(out int item)
    {
        item = -1;

        var agent = AgentEmote.Instance();
        var context = AgentContext.Instance();

        if (agent == null || context == null || context->CurrentContextMenu == null)
            return false;

        var menu = context->CurrentContextMenu;

        for (var entry = 0; entry < 26; entry++)
        {
            if (menu->EventHandlers[8 + entry].Value != (AtkEventInterface*)agent || menu->EventHandlerParams[8 + entry] != 0x10001)
                continue;

            item = entry;
            return true;
        }

        return false;
    }

    private static void ClearFlag(AtkValue* values, uint valueCount, int item)
    {
        if (values == null || valueCount <= 8 || values[0].Type != AtkValueType.UInt)
            return;

        var items = (int)values[0].UInt;
        var flag = 8 + items + item;

        if (item < items && flag < valueCount)
            values[flag].SetInt(0);
    }
}
