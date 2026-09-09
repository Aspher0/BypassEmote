using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V2_3_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV2_3_0_0(),
    };

    private static ChangelogVersion CreateV2_3_0_0()
        => new ChangelogVersion
        {
            Version = new(2, 3, 0, 0),
            Date = "09-09-2026",
            Title = "Animation refresh rework",
            TitleColor = Blue,
            Description = "Reworks how animations are refreshed, and stops other players from seeing you redraw.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                EntryBullet("Added \"Add an override...\" to the right click menu of the main UI.\n" +
                    "It opens the \"Emote overrides\" tab with the emote already picked.", Orange, 1),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("\"Always cache-break\" no longer makes other players see you redraw for random emotes.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Reworked how the plugin refreshes an animation, it is now way more efficient and optimized. Dropped from 10+ sigs to 2.", Blue, 1),
                EntryBullet("Various technical enhancements.", White, 1),
            }
        };
}
