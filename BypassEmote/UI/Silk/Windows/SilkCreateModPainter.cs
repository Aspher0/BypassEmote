using BypassEmote.EmoteSwap;
using BypassEmote.Localization;
using BypassEmote.UI.Silk.Main;
using BypassEmote.UI.Silk.Settings;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Animations.Helpers;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Windows;

internal sealed class SilkCreateModPainter
{
    private static string Lead => L.SilkCreateModIntro.Text;

    private static string SourceLabel => L.EmoteToPlay.Text;
    private static string TargetLabel => L.PlayedOver.Text;
    private static string SourceHelp => L.EmoteToPlayHelp.Text;
    private static string TargetHelp => L.PlayedOverHelp.Text;
    private static string SourcePlaceholder => L.PickEmoteToPlay.Text;
    private static string TargetPlaceholder => L.PickEmoteToPlayOver.Text;
    private static string EnableName => L.EnableOnCreation.Text;
    private static string EnableHelp => L.EnableOnCreationHelp.Text;
    private static string PriorityName => L.HighestPriority.Text;
    private static string PriorityHelp => L.SilkHighestPriorityHelp.Text;
    private static string NamePlaceholder => L.ModNamePlaceholder.Text;
    private static string CreateLabel => L.CreateTheMod.Text;
    private static string NoAnimationTooltip => L.NoAnimationForBody.Text;
    private static string VanillaAnimation => L.VanillaAnimation.Text;
    private static string OwnedChip => L.Owned.Text;
    private static string NotUnlockedChip => L.NotUnlocked.Text;
    private static string WhyBothEmotes => L.PickBoth.Text;
    private static string WhySameEmote => L.NotOverItself.Text;
    private static string WhyRaces => L.PickRace.Text;

    private static readonly string[] RaceNames = BuildRaceNames();

    private readonly SilkScrollArea scroll = new();
    private readonly SilkEmotePicker sourcePicker = new("##silkmodsource", L.SearchEmotes);
    private readonly SilkEmotePicker targetPicker = new("##silkmodtarget", L.SearchEmotes);
    private readonly HashSet<string> picked = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RacePaths?> pathsByRace = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> moddedByRace = new(StringComparer.Ordinal);
    private readonly List<string> warnings = new();

    private SilkEmoteEntry? source;
    private SilkEmoteEntry? target;
    private string modName = string.Empty;
    private bool enableOnCreation = true;
    private bool highestPriority;
    private (uint Source, uint Target)? filledFor;
    private string? autoName;
    private string? sourceAnimation;
    private bool sourceAnimationModded;
    private (uint Source, uint Target)? warningsFor;
    private int warningsRevision;
    private string? doneText;
    private string? failedText;

    private readonly CreateModWindow window;
    private readonly string animKey;

    internal SilkCreateModPainter(CreateModWindow window)
    {
        this.window = window;
        animKey = "SilkWindow." + window.Id;
    }

    public uint? PendingSource { get; set; }

    private int opening;

    public void Show()
    {
        PendingSource = null;
        doneText = null;
        failedText = null;
    }

    public void ShowFor(Emote emote)
    {
        PendingSource = emote.RowId;
        doneText = null;
        failedText = null;
        source = SilkEmoteIndex.Get(emote.RowId);
        Refill();
    }

    private void TakeOpening()
    {
        if (opening == window.Opening)
            return;

        opening = window.Opening;

        if (window.OpenedFor is { } emote)
            ShowFor(emote);
        else
            Show();
    }

    internal void DrawBody(Vector2 min, Vector2 max)
    {
        TakeOpening();

        var scale = SilkUi.Scale;
        var footHeight = (27f * scale) + (SilkControls.ButtonHeight * scale);
        var bodyMax = new Vector2(max.X, max.Y - footHeight);
        var offset = scroll.Begin(min, bodyMax, !sourcePicker.IsOpen && !targetPicker.IsOpen);

        ImGui.PushClipRect(min, bodyMax, true);

        float content;

        try
        {
            var width = max.X - min.X - (32f * scale);
            var origin = new Vector2(min.X + (16f * scale), min.Y + (4f * scale) - offset);
            content = DrawContent(origin, width) + (20f * scale);
        }
        finally
        {
            ImGui.PopClipRect();
        }

        scroll.End(min, bodyMax, content);
        DrawFoot(new Vector2(min.X, bodyMax.Y), new Vector2(max.X, max.Y));
        ImGui.SetScrollY(0f);
    }

