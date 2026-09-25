using Dalamud.Interface;
using NoireLib.Changelog;
using NoireLib.UI;
using System.Collections.Generic;

namespace BypassEmote.Changelog.Versions;

public class V2_4_x_x : BaseChangelogVersion
{
    private static readonly NoireMotion MalouMotion = NoireMotion.Create()
        .Scaling(0.97f, 1.04f, 0.45f)
        .Rocking(2.5f, 0.35f);

    public override List<ChangelogVersion> GetVersions() => new()
    {
        CreateV2_4_0_0(),
    };

    private static ChangelogVersion CreateV2_4_0_0()
        => new ChangelogVersion
        {
            Version = new(2, 4, 0, 0),
            Date = "24-09-2026",
            Title = "New interface & translations",
            TitleColor = Blue,
            Description = "Adds a brand new interface, translations, shows your locked emotes in the game's own emote window and fix some issues with modded animations.",
            Entries = new List<ChangelogEntry>
            {
                Header("New Features", Orange, 0, FontAwesomeIcon.Star),
                Entry("Added a brand new interface, which is now the default one.\n" +
                    "Every window has been redesigned: the main UI, the settings, \"Create a mod\", the hotbar assignment, the changelog and the logs.\n" +
                    "If you prefer the old look, you can switch back to the classic interface at any time in the settings.", Orange, 1, FontAwesomeIcon.PaintBrush, White),
                EntryBullet("Added a preview popup next to the main UI. Click an emote to make it appear. In Emote Swap mode, it will show you which of your emotes " +
                    "it will play through, offer you to play it, add it to your favorites, block it, assign it to a hotbar or play it on your minion, pet or chocobo.\n" +
                    "Double click an emote to play it right away. You can turn the popup off in the settings.", White, 1),
                EntryBullet("The new interface has its own window settings, click the Window Options button next to the close button. " +
                    "It allows you to adjust opacity, text size, make the plugin always on top, click through, lock its position, its width and/or height, " +
                    "and make the windows stay visible in gpose, when the UI is hidden, during cutscenes, or when the game hides its own UI.", White, 1),
                EntryBullet("Added a \"Compact rows\" option to make the emote list smaller. On by default.", White, 1),
                Separator(),
                Entry("Added translations.\nThe plugin is now available in French, German, Japanese, Korean and Simplified Chinese." +
                    "You can pick your language in the settings.", Green, 1, FontAwesomeIcon.Language, White),
                EntryBullet("Added a translation editor, accessible from the settings. You can translate the plugin in game, see your changes live and save them.\n" +
                    "Translations or improvements are very much welcome. Please open an issue or a pull request on the plugin's GitHub if you'd like to contribute.", Orange, 1),
                Separator(),
                Entry("Locked emotes are now listed in the game's own emote window.\n" +
                    "You can search locked emotes and use them from there. Also added an option to grey or un-grey them from your hotbars, " +
                    "and this applies to the Emote game window as well.", Orange, 1, FontAwesomeIcon.List, White),
                EntryBullet("/be sync now also restarts the sounds and VFXs of your emotes in Emote Swap mode as well, try it!", Orange, 1),
                EntryBullet("/belogs now exports more information, to help figuring out what went wrong.", White, 1),
                Separator(),
                EffectEntryBullet("Added Malou. Please take good care of him.", NoireGradient.Rainbow, MalouMotion, 1),
                Separator(),
                Header("Bug fixes", LightRed, 0, FontAwesomeIcon.Bug),
                EntryBullet("Fixed mods that replace an emote's timeline (.tmb) files not being taken into account by Emote Swap.", LightRed, 1),
                EntryBullet("Fixed swapped animations parsing in VFXEditor.", White, 1),
                EntryBullet("Fixed bypassing emotes from your hotbars on the Chinese client.", White, 1),
                EntryBullet("Various bug fixes over the versions.", White, 1),
                Separator(),
                Header("Technical Changes", Blue, 0, FontAwesomeIcon.Wrench),
                EntryBullet("Both interfaces are now built on NoireLib's new interface system.", Blue, 1),
                EntryBullet("Updated to the latest Penumbra API.", White, 1),
                EntryBullet("Swapping an emote is now slightly faster.", White, 1),
                EntryBullet("Removed the outdated mod writer.", White, 1),
                EntryBullet("Various technical enhancements.", White, 1),
            }
        };
}
