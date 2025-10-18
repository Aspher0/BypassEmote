using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_5_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV1_5_0_0(),
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
                Entry("You can now emote while sitting, groundsitting, mounted or riding pillion.", White, 1, FontAwesomeIcon.Chair, Orange),
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
}