    private float DrawContent(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var y = origin.Y;

        if (Service.Penumbra is not { Available: true })
        {
            var reason = Service.Penumbra?.UnavailableReason is { Length: > 0 } text ? text : L.PenumbraNotRunning.Text;
            y += SilkSettingsKit.Notice(SilkNoticeKind.Bad, Paragraphs(reason), new Vector2(origin.X, y), width);
            return y - origin.Y;
        }

        Refill();

        y += SilkText.DrawParagraph(new Vector2(origin.X + (2f * scale), y + (4f * scale)), Lead, 12.5f, SilkWeight.Medium, SilkPalette.Ink2, width - (4f * scale), 1.55f) + (6f * scale);

        y += 14f * scale;
        y += DrawCompose(new Vector2(origin.X, y), width);

        y += DrawRaceSection(new Vector2(origin.X, y), width);

        y += SilkSettingsKit.Section(L.ModLabel.Text, new Vector2(origin.X, y), width);

        var name = modName;

        if (SilkControls.Field("##silkmodname", ref name, new Vector2(origin.X, y), width, NamePlaceholder, SilkIcon.Tag, SilkControls.FieldHeight, 64))
        {
            modName = name;
            autoName = null;
            doneText = null;
            failedText = null;
        }

        y += SilkControls.FieldHeight * scale;
        y += 10f * scale;

        var rows = new SilkSettingRows(new Vector2(origin.X, y), width);
        var enableRow = rows.Row(EnableName, EnableHelp, SilkControls.SwitchHeight);

        if (SilkControls.Switch("##silkmodenable", ref enableOnCreation, enableRow.RightAligned(SilkControls.SwitchWidth)))
            doneText = null;

        var priorityRow = rows.Row(PriorityName, PriorityHelp, SilkControls.SwitchHeight);

        if (SilkControls.Switch("##silkmodpriority", ref highestPriority, priorityRow.RightAligned(SilkControls.SwitchWidth)))
            doneText = null;

        y += rows.End();

        RefreshWarnings();

        foreach (var warning in warnings)
        {
            y += 10f * scale;
            y += DrawWarning(new Vector2(origin.X, y), width, warning);
        }

        if (failedText != null)
        {
            y += 10f * scale;
            y += SilkSettingsKit.Notice(SilkNoticeKind.Bad, Paragraphs(failedText), new Vector2(origin.X, y), width);
        }

        if (doneText != null)
        {
            y += 12f * scale;
            y += DrawDone(new Vector2(origin.X, y), width, doneText);
        }

        return y - origin.Y;
    }

    private float DrawCompose(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var arrow = 34f * scale;
        var gap = 8f * scale;
        var slotWidth = MathF.Floor((width - arrow - (gap * 2f)) * 0.5f);
        var labelHeight = SilkControls.HelpSize * scale;
        var pickHeight = 96f * scale;
        var height = labelHeight + (6f * scale) + pickHeight;

        DrawSlot(new Vector2(origin.X, origin.Y), slotWidth, labelHeight, pickHeight, true);
        DrawSlot(new Vector2(origin.X + slotWidth + arrow + (gap * 2f), origin.Y), width - slotWidth - arrow - (gap * 2f), labelHeight, pickHeight, false);

        var arrowSize = 22f * scale;
        var arrowMin = new Vector2(origin.X + slotWidth + gap + ((arrow - arrowSize) * 0.5f), origin.Y + labelHeight + (6f * scale) + (18f * scale) + ((pickHeight - (18f * scale) - arrowSize) * 0.5f));
        SilkIcons.Draw(SilkIcon.ArrowLeft, arrowMin, arrowSize, SilkPalette.Ice);

        return height;
    }

