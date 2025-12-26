using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V1_6_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV1_6_0_0(),
        CreateV1_6_1_2(),
        CreateV1_6_2_0(),
        CreateV1_6_5_2(),
    };

    private static ChangelogVersion CreateV1_6_0_0()
        => new ChangelogVersion
        {
            Version = new(1, 6, 0, 0),
            Date = "12-11-2025",
            Title = "Emote handling enhancement",
            TitleColor = Blue,
            Description = "Enhances the emote handling both locally and while synced.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                Entry("Added support for special emotes such as /dote and /allsaintscharm where the target would not be handled properly.", Orange, 1, FontAwesomeIcon.Users, White),
                Entry("Added /be sync and /be syncall which will respectively sync the emotes of bypassed players only, or all players on the map.", Orange, 1, FontAwesomeIcon.Sync, White),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Fixed emote not being bypassed when you passed the emote command with parameters, such as \"/beesknees motion\".", White, 1),
                EntryBullet("Fixed a bug where bypassing a looped emote would not cancel after mounting-dismounting, interacting with NPCs, casting, etc.", White, 1),
                EntryBullet("Fixed a bug where you could bypass emotes in GPose.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Improved how the syncing works, and specifically how other players are handled when they stop emoting.", White, 1),
            }
        };

    private static ChangelogVersion CreateV1_6_1_2()
        => new ChangelogVersion
        {
            Version = new(1, 6, 1, 2),
            Date = "14-11-2025",
            Title = "Various bug fixes",
            TitleColor = Blue,
            Description = "Fixes a few bugs with some specific emotes and syncing.",
            Entries = new List<ChangelogEntry>
            {
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Fixed some emotes not being bypassed properly, for example /waterfloat and /waterflip.", White, 1),
                EntryBullet("Improved how the syncing works (again), and specifically how other players are handled when they stop emoting.\n" +
                    "A small delay will be added before the emote is sent over IPC.\nFurthermore, movement detections for other players has been improved to " +
                    "add some leeway so that the emote doesnt get stopped when the player is still moving a bit.", White, 1),
                EntryBullet("Fixed emotes bypassing under some conditions that are not valid (Casting, in cutscene, in GPose, In event ...).", White, 1),
            }
        };

    private static ChangelogVersion CreateV1_6_2_0()
        => new ChangelogVersion
        {
            Version = new(1, 6, 2, 0),
            Date = "05-12-2025",
            Title = "Facial expressions",
            TitleColor = Blue,
            Description = "Fixed a few bugs concerning facial expressions and facing targets.",
            Entries = new List<ChangelogEntry>
            {
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Prevented facial expressions from stopping bypassed looped emotes.", White, 1),
                EntryBullet("Facial expression emotes no longer makes the character face its target.", White, 1),
                EntryBullet("Additionnaly, the character will no longer rotate when emoting to itself (self-targetting).", White, 1),
                EntryBullet("Commands /be sync and /be syncall will now better handle syncing emotes that have intro + loop phases, " +
                    "hence improving the \"syncing\", where a slight delay would occur before for those type of emotes.", Orange, 1),
            }
        };

    private static ChangelogVersion CreateV1_6_5_2()
        => new ChangelogVersion
        {
            Version = new(1, 6, 5, 2),
            Date = "26-12-2025",
            Title = "Minions & Battle NPCs support",
            TitleColor = Blue,
            Description = "Added support for Minions & Battle NPCs. Revised IPC.",
            Entries = new List<ChangelogEntry>
            {
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Fixed a bug with facing targets.", White, 1),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Added support for Minions. You can now apply any emote to your own minions provided they have been turned human.", Blue, 1),
                EntryBullet("Added support for Battle NPCs. You can now apply any emote Battle NPCs in the world.", Blue, 1),
                EntryBullet("Revised IPC to support above changes.", White, 1),
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                EntryBullet("Added the /bem command to apply emotes to your minion, provided it's been summoned and turned human.\n" +
                    "You do not need to target the minion, and just like /bet, you can use an emote argument or the stop argument (/bem stop, /bem beesknees, /bem /tea, ...).\n" +
                    "Useful for untargettable minions such as the cushion or the campfire.", Orange, 1),
                EntryBullet("Additionally, added the /bep command to apply emotes to your pet (carbuncle ...), provided it's been summoned and turned human.", Orange, 1),
                EntryBullet("Added a configuration option to choose whether you want your minion or pet to stop emoting when it moves.\n" +
                    "Useful for the Plush Cushion or the Campfire if you're using SimpleHeels' temporary offsets.", White, 1),
            }
        };
}
