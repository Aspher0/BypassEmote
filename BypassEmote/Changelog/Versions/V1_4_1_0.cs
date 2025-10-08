using BypassEmote.Models;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_4_1_0 : BaseChangelogVersion
{
    public override ChangelogVersion GetVersion()
    {
        return new ChangelogVersion
        {
            Version = "1.4.1.0",
            Date = "08-10-2025",
            Title = "Improved Emote Detection",
            TitleColor = InfoColor,
            Description = "Fixed an incorrect detection of game emotes.",
            Entries = new List<ChangelogEntry>
            {
                Header("Improved Emote Detection", WarningColor),
                Entry("The game will now display Patch 7.35 game emotes.", null, null, 1, InfoColor),
            }
        };
    }
}
