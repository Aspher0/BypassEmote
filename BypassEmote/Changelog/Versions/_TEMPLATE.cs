using BypassEmote.Models;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.Changelog.Versions;

/// <summary>
/// Template for creating new changelog versions.
/// </summary>
public class V0_0_0_0_TEMPLATE : BaseChangelogVersion
{
    public override ChangelogVersion GetVersion()
    {
        return new ChangelogVersion
        {
            Version = "0.0.0.0",
            Date = "XX-XX-2025",
            Title = "Your Title Here", // Optional title
            TitleColor = InfoColor, // Optional title color
            Description = "Your description here...", // Optional description
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", SuccessColor, FontAwesomeIcon.Plus),
                
                Feature("Added something new"),
                Improvement("Improved something"),
                BugFix("Fixed something"),
                
                Entry("Changed the emote manager"),
                    Indented("Added support for custom emotes", 1),
                    Indented("Added performance optimizations", 1),
                        Indented("Improved memory management", 2),
                        Indented("Enhanced caching system", 2),
                    Indented("Added configuration validation", 1),
                
                Separator(),
                
                Header("Bug Fixes", ErrorColor),
                BugFix("Fixed crash on startup"),
                
                Button("Support Development", "Donate", 
                    () => Service.OpenLinkInDefaultBrowser("https://ko-fi.com/aspher0"),
                    FontAwesomeIcon.Heart, new Vector4(0.9f, 0.2f, 0.5f, 1f))
            }
        };
    }
}
