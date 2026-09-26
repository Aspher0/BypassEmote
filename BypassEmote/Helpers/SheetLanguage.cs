using Dalamud.Game;
using Lumina.Excel.Sheets;
using NoireLib.Helpers;
using NoireLib.UI;
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
#if DEBUG
        const bool everyLanguage = true;
#else
        const bool everyLanguage = false;
#endif

        NoireScriptFonts.IncludeSheet<Emote>("emotes", Texts, everyLanguage);
    }

    private static IEnumerable<string?> Texts(Emote emote)
    {
        yield return emote.Name.ExtractText();

        if (emote.TextCommand.ValueNullable is not { } command)
            yield break;

        yield return command.Command.ExtractText();
        yield return command.ShortCommand.ExtractText();
        yield return command.Alias.ExtractText();
        yield return command.ShortAlias.ExtractText();
    }
}
