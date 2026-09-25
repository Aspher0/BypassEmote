using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed class SilkRowMenu
{
    private enum Item
    {
        None,
        Play,
        ForceSwap,
        Hotbar,
        Minion,
        Pet,
        Chocobo,
        Override,
        CreateMod,
        SyncEveryone,
        SyncDirect,
        Separator,
    }

    private static string ForceTip => L.OnlyEmoteSwapSwaps.Text;

    private static readonly Item[] Scratch = new Item[16];

    private SilkEmoteEntry? entry;
    private bool sync;
    private bool open;
    private Vector2 at;
    private Vector2 windowMin;
    private Vector2 windowMax;
    private float openedAt;
    private int openedFrame;

    internal bool IsOpen => open;

    internal bool MouseInside(Vector2 point)
        => open && point.X >= windowMin.X && point.Y >= windowMin.Y && point.X < windowMax.X && point.Y < windowMax.Y;

    internal void OpenRow(SilkEmoteEntry target, Vector2 mouse, float now)
    {
        entry = target;
        sync = false;
        Show(mouse, now);
    }

    internal void OpenSync(Vector2 position, float now)
    {
        entry = null;
        sync = true;
        Show(position, now);
    }

    private void Show(Vector2 position, float now)
    {
        at = position;
        openedAt = now;
        open = true;
        openedFrame = ImGui.GetFrameCount();
    }

    internal void Close() => open = false;

    private int Build()
    {
        var n = 0;

        if (sync)
        {
            Scratch[n++] = Item.SyncEveryone;
            Scratch[n++] = Item.SyncDirect;
            return n;
        }

        if (entry == null)
            return 0;

        Scratch[n++] = Item.Play;

        if (EmoteActions.CanForceSwap)
            Scratch[n++] = Item.ForceSwap;

        if (entry.Assignable)
            Scratch[n++] = Item.Hotbar;

        Scratch[n++] = Item.Separator;
        Scratch[n++] = Item.Minion;
        Scratch[n++] = Item.Pet;
        Scratch[n++] = Item.Chocobo;

        var tail = n;

        if (EmoteActions.InEmoteSwap)
            Scratch[n++] = Item.Override;

        if (EmoteActions.PenumbraReady)
            Scratch[n++] = Item.CreateMod;

        if (n > tail)
        {
            Array.Copy(Scratch, tail, Scratch, tail + 1, n - tail);
            Scratch[tail] = Item.Separator;
            n++;
        }

        return n;
    }

    private static string Label(Item item) => item switch
    {
        Item.Play => L.PlayOnYourself.Text,
        Item.ForceSwap => L.ForceSwap.Text,
        Item.Hotbar => L.AssignToHotbarSlot.Text,
        Item.Minion => L.ApplyOnMinion.Text,
        Item.Pet => L.ApplyOnPet.Text,
        Item.Chocobo => L.ApplyOnChocobo.Text,
        Item.Override => L.AddOverride.Text,
        Item.CreateMod => L.CreateModFromEmote.Text,
        Item.SyncEveryone => L.SyncEveryoneCommand.Text,
        Item.SyncDirect => L.SyncDirectCommand.Text,
        _ => string.Empty,
    };

    private static SilkIconShape? Icon(Item item) => item switch
    {
        Item.Play => SilkMainIcons.Play,
        Item.ForceSwap => SilkMainIcons.Swap,
        Item.Hotbar => SilkMainIcons.Hotbar,
        Item.Override => SilkMainIcons.Overrides,
        Item.CreateMod => SilkMainIcons.Cube,
        Item.SyncEveryone or Item.SyncDirect => SilkMainIcons.Sync,
        _ => null,
    };

    internal void DrawWindow(SilkMainPainter view, float s, float now, bool reduced)
    {
        if (!open)
            return;

        var count = Build();

        if (count == 0)
        {
            open = false;
            return;
        }

        var headerH = entry != null ? 38f * s + 4f * s : 0f;
        var contentW = 0f;
        var height = 5f * s * 2f + headerH;

        if (entry != null)
            contentW = 8f * s + 22f * s + 9f * s + SilkFonts.Measure(SilkFace.Ui700, 12.5f, entry.Name).X + 8f * s;

        for (var i = 0; i < count; i++)
        {
            var item = Scratch[i];

            if (item == Item.Separator)
            {
                height += 9f * s;
                continue;
            }

            var w = 9f * s + (Icon(item) != null ? 24f * s : 0f) + SilkFonts.Measure(SilkFace.Ui500, 12.5f, Label(item)).X + 9f * s;
            contentW = MathF.Max(contentW, w);
            height += 30f * s;
        }

        var width = MathF.Max(220f * s, contentW + 10f * s);
        var viewport = ImGui.GetMainViewport();
        var vMax = viewport.Pos + viewport.Size;
        var pos = new Vector2(MathF.Min(at.X, vMax.X - 236f * s), MathF.Min(at.Y, vMax.Y - 250f * s));
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

        var visible = ImGui.Begin("##silkrowmenu"u8, flags);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        if (visible)
        {
            NoireWindowChrome.KeepInFront();
            DrawBody(view, pos, width, height, count, s, now, reduced);
        }

        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        ImGui.End();

        if (open && ImGui.GetFrameCount() > openedFrame
            && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right) || NoireDismiss.PressedInGame) && !hovered)
        {
            open = false;
        }

        if (open && ImGui.IsKeyPressed(ImGuiKey.Escape))
            open = false;
    }

    private void DrawBody(SilkMainPainter view, Vector2 pos, float width, float height, int count, float s, float now, bool reduced)
    {
        var dl = ImGui.GetWindowDrawList();
        var t = reduced ? 1f : Math.Clamp((now - openedAt) / 0.18f, 0f, 1f);
        var alpha = reduced ? 1f : Math.Clamp((now - openedAt) / 0.14f, 0f, 1f);
        var scale = 0.97f + 0.03f * SilkUi.EaseOut.Evaluate(t);
        var start = dl.VtxBuffer.Size;
        var min = pos;
        var max = pos + new Vector2(width, height);
        var r = 12f * s;

        dl.PushClipRectFullScreen();
        SilkMainDraw.Shadow(dl, min, max, r, SilkMainDraw.Col(0, 0, 0, 0.95f), 50f * s, -10f * s, new Vector2(0f, 22f * s));
        dl.AddRectFilled(min, max, SilkMainDraw.Col(10, 15, 26, 0.98f), r);
        SilkMainDraw.OuterRing(dl, min, max, r, SilkMainDraw.Col(191, 211, 236, 0.16f));

        var x = min.X + 5f * s;
        var y = min.Y + 5f * s;
        var inner = width - 10f * s;

        if (entry != null)
        {
            var iconMin = new Vector2(x + 8f * s, y + 6f * s);
            var hasIcon = SilkGameIcon.Draw(dl, entry.IconId, iconMin, iconMin + new Vector2(22f * s), 6f * s);
            var textX = hasIcon ? iconMin.X + 22f * s + 9f * s : x + 8f * s;
            var lh = SilkFonts.LineHeight(SilkFace.Ui700, 12.5f);
            SilkFonts.Draw(dl, new Vector2(MathF.Round(textX), MathF.Round(y + 6f * s + (22f * s - lh) * 0.5f)), SilkPalette.U32(SilkPalette.Ink),
                SilkFace.Ui700, 12.5f, entry.Name);
            dl.AddRectFilled(new Vector2(x, y + 37f * s), new Vector2(x + inner, y + 38f * s), SilkMainDraw.Col(191, 211, 236, 0.09f));
            y += 38f * s + 4f * s;
        }

        var picked = Item.None;

        for (var i = 0; i < count; i++)
        {
            var item = Scratch[i];

            if (item == Item.Separator)
            {
                dl.AddRectFilled(new Vector2(x + 2f * s, y + 4f * s), new Vector2(x + inner - 2f * s, y + 5f * s), SilkMainDraw.Col(191, 211, 236, 0.09f));
                y += 9f * s;
                continue;
            }

            var bMin = new Vector2(x, y);
            var bMax = new Vector2(x + inner, y + 30f * s);
            var disabled = item == Item.ForceSwap && !EmoteActions.InEmoteSwap;
            ImGui.SetCursorScreenPos(bMin);
            ImGui.PushID((int)item);
            var clicked = ImGui.InvisibleButton("mi"u8, bMax - bMin);
            var hot = ImGui.IsItemHovered() && !disabled;
            ImGui.PopID();

            if (hot)
                dl.AddRectFilled(bMin, bMax, SilkMainDraw.Col(191, 211, 236, 0.12f), 7f * s);

            var textColor = disabled ? SilkPalette.Fade(SilkPalette.Ink3, 0.6f) : hot ? SilkPalette.Ink : SilkPalette.Ink2;
            var iconColor = disabled ? SilkPalette.Fade(SilkPalette.Ink3, 0.6f) : hot ? SilkPalette.Ice : SilkPalette.Ink3;
            var tx = bMin.X + 9f * s;

            if (Icon(item) is { } icon)
            {
                SilkIcons.Draw(dl, icon, new Vector2(tx, bMin.Y + 8f * s), 14f * s, SilkPalette.U32(iconColor));
                tx += 24f * s;
            }

            var lh = SilkFonts.LineHeight(SilkFace.Ui500, 12.5f);
            SilkFonts.Draw(dl, new Vector2(MathF.Round(tx), MathF.Round(bMin.Y + (30f * s - lh) * 0.5f)), SilkPalette.U32(textColor),
                SilkFace.Ui500, 12.5f, Label(item));

            if (disabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                SilkTooltip.Hover(ForceTip, bMin, bMax);

            if (clicked && !disabled)
                picked = item;

            y += 30f * s;
        }

        dl.PopClipRect();

        var pivot = min;
        SilkMainDraw.Transform(dl, start, pivot, new Vector2(scale), 0f, Vector2.Zero);
        SilkMainDraw.MultiplyAlpha(dl, start, alpha);

        if (picked != Item.None)
            Run(view, picked);
    }

    private void Run(SilkMainPainter view, Item item)
    {
        var target = entry;
        open = false;

        switch (item)
        {
            case Item.SyncEveryone:
                EmoteActions.Sync(true);
                return;
            case Item.SyncDirect:
                EmoteActions.Sync(false);
                return;
        }

        if (target == null)
            return;

        switch (item)
        {
            case Item.Play:
                view.Play(target);
                break;
            case Item.ForceSwap:
                EmoteActions.ForceSwap(target.Row);
                break;
            case Item.Hotbar:
                EmoteActions.OpenHotbar(target.Row);
                break;
            case Item.Minion:
                EmoteActions.PlayOn(Companion.Minion, target.Row);
                break;
            case Item.Pet:
                EmoteActions.PlayOn(Companion.Pet, target.Row);
                break;
            case Item.Chocobo:
                EmoteActions.PlayOn(Companion.Chocobo, target.Row);
                break;
            case Item.Override:
                EmoteActions.OpenOverride(target.RowId);
                break;
            case Item.CreateMod:
                EmoteActions.OpenCreateMod(target.Row);
                break;
        }
    }
}
