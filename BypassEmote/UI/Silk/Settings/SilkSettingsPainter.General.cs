using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.Localizer;
using NoireLib.UI;
using NoireLib.UpdateTracker;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Settings;

internal sealed partial class SilkSettingsPainter
{
    private static string PluginEnabledName => L.PluginEnabled.Text;
    private static string HotbarBypassName => L.HotbarBypass.Text;
    private static string LockedEmotesInWindowName => L.SilkLockedInWindow.Text;
    private static string LockedEmotesName => L.LockedAsUsable.Text;
    private static string StopOnMoveName => L.StopOnMove.Text;
    private static string PreviewPopupName => L.PreviewPopupSetting.Text;
    private static string UpdateNotificationName => L.UpdateNotification.Text;
    private static string ChangelogName => L.ChangelogOnUpdate.Text;

    private static string PluginEnabledHelp => L.PluginEnabledHelp.Text;

    private static string HotbarBypassHelp => L.HotbarBypassHelp.Text;

    private static string LockedEmotesInWindowHelp => L.LockedInWindowHelp.Text;

    private static string LockedEmotesHelp => L.LockedAsUsableHelp.Text;

    private static string StopOnMoveHelp => L.StopOnMoveHelp.Text;

    private static string PreviewPopupHelp => L.PreviewPopupSettingHelp.Text;

    private static string UpdateNotificationHelp => L.UpdateNotificationHelp.Text;

    private static string ChangelogHelp => L.ChangelogOnUpdateHelp.Text;

    private static string NewInterfaceTitle => L.SkinSilk.Text;
    private static string NewInterfaceText => L.NewInterfaceText.Text;
    private static string ClassicInterfaceTitle => L.SkinClassic.Text;
    private static string ClassicInterfaceText => L.ClassicInterfaceText.Text;

    private static readonly IdLabel TranslateLabel = new(L.Translate, "##silktranslate");

    private float DrawGeneral(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var y = origin.Y;

        y += SilkSettingsKit.Section(L.SectionInterface.Text, new Vector2(origin.X, y), width);
        y += DrawInterfaceCards(new Vector2(origin.X, y), width);
        y += DrawLanguageRows(new Vector2(origin.X, y), width);

        y += SilkSettingsKit.Section(L.SectionPlugin.Text, new Vector2(origin.X, y), width);

        var rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var pluginEnabled = Configuration.PluginEnabled;
        if (SwitchRow(ref rows, "##silkplugin", PluginEnabledName, PluginEnabledHelp, ref pluginEnabled))
            Configuration.PluginEnabled = pluginEnabled;

        var hotbarBypass = Configuration.BypassOnHotbarSlotTriggered;
        if (SwitchRow(ref rows, "##silkhotbarbypass", HotbarBypassName, HotbarBypassHelp, ref hotbarBypass))
            Configuration.BypassOnHotbarSlotTriggered = hotbarBypass;

        var lockedInWindow = Configuration.ShowLockedEmotesInGameWindow;
        if (SwitchRow(ref rows, "##silklockedwindow", LockedEmotesInWindowName, LockedEmotesInWindowHelp, ref lockedInWindow))
            Configuration.ShowLockedEmotesInGameWindow = lockedInWindow;

        var lockedAsUsable = Configuration.ShowLockedEmotesAsUsable;
        if (SwitchRow(ref rows, "##silklockedlit", LockedEmotesName, LockedEmotesHelp, ref lockedAsUsable))
            Configuration.ShowLockedEmotesAsUsable = lockedAsUsable;

        var stopOnMove = Configuration.StopOwnedObjectEmoteOnMove;
        if (SwitchRow(ref rows, "##silkstoponmove", StopOnMoveName, StopOnMoveHelp, ref stopOnMove))
        {
            Configuration.StopOwnedObjectEmoteOnMove = stopOnMove;
            IpcHelper.NotifyConfigChanged();
        }

        y += rows.End();

        y += SilkSettingsKit.Section(L.SectionEmoteList.Text, new Vector2(origin.X, y), width);

        rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var preview = Configuration.EnablePreviewPopup;
        if (SwitchRow(ref rows, "##silkpreview", PreviewPopupName, PreviewPopupHelp, ref preview))
            Configuration.EnablePreviewPopup = preview;

        var compact = Configuration.CompactEmoteList;
        if (SwitchRow(ref rows, "##silkcompact", L.CompactRows.Text, L.CompactRowsHelp.Text, ref compact))
            Configuration.CompactEmoteList = compact;

        y += rows.End();

        y += SilkSettingsKit.Section(L.SectionUpdates.Text, new Vector2(origin.X, y), width);

        rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var updateNotification = Configuration.ShowUpdateNotification;
        if (SwitchRow(ref rows, "##silkupdates", UpdateNotificationName, UpdateNotificationHelp, ref updateNotification))
        {
            Configuration.ShowUpdateNotification = updateNotification;
            var updateTracker = NoireLibMain.GetModule<NoireUpdateTracker>();
            updateTracker?.SetShouldShowNotificationOnUpdate(Configuration.ShowUpdateNotification);
            updateTracker?.SetShouldPrintMessageInChatOnUpdate(Configuration.ShowUpdateNotification);
        }

        var changelog = Configuration.ShowChangelogOnUpdate;
        if (SwitchRow(ref rows, "##silkchangelog", ChangelogName, ChangelogHelp, ref changelog))
        {
            Configuration.ShowChangelogOnUpdate = changelog;
            NoireLibMain.GetModule<NoireChangelogManager>()?.SetAutomaticallyShowChangelog(Configuration.ShowChangelogOnUpdate);
        }

        y += rows.End();

        y += SilkSettingsKit.Section("???", new Vector2(origin.X, y), width);

        rows = new SilkSettingRows(new Vector2(origin.X, y), width);

        var malou = Configuration.AdoptMalou;
        if (SwitchRow(ref rows, "##silkmalou", L.AdoptMalou.Text, L.AdoptMalouHelp.Text, ref malou))
            Configuration.AdoptMalou = malou;

        y += rows.End();

        return y - origin.Y;
    }

