using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI.Classic;

internal sealed class ClassicMainPainter
{
    private enum LockedTab { All, General, Special, Expressions, Other, Favorites, Blocked }
    private LockedTab currentTab = LockedTab.All;
    private string searchText = string.Empty;
    private Emote? contextMenuEmote = null;

    private EmoteQuickAdd? favoriteAdd;
    private EmoteQuickAdd? blockedAdd;

    private static readonly IdLabel ShowAllLabel = new(L.ShowAllEmotes, "###ShowAllEmotes");
    private static readonly IdLabel ShowInvalidLabel = new(L.ShowInvalidEmotesClassic, "###ShowInvalidEmotes");
    private static readonly IdLabel ShowIdsLabel = new(L.ShowIds, "###ShowIDs");
    private static readonly IdLabel AllLabel = new(L.TabAll, "###All");
    private static readonly IdLabel GeneralLabel = new(L.TabGeneral, "###General");
    private static readonly IdLabel SpecialLabel = new(L.TabSpecial, "###Special");
    private static readonly IdLabel ExpressionsLabel = new(L.TabExpressions, "###Expressions");
    private static readonly IdLabel OtherLabel = new(L.TabOther, "###Other");
    private static readonly IdLabel FavLabel = new(L.TabFavourites, "###Fav");
    private static readonly IdLabel BlockedLabel = new(L.TabBlocked, "###Blocked");
    private static readonly IdLabel KofiLabel = new(L.SupportOnKofi, "##BypassEmoteKofi");
    private static readonly IdLabel DiscordLabel = new(L.Discord, "##BypassEmoteDiscord");

    private static readonly ButtonStyle KofiStyle = new()
    {
        Color = ColorHelper.HexToVector4("#FF5E5B"),
        HoveredColor = ColorHelper.HexToVector4("#FF7B79"),
        ActiveColor = ColorHelper.HexToVector4("#DE4B48"),
        TextColor = Vector4.One,
        IconColor = Vector4.One,
        Icon = FontAwesomeIcon.Heart,
    };

    private static readonly ButtonStyle DiscordStyle = new()
    {
        Color = ColorHelper.HexToVector4("#5865F2"),
        HoveredColor = ColorHelper.HexToVector4("#727DF5"),
        ActiveColor = ColorHelper.HexToVector4("#4752C4"),
        TextColor = Vector4.One,
        IconColor = Vector4.One,
        Icon = FontAwesomeIcon.Comments,
    };

    private static readonly System.Collections.Generic.Dictionary<string, (float Width, string Text)> Shortened = new();

