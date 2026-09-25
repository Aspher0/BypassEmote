using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString PromptTitle = new("prompt.title", "Choose how Bypass Emote plays locked emotes");
    public static readonly NoireString PromptUseEmoteSwap = new("prompt.emote_swap", "Use Emote Swap");
    public static readonly NoireString PromptKeepDirectPlay = new("prompt.direct_play", "Keep Direct Play");
    public static readonly NoireString PromptHeading = new("prompt.heading", "A safer way to play locked emotes");
    public static readonly NoireString PromptBefore = new("prompt.before",
        "Until now Bypass Emote forced the animation onto your character from your own client. Nothing was ever "
        + "sent to the server, but in specific cases, where your character would be in any pose other than the base one, the "
        + "game client would send duplicate change pose packets to the server. This is not caused by the plugin itself, but rather "
        + "by how the game handles pose changes. The \"Idle Animation Delay\" setting in the game's "
        + "Character Configuration > Control Settings > Character tab is what causes this.");
    public static readonly NoireString PromptNow = new("prompt.now",
        "The new mode uses Penumbra to swap locked emotes onto unlocked ones. "
        + "The game plays the emote itself. Nothing mismatches between the game and the server anymore.");
    public static readonly NoireString PromptChangeLater = new("prompt.change_later", "You can change this at any time in the settings.");
}