    private void DrawSlot(Vector2 origin, float width, float labelHeight, float pickHeight, bool isSource)
    {
        var scale = SilkUi.Scale;
        var entry = isSource ? source : target;
        var label = isSource ? SourceLabel : TargetLabel;

        SilkText.DrawInBox(new Vector2(origin.X, origin.Y), new Vector2(origin.X + width, origin.Y + labelHeight), SilkFonts.Upper(label), 10f, SilkWeight.Bold, SilkPalette.Ink3, UiAlign.Start, 1f);
        SilkControls.HelpMark(isSource ? "##silksourcehelp" : "##silktargethelp", isSource ? SourceHelp : TargetHelp,
            new Vector2(origin.X + width - (SilkControls.HelpSize * scale * 0.5f), origin.Y + (labelHeight * 0.5f)));

        var min = new Vector2(origin.X, origin.Y + labelHeight + (6f * scale));
        var max = new Vector2(origin.X + width, min.Y + pickHeight);
        var radius = 12f * scale;
        var clicked = SilkSettingsKit.Hit(isSource ? "##silkmodsourcepick" : "##silkmodtargetpick", min, max, out var hovered);
        var vivid = entry == null ? SilkPalette.Ice : SilkSettingsKit.Vivid(entry.IconId);

        if (entry == null)
        {
            SilkPaint.Fill(min, max, SilkPalette.Panel, radius);
            SilkPaint.InsetRing(min, max, hovered ? SilkPalette.Rgb(191, 211, 236, 0.45f) : SilkPalette.Line2, radius, scale);
        }
        else
        {
            SilkPaint.Fill(min, max, SilkPalette.Panel, radius);
            SilkPaint.AngleGradient(min, max, 135f, SilkPalette.Alpha(vivid, 0.16f), SilkPalette.Alpha(vivid, 0f), radius);
            SilkPaint.InsetRing(min, max, SilkPalette.Alpha(vivid, hovered ? 0.6f : 0.45f), radius, scale);
        }

        var icon = 52f * scale;
        var iconMin = new Vector2(min.X + (12f * scale), MathF.Round(((min.Y + max.Y) * 0.5f) - (icon * 0.5f)));
        var iconMax = iconMin + new Vector2(icon, icon);
        var chevron = 14f * scale;
        var chevronX = max.X - (12f * scale) - chevron;

        SilkIcons.Draw(SilkIcon.Chevron, new Vector2(chevronX, MathF.Round(((min.Y + max.Y) * 0.5f) - (chevron * 0.5f))), chevron, SilkPalette.Ink3);

        var textLeft = iconMin.X + icon + (12f * scale);
        var textRight = chevronX - (10f * scale);

        if (entry == null)
        {
            SilkPaint.InsetRing(iconMin, iconMax, SilkPalette.Rgb(191, 211, 236, 0.3f), 12f * scale, scale);
            SilkIcons.DrawCentered(SilkIcon.Plus, (iconMin + iconMax) * 0.5f, 18f * scale, SilkPalette.Ink3);
            SilkText.DrawInBox(new Vector2(textLeft, min.Y), new Vector2(textRight, max.Y), isSource ? SourcePlaceholder : TargetPlaceholder, 13f, SilkWeight.SemiBold, SilkPalette.Ink3, UiAlign.Start, 0f, false, 0f);
        }
        else
        {
            SilkPaint.BoxShadow(iconMin, iconMax, new Vector2(0f, 8f * scale), 20f * scale, -10f * scale, SilkPalette.Alpha(vivid, 0.8f), 12f * scale);
            SilkSettingsKit.Glyph(iconMin, iconMax, entry.IconId, 12f * scale);

            var nameLine = SilkText.NaturalLine(15f, SilkWeight.ExtraBold);
            var codeLine = SilkText.NaturalLine(11f, SilkWeight.Regular, true);
            var chipHeight = SilkText.NaturalLine(10.5f, SilkWeight.SemiBold) + (4f * scale);
            var block = nameLine + codeLine + (6f * scale) + chipHeight;
            var top = MathF.Round(((min.Y + max.Y) * 0.5f) - (block * 0.5f));

            SilkText.Draw(new Vector2(textLeft, top), entry.Name, 15f, SilkWeight.ExtraBold, SilkPalette.Ink, 0f, false, MathF.Max(1f, textRight - textLeft));

            var command = entry.Commands.Length > 0 ? entry.Commands[0] : string.Empty;

            if (command.Length > 0)
                SilkText.Draw(new Vector2(textLeft, top + nameLine), command, 11f, SilkWeight.Regular, SilkPalette.Ink3, 0f, true, MathF.Max(1f, textRight - textLeft));

            var chip = isSource ? sourceAnimation ?? VanillaAnimation : entry.Owned ? OwnedChip : NotUnlockedChip;
            var modded = isSource && sourceAnimationModded;
            var chipWidth = SilkText.Width(chip, 10.5f, SilkWeight.SemiBold) + (14f * scale);
            var chipMin = new Vector2(textLeft, top + nameLine + codeLine + (6f * scale));
            var chipMax = chipMin + new Vector2(MathF.Min(chipWidth, MathF.Max(20f, textRight - textLeft)), chipHeight);

            SilkPaint.Fill(chipMin, chipMax, modded ? SilkPalette.Rgb(158, 140, 255, 0.16f) : SilkPalette.Rgb(191, 211, 236, 0.07f), chipHeight * 0.5f);
            SilkText.DrawInBox(chipMin + new Vector2(7f * scale, 0f), chipMax - new Vector2(7f * scale, 0f), chip, 10.5f, SilkWeight.SemiBold,
                modded ? SilkPalette.VioLight : SilkPalette.Ink3, UiAlign.Start, 0f, false, 0f);
        }

        if (clicked)
        {
            var picker = isSource ? sourcePicker : targetPicker;
            picker.Filter = static candidate => !candidate.Invalid;
            picker.Open();
        }

        var chosen = isSource
            ? sourcePicker.Draw(min, max, max.X - min.X, false)
            : targetPicker.Draw(min, max, max.X - min.X, false);

        if (chosen is not { } rowId)
            return;

        if (isSource)
            source = SilkEmoteIndex.Get(rowId);
        else
            target = SilkEmoteIndex.Get(rowId);

        doneText = null;
        failedText = null;
        AutoName();
    }