    private static bool SwitchRow(ref SilkSettingRows rows, string id, string name, string help, ref bool value, string? alarm = null, bool danger = false)
    {
        var row = rows.Row(name, help, SilkControls.SwitchHeight, alarm);
        return SilkControls.Switch(id, ref value, row.RightAligned(SilkControls.SwitchWidth), danger);
    }

    private static float DrawLanguageRows(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var rows = new SilkSettingRows(new Vector2(origin.X, origin.Y + (10f * scale)), width);

        var languageRow = rows.Row(L.Language.Text, L.LanguageHelp.Text, SilkControls.ComboHeight);
        var language = NoireLanguagePicker.Active;

        if (SilkControls.Combo("##silklanguage", ref language, LanguageItems(), languageRow.ControlMin, languageRow.ControlWidth))
            NoireLanguagePicker.Pick(language);

        var translateRow = rows.Row(L.Translation.Text, L.TranslationHelp.Text, SilkControls.ButtonHeight);

        if (SilkControls.Button(TranslateLabel.Text, translateRow.ControlMin, SilkButtonKind.Normal, null, false, translateRow.ControlWidth))
            NoireTranslationEditor.Open();

        var credits = NoireLanguages.CreditLines;

        if (credits.Count > 0)
        {
            var creditsRow = rows.Row(NoireStrings.TranslationCredits.Text, null, SilkControls.ButtonHeight);
            var label = showCredits ? NoireStrings.Hide.Text : NoireStrings.Show.Text;

            if (SilkControls.Button(label, creditsRow.ControlMin, SilkButtonKind.Normal, SilkIcon.Chevron, false, creditsRow.ControlWidth))
                showCredits = !showCredits;

            if (showCredits)
            {
                foreach (var line in credits)
                    rows.Note(line);
            }
        }

        return (10f * scale) + rows.End();
    }

    private static bool showCredits;

    private static SilkComboItem[] languageItems = [];
    private static string[]? languageItemsFor;

    private static SilkComboItem[] LanguageItems()
    {
        var names = NoireLanguagePicker.Names;

        if (ReferenceEquals(names, languageItemsFor))
            return languageItems;

        languageItemsFor = names;
        languageItems = new SilkComboItem[names.Length];

        for (var i = 0; i < names.Length; i++)
            languageItems[i] = new SilkComboItem(names[i]);

        return languageItems;
    }

    private float DrawInterfaceCards(Vector2 origin, float width)
    {
        var scale = SilkUi.Scale;
        var gap = 10f * scale;
        var cardWidth = MathF.Floor((width - gap) * 0.5f);
        var titleLine = SilkText.NaturalLine(13f, SilkWeight.Bold);
        var textLine = SilkText.LineBox(11.5f, 1.4f);
        var height = (10f * scale) + (56f * scale) + (9f * scale) + titleLine + (2f * scale) + textLine + (10f * scale);

        for (var i = 0; i < 2; i++)
        {
            var classic = i == 1;
            var min = new Vector2(origin.X + ((cardWidth + gap) * i), origin.Y);
            var max = min + new Vector2(i == 1 ? width - cardWidth - gap : cardWidth, height);
            var radius = 12f * scale;

            if (SilkSettingsKit.Hit(classic ? "##silkuiclassic" : "##silkuinew", min, max, out _) && classic)
                NoireUI.RunOnDraw(static () => Service.Plugin.SetClassicInterface(true));

            if (!classic)
            {
                SilkPaint.Fill(min, max, SilkPalette.Rgb(191, 211, 236, 0.08f), radius);
                SilkPaint.InsetRing(min, max, SilkPalette.Rgb(191, 211, 236, 0.5f), radius, scale);
            }
            else
            {
                SilkPaint.Fill(min, max, SilkPalette.Panel, radius);
                SilkPaint.InsetRing(min, max, SilkPalette.Line, radius, scale);
            }

            var pvMin = min + new Vector2(10f * scale, 10f * scale);
            var pvMax = new Vector2(max.X - (10f * scale), pvMin.Y + (56f * scale));

            if (classic)
                SilkInterfaceArt.Classic(pvMin, pvMax, scale);
            else
                SilkInterfaceArt.New(pvMin, pvMax, scale);

            var textTop = pvMax.Y + (9f * scale);
            var textRoom = pvMax.X - pvMin.X;
            SilkText.Draw(new Vector2(pvMin.X, SilkText.GlyphTop(textTop, titleLine, 13f, SilkWeight.Bold)), classic ? ClassicInterfaceTitle : NewInterfaceTitle, 13f, SilkWeight.Bold, SilkPalette.Ink, maxWidth: textRoom);

            var pTop = textTop + titleLine + (2f * scale);
            SilkText.Draw(new Vector2(pvMin.X, SilkText.GlyphTop(pTop, textLine, 11.5f, SilkWeight.Regular, 1.4f)), classic ? ClassicInterfaceText : NewInterfaceText, 11.5f, SilkWeight.Regular, SilkPalette.Ink3, maxWidth: textRoom);
        }

        return height;
    }
}

