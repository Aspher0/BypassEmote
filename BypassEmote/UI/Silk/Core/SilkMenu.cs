using BypassEmote.UI.Silk.Main;
using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk;

public sealed class SilkMenu
{
    private readonly struct Entry
    {
        public Entry(int id, string label, SilkIconShape? icon, bool requiresCtrl, string? hint, bool separator)
        {
            Id = id;
            Label = label;
            Icon = icon;
            RequiresCtrl = requiresCtrl;
            Hint = hint;
            Separator = separator;
        }

        public int Id { get; }
        public string Label { get; }
        public SilkIconShape? Icon { get; }
        public bool RequiresCtrl { get; }
        public string? Hint { get; }
        public bool Separator { get; }
    }

    private readonly List<Entry> entries = [];
    private readonly string windowId;
    private bool open;
    private Vector2 at;
    private Vector2 windowMin;
    private Vector2 windowMax;
    private float openedAt;
    private int openedFrame;

    public SilkMenu(string windowId)
    {
        this.windowId = windowId;
    }

    public bool IsOpen => open;

    public bool MouseInside(Vector2 point)
        => open && point.X >= windowMin.X && point.Y >= windowMin.Y && point.X < windowMax.X && point.Y < windowMax.Y;

    public void Clear() => entries.Clear();

    public void Add(int id, string label, SilkIconShape? icon = null, bool requiresCtrl = false, string? hint = null)
        => entries.Add(new Entry(id, label, icon, requiresCtrl, hint, false));

    public void Separator() => entries.Add(new Entry(0, string.Empty, null, false, null, true));

    public void Open(Vector2 position)
    {
        at = position;
        openedAt = SilkUi.Time;
        openedFrame = ImGui.GetFrameCount();
        open = true;
    }

    public void Close() => open = false;

    public int Draw()
    {
        if (!open || entries.Count == 0)
            return -1;

        var s = SilkUi.Scale;
        var contentW = 0f;
        var height = 10f * s;

        foreach (var entry in entries)
        {
            if (entry.Separator)
            {
                height += 9f * s;
                continue;
            }

            var w = 9f * s + (entry.Icon != null ? 24f * s : 0f) + SilkFonts.Measure(SilkFace.Ui500, 12.5f, entry.Label).X + 9f * s;
            contentW = MathF.Max(contentW, w);
            height += 30f * s;
        }

        var width = MathF.Max(220f * s, contentW + 10f * s);
        var viewport = ImGui.GetMainViewport();
        var vMax = viewport.Pos + viewport.Size;
        var pos = new Vector2(MathF.Min(at.X, vMax.X - width - 8f * s), MathF.Min(at.Y, vMax.Y - height - 8f * s));
        pos = Vector2.Max(pos, viewport.Pos);
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        windowMin = pos;
        windowMax = pos + new Vector2(width, height);

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(new Vector2(width, height));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;

        var visible = ImGui.Begin(windowId, flags);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        var picked = -1;

        if (visible)
        {
            NoireWindowChrome.KeepInFront();
            picked = DrawBody(pos, width, height, s);
        }

        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        ImGui.End();

        if (picked >= 0)
            open = false;

        if (open && ImGui.GetFrameCount() > openedFrame
            && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right) || NoireDismiss.PressedInGame) && !hovered)
        {
            open = false;
        }

        if (open && ImGui.IsKeyPressed(ImGuiKey.Escape))
            open = false;

        return picked;
    }

    private int DrawBody(Vector2 pos, float width, float height, float s)
    {
        var dl = ImGui.GetWindowDrawList();
        var reduced = SilkUi.ReducedMotion;
        var now = SilkUi.Time;
        var t = reduced ? 1f : Math.Clamp((now - openedAt) / 0.18f, 0f, 1f);
        var alpha = reduced ? 1f : Math.Clamp((now - openedAt) / 0.14f, 0f, 1f);
        var scale = 0.97f + 0.03f * SilkUi.EaseOut.Evaluate(t);
        var start = dl.VtxBuffer.Size;
        var min = pos;
        var max = pos + new Vector2(width, height);
        var r = 12f * s;
        var ctrl = ImGui.GetIO().KeyCtrl;

        dl.PushClipRectFullScreen();
        SilkMainDraw.Shadow(dl, min, max, r, SilkMainDraw.Col(0, 0, 0, 0.95f), 50f * s, -10f * s, new Vector2(0f, 22f * s));
        dl.AddRectFilled(min, max, SilkMainDraw.Col(10, 15, 26, 0.98f), r);
        SilkMainDraw.OuterRing(dl, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.16f));

        var x = min.X + 5f * s;
        var y = min.Y + 5f * s;
        var inner = width - 10f * s;
        var picked = -1;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (entry.Separator)
            {
                dl.AddRectFilled(new Vector2(x + 2f * s, y + 4f * s), new Vector2(x + inner - 2f * s, y + 5f * s), SilkMainDraw.Col(191, 211, 236, 0.09f));
                y += 9f * s;
                continue;
            }

            var bMin = new Vector2(x, y);
            var bMax = new Vector2(x + inner, y + 30f * s);
            var disabled = entry.RequiresCtrl && !ctrl;
            ImGui.SetCursorScreenPos(bMin);
            ImGui.PushID(i);
            var clicked = ImGui.InvisibleButton("mi"u8, bMax - bMin);
            var hovered = ImGui.IsItemHovered();
            ImGui.PopID();
            var hot = hovered && !disabled;

            if (hot)
                dl.AddRectFilled(bMin, bMax, SilkMainDraw.Col(191, 211, 236, 0.12f), 7f * s);

            var textColor = disabled ? SilkPalette.Fade(SilkPalette.Ink3, 0.6f) : hot ? SilkPalette.Ink : SilkPalette.Ink2;
            var iconColor = disabled ? SilkPalette.Fade(SilkPalette.Ink3, 0.6f) : hot ? SilkPalette.Ice : SilkPalette.Ink3;
            var tx = bMin.X + 9f * s;

            if (entry.Icon is { } icon)
            {
                SilkIcons.Draw(dl, icon, new Vector2(tx, bMin.Y + 8f * s), 14f * s, SilkPalette.U32(iconColor));
                tx += 24f * s;
            }

            var lh = SilkFonts.LineHeight(SilkFace.Ui500, 12.5f);
            SilkFonts.Draw(dl, new Vector2(MathF.Round(tx), MathF.Round(bMin.Y + (30f * s - lh) * 0.5f)), SilkPalette.U32(textColor),
                SilkFace.Ui500, 12.5f, entry.Label);

            if (disabled && hovered && entry.Hint != null)
                SilkTooltip.Hover(entry.Hint, bMin, bMax, true);

            if (clicked && !disabled)
                picked = entry.Id;

            y += 30f * s;
        }

        dl.PopClipRect();

        SilkMainDraw.Transform(dl, start, min, new Vector2(scale), 0f, Vector2.Zero);
        SilkMainDraw.MultiplyAlpha(dl, start, alpha);
        return picked;
    }
}