    private float DrawRaceSection(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var y = origin.Y;
        var line = 18f * scale;
        var top = y + (20f * scale);
        var title = SilkFonts.Upper(L.RacesCovered.Text);
        var titleWidth = SilkText.Width(title, 11f, SilkWeight.Bold, 1f);

        SilkText.DrawInBox(new Vector2(origin.X + (2f * scale), top), new Vector2(origin.X + width, top + line), title, 11f, SilkWeight.Bold, SilkPalette.SectText, UiAlign.Start, 1f);

        var allWidth = SilkText.Width(L.All.Text, 11f, SilkWeight.SemiBold) + (16f * scale);
        var noneWidth = SilkText.Width(L.None.Text, 11f, SilkWeight.SemiBold) + (16f * scale);
        var allMin = new Vector2(origin.X + (2f * scale) + titleWidth + (10f * scale), top);
        var noneMin = new Vector2(allMin.X + allWidth + (4f * scale), top);

        if (ToolButton("##silkraceall", L.All.Text, allMin, allWidth))
        {
            picked.Clear();

            foreach (var race in RaceNames)
            {
                if (Available(race))
                    picked.Add(race);
            }

            doneText = null;
        }

        if (ToolButton("##silkracenone", L.None.Text, noneMin, noneWidth))
        {
            picked.Clear();
            doneText = null;
        }

        var ruleX = noneMin.X + noneWidth + (10f * scale);
        var ruleY = MathF.Round(top + (line * 0.5f));
        var right = origin.X + width - (2f * scale);

        if (right > ruleX)
            SilkPaint.HorizontalGradient(new Vector2(ruleX, ruleY), new Vector2(right, ruleY + scale), SilkPalette.Line2, SilkPalette.Alpha(SilkPalette.Line2, 0f), 0f);

        y = top + line + (8f * scale);

        var gap = 6f * scale;
        var cell = (width - (gap * 3f)) / 4f;
        var rowHeight = 32f * scale;

        for (var i = 0; i < RaceNames.Length; i++)
        {
            var race = RaceNames[i];
            var column = i % 4;
            var row = i / 4;
            var min = new Vector2(origin.X + ((cell + gap) * column), y + ((rowHeight + gap) * row));
            var max = min + new Vector2(cell, rowHeight);
            var available = Available(race);
            var on = picked.Contains(race);

            ImGui.PushID(i);
            var clicked = SilkSettingsKit.Hit("race", min, max, out var hovered);
            ImGui.PopID();

            if (clicked && available)
            {
                if (!picked.Remove(race))
                    picked.Add(race);

                doneText = null;
            }

            var alpha = available ? 1f : 0.35f;
            var radius = 8f * scale;

            SilkPaint.Fill(min, max, SilkPalette.Fade(on ? SilkPalette.Rgb(191, 211, 236, 0.08f) : SilkPalette.Rgb(0, 0, 0, 0.25f), alpha), radius);
            SilkPaint.InsetRing(min, max, SilkPalette.Fade(on ? SilkPalette.Rgb(191, 211, 236, 0.35f) : SilkPalette.Line, alpha), radius, scale);

            var box = 14f * scale;
            var boxMin = new Vector2(min.X + (10f * scale), MathF.Round(((min.Y + max.Y) * 0.5f) - (box * 0.5f)));
            var boxMax = boxMin + new Vector2(box, box);

            if (on)
            {
                SilkPaint.Fill(boxMin, boxMax, SilkPalette.Fade(SilkPalette.Ice, alpha), 4f * scale);
                SilkIcons.DrawCentered(SilkIcon.Check, (boxMin + boxMax) * 0.5f, 9f * scale, SilkPalette.Fade(SilkPalette.PriText, alpha));
            }
            else
            {
                SilkPaint.InsetRing(boxMin, boxMax, SilkPalette.Fade(SilkPalette.Line2, alpha), 4f * scale, 1.5f * scale);
            }

            SilkText.DrawInBox(new Vector2(boxMax.X + (8f * scale), min.Y), new Vector2(max.X - (8f * scale), max.Y), race, 12f, SilkWeight.SemiBold,
                SilkPalette.Fade(on ? SilkPalette.Ink : SilkPalette.Ink3, alpha), UiAlign.Start, 0f, false, 0f);

            if (!available && hovered)
                SilkTooltip.Hover(NoAnimationTooltip, min, max);
        }

        var rows = (RaceNames.Length + 3) / 4;
        return y + (rows * (rowHeight + gap)) - gap - origin.Y;
    }

