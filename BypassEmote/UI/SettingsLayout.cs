using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI;

internal static class SettingsLayout
{
    private const float ColumnGap = 14f;
    private const float TooltipEms = 32f;
    private const float HeadingGap = 6f;

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings;

    internal static ImRaii.TableDisposable Rows(string id, float nameWidth, float controlWidth)
    {
        var table = ImRaii.Table(id, 3, TableFlags);

        if (table)
        {
            ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthFixed, nameWidth);
            ImGui.TableSetupColumn("##control", ImGuiTableColumnFlags.WidthFixed, controlWidth);
            ImGui.TableSetupColumn("##help", ImGuiTableColumnFlags.WidthFixed);
        }

        return table;
    }

    internal static void Name(string name, string? alarm = null)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        ImGui.AlignTextToFramePadding();

        if (string.IsNullOrEmpty(alarm))
        {
            ImGui.TextUnformatted(name);
        }
        else
        {
            ImGui.TextColored(NoireTheme.Current.Resolve(ThemeColor.Danger), name);

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * TooltipEms);
                ImGui.TextUnformatted(alarm);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
    }

    internal static bool Check(string name, ref bool value)
    {
        Name(name);
        return ImGui.Checkbox("##" + name, ref value);
    }

    internal static void Help(string help)
    {
        ImGui.TableNextColumn();
        Marker(help);
    }

    internal static void Marker(string help)
    {
        ImGui.AlignTextToFramePadding();

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());

        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * TooltipEms);
        ImGui.TextUnformatted(help);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    internal static void Heading(string text)
    {
        var color = NoireTheme.Current.Resolve(ThemeColor.TextMuted);

        ImGui.Dummy(new Vector2(1f, NoireUI.Scaled(HeadingGap)));

        using (ImRaii.PushColor(ImGuiCol.Text, color))
            NoireText.Tracked(text, NoireText.CapsTracking, TextSize.Caption);

        var after = ImGui.GetItemRectMax();
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        var middle = MathF.Floor(ImGui.GetItemRectMin().Y + (ImGui.GetItemRectSize().Y * 0.5f));
        var start = after.X + NoireUI.Scaled(ColumnGap);

        if (right > start)
            NoireShapes.Rect(new Vector2(start, middle), new Vector2(right, middle + 1f), ColorHelper.ScaleAlpha(color, 0.3f));

        ImGui.Dummy(new Vector2(1f, NoireUI.Scaled(HeadingGap)));
    }

    internal static float NameColumn(params string[] names) => Widest(names) + NoireUI.Scaled(ColumnGap);

    internal static float ControlColumn(params string[][] optionLists)
    {
        var widest = 0f;

        foreach (var list in optionLists)
            widest = MathF.Max(widest, Widest(list));

        var style = ImGui.GetStyle();

        return widest + (style.FramePadding.X * 2f) + ImGui.GetFrameHeight() + style.ItemInnerSpacing.X;
    }

    internal static float CheckboxColumn() => ImGui.GetFrameHeight();

    private static float Widest(string[] texts)
    {
        var widest = 0f;

        foreach (var text in texts)
            widest = MathF.Max(widest, ImGui.CalcTextSize(text).X);

        return widest;
    }
}
