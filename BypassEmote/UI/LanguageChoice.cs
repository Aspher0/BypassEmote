using NoireLib.Localizer;
using NoireLib.UI;
using System;
using System.Collections.Generic;

namespace BypassEmote.UI;

internal static class LanguageChoice
{
    private static readonly List<NoireLanguageInfo> Offered = [];

    private static IReadOnlyList<NoireLanguageInfo>? source;
    private static string[] names = [];
    private static string? pending;

    internal static IReadOnlyList<NoireLanguageInfo> Languages
    {
        get
        {
            Refresh();
            return Offered;
        }
    }

    internal static string[] Names
    {
        get
        {
            Refresh();
            return names;
        }
    }

    internal static int Active
    {
        get
        {
            Refresh();

            for (var i = 0; i < Offered.Count; i++)
            {
                if (pending == null ? Offered[i].IsActive : string.Equals(Offered[i].Code, pending, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }
    }

    internal static void Pick(int index)
    {
        Refresh();

        if ((uint)index >= (uint)Offered.Count)
            return;

        if (pending != null)
            NoireScriptFonts.Unprepare(pending);

        pending = null;

        if (Offered[index].IsActive)
            return;

        pending = Offered[index].Code;
        NoireScriptFonts.Prepare(pending);
        Commit();
    }

    internal static void Commit()
    {
        if (pending == null || !NoireFont.GlyphsApplied)
            return;

        var code = pending;
        pending = null;

        NoireLanguages.Localizer?.SetCurrentLocale(code);
        NoireScriptFonts.Unprepare(code);
    }

    private static void Refresh()
    {
        var languages = NoireLanguages.Localizer?.Languages;

        if (ReferenceEquals(languages, source))
            return;

        source = languages;
        Offered.Clear();

        if (languages != null)
        {
            foreach (var language in languages)
            {
#if !DEBUG
                if (NoireLanguages.IsPseudo(language.Code))
                    continue;
#endif
                Offered.Add(language);
            }
        }

        names = new string[Offered.Count];

        for (var i = 0; i < Offered.Count; i++)
            names[i] = Offered[i].NativeName;
    }
}