    private static bool ToolButton(string id, string label, Vector2 min, float width)
    {
        var scale = SilkUi.Scale;
        var max = min + new Vector2(width, 18f * SilkUi.Scale);
        var clicked = SilkSettingsKit.Hit(id, min, max, out var hovered);

        if (hovered)
            SilkPaint.Fill(min, max, SilkPalette.HoverWash, 6f * scale);

        SilkText.DrawInBox(min, max, label, 11f, SilkWeight.SemiBold, hovered ? SilkPalette.Ink : SilkPalette.Ink3, UiAlign.Center);
        return clicked;
    }

    private float DrawWarning(Vector2 origin, float width, string text)
    {
        var scale = SilkUi.Scale;
        var inner = width - (28f * scale) - (24f * scale);
        var textHeight = SilkText.ParagraphHeight(text, 12f, SilkWeight.Medium, inner, 1.5f);
        var height = textHeight + (24f * scale);
        var max = origin + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(origin, max, SilkPalette.WarnBg, radius);
        SilkPaint.InsetRing(origin, max, SilkPalette.WarnRing, radius, scale);
        SilkIcons.Draw(SilkIcon.Warn, new Vector2(origin.X + (14f * scale), origin.Y + (14f * scale)), 14f * scale, SilkPalette.WarnText);
        SilkText.DrawParagraph(new Vector2(origin.X + (14f * scale) + (24f * scale), origin.Y + (12f * scale)), text, 12f, SilkWeight.Medium, SilkPalette.WarnText, inner, 1.5f);

        return height;
    }

