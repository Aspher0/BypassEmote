using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_7_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV1_7_2_0(),
    };

    private static ChangelogVersion CreateV1_7_2_0()
        => new ChangelogVersion
        {
            Version = new(1, 7, 2, 0),
            Date = "03-04-2026",
            Title = "Emote handling enhancement",
            TitleColor = Blue,
            Description = "Enhances the target handling and the emote bypassing.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                EntryBullet("BypassEmote now handles hotbar emote slots. If you click a locked emote hotbar slot (greyed out), the emote will play.", Orange, 1),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("The soft target is now properly handled. If you have both a target and a soft target, bypassing an emote will prioritize the soft target.", White, 1),
                EntryBullet("Various bug fixes over the versions.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Reworked the IPC Data being sent to consumers.", White, 1),
                EntryBullet("Various technical enhancements.", White, 1),
            }
        };
}
