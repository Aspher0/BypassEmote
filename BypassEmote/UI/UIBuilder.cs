using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using BypassEmote.Helpers;
using Lumina.Excel.Sheets;
using System.Linq;
using BypassEmote.Data;

namespace BypassEmote.UI;

public class UIBuilder : Window, IDisposable
{
    private enum LockedTab { All, General, Special, Expressions, Other }
    private LockedTab _currentTab = LockedTab.All;
    private string _searchText = string.Empty;

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
        bool showAllEmotes = Service.Configuration!.ShowAllEmotes;
        if (ImGui.Checkbox("Show all emotes", ref showAllEmotes))
        {
            Service.Configuration.UpdateConfiguration(() => Service.Configuration!.ShowAllEmotes = showAllEmotes);
        }

        ImGui.SameLine();

        if (ImGui.Button("Refresh Locked Emotes"))
        {
            Service.RefreshLockedEmotes();
        }

        ImGui.Separator();

        // Add search input field
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##SearchEmotes", "Search emotes...", ref _searchText, 256);

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
            if (ImGui.BeginTabItem("Expressions"))
            {
                _currentTab = LockedTab.Expressions;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Other"))
            {
                _currentTab = LockedTab.Other;
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        // Fill remaining space with a padded, gray child containing the list
        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5f, 5f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));

        ImGui.BeginChild("##LockedEmotesBox", new Vector2(avail.X, avail.Y), true, ImGuiWindowFlags.None);

        var displayedEmotes = new List<(Emote, EmoteData.EmoteCategory)>(Service.LockedEmotes);

        if (Service.Configuration!.ShowAllEmotes)
            displayedEmotes = Service.Emotes.Select(e => (e, CommonHelper.GetEmoteCategory(e))).ToList() ?? new List<(Emote, EmoteData.EmoteCategory)>();

        displayedEmotes = displayedEmotes.OrderByDescending(e => e.Item1.RowId).ToList();

        foreach (var emote in displayedEmotes)
        {
            if (CommonHelper.GetEmotePlayType(emote.Item1) == EmoteData.EmotePlayType.DoNotPlay)
                continue;

            // Filter based on selected tab
            if (_currentTab == LockedTab.General && CommonHelper.GetEmoteCategory(emote.Item1) != EmoteData.EmoteCategory.General)
                continue;
            if (_currentTab == LockedTab.Special && CommonHelper.GetEmoteCategory(emote.Item1) != EmoteData.EmoteCategory.Special)
                continue;
            if (_currentTab == LockedTab.Expressions && CommonHelper.GetEmoteCategory(emote.Item1) != EmoteData.EmoteCategory.Expressions)
                continue;
            if (_currentTab == LockedTab.Other && CommonHelper.GetEmoteCategory(emote.Item1) != EmoteData.EmoteCategory.Unknown)
                continue;

            var display = CommonHelper.GetEmoteName(emote.Item1);

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

            var label = commands.Count > 0 ? $"{display} ({string.Join(", ", commands)})" : display;

            // Filter based on search text
            if (!string.IsNullOrWhiteSpace(_searchText) &&
                !label.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            // Draw icon on the left
            var iconSize = 25f;
            try
            {
                var iconTex = Service.TextureProvider.GetFromGameIcon((uint)CommonHelper.GetEmoteIcon(emote.Item1));
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

            if (ImGui.Selectable(label, false))
                EmotePlayer.PlayEmote(Service.ClientState.LocalPlayer, emote.Item1);
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    public void Dispose() { }
}
