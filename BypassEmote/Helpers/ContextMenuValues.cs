using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;

namespace BypassEmote.Helpers;

internal static unsafe class ContextMenuValues
{
    internal static void EnableExecute(AtkUnitBase* menu)
    {
        if (menu == null || !menu->IsVisible)
            return;

        var values = menu->AtkValues;
        var item = FindExecute(values, menu->AtkValuesCount);

        if (item < 0)
            return;

        var flag = 8 + (int)values[0].UInt + item;

        values[flag].Type = AtkValueType.Int;
        values[flag].Int = 0;

        var list = FindList(menu);

        if (list == null || item >= list->ListLength)
            return;

        if (list->GetItemDisabledState(item))
            list->SetItemDisabledState(item, false);
    }

    private static int FindExecute(AtkValue* values, uint valueCount)
    {
        if (values == null || valueCount <= 8 || values[0].Type != AtkValueType.UInt)
            return -1;

        var items = (int)values[0].UInt;

        if (items <= 0 || 8 + items + items > valueCount)
            return -1;

        if (ExecuteLabel() is not { Length: > 0 } label)
            return -1;

        for (var item = 0; item < items; item++)
        {
            var name = values[8 + item];

            if (!IsText(name.Type) || name.String.Value == null)
                continue;

            if (name.String.ToString() == label)
                return item;
        }

        return -1;
    }

    private static AtkComponentList* FindList(AtkUnitBase* menu)
    {
        for (var index = 0; index < menu->UldManager.NodeListCount; index++)
        {
            var node = menu->UldManager.NodeList[index];

            if (node == null || (ushort)node->Type != 1009)
                continue;

            var component = ((AtkComponentNode*)node)->Component;

            if (component != null)
                return (AtkComponentList*)component;
        }

        return null;
    }

    private static bool IsText(AtkValueType type)
        => type is AtkValueType.String or AtkValueType.ConstString or AtkValueType.ManagedString;

    private static string? ExecuteLabel()
        => ExcelSheetHelper.TryGetRow<Addon>(1167, out var row) && row.HasValue
        ? row.Value.Text.ExtractText()
        : null;
}
