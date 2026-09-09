using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V2_2_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV2_2_0_0(),
    };

    private static ChangelogVersion CreateV2_2_0_0()
        => new ChangelogVersion
        {
            Version = new(2, 2, 0, 0),
            Date = "09-09-2026",
            Title = "Emote overrides",
            TitleColor = Blue,
            Description = "Adds \"Emote overrides\", and various bug fixes and improvements.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                Entry("Added the \"Emote overrides\" tab to the configuration window.\n" +
                    "Pick a locked emote, then pick the emotes it is allowed to land on. Emote Swap will then use " +
                    "your list instead of finding a best match, in the order you put it in.\n" +
                    "If none of them can be played, the plugin tells you which ones it skipped and why.", Orange, 1, FontAwesomeIcon.ArrowsLeftRight, White),
                Separator(),
                EntryBullet("The \"Fav\" and \"Blocked\" tabs of the main window now have their own dropdown, to add an emote to either list without hunting for it in the other tabs.", Orange, 1),
                EntryBullet("The \"Create a mod\" window now warns you about mismatches and other potentially unwanted behaviors, such as when you have not unlocked the target emote, when one of your own mods already changes it, " +
                    "or when the two emotes have turn, loop, sound or intro mismatches.", Orange, 1),
                EntryBullet("Added \"Support me on Ko-fi\" and \"Discord\" buttons to the main window.", White, 1),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Fixed \"Create a mod\" sometimes writing the wrong animation instead of the one you asked for.", White, 1),
                EntryBullet("Fixed changing world turning off the generated Penumbra mod and its options.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Fixed one of the signatures the plugin needed for the cache-breaker feature.\n" +
                    "Without it, a swap could play the emote you played before it.", Blue, 1),
                EntryBullet("Various technical enhancements.", White, 1),
            }
        };
}
