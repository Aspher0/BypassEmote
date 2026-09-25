using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Classic;

internal sealed class EmoteQuickAdd
{
    private const float IconSize = 20f;

    private readonly NoireExcelPicker<Emote> _picker;
    private readonly Dictionary<uint, UiImageSource> _icons = new();
    private readonly NoireString _placeholder;

    internal EmoteQuickAdd(string id, NoireString placeholder)
    {
        _placeholder = placeholder;

        _picker = new NoireExcelPicker<Emote>(id, CommonHelper.GetEmoteName)
        {
            Icon = CommonHelper.GetEmoteIcon,
            IconSize = IconSize,
            Include = emote => CommonHelper.GetEmotePlayType(emote) != EmotePlayType.DoNotPlay,
            FilterHint = L.SearchEmotes.Text,
            PreviewPlaceholder = placeholder.Text,
        };

        _picker.Combo.VisibleItemCount = 12;
        _picker.Combo.ItemRenderer = DrawRow;
    }

    internal Func<uint, bool>? Marked { get; set; }

    internal Vector4 MarkedColor { get; set; } = new(1f, 0.9f, 0f, 1f);

    internal NoireString? MarkedNote { get; set; }

    internal uint? Draw(float width)
    {
        _picker.Combo.Width = width;
        _picker.FilterHint = L.SearchEmotes.Text;
        _picker.PreviewPlaceholder = _placeholder.Text;

        if (!_picker.Draw() || _picker.SelectedRowId is not { } rowId)
            return null;

        _picker.ClearSelection();
        return rowId;
    }

    private void DrawRow(UiComboItemDraw<ExcelPickerEntry<Emote>> option)
    {
        var size = NoireUI.Scaled(IconSize);
        var marked = Marked?.Invoke(option.Item.RowId) == true;

        if (option.Item.IconId != 0 && IconFor(option.Item.IconId).GetWrap() is { } wrap)
            ImGui.Image(wrap.Handle, new Vector2(size, size));
        else
            ImGui.Dummy(new Vector2(size, size));

        ImGui.SameLine(0f, NoireUI.Scaled(6f));

        var offset = (size - NoireText.LineHeight()) * 0.5f;

        if (offset > 0f)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offset);

        using (ImRaii.PushColor(ImGuiCol.Text, MarkedColor, marked))
            option.DrawLabel();

        if (!marked || MarkedNote is not { } note || note.Text.Length == 0)
            return;

        ImGui.SameLine(0f, NoireUI.Scaled(6f));
        ImGui.TextColored(MarkedColor, note.Text);
    }

    private UiImageSource IconFor(uint iconId)
    {
        if (_icons.TryGetValue(iconId, out var cached))
            return cached;

        var source = UiImageSource.FromGameIcon(iconId);
        _icons[iconId] = source;
        return source;
    }
}
