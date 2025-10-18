using Dalamud.Interface;
using NoireLib.Changelog;
using NoireLib.Helpers.Colors;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_4_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV1_4_0_0(),
        CreateV1_4_1_0(),
    };

    private static ChangelogVersion CreateV1_4_0_0()
    {
        var pastelPink = ColorHelper.HexToVector4("#ffa3d4ff");
        return new ChangelogVersion
        {
            Version = new(1,4,0,0),
            Date = "16-09-2025",
            Title = "Various QoLs and Changelog System",
            TitleColor = Blue,
            Description = "Major update introducing a comprehensive changelog system with UI integration, configuration tracking, and enhanced user experience features.",
            Entries = new List<ChangelogEntry>
            {
                Header("Changelog system", Orange, FontAwesomeIcon.Book),
                Entry("Introduced ChangelogWindow UI for viewing updates", Blue, 1),
                    Entry("Accessible via main plugin window book button", null, 2),
                    Entry("Clean, organized display of version history", null, 2),
                    Entry("Interactive elements and color-coded entries", null, 2),

                Entry("Comprehensive changelog management system", null, 1),
                    Entry("Versioned changelog entries with structured data", null, 2),
                    Entry("Automatic changelog version tracking", null, 2),
                    Entry("Support for rich formatting and icons", null, 2),

                Separator(),

                Header("Added new emote data collected from FFXIVCollect", Orange, FontAwesomeIcon.Database),
                    Entry("Added which patch the emote is from", null, 1),
                    Entry("Added the obtention methods to get the emote", null, 1),

                Separator(),

                Header("Configuration & Settings", Orange, FontAwesomeIcon.Cog),
                Entry("Updated configuration system to track changelog versions", null, 1),
                    Entry("Show changelog on update", Blue, 2),
                    Entry("Last seen changelog version tracking", null, 2),
                Entry("Added an option to show update notifications", Blue, 1),

                Separator(),

                Header("User Interface", Orange, FontAwesomeIcon.Eye),
                Entry("Enhanced main plugin window with new action buttons in the title bar", null, 1),
                    Entry("Added changelog button in title bar", Blue, 2),
                    Entry("New settings access button", Blue, 2),
                    Button("Integrated support button (Ko-Fi)", pastelPink, "Donate", null, pastelPink, (m) => { Service.OpenKofi();  }),

                Entry("Added favorited emotes feature", Blue, 1, FontAwesomeIcon.Star, Orange),
                    Entry("Easily access frequently used emotes", null, 2),
                    Entry("Mark/unmark emotes as favorites", null, 2),
                    Entry("View all favorited emotes in the \"Fav\" tab", null, 2),

                Entry("Added an info circle you can hover to see what patch the emote is from, and how to obtain it", Blue, 1, FontAwesomeIcon.InfoCircle, null),
            }
        };
    }

    private static ChangelogVersion CreateV1_4_1_0() 
        => new ChangelogVersion
        {
            Version = new(1, 4, 1, 0),
            Date = "08-10-2025",
            Title = "Improved Emote Detection",
            TitleColor = Blue,
            Description = "Fixed an incorrect detection of game emotes.",
            Entries = new List<ChangelogEntry>
            {
                Header("Improved Emote Detection", Orange),
                Entry("The game will now display Patch 7.35 game emotes.", Blue, 1),
            }
        };
}
