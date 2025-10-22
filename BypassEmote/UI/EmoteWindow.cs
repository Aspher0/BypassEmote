using BypassEmote.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI;

public class EmoteWindow : Window, IDisposable
{
    private enum LockedTab { All, General, Special, Expressions, Other, Favorites }
    private LockedTab currentTab = LockedTab.All;
    private string searchText = string.Empty;

    public EmoteWindow() : base("Bypass Emote - Locked Emotes##BypassEmoteMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        TitleBarButtons.Add(new()
        {
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenSettings(); },
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Open settings"),
        });

        TitleBarButtons.Add(new()
        {
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenChangelog(); },
            Icon = FontAwesomeIcon.Book,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Show changelogs"),
        });

        TitleBarButtons.Add(new()
        {
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.OpenKofi(); },
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Support me"),
        });
    }

    public override void Draw()
    {
        var buttonText = "Refresh Locked Emotes";
        var buttonWidth = ImGui.CalcTextSize(buttonText).X + ImGui.GetStyle().FramePadding.X * 2;
        var availWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((availWidth - buttonWidth) * 0.5f);

        if (ImGui.Button(buttonText))
        {
            Service.RefreshLockedEmotes();
        }

        ImGui.Separator();

        // Calculate total width needed for both checkboxes
        var showAllText = "Show all emotes";
        var showInvalidText = "Show invalid emotes";
        var showIdsText = "Show IDs";
        var showAllWidth = ImGui.CalcTextSize(showAllText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var showInvalidWidth = ImGui.CalcTextSize(showInvalidText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var showIdsWidth = ImGui.CalcTextSize(showIdsText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = showAllWidth + spacing + showInvalidWidth + spacing + showIdsWidth;
        availWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((availWidth - totalWidth) * 0.5f);

        bool showAllEmotes = Service.Configuration!.ShowAllEmotes;
        if (ImGui.Checkbox("Show all emotes", ref showAllEmotes))
        {
            Service.Configuration.UpdateConfiguration(() => Service.Configuration!.ShowAllEmotes = showAllEmotes);
        }

        ImGui.SameLine();

        bool showInvalidEmotes = Service.Configuration!.ShowInvalidEmotes;
        if (ImGui.Checkbox("Show Invalid Emotes", ref showInvalidEmotes))
        {
            Service.Configuration.UpdateConfiguration(() => Service.Configuration!.ShowInvalidEmotes = showInvalidEmotes);
        }

        ImGui.SameLine();

        bool showEmoteIds = Service.Configuration!.ShowEmoteIds;
        if (ImGui.Checkbox("Show IDs", ref showEmoteIds))
        {
            Service.Configuration.UpdateConfiguration(() => Service.Configuration!.ShowEmoteIds = showEmoteIds);
        }

        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##SearchEmotes", "Search emotes...", ref searchText, 256);

        if (ImGui.BeginTabBar("##LockedEmotesTabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("All"))
            {
                currentTab = LockedTab.All;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("General"))
            {
                currentTab = LockedTab.General;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Special"))
            {
                currentTab = LockedTab.Special;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Expressions"))
            {
                currentTab = LockedTab.Expressions;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Other"))
            {
                currentTab = LockedTab.Other;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Fav", ImGuiTabItemFlags.Leading))
            {
                currentTab = LockedTab.Favorites;
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        // Fill remaining space with a padded, gray child containing the list
        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5f, 5f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));

        ImGui.BeginChild("##LockedEmotesBox", new Vector2(avail.X, avail.Y), true, ImGuiWindowFlags.None);

        var displayedEmotes = new List<(Emote, NoireLib.Enums.EmoteCategory)>(Service.LockedEmotes);

        if (Service.Configuration!.ShowAllEmotes || currentTab == LockedTab.Favorites)
        {
            var emoteSheet = ExcelSheetHelper.GetSheet<Emote>();

            displayedEmotes = emoteSheet != null
                ? emoteSheet.Select(e => (e, EmoteHelper.GetEmoteCategory(e))).ToList()
                : new List<(Emote, NoireLib.Enums.EmoteCategory)>();
        }

        if (!Service.Configuration.ShowInvalidEmotes && currentTab != LockedTab.Favorites)
            displayedEmotes.RemoveAll(e => !Helpers.CommonHelper.IsEmoteDisplayable(e.Item1));

        displayedEmotes = displayedEmotes.OrderByDescending(e => e.Item1.RowId).ToList();

        // Check if we're in favorites tab and if there are any favorites
        if (currentTab == LockedTab.Favorites && Service.Configuration.FavoriteEmotes.Count == 0)
        {
            // Center the "No favorited emote" message
            var textSize = ImGui.CalcTextSize("No favorited emote");
            var windowSize = ImGui.GetWindowSize();
            ImGui.SetCursorPos(new Vector2(
                (windowSize.X - textSize.X) * 0.5f,
                (windowSize.Y - textSize.Y) * 0.5f
            ));
            ImGui.TextDisabled("No favorited emote");
        }
        else
        {
            foreach (var emote in displayedEmotes)
            {
                if (Helpers.CommonHelper.GetEmotePlayType(emote.Item1) == EmoteData.EmotePlayType.DoNotPlay)
                    continue;

                // Filter based on selected tab
                if (currentTab == LockedTab.Favorites && !Service.Configuration.FavoriteEmotes.Contains(emote.Item1.RowId))
                    continue;
                if (currentTab == LockedTab.General && emote.Item2 != NoireLib.Enums.EmoteCategory.General)
                    continue;
                if (currentTab == LockedTab.Special && emote.Item2 != NoireLib.Enums.EmoteCategory.Special)
                    continue;
                if (currentTab == LockedTab.Expressions && emote.Item2 != NoireLib.Enums.EmoteCategory.Expressions)
                    continue;
                if (currentTab == LockedTab.Other && emote.Item2 != NoireLib.Enums.EmoteCategory.Unknown)
                    continue;

                var displayedName = Service.Configuration!.ShowEmoteIds ? $"[{emote.Item1.RowId}] " : "";
                displayedName += Helpers.CommonHelper.GetEmoteName(emote.Item1);

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

                var label = commands.Count > 0 ? $"{displayedName} ({string.Join(", ", commands)})" : displayedName;

                // Filter based on search text
                if (!string.IsNullOrWhiteSpace(searchText) &&
                    !label.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Draw favorite star on the very left
                var starSize = 20f;
                var isFavorite = Service.Configuration.FavoriteEmotes.Contains(emote.Item1.RowId);
                var starColor = isFavorite ? new Vector4(1f, 0.9f, 0f, 1f) : new Vector4(0.35f, 0.35f, 0.35f, 1f); // Yellow if favorite, gray if not
                var starIcon = FontAwesomeIcon.Star;

                var initialPosY = ImGui.GetCursorPosY();

                // Draw star button
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Text, starColor);
                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.2f, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, 0.5f));
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

                // Align star vertically with text
                ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (25f - starSize) * 0.5f));

                if (ImGui.Button($"{starIcon.ToIconString()}##star_{emote.Item1.RowId}", new Vector2(starSize, starSize)))
                {
                    ToggleFavorite(emote.Item1.RowId);
                }

                ImGui.PopStyleVar();
                ImGui.PopStyleColor(4);
                ImGui.PopFont();

                // Set cursor for pointer on hover
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                ImGui.SameLine();

                // Reset Y position for the icon
                ImGui.SetCursorPosY(initialPosY);

                // Draw icon next to star
                var iconSize = 25f;
                try
                {
                    var iconTex = NoireService.TextureProvider.GetFromGameIcon((uint)Helpers.CommonHelper.GetEmoteIcon(emote.Item1));
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
                    EmotePlayer.PlayEmote(NoireService.ClientState.LocalPlayer, emote.Item1);

                // Draw small info/exclamation icon to the right of the selectable with tooltip of sources
                if (Service.EmoteSources.TryGetValue(emote.Item1.RowId, out var emoteSources) && 
                    (!string.IsNullOrWhiteSpace(emoteSources.Patch) || emoteSources.Sources.Count > 0))
                {
                    ImGui.SameLine();

                    ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (iconSize - ImGui.GetTextLineHeight()) * 0.5f));

                    ImGui.PushFont(UiBuilder.IconFont);
                    var infoColor = new Vector4(0.65f, 0.65f, 0.65f, 1f); // gray
                    ImGui.PushStyleColor(ImGuiCol.Text, infoColor);
                    ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    ImGui.PopStyleColor();
                    ImGui.PopFont();

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        
                        // Display patch information as the first line if available
                        if (!string.IsNullOrWhiteSpace(emoteSources.Patch))
                        {
                            ImGui.Text($"Patch: {emoteSources.Patch}");
                            if (emoteSources.Sources.Count > 0)
                                ImGui.Separator();
                        }
                        
                        // Display sources
                        foreach (var entry in emoteSources.Sources)
                        {
                            ImGui.Text($"{entry.Type}: {entry.Text}");
                        }
                        
                        ImGui.EndTooltip();
                    }
                }
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void ToggleFavorite(uint emoteId)
    {
        Service.Configuration!.UpdateConfiguration(() =>
        {
            if (Service.Configuration.FavoriteEmotes.Contains(emoteId))
            {
                Service.Configuration.FavoriteEmotes.Remove(emoteId);
            }
            else
            {
                Service.Configuration.FavoriteEmotes.Add(emoteId);
            }
        });
    }

    public void Dispose() { }
}
