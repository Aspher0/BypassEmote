using Dalamud.Interface;
using NoireLib.Changelog;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V2_0_x_x : BaseChangelogVersion
{
    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV2_0_0_0(),
    };

    private static ChangelogVersion CreateV2_0_0_0()
        => new ChangelogVersion
        {
            Version = new(2, 0, 0, 0),
            Date = "22-08-2026",
            Title = "Emote Swap",
            TitleColor = Blue,
            Description = "Adds a new way of bypassing emotes through Penumbra, visible on every sync service.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Book),
                Entry("Added Emote Swap, a new bypass mode, which is now the default one.\n" +
                    "Instead of applying the animation on your character, the plugin looks for an emote you have unlocked that behaves like the one you asked for, " +
                    "plays that one for real, and swaps its animation with the locked one through a Penumbra mod it manages for you.\n" +
                    "The game itself plays the emote, so other players see it over any sync service without their plugin having to integrate BypassEmote.", Orange, 1, FontAwesomeIcon.ExchangeAlt, White),
                EntryBullet("Emote Swap needs Penumbra installed and enabled.", Orange, 1),
                EntryBullet("A prompt will appear on your first launch to let you pick the mode you want. You can change your mind at any time in the configuration window.", White, 1),
                EntryBullet("Added a lot of configuration options for Emote Swap, such as how close a match has to be, when the swap is turned off, how many swaps are kept per emote, " +
                    "and whether your name appears on the generated mod.", White, 1),
                EntryBullet("The default configuration is already set up the way it works best, you do not need to touch any of it.", Orange, 1),
                EntryBullet("Every swap is kept as an option of the generated mod, so playing the same emote again puts it back without rebuilding anything.", White, 1),
                Separator(),
                Entry("Emotes are now played according to your character's condition.\n" +
                    "Sitting on the ground, sitting in a chair, mounted, riding pillion, swimming, diving, holding an umbrella or a torch, wearing a fashion accessory: " +
                    "the plugin plays the variant that matches the state you are in, instead of forcing the standing animation on you.\n" +
                    "If an emote cannot be played in the state you are in, the plugin will tell you so instead of playing something wrong.", Orange, 1, FontAwesomeIcon.Walking, White),
                EntryBullet("Emote Swap follows the same rule, and will only play an emote you own that can be executed in the state you are in.", White, 1),
                EntryBullet("The main UI now shows the same condition icons as the game's own emote window, so you can see at a glance where an emote can be played.", White, 1),
                Separator(),
                Entry("Added a safe mode to Direct Play, the original method, which is on by default.\n" +
                    "While it is on, you can only bypass emotes from the base pose (pose 0) of your current stance. This is because the game client sends duplicate " +
                    "change pose packets when your character is in any other pose, which is not something the plugin causes, but not something it can prevent either.\n" +
                    "You can lift that limit in the configuration window if you know what you are doing.", Orange, 1, FontAwesomeIcon.ShieldAlt, White),
                EntryBullet("Safe mode is not a promise either, it only keeps Direct Play to the states where nothing has ever been seen to go wrong. Use Emote Swap if you want to be certain.", White, 1),
                Separator(),
                EntryBullet("Added the possibility to create a permanent Penumbra mod from any emote.\n" +
                    "Click the \"Create a mod\" button in the main UI, or right click an emote and pick \"Create a mod from this emote...\".\n" +
                    "Useful if you want to keep an animation over one of your emotes without the plugin having to do anything.", Orange, 1),
                EntryBullet("You can now stop Emote Swap from playing one of your own emotes, by clicking the ban icon next to its star.\n" +
                    "Blocked emotes have their own tab in the main UI. Useful if you would rather not have a specific one of yours played, for example one that makes noise.", Orange, 1),
                EntryBullet("Added drag and drop from the main UI onto your hotbars. Drag an emote onto a slot of hotbars 1 to 10 and it will be assigned to it.", Orange, 1),
                EntryBullet("Added configuration options for the messages the plugin prints in chat, and for how often the same one is repeated.", White, 1),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Fixed emotes that make your character draw its weapon.", White, 1),
                EntryBullet("Fixed visual effects not playing on some bypassed emotes.", White, 1),
                EntryBullet("Various bug fixes over the versions.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("BypassEmote now checks that the game patch you are on has been approved before enabling anything it finds by signature.\n" +
                    "If a patch breaks something, the plugin turns those parts off by itself and checks every 10 minutes until it is approved again, " +
                    "instead of doing something it should not.", Blue, 1),
                EntryBullet("Moved a large part of the plugin's internals to NoireLib.", White, 1),
                EntryBullet("Various technical enhancements.", White, 1),
            }
        };
}
