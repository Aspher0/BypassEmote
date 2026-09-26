using Dalamud.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Collections.Generic;

namespace BypassEmote.Helpers;

internal static class SheetLanguage
{
    private static ClientLanguage? forced;

    internal static ClientLanguage? Forced
    {
        get => forced;
        set
        {
            if (forced == value)
                return;

            forced = value;
            Version++;
        }
    }

    internal static int Version { get; private set; }

    internal static Emote Display(Emote emote)
        => forced is { } language && ExcelSheetHelper.TryGetRow<Emote>(emote.RowId, out var row, language) && row is { } shown
            ? shown
            : emote;

    internal static void IncludeGlyphs()
    {
        var texts = new List<string>();

#if DEBUG
        foreach (var language in Enum.GetValues<ClientLanguage>())
            AddTexts(texts, ExcelSheetHelper.GetSheet<Emote>(language));
#else
        AddTexts(texts, ExcelSheetHelper.GetSheet<Emote>());
#endif

        NoireScriptFonts.Include("emotes", texts);
    }

    private static void AddTexts(List<string> texts, ExcelSheet<Emote>? sheet)
    {
        if (sheet == null)
            return;

        foreach (var emote in sheet)
        {
            texts.Add(emote.Name.ExtractText());

            if (emote.TextCommand.ValueNullable is not { } command)
                continue;

            texts.Add(command.Command.ExtractText());
            texts.Add(command.ShortCommand.ExtractText());
            texts.Add(command.Alias.ExtractText());
            texts.Add(command.ShortAlias.ExtractText());
        }
    }
}
