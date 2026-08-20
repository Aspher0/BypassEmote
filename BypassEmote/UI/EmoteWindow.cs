using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI;

public class EmoteWindow : Window, IDisposable
{
    private enum LockedTab { All, General, Special, Expressions, Other, Favorites, Blocked }
    private LockedTab currentTab = LockedTab.All;
    private string searchText = string.Empty;
    private Emote? contextMenuEmote = null;

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
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenMessageJournal(); },
            Icon = FontAwesomeIcon.TimesCircle,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Show every logs"),
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
        DrawToolbar();

        ImGui.Separator();

        var showAllText = "Show all emotes";
        var showInvalidText = "Show invalid emotes";
        var showIdsText = "Show IDs";
        var showAllWidth = ImGui.CalcTextSize(showAllText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var showInvalidWidth = ImGui.CalcTextSize(showInvalidText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var showIdsWidth = ImGui.CalcTextSize(showIdsText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = showAllWidth + spacing + showInvalidWidth + spacing + showIdsWidth;
        var availWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((availWidth - totalWidth) * 0.5f);

        bool showAllEmotes = Configuration.ShowAllEmotes;
        if (ImGui.Checkbox("Show all emotes", ref showAllEmotes))
            Configuration.ShowAllEmotes = showAllEmotes;

        ImGui.SameLine();

        bool showInvalidEmotes = Configuration.ShowInvalidEmotes;
        if (ImGui.Checkbox("Show Invalid Emotes", ref showInvalidEmotes))
            Configuration.ShowInvalidEmotes = showInvalidEmotes;

        ImGui.SameLine();

        bool showEmoteIds = Configuration.ShowEmoteIds;
        if (ImGui.Checkbox("Show IDs", ref showEmoteIds))
            Configuration.ShowEmoteIds = showEmoteIds;

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
            if (ImGui.BeginTabItem("Blocked", ImGuiTabItemFlags.Leading))
            {
                currentTab = LockedTab.Blocked;
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5f, 5f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));

        ImGui.BeginChild("##LockedEmotesBox", new Vector2(avail.X, avail.Y), true, ImGuiWindowFlags.None);

        var displayedEmotes = new List<(Emote, NoireLib.Enums.EmoteCategory)>(Service.LockedEmotes);

        if (Configuration.ShowAllEmotes || currentTab is LockedTab.Favorites or LockedTab.Blocked)
        {
            var emoteSheet = ExcelSheetHelper.GetSheet<Emote>();

            displayedEmotes = emoteSheet != null
                ? emoteSheet.Select(e => (e, EmoteHelper.GetEmoteCategory(e))).ToList()
                : new List<(Emote, NoireLib.Enums.EmoteCategory)>();
        }

        if (!Configuration.ShowInvalidEmotes && currentTab is not (LockedTab.Favorites or LockedTab.Blocked))
            displayedEmotes.RemoveAll(e => !CommonHelper.IsEmoteDisplayable(e.Item1));

        displayedEmotes = displayedEmotes.OrderByDescending(e => e.Item1.RowId).ToList();

        var emptyListMessage = currentTab switch
        {
            LockedTab.Favorites when Configuration.FavoriteEmotes.Count == 0 => "No favorited emote",
            LockedTab.Blocked when Configuration.BlockedTargetEmotesEmoteSwap.Count == 0 => "No blocked emote",
            _ => null,
        };

        if (emptyListMessage != null)
        {
            var textSize = ImGui.CalcTextSize(emptyListMessage);
            var windowSize = ImGui.GetWindowSize();
            ImGui.SetCursorPos(new Vector2(
                (windowSize.X - textSize.X) * 0.5f,
                (windowSize.Y - textSize.Y) * 0.5f
            ));
            ImGui.TextDisabled(emptyListMessage);
        }
        else
        {
            foreach (var emote in displayedEmotes)
            {
                if (CommonHelper.GetEmotePlayType(emote.Item1) == EmotePlayType.DoNotPlay)
                    continue;

                if (currentTab == LockedTab.Favorites && !Configuration.FavoriteEmotes.Contains(emote.Item1.RowId))
                    continue;
                if (currentTab == LockedTab.Blocked && !Configuration.BlockedTargetEmotesEmoteSwap.Contains(emote.Item1.RowId))
                    continue;
                if (currentTab == LockedTab.General && emote.Item2 != NoireLib.Enums.EmoteCategory.General)
                    continue;
                if (currentTab == LockedTab.Special && emote.Item2 != NoireLib.Enums.EmoteCategory.Special)
                    continue;
                if (currentTab == LockedTab.Expressions && emote.Item2 != NoireLib.Enums.EmoteCategory.Expressions)
                    continue;
                if (currentTab == LockedTab.Other && emote.Item2 != NoireLib.Enums.EmoteCategory.Unknown)
                    continue;

                var displayedName = Configuration.ShowEmoteIds ? $"[{emote.Item1.RowId}] " : "";
                displayedName += CommonHelper.GetEmoteName(emote.Item1);

                // Every command form the emote answers to, comma separated.
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

                if (!string.IsNullOrWhiteSpace(searchText) &&
                    !label.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                var starSize = 20f;
                var isFavorite = Configuration.FavoriteEmotes.Contains(emote.Item1.RowId);
                var starColor = isFavorite ? new Vector4(1f, 0.9f, 0f, 1f) : new Vector4(0.35f, 0.35f, 0.35f, 1f); // Yellow if favorite, gray if not
                var starIcon = FontAwesomeIcon.Star;

                var initialPosY = ImGui.GetCursorPosY();

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Text, starColor);
                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.2f, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, 0.5f));
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

                ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (25f - starSize) * 0.5f));

                if (ImGui.Button($"{starIcon.ToIconString()}##star_{emote.Item1.RowId}", new Vector2(starSize, starSize)))
                    ToggleFavorite(emote.Item1.RowId);

                ImGui.PopStyleVar();
                ImGui.PopStyleColor(4);
                ImGui.PopFont();

                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                ImGui.SameLine(0, 2f);

                var isBlocked = Configuration.BlockedTargetEmotesEmoteSwap.Contains(emote.Item1.RowId);
                var blockColor = isBlocked ? new Vector4(0.9f, 0.2f, 0.2f, 1f) : new Vector4(0.35f, 0.35f, 0.35f, 1f); // Red if blocked, gray if not

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Text, blockColor);
                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.2f, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, 0.5f));
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

                ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (25f - starSize) * 0.5f));

                if (ImGui.Button($"{FontAwesomeIcon.Ban.ToIconString()}##block_{emote.Item1.RowId}", new Vector2(starSize, starSize)))
                    ToggleBlockedTarget(emote.Item1.RowId);

                ImGui.PopStyleVar();
                ImGui.PopStyleColor(4);
                ImGui.PopFont();

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip(isBlocked
                        ? "Blocked: no swap will land on this emote"
                        : "Block this emote as a swap target");
                }

                ImGui.SameLine();

                ImGui.SetCursorPosY(initialPosY);

                var iconSize = 25f;
                try
                {
                    var iconTex = NoireService.TextureProvider.GetFromGameIcon(Helpers.CommonHelper.GetEmoteIcon(emote.Item1));
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

                if (ImGui.Selectable(label, false) && !HotbarDragDrop.IsDragging)
                {
                    if (Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap)
                        Plugin.PlaySelfEmote(emote.Item1);
                    else
                        EmotePlayer.PlayEmote(NoireService.ObjectTable.LocalPlayer, emote.Item1);
                }

                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) &&
                    CommonHelper.IsEmoteAssignableToHotbar(emote.Item1))
                    HotbarDragDrop.BeginDrag(emote.Item1);

                var selectableHovered = ImGui.IsItemHovered();

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    contextMenuEmote = emote.Item1;
                    ImGui.OpenPopup($"emote_context_menu_{emote.Item1.RowId}");
                }

                var infoIconHovered = false;

                if (Service.EmoteSources.TryGetValue(emote.Item1.RowId, out var emoteSources) &&
                    (!string.IsNullOrWhiteSpace(emoteSources.Patch) || emoteSources.Sources.Count > 0))
                {
                    ImGui.SameLine();

                    ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (iconSize - ImGui.GetTextLineHeight()) * 0.5f));

                    ImGui.PushFont(UiBuilder.IconFont);
                    var infoColor = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Text, infoColor);
                    ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    ImGui.PopStyleColor();
                    ImGui.PopFont();

                    infoIconHovered = ImGui.IsItemHovered();

                    if (infoIconHovered)
                    {
                        ImGui.BeginTooltip();

                        if (!string.IsNullOrWhiteSpace(emoteSources.Patch))
                        {
                            ImGui.Text($"Patch: {emoteSources.Patch}");
                            if (emoteSources.Sources.Count > 0)
                                ImGui.Separator();
                        }

                        foreach (var entry in emoteSources.Sources)
                        {
                            ImGui.Text($"{entry.Type}: {entry.Text}");
                        }

                        ImGui.EndTooltip();
                    }
                }

                if (selectableHovered && !infoIconHovered && !HotbarDragDrop.IsDragging)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Left-click to apply to yourself");
                    ImGui.TextUnformatted("Right-click for more options");
                    if (CommonHelper.IsEmoteAssignableToHotbar(emote.Item1))
                        ImGui.TextUnformatted("Drag onto a hotbar slot to assign it");
                    ImGui.Separator();
                    ConditionIcons.Draw(EmoteHelper.GetEmoteConditions(emote.Item1), ImGui.GetTextLineHeight() * 1.1f);
                    ImGui.EndTooltip();
                }
            }
        }

        if (contextMenuEmote.HasValue)
        {
            using (var popup = ImRaii.Popup($"emote_context_menu_{contextMenuEmote.Value.RowId}"))
            {
                if (popup)
                {
                    if (ImGui.MenuItem("Apply emote on Minion"))
                    {
                        if (contextMenuEmote.HasValue)
                            ApplyEmoteOnMinion(contextMenuEmote.Value);
                    }

                    if (ImGui.MenuItem("Apply emote on Pet"))
                    {
                        if (contextMenuEmote.HasValue)
                            ApplyEmoteOnPet(contextMenuEmote.Value);
                    }

                    if (ImGui.MenuItem("Apply emote on Chocobo"))
                    {
                        if (contextMenuEmote.HasValue)
                            ApplyEmoteOnBuddy(contextMenuEmote.Value);
                    }

                    if (contextMenuEmote.HasValue && CommonHelper.IsEmoteAssignableToHotbar(contextMenuEmote.Value))
                    {
                        ImGui.Separator();

                        if (ImGui.MenuItem("Assign emote to Hotbar..."))
                        {
                            Service.Plugin.OpenAssignHotbar(contextMenuEmote.Value);
                            ImGui.CloseCurrentPopup();
                        }
                    }

                    if (contextMenuEmote.HasValue && Service.Penumbra is { Available: true })
                    {
                        ImGui.Separator();

                        if (ImGui.MenuItem("Create a mod from this emote..."))
                        {
                            Service.Plugin.OpenCreateMod(contextMenuEmote.Value);
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private const string SyncPopupId = "##BypassEmoteSyncMenu";

    private static void DrawToolbar()
    {
        var penumbraReady = Service.Penumbra is { Available: true };
        var segments = penumbraReady ? 3 : 2;

        var style = ImGui.GetStyle();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight() * 1.3f;
        var segment = MathF.Floor(width / segments);
        var origin = ImGui.GetCursorScreenPos();

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0f, style.ItemSpacing.Y)))
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0f))
        {
            if (ToolbarButton(FontAwesomeIcon.SyncAlt, "Refresh Locked Emotes", "##BypassEmoteRefresh", segment, height))
                Service.RefreshLockedEmotes();

            ImGui.SameLine();

            var lastWidth = width - (segment * (segments - 1));
            DrawSyncButton(penumbraReady ? segment : lastWidth, height);

            if (penumbraReady)
            {
                ImGui.SameLine();

                if (ToolbarButton(FontAwesomeIcon.ExchangeAlt, "Create a mod", "##BypassEmoteCreateMod", lastWidth, height))
                    Service.Plugin.OpenCreateMod();
            }
        }

        var seam = ImGui.GetColorU32(ImGuiCol.Border);
        var drawList = ImGui.GetWindowDrawList();

        for (var index = 1; index < segments; index++)
        {
            var x = MathF.Floor(origin.X + (segment * index));
            drawList.AddLine(new Vector2(x, origin.Y), new Vector2(x, origin.Y + height), seam);
        }
    }

    private static bool ToolbarButton(FontAwesomeIcon icon, string tooltip, string id, float width, float height)
    {
        bool pressed;

        using (ImRaii.PushFont(UiBuilder.IconFont))
            pressed = ImGui.Button(icon.ToIconString() + id, new Vector2(width, height));

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return pressed;
    }

    private static void DrawSyncButton(float width, float height)
    {
        var directPlay = Configuration.SelfBypassMode == SelfBypassMode.DirectPlay;

        if (ToolbarButton(FontAwesomeIcon.PeopleArrows, directPlay ? "Sync..." : "Sync All", "##BypassEmoteSync", width, height))
        {
            if (directPlay)
                ImGui.OpenPopup(SyncPopupId);
            else
                EmotePlayer.SyncEmotes(true);
        }

        using var popup = ImRaii.Popup(SyncPopupId);
        if (!popup)
            return;

        if (ImGui.MenuItem("Sync BE users"))
            EmotePlayer.SyncEmotes(false);

        if (ImGui.MenuItem("Sync all"))
            EmotePlayer.SyncEmotes(true);
    }

    private void ToggleFavorite(uint emoteId)
    {
        if (Configuration.FavoriteEmotes.Contains(emoteId))
            Configuration.FavoriteEmotes.Remove(emoteId);
        else
            Configuration.FavoriteEmotes.Add(emoteId);

        Configuration.Save(); // Needed until I update NoireLib to auto-save list changes
    }

    private void ToggleBlockedTarget(uint emoteId)
    {
        if (Configuration.BlockedTargetEmotesEmoteSwap.Contains(emoteId))
            Configuration.BlockedTargetEmotesEmoteSwap.Remove(emoteId);
        else
            Configuration.BlockedTargetEmotesEmoteSwap.Add(emoteId);

        Configuration.Save();
    }

    private void ApplyEmoteOnMinion(Emote emote)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = CharacterHelper.GetCompanionAddress(player);
        if (addr == 0)
        {
            FeedbackHelper.Info("No minion summoned.");
            return;
        }

        if (NoireService.ObjectTable.FirstOrDefault(o => o.Address == addr) is not ICharacter minion)
            return;

        EmotePlayer.PlayEmote(minion, emote);
    }

    private void ApplyEmoteOnPet(Emote emote)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = CharacterHelper.GetPetAddress(player);
        if (addr == 0)
        {
            FeedbackHelper.Info("No pet summoned.");
            return;
        }

        if (NoireService.ObjectTable.FirstOrDefault(o => o.Address == addr) is not ICharacter pet)
            return;

        EmotePlayer.PlayEmote(pet, emote);
    }

    private void ApplyEmoteOnBuddy(Emote emote)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = CharacterHelper.GetBuddyAddress(player);
        if (addr == 0)
        {
            FeedbackHelper.Info("No chocobo summoned.");
            return;
        }

        if (NoireService.ObjectTable.FirstOrDefault(o => o.Address == addr) is not ICharacter buddy)
            return;

        EmotePlayer.PlayEmote(buddy, emote);
    }

    public void Dispose() { }
}
