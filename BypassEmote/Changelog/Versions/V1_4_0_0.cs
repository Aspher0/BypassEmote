using BypassEmote.Models;
using Dalamud.Interface;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_4_0_0 : BaseChangelogVersion
{
    public override ChangelogVersion GetVersion()
    {
        return new ChangelogVersion
        {
            Version = "1.4.0.0",
            Date = "16-09-2025",
            Title = "Various QoLs and Changelog System",
            TitleColor = InfoColor,
            Description = "Major update introducing a comprehensive changelog system with UI integration, configuration tracking, and enhanced user experience features.",
            Entries = new List<ChangelogEntry>
            {
                Header("Changelog system", WarningColor, FontAwesomeIcon.Book),
                Entry("Introduced ChangelogWindow UI for viewing updates", null, null, 1, InfoColor),
                    Indented("Accessible via main plugin window book button", 2),
                    Indented("Clean, organized display of version history", 2),
                    Indented("Interactive elements and color-coded entries", 2),

                Entry("Comprehensive changelog management system", null, null, 1),
                    Indented("Versioned changelog entries with structured data", 2),
                    Indented("Automatic changelog version tracking", 2),
                    Indented("Support for rich formatting and icons", 2),

                Separator(),

                Header("Added new emote data collected from FFXIVCollect", WarningColor, FontAwesomeIcon.Database),
                    Indented("Added which patch the emote is from", 1),
                    Indented("Added the obtention methods to get the emote", 1),

                Separator(),

                Header("Configuration & Settings", WarningColor, FontAwesomeIcon.Cog),
                Entry("Updated configuration system to track changelog versions", null, null, 1),
                    Indented("Show changelog on update", 2, null, null, InfoColor),
                    Indented("Last seen changelog version tracking", 2),
                Indented("Added an option to show update notifications", 1, null, null, InfoColor),

                Separator(),

                Header("User Interface", WarningColor, FontAwesomeIcon.Eye),
                Entry("Enhanced main plugin window with new action buttons in the title bar", null, null, 1),
                    Indented("Added changelog button in title bar", 2, null, null, InfoColor),
                    Indented("New settings access button", 2, null, null, InfoColor),
                    new() { ButtonText = "Donate", ButtonAction = Service.OpenKofi, ButtonColor = PastelPinkColor, IndentLevel = 2, Text = "Integrated support button (Ko-Fi)", TextColor = PastelPinkColor },

                Entry("Added favorited emotes feature", FontAwesomeIcon.Star, WarningColor, 1, InfoColor),
                    Indented("Easily access frequently used emotes", 2),
                    Indented("Mark/unmark emotes as favorites", 2),
                    Indented("View all favorited emotes in the \"Fav\" tab", 2),

                Entry("Added an info circle you can hover to see what patch the emote is from, and how to obtain it", FontAwesomeIcon.InfoCircle, null, 1, InfoColor),
            }
        };
    }
}
