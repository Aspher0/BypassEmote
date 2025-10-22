using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_5_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV1_5_0_0(),
        CreateV1_5_1_0(),
        CreateV1_5_1_1(),
    };

    private static ChangelogVersion CreateV1_5_0_0()
        => new ChangelogVersion
        {
            Version = new(1, 5, 0, 0),
            Date = "18-10-2025",
            Title = "Bugfix, emote handling enhancement, and internal system change",
            TitleColor = Blue,
            Description = "Fixed emote playing capabilities, now allowing to bypass emotes when sitting or on a mount. Integrated NoireLib for improved utilities and features.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, FontAwesomeIcon.Plus, Orange),
                Entry("You can now emote while sitting, groundsitting, mounted or riding pillion.", Orange, 1, FontAwesomeIcon.Chair, Orange),
                Entry("You can now show emote IDs in the emote list.", White, 1, FontAwesomeIcon.ListOl, Orange),

                Separator(),

                Header("Bug Fixes", LightRed, FontAwesomeIcon.Wrench, LightRed),
                Entry("Fixed a bug where Dalamud UI language would be used to get emote commands instead of game language when typing auto-translate emote commands.", White, 1, FontAwesomeIcon.Language, LightRed),
                Entry("Fixed a bug where NPCs emotes would bypass once and then block afterwards.", White, 1, FontAwesomeIcon.PeopleGroup, LightRed),

                Separator(),

                Header("Technical Changes", Blue, FontAwesomeIcon.Code, Blue),
                Entry("Integrated NoireLib for improved utilities and changelog system.", White, 1, FontAwesomeIcon.Book, Blue),
                Entry("Refactored emote and character helpers to use NoireLib utilities.", White, 1, FontAwesomeIcon.CodeBranch, Blue),
            }
        };

    private static ChangelogVersion CreateV1_5_1_0()
        => new ChangelogVersion
        {
            Version = new(1, 5, 1, 0),
            Date = "22-10-2025",
            Title = "Bugfix, new UI option and various internal changes",
            TitleColor = Blue,
            Description = "",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, FontAwesomeIcon.Plus, Orange),
                Entry("For debug purposes, you can now show absolutely *all* in game emotes, including invalid ones, with an option in the emote window. Look for \"Show Invalid Emotes\".", White, 1, FontAwesomeIcon.Bug, Orange),

                Separator(),

                Header("Bug Fixes", LightRed, FontAwesomeIcon.Wrench, LightRed),
                Entry("Emotes previously in the \"Other\" tab had disappeared. Those are now back.", White, 1),
                Entry("Other various bug fixes.", White, 1),

                Separator(),

                Header("Technical Changes", Blue, FontAwesomeIcon.Code, Blue),
                Entry("Refactored the code to use the new NoireLib helpers and utilities.", White, 1),
            }
        };

    private static ChangelogVersion CreateV1_5_1_1()
        => new ChangelogVersion
        {
            Version = new(1, 5, 1, 1),
            Date = "22-10-2025",
            Title = "Hotfix",
            TitleColor = Blue,
            Description = "",
            Entries = new List<ChangelogEntry>
            {
                    Header("Bug Fixes", LightRed, FontAwesomeIcon.Wrench, LightRed),
                    Entry("Fixed the changelog that would not show anymore.", White, 1),
                    Entry("Fixed the settings to show the changelog and print in chat on update that were not enforced anymore.", White, 1),
            }
        };
}
