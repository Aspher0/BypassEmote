using BypassEmote.EmoteSwap;
using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI;

internal static class SwapAdviceView
{
    private const float WrapEms = 30f;

    internal static Vector4 ColorFor(SwapAdvice.Severity severity) => severity switch
    {
        SwapAdvice.Severity.Error => NoireTheme.Current.Resolve(ThemeColor.Danger),
        SwapAdvice.Severity.Warning => NoireTheme.Current.Resolve(ThemeColor.Warning),
        _ => NoireTheme.Current.Resolve(ThemeColor.TextMuted),
    };

    internal static void DrawLines(IReadOnlyList<SwapAdvice.Line> lines, float wrapAt)
    {
        ImGui.PushTextWrapPos(wrapAt);

        foreach (var line in lines)
            ImGui.TextColored(ColorFor(line.Severity), $"- {line.Text}");

        ImGui.PopTextWrapPos();
    }

    internal static void DrawTooltip(string title, IReadOnlyList<SwapAdvice.Line> lines)
    {
        if (lines.Count == 0)
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * WrapEms);

        ImGui.TextColored(ColorFor(SwapAdvice.WorstOf(lines)), title);
        DrawLines(lines, ImGui.GetFontSize() * WrapEms);

        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