    private static bool FittedCheckbox(IdLabel label, string text, float squeeze, ref bool value)
    {
        if (squeeze >= 1f)
            return ImGui.Checkbox(label.Text, ref value);

        var width = ImGui.CalcTextSize(text).X * squeeze;
        var changed = ImGui.Checkbox(label.Id, ref value);
        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Shorten(text, width));

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);

        return changed;
    }

    private static string Shorten(string text, float width)
    {
        if (Shortened.TryGetValue(text, out var cached) && MathF.Abs(cached.Width - width) < 0.5f)
            return cached.Text;

        var fitted = text;

        if (ImGui.CalcTextSize(text).X > width)
        {
            var length = text.Length;

            while (length > 0 && ImGui.CalcTextSize(string.Concat(text.AsSpan(0, length), "...")).X > width)
                length--;

            fitted = string.Concat(text.AsSpan(0, length).TrimEnd(), "...");
        }

        if (Shortened.Count > 64)
            Shortened.Clear();

        Shortened[text] = (width, fitted);
        return fitted;
    }

    internal void Draw()
    {
        DrawToolbar();

        ImGui.Separator();

        var showAllText = L.ShowAllEmotes.Text;
        var showInvalidText = L.ShowInvalidEmotes.Text;
        var showIdsText = L.ShowIds.Text;
        var showAllWidth = ImGui.CalcTextSize(showAllText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var showInvalidWidth = ImGui.CalcTextSize(showInvalidText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var showIdsWidth = ImGui.CalcTextSize(showIdsText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = showAllWidth + spacing + showInvalidWidth + spacing + showIdsWidth;
        var availWidth = ImGui.GetContentRegionAvail().X;

        var squeeze = 1f;

        if (totalWidth > availWidth)
        {
            var fixedWidth = totalWidth - ImGui.CalcTextSize(showAllText).X - ImGui.CalcTextSize(showInvalidText).X - ImGui.CalcTextSize(showIdsText).X;
            squeeze = MathF.Max(0f, availWidth - fixedWidth) / MathF.Max(1f, totalWidth - fixedWidth);
            totalWidth = availWidth;
        }

        ImGui.SetCursorPosX((availWidth - totalWidth) * 0.5f);

        bool showAllEmotes = Configuration.ShowAllEmotes;
        if (FittedCheckbox(ShowAllLabel, showAllText, squeeze, ref showAllEmotes))
            Configuration.ShowAllEmotes = showAllEmotes;

        ImGui.SameLine();

        bool showInvalidEmotes = Configuration.ShowInvalidEmotes;
        if (FittedCheckbox(ShowInvalidLabel, L.ShowInvalidEmotesClassic.Text, squeeze, ref showInvalidEmotes))
            Configuration.ShowInvalidEmotes = showInvalidEmotes;

        ImGui.SameLine();

        bool showEmoteIds = Configuration.ShowEmoteIds;
        if (FittedCheckbox(ShowIdsLabel, showIdsText, squeeze, ref showEmoteIds))
            Configuration.ShowEmoteIds = showEmoteIds;

        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##SearchEmotes", L.SearchEmotes.Text, ref searchText, 256);

        if (ImGui.BeginTabBar("##LockedEmotesTabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem(AllLabel.Text))
            {
                currentTab = LockedTab.All;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(GeneralLabel.Text))
            {
                currentTab = LockedTab.General;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(SpecialLabel.Text))
            {
                currentTab = LockedTab.Special;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(ExpressionsLabel.Text))
            {
                currentTab = LockedTab.Expressions;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(OtherLabel.Text))
            {
                currentTab = LockedTab.Other;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(FavLabel.Text))
            {
                currentTab = LockedTab.Favorites;
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(BlockedLabel.Text))
            {
                currentTab = LockedTab.Blocked;
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        var avail = ImGui.GetContentRegionAvail();
        var footerHeight = ImGui.GetFrameHeight() * 1.25f;
        var boxHeight = MathF.Max(ImGui.GetFrameHeight(), avail.Y - footerHeight - ImGui.GetStyle().ItemSpacing.Y);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5f, 5f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));

        ImGui.BeginChild("##LockedEmotesBox", new Vector2(avail.X, boxHeight), true, ImGuiWindowFlags.None);

        DrawQuickAdd();

        ImGui.BeginChild("##LockedEmotesList", Vector2.Zero, false, ImGuiWindowFlags.None);

        var wholeSheet = Configuration.ShowAllEmotes || currentTab is LockedTab.Favorites or LockedTab.Blocked;
        var hideInvalid = !Configuration.ShowInvalidEmotes && currentTab is not (LockedTab.Favorites or LockedTab.Blocked);
        var displayedEmotes = PreparedEmotes(wholeSheet, hideInvalid);

        var emptyListMessage = currentTab switch
        {
            LockedTab.Favorites when Configuration.FavoriteEmotes.Count == 0 => L.NoFavoritedClassic.Text,
            LockedTab.Blocked when Configuration.BlockedTargetEmotesEmoteSwap.Count == 0 => L.NoBlockedEmote.Text,
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
            visibleRows.Clear();

            for (var i = 0; i < displayedEmotes.Length; i++)
            {
                var emote = displayedEmotes[i];

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

                if (!string.IsNullOrWhiteSpace(searchText) &&
                    !EmoteLabel(emote.Item1, Configuration.ShowEmoteIds).Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                visibleRows.Add(i);
            }

            var clipper = new ImGuiListClipper();
            clipper.Begin(visibleRows.Count);

            while (clipper.Step())
            {
                for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                    DrawEmoteRow(displayedEmotes[visibleRows[row]]);
            }

            clipper.End();
        }

        if (contextMenuEmote.HasValue)
        {
            using (var popup = ImRaii.Popup(EmoteContextMenuId))
            {
                if (popup)
                {
                    var inEmoteSwap = Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap;
#if DEBUG
                    if (ImGui.MenuItem(L.ForceSwap.Text, string.Empty, false, inEmoteSwap) && contextMenuEmote.HasValue)
                        EmoteActions.ForceSwap(contextMenuEmote.Value);

                    if (!inEmoteSwap && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip(L.OnlyEmoteSwapSwaps.Text);

                    ImGui.Separator();
#endif

                    if (ImGui.MenuItem(L.ApplyOnMinionClassic.Text))
                    {
                        if (contextMenuEmote.HasValue)
                            EmoteActions.PlayOn(Companion.Minion, contextMenuEmote.Value);
                    }

                    if (ImGui.MenuItem(L.ApplyOnPetClassic.Text))
                    {
                        if (contextMenuEmote.HasValue)
                            EmoteActions.PlayOn(Companion.Pet, contextMenuEmote.Value);
                    }

                    if (ImGui.MenuItem(L.ApplyOnChocoboClassic.Text))
                    {
                        if (contextMenuEmote.HasValue)
                            EmoteActions.PlayOn(Companion.Chocobo, contextMenuEmote.Value);
                    }

                    if (contextMenuEmote.HasValue && inEmoteSwap)
                    {
                        ImGui.Separator();

                        if (ImGui.MenuItem(L.AddOverrideClassic.Text))
                        {
                            Service.Plugin.OpenOverrides(contextMenuEmote.Value.RowId);
                            ImGui.CloseCurrentPopup();
                        }
                    }

                    if (contextMenuEmote.HasValue && CommonHelper.IsEmoteAssignableToHotbar(contextMenuEmote.Value))
                    {
                        ImGui.Separator();

                        if (ImGui.MenuItem(L.AssignToHotbarClassic.Text))
                        {
                            Service.Plugin.OpenAssignHotbar(contextMenuEmote.Value);
                            ImGui.CloseCurrentPopup();
                        }
                    }

                    if (contextMenuEmote.HasValue && Service.Penumbra is { Available: true })
                    {
                        ImGui.Separator();

                        if (ImGui.MenuItem(L.CreateModFromEmote.Text))
                        {
                            Service.Plugin.OpenCreateMod(contextMenuEmote.Value);
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
            }
        }

        ImGui.EndChild();

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        DrawSupportBar(footerHeight);
    }

    private void DrawEmoteRow((Emote, NoireLib.Enums.EmoteCategory) emote)
    {
        var label = EmoteLabel(emote.Item1, Configuration.ShowEmoteIds);

        var starSize = 20f;
        var isFavorite = Configuration.FavoriteEmotes.Contains(emote.Item1.RowId);
        var starColor = isFavorite ? new Vector4(1f, 0.9f, 0f, 1f) : new Vector4(0.35f, 0.35f, 0.35f, 1f);

        var initialPosY = ImGui.GetCursorPosY();

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, starColor);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.2f, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

        ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (25f - starSize) * 0.5f));

        ImGui.PushID((int)emote.Item1.RowId);
        var starClicked = ImGui.Button(StarButton, new Vector2(starSize, starSize));
        ImGui.PopID();

        if (starClicked)
            EmoteActions.ToggleFavourite(emote.Item1.RowId);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        ImGui.SameLine(0, 2f);

        var isBlocked = Configuration.BlockedTargetEmotesEmoteSwap.Contains(emote.Item1.RowId);
        var blockColor = isBlocked ? new Vector4(0.9f, 0.2f, 0.2f, 1f) : new Vector4(0.35f, 0.35f, 0.35f, 1f);

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, blockColor);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.2f, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

        ImGui.SetCursorPosY(initialPosY + MathF.Max(0, (25f - starSize) * 0.5f));

        ImGui.PushID((int)emote.Item1.RowId);
        var blockClicked = ImGui.Button(BlockButton, new Vector2(starSize, starSize));
        ImGui.PopID();

        if (blockClicked)
            EmoteActions.ToggleBlocked(emote.Item1.RowId);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(isBlocked
                ? L.BlockedTooltip.Text
                : L.BlockTooltip.Text);
        }

        ImGui.SameLine();

        ImGui.SetCursorPosY(initialPosY);

        var iconSize = 25f;
        try
        {
            var iconTex = EmoteIcon(emote.Item1);
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
        }

        if (ImGui.Selectable(label, false) && !HotbarDragDrop.IsDragging)
        {
            EmoteActions.PlaySelf(emote.Item1);
        }

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) &&
            CommonHelper.IsEmoteAssignableToHotbar(emote.Item1))
            HotbarDragDrop.BeginDrag(emote.Item1);

        var selectableHovered = ImGui.IsItemHovered();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            contextMenuEmote = emote.Item1;
            ImGui.OpenPopup(EmoteContextMenuId);
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
            ImGui.TextUnformatted(InfoIcon);
            ImGui.PopStyleColor();
            ImGui.PopFont();

            infoIconHovered = ImGui.IsItemHovered();

            if (infoIconHovered)
            {
                ImGui.BeginTooltip();

                if (!string.IsNullOrWhiteSpace(emoteSources.Patch))
                {
                    ImGui.TextUnformatted(PatchLineOf(emoteSources.Patch));
                    if (emoteSources.Sources.Count > 0)
                        ImGui.Separator();
                }

                foreach (var line in SourceLinesOf(emote.Item1.RowId, emoteSources.Sources))
                    ImGui.TextUnformatted(line);

                ImGui.EndTooltip();
            }
        }

        if (selectableHovered && !infoIconHovered && !HotbarDragDrop.IsDragging)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(L.LeftClickApply.Text);
            ImGui.TextUnformatted(L.RightClickOptions.Text);
            if (CommonHelper.IsEmoteAssignableToHotbar(emote.Item1))
                ImGui.TextUnformatted(L.DragToHotbar.Text);
            ImGui.Separator();
            ConditionIcons.Draw(EmoteHelper.GetEmoteConditions(emote.Item1), ImGui.GetTextLineHeight() * 1.1f);
            ImGui.EndTooltip();
        }
    }

    private static void DrawSupportBar(float height)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X;
        var half = MathF.Floor((width - spacing) * 0.5f);

        if (NoireButtons.Button(KofiLabel.Text, KofiStyle, new Vector2(half, height)))
            Service.OpenKofi();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(L.KofiTooltip.Text);

        ImGui.SameLine();

        if (NoireButtons.Button(DiscordLabel.Text, DiscordStyle, new Vector2(width - half - spacing, height)))
            Service.OpenDiscord();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(L.DiscordTooltip.Text);
    }

    private void DrawQuickAdd()
    {
        if (currentTab is not (LockedTab.Favorites or LockedTab.Blocked))
            return;

        var favorites = currentTab == LockedTab.Favorites;

        var picker = favorites
            ? favoriteAdd ??= new EmoteQuickAdd("BypassEmoteFavoriteAdd", L.AddFavoriteClassic)
            {
                Marked = rowId => Configuration.FavoriteEmotes.Contains(rowId),
                MarkedColor = new Vector4(1f, 0.9f, 0f, 1f),
                MarkedNote = L.MarkedFavoriteClassic,
            }
            : blockedAdd ??= new EmoteQuickAdd("BypassEmoteBlockedAdd", L.BlockEmotePlaceholder)
            {
                Marked = rowId => Configuration.BlockedTargetEmotesEmoteSwap.Contains(rowId),
                MarkedColor = new Vector4(0.9f, 0.2f, 0.2f, 1f),
                MarkedNote = L.MarkedBlocked,
            };

        if (picker.Draw(ImGui.GetContentRegionAvail().X) is { } rowId)
        {
            if (favorites)
                EmoteActions.ToggleFavourite(rowId);
            else
                EmoteActions.ToggleBlocked(rowId);
        }

        ImGui.Separator();
    }

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
            if (ToolbarButton(FontAwesomeIcon.SyncAlt, L.RefreshLockedEmotes.Text, "##BypassEmoteRefresh", segment, height))
                Service.RefreshLockedEmotes();

            ImGui.SameLine();

            var lastWidth = width - (segment * (segments - 1));
            DrawSyncButton(penumbraReady ? segment : lastWidth, height);

            if (penumbraReady)
            {
                ImGui.SameLine();

                if (ToolbarButton(FontAwesomeIcon.ExchangeAlt, L.CreateMod.Text, "##BypassEmoteCreateMod", lastWidth, height))
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

        if (ToolbarButton(FontAwesomeIcon.PeopleArrows, directPlay ? L.SyncMenu.Text : L.SyncEveryoneCommand.Text, "##BypassEmoteSync", width, height))
        {
            if (directPlay)
                ImGui.OpenPopup("##BypassEmoteSyncMenu");
            else
                EmotePlayer.SyncEmotes(true);
        }

        using var popup = ImRaii.Popup("##BypassEmoteSyncMenu");
        if (!popup)
            return;

        if (ImGui.MenuItem(L.SyncEveryoneCommand.Text))
            EmotePlayer.SyncEmotes(true);

        if (ImGui.MenuItem(L.SyncDirectCommand.Text))
            EmotePlayer.SyncEmotes(false);
    }

    private static readonly Dictionary<int, (Emote, NoireLib.Enums.EmoteCategory)[]> preparedEmotes = new();
    private static readonly Dictionary<(uint, bool), string> emoteLabels = new();
    private static int emoteLabelsLanguage;
    private static readonly List<int> visibleRows = new();

    private const string EmoteContextMenuId = "emote_context_menu";
    private static readonly string StarButton = FontAwesomeIcon.Star.ToIconString() + "##star";
    private static readonly string BlockButton = FontAwesomeIcon.Ban.ToIconString() + "##block";
    private static readonly string InfoIcon = FontAwesomeIcon.ExclamationCircle.ToIconString();

    private static readonly Dictionary<uint, (List<(string Type, string Text)> From, string[] Lines)> sourceLines = new();
    private static readonly Dictionary<string, string> patchLines = new();
    private static int patchLinesRevision = -1;

    private static string[] SourceLinesOf(uint rowId, List<(string Type, string Text)> sources)
    {
        if (sourceLines.TryGetValue(rowId, out var held) && ReferenceEquals(held.From, sources) && held.Lines.Length == sources.Count)
            return held.Lines;

        var lines = new string[sources.Count];

        for (var i = 0; i < lines.Length; i++)
            lines[i] = sources[i].Type + ": " + sources[i].Text;

        sourceLines[rowId] = (sources, lines);
        return lines;
    }

    private static string PatchLineOf(string patch)
    {
        if (patchLinesRevision != NoireLanguages.Revision)
        {
            patchLinesRevision = NoireLanguages.Revision;
            patchLines.Clear();
        }

        if (!patchLines.TryGetValue(patch, out var line))
            patchLines[patch] = line = L.PatchLine.With("patch", patch);

        return line;
    }
    private static object? preparedFrom;

    private static (Emote, NoireLib.Enums.EmoteCategory)[] PreparedEmotes(bool wholeSheet, bool hideInvalid)
    {
        if (!ReferenceEquals(preparedFrom, Service.LockedEmotes))
        {
            preparedEmotes.Clear();
            preparedFrom = Service.LockedEmotes;
        }

        var key = (wholeSheet ? 2 : 0) | (hideInvalid ? 1 : 0);

        if (preparedEmotes.TryGetValue(key, out var held))
            return held;

        List<(Emote, NoireLib.Enums.EmoteCategory)> rows;

        if (wholeSheet)
        {
            var emoteSheet = ExcelSheetHelper.GetSheet<Emote>();

            rows = emoteSheet != null
                ? emoteSheet.Select(e => (e, EmoteHelper.GetEmoteCategory(e))).ToList()
                : new List<(Emote, NoireLib.Enums.EmoteCategory)>();
        }
        else
        {
            rows = new List<(Emote, NoireLib.Enums.EmoteCategory)>(Service.LockedEmotes);
        }

        if (hideInvalid)
            rows.RemoveAll(e => !CommonHelper.IsEmoteDisplayable(e.Item1));

        rows.RemoveAll(e => CommonHelper.GetEmotePlayType(e.Item1) == EmotePlayType.DoNotPlay);

        var built = rows.OrderByDescending(e => e.Item1.RowId).ToArray();
        preparedEmotes[key] = built;

        return built;
    }

    private static string EmoteLabel(Emote emote, bool withIds)
    {
        if (emoteLabelsLanguage != SheetLanguage.Version)
        {
            emoteLabels.Clear();
            emoteLabelsLanguage = SheetLanguage.Version;
        }

        if (emoteLabels.TryGetValue((emote.RowId, withIds), out var held))
            return held;

        var text = SheetLanguage.Display(emote);
        var displayedName = withIds ? $"[{emote.RowId}] " : "";
        displayedName += CommonHelper.GetEmoteName(text);

        var commands = new List<string>(4);
        var tc = text.TextCommand.ValueNullable;
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
        emoteLabels[(emote.RowId, withIds)] = label;

        return label;
    }

    private static Dalamud.Interface.Textures.ISharedImmediateTexture? EmoteIcon(Emote emote)
        => IconHelper.Get(Helpers.CommonHelper.GetEmoteIcon(emote));
}
