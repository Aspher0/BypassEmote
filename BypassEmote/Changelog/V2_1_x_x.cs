using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V2_1_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV2_1_0_0(),
    };

    private static ChangelogVersion CreateV2_1_0_0()
        => new ChangelogVersion
        {
            Version = new(2, 1, 0, 0),
            Date = "23-08-2026",
            Title = "Animation cache breaking",
            TitleColor = Blue,
            Description = "Added \"Always cache-break\" option for Emote Swap, and various bug fixes.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                Entry("Added \"Always cache-break\", a new option for Emote Swap.\n" +
                    "With it on, every emote you play will be cache-broken, especially the ones you own. Play an emote, enable or disable any mod affecting that emote, " +
                    "play the emote again, the new animation refreshes and shows without needing to redraw nor to stop the emote.", Orange, 1, FontAwesomeIcon.SyncAlt, White),
                EntryBullet("It is off by default, you can enable it in the configuration window.", White, 1),
                Separator(),
                EntryBullet("Added /belogs (also available as /be logs), which exports a zip of the plugin logs and settings to send to " +
                    "the developer. Check its content before sending it, it is not meant to be posted publicly.", Orange, 1),
                EntryBullet("The plugin now recognizes the Korean and Chinese game clients.", White, 1),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Playing the real emote of a swap while the swap is still playing now refreshes the animation.\n" +
                    "Before, playing for example /beesknees while a swap was using it kept showing the swapped animation " +
                    "until you stopped emoting.", White, 1),
                EntryBullet("Disabling the plugin now disables the generated mod.", White, 1),
                EntryBullet("Playing an emote while a swapped idle pose is active now stops the idle pose swap.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Reworked how animations are loaded around the game's caches.", Blue, 1),
                EntryBullet("Various technical enhancements.", White, 1),
            }
        };
}