    private float DrawDone(Vector2 origin, float width, string text)
    {
        var scale = SilkUi.Scale;
        var inner = width - (28f * scale) - (26f * scale);
        var textHeight = SilkText.ParagraphHeight(text, 12.5f, SilkWeight.Medium, inner, 1.5f);
        var height = textHeight + (24f * scale);
        var max = origin + new Vector2(width, height);
        var radius = 12f * scale;

        SilkPaint.Fill(origin, max, SilkPalette.DoneBg, radius);
        SilkPaint.InsetRing(origin, max, SilkPalette.DoneRing, radius, scale);
        SilkIcons.Draw(SilkIcon.Ok, new Vector2(origin.X + (14f * scale), origin.Y + (12f * scale)), 16f * scale, SilkPalette.Ok);
        SilkText.DrawParagraph(new Vector2(origin.X + (14f * scale) + (26f * scale), origin.Y + (12f * scale)), text, 12.5f, SilkWeight.Medium, SilkPalette.DoneText, inner, 1.5f);

        return height;
    }

    private void DrawFoot(Vector2 min, Vector2 max)
    {
        var scale = SilkUi.Scale;

        SilkPaint.Fill(min, max, SilkPalette.Rgb(0, 0, 0, 0.18f), 0f);
        SilkPaint.Fill(min, new Vector2(max.X, min.Y + scale), SilkPalette.Line, 0f);

        var why = Why();
        var buttonWidth = SilkControls.ButtonWidth(CreateLabel, SilkIcon.Plus);
        var buttonMin = new Vector2(max.X - (16f * scale) - buttonWidth, min.Y + (13f * scale));

        if (why != null)
            SilkText.DrawInBox(new Vector2(min.X + (16f * scale), min.Y), new Vector2(buttonMin.X - (10f * scale), max.Y - (2f * scale)), why, 12f, SilkWeight.Medium, SilkPalette.Ink3, UiAlign.Start, 0f, false, 0f);

        if (SilkControls.Button(CreateLabel, buttonMin, SilkButtonKind.Primary, SilkIcon.Plus, why != null))
            Create();
    }

    private string? Why()
    {
        if (Service.Penumbra is not { Available: true })
            return L.PenumbraNotRunning.Text;

        if (source == null || target == null || PermanentModBuilder.CleanName(modName).Length == 0)
            return WhyBothEmotes;

        if (source.RowId == target.RowId)
            return WhySameEmote;

        return picked.Count == 0 ? WhyRaces : null;
    }

    private void Create()
    {
        if (source == null || target == null)
            return;

        if (Service.Catalog is not { Ready: true } catalog)
        {
            Report(false, L.EmoteDataLoading.Text);
            return;
        }

        if (catalog.Get(source.RowId) is not { } sourceAttributes || catalog.Get(target.RowId) is not { } targetAttributes)
        {
            Report(false, L.NoReadableAnimation.Text);
            return;
        }

        var skeletons = new List<string>(picked.Count);

        foreach (var race in RaceNames)
        {
            if (picked.Contains(race))
                skeletons.Add(SkeletonOf(race));
        }

        var outcome = PermanentModBuilder.Create(sourceAttributes, targetAttributes, skeletons, PermanentModBuilder.CleanName(modName), enableOnCreation, highestPriority);
        Report(outcome.Created, outcome.Message);
    }

    private void Report(bool good, string message)
    {
        doneText = good ? message : null;
        failedText = good ? null : message;
    }

    private void AutoName()
    {
        if (source == null || target == null)
            return;

        if (modName.Length != 0 && !ReferenceEquals(modName, autoName))
            return;

        autoName = L.AutoModName.With("source", source.Name, "target", target.Name);
        modName = autoName;
    }

    private void Refill()
    {
        var pair = source != null && target != null ? ((uint, uint)?)(source.RowId, target.RowId) : null;

        if (pair == filledFor)
            return;

        filledFor = pair;
        pathsByRace.Clear();
        moddedByRace.Clear();
        picked.Clear();
        warningsFor = null;

        foreach (var race in RaceNames)
        {
            if (Available(race))
                picked.Add(race);
        }

        sourceAnimation = null;
        sourceAnimationModded = false;

        if (source == null || Service.Catalog is not { Ready: true } catalog || catalog.Get(source.RowId) is not { } attributes
            || Service.Orchestrator is not { } orchestrator || NoireService.ObjectTable.LocalPlayer is not { } player)
        {
            return;
        }

        if (orchestrator.ModServingAnimation(attributes, SwapOrchestrator.SkeletonFor(player)) is { } modName)
        {
            sourceAnimation = L.ModdedAnimation.With("mod", modName);
            sourceAnimationModded = true;
        }
        else
        {
            sourceAnimation = VanillaAnimation;
        }
    }

