using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using BypassEmote.Helpers;

namespace BypassEmote.UI;

public class UIBuilder : Window, IDisposable
{
    private enum LockedTab { All, General, Special }
    private LockedTab _currentTab = LockedTab.All;

    public UIBuilder() : base("Bypass Emote - Locked Emotes##BypassEmoteMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        const string refreshLabel = "Refresh Locked Emotes";
        var style = ImGui.GetStyle();
        var textSize = ImGui.CalcTextSize(refreshLabel);
        var buttonWidth = textSize.X + style.FramePadding.X * 2f;
        var availX = ImGui.GetContentRegionAvail().X;
        var offsetX = MathF.Max(0f, (availX - buttonWidth) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

        if (ImGui.Button(refreshLabel))
        {
            Service.RefreshLockedEmotes();
        }

        ImGui.Separator();

        if (ImGui.BeginTabBar("##LockedEmotesTabs"))
        {
            if (ImGui.BeginTabItem("All"))
            {
                _currentTab = LockedTab.All;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("General"))
            {
                _currentTab = LockedTab.General;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Special"))
            {
                _currentTab = LockedTab.Special;
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        // Fill remaining space with a padded, gray child containing the list
        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5f, 5f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));

        ImGui.BeginChild("##LockedEmotesBox", new Vector2(avail.X, avail.Y), true, ImGuiWindowFlags.None);

        foreach (var emote in Service.LockedEmotes)
        {
            // Filter based on selected tab
            if (_currentTab == LockedTab.General && emote.Item2 != CommonHelper.EmoteCategory.OneShot)
                continue;
            if (_currentTab == LockedTab.Special && emote.Item2 != CommonHelper.EmoteCategory.Looped)
                continue;

            var display = emote.Item1.Name.ToString();
            if (string.IsNullOrWhiteSpace(display))
                display = emote.Item1.TextCommand.ValueNullable?.Command.ExtractText() ?? $"Emote {emote.Item1.RowId}";

            // Build commands string (all associated, comma separated)
            var commands = new List<string>(4);
            var tc = emote.Item1.TextCommand.ValueNullable;
            void AddCmd(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                var cmd = s.StartsWith('/') ? s : "/" + s;
                if (!commands.Exists(c => string.Equals(c, cmd, StringComparison.OrdinalIgnoreCase)))
                    commands.Add(cmd);
            }
            AddCmd(tc?.Command.ExtractText());
            AddCmd(tc?.ShortCommand.ExtractText());
            AddCmd(tc?.Alias.ExtractText());
            AddCmd(tc?.ShortAlias.ExtractText());

            // Draw icon on the left
            var iconSize = 25f;
            try
            {
                var iconTex = Service.TextureProvider.GetFromGameIcon((uint)emote.Item1.Icon);
                var wrap = iconTex?.GetWrapOrEmpty();
                if (wrap != null)
                {
                    var posY = ImGui.GetCursorPosY();
                    ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
                    ImGui.SameLine();
                    ImGui.SetCursorPosY(posY + MathF.Max(0, (iconSize - ImGui.GetTextLineHeight()) * 0.5f));
                }
            }
            catch
            {
                // ignore icon issues
            }

            var label = commands.Count > 0 ? $"{display} ({string.Join(", ", commands)})" : display;

            if (ImGui.Selectable(label, false))
            {
                EmotePlayer.PlayEmote(Service.ClientState.LocalPlayer, emote.Item1);
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    public void Dispose() { }
}