internal static class SilkInterfaceArt
{
    private static readonly float[] BandRows = [-12f, -6f, 0f, 6f, 11f, 16f, 22f, 28f, 34f];
    private static readonly float[] BandAlpha = [0.023f, 0.159f, 0.5f, 0.841f, 0.934f, 0.841f, 0.5f, 0.159f, 0.023f];
    private static readonly float[] BandStops = [0f, 1f / 3f, 2f / 3f, 1f];

    internal static void New(Vector2 min, Vector2 max, float scale)
    {
        var radius = 8f * scale;
        SilkPaint.AngleGradient(min, max, 160f, SilkPalette.Hex(0x0c1426), SilkPalette.Hex(0x060910), radius);

        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        drawList.PushClipRect(min, max, true);
        Band(drawList, min, max, scale);

        var stripe = SilkPalette.Rgb(191, 211, 236, 0.16f);
        var top = min.Y + (8f * scale);
        var bottom = max.Y - (8f * scale);

        for (var y = top; y < bottom - 0.5f; y += 11f * scale)
            SilkPaint.Fill(new Vector2(min.X + (8f * scale), y), new Vector2(max.X - (8f * scale), MathF.Min(bottom, y + (6f * scale))), stripe, 0f);

        drawList.PopClipRect();
    }

    internal static void Classic(Vector2 min, Vector2 max, float scale)
    {
        var radius = 8f * scale;
        SilkPaint.Fill(min, max, SilkPalette.Hex(0x1f1f1f), radius);

        var drawList = NoireShapes.DrawList;

        if (drawList.IsNull)
            return;

        drawList.PushClipRect(min, max, true);
        SilkPaint.Fill(min, new Vector2(max.X, min.Y + (10f * scale)), SilkPalette.Hex(0x2c2c2c), radius, RectCorners.Top);
        SilkPaint.Fill(new Vector2(min.X, min.Y + (9f * scale)), new Vector2(max.X, min.Y + (10f * scale)), SilkPalette.Hex(0x444444), 0f);

        var stripe = SilkPalette.Hex(0x3a3a3a);
        var top = min.Y + (18f * scale);
        var bottom = max.Y - (8f * scale);

        for (var y = top; y < bottom - 0.5f; y += 10f * scale)
            SilkPaint.Fill(new Vector2(min.X + (8f * scale), y), new Vector2(max.X - (8f * scale), MathF.Min(bottom, y + (5f * scale))), stripe, 0f);

        drawList.PopClipRect();
        SilkPaint.InsetRing(min, max, SilkPalette.Hex(0x3a3a3a), radius, scale);
    }

    private static void Band(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var columns = BandStops.Length;
        var rows = BandRows.Length;
        var width = max.X - min.X;
        var centreX = (min.X + max.X) * 0.5f;
        var top = min.Y + (18f * scale);
        var shear = 0.140541f;
        var uv = ImGui.GetFontTexUvWhitePixel();
        var vio = SilkPalette.Vio;
        var ice = SilkPalette.Ice;

        drawList.PrimReserve((columns - 1) * (rows - 1) * 6, columns * rows);
        var baseIndex = (uint)drawList.VtxCurrentIdx;

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                var x = min.X + (width * BandStops[c]);
                var y = top + (BandRows[r] * scale) - (shear * (x - centreX));
                var color = c <= 1 ? vio : ice;
                var alpha = c == 1 || c == 2 ? 0.5f * BandAlpha[r] : 0f;
                drawList.PrimWriteVtx(new Vector2(x, y), uv, SilkPalette.U32(new Vector4(color.X, color.Y, color.Z, alpha)));
            }
        }

        for (var r = 0; r < rows - 1; r++)
        {
            for (var c = 0; c < columns - 1; c++)
            {
                var a = (ushort)(baseIndex + (uint)((r * columns) + c));
                var b = (ushort)(a + 1);
                var d = (ushort)(a + columns);
                var e = (ushort)(d + 1);
                drawList.PrimWriteIdx(a);
                drawList.PrimWriteIdx(b);
                drawList.PrimWriteIdx(e);
                drawList.PrimWriteIdx(a);
                drawList.PrimWriteIdx(e);
                drawList.PrimWriteIdx(d);
            }
        }
    }
}