    private bool Available(string race) => PathsFor(race) != null;

    private RacePaths? PathsFor(string race)
    {
        if (pathsByRace.TryGetValue(race, out var known))
            return known;

        RacePaths? paths = null;

        if (Service.Orchestrator is { } orchestrator && Service.Catalog is { Ready: true } catalog
            && source != null && target != null
            && catalog.Get(source.RowId) is { } sourceAttributes && catalog.Get(target.RowId) is { } targetAttributes)
        {
            paths = orchestrator.PathsFor(sourceAttributes, targetAttributes, SkeletonOf(race));
        }

        pathsByRace[race] = paths;
        return paths;
    }

    private bool ModdedFor(string race)
    {
        if (moddedByRace.TryGetValue(race, out var known))
            return known;

        var modded = false;

        if (Service.Orchestrator is { } orchestrator && PathsFor(race) is { } paths)
        {
            foreach (var path in paths.SourcePaths)
            {
                if (orchestrator.ForeignModServes(path))
                {
                    modded = true;
                    break;
                }
            }
        }

        moddedByRace[race] = modded;
        return modded;
    }

    private void RefreshWarnings()
    {
        var pair = source != null && target != null ? ((uint, uint)?)(source.RowId, target.RowId) : null;
        var revision = picked.Count;

        if (warningsFor == pair && warningsRevision == revision)
            return;

        warningsFor = pair;
        warningsRevision = revision;
        warnings.Clear();

        if (source == null || target == null || source.RowId == target.RowId
            || Service.Catalog is not { Ready: true } catalog
            || catalog.Get(source.RowId) is not { } sourceAttributes
            || catalog.Get(target.RowId) is not { } targetAttributes)
        {
            return;
        }

        var sourceName = SwapOrchestrator.NameOf(source.RowId);
        var targetName = SwapOrchestrator.NameOf(target.RowId);

        if (!EmoteHelper.IsEmoteUnlocked(target.RowId))
            warnings.Add(L.NotUnlockedTarget.With("target", targetName));

        if (Service.Orchestrator is { } orchestrator && NoireService.ObjectTable.LocalPlayer is { } player
            && orchestrator.ModServingAnimation(targetAttributes, SwapOrchestrator.SkeletonFor(player)) is { Length: > 0 } modName)
        {
            warnings.Add(L.ModChangesTarget.With("mod", modName, "target", targetName));
        }

        foreach (var line in SwapAdvice.Behaviour(sourceAttributes, sourceName, targetAttributes, targetName))
            warnings.Add(line.Text);

        if (picked.Count == 0)
            return;

        var ownSkeleton = NoireService.ObjectTable.LocalPlayer is { } localPlayer ? SwapOrchestrator.SkeletonFor(localPlayer) : null;
        var plan = RaceCoveragePlanner.For(RaceNames, picked, PathsFor, ModdedFor,
            race => string.Equals(SkeletonOf(race), ownSkeleton, StringComparison.OrdinalIgnoreCase));

        foreach (var shared in plan.Shared)
            warnings.Add(L.SharedAnimationFile.With("losers", string.Join(", ", shared.Losers), "winner", shared.Winner));

        if (plan.AlsoReached.Count > 0)
            warnings.Add(L.AlsoReached.With("races", string.Join(", ", plan.AlsoReached)));
    }

    private static SilkParagraph[] Paragraphs(string text) => [new(text)];

    private static string SkeletonOf(string race)
    {
        foreach (var entry in RaceGenderData.AllRaces)
        {
            if (entry.Name == race)
                return entry.Id;
        }

        return string.Empty;
    }

    private static string[] BuildRaceNames()
    {
        var names = new string[RaceGenderData.AllRaces.Count];

        for (var i = 0; i < names.Length; i++)
            names[i] = RaceGenderData.AllRaces[i].Name;

        return names;
    }
}
