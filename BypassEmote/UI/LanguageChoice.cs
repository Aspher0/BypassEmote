using NoireLib.Localizer;
using System.Collections.Generic;

namespace BypassEmote.UI;

internal static class LanguageChoice
{
    private static readonly List<NoireLanguageInfo> Offered = [];

    private static IReadOnlyList<NoireLanguageInfo>? source;
    private static string[] names = [];

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
                if (Offered[i].IsActive)
                    return i;
            }

            return 0;
        }
    }

    internal static void Pick(int index)
    {
        Refresh();

        if ((uint)index < (uint)Offered.Count)
            NoireLanguages.Localizer?.SetCurrentLocale(Offered[index].Code);
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
