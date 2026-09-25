using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString OverriddenMark = new("overrides.marked.source", "(overridden)");
    public static readonly NoireString AlreadyTargetMark = new("overrides.marked.target", "(already a target)");
    public static readonly NoireString RemoveOverride = new("overrides.remove", "Remove this override");
    public static readonly NoireString LimitedHelp = new("overrides.limited.help",
        "On: this emote plays on one of these targets, or not at all."
        + "\nOff: your usual settings take over when none of these targets can be used.");

    public static readonly NoireString ClassicOverridesIntro = new("classic.overrides.intro",
        "This tab allows you to make sure a locked emote will always use one or multiple specific target emotes.");
    public static readonly NoireString CatalogBuilding = new("overrides.catalog_building", "The emote catalog is still building.");
    public static readonly NoireString ClassicOverrideEmote = new("classic.overrides.source", "Override an emote...");
    public static readonly NoireString ClassicNoOverride = new("classic.overrides.none", "No override yet.");
    public static readonly NoireString ClassicEmptyOverride = new("classic.overrides.empty", "{name}  (empty)");
    public static readonly NoireString ClassicPickLeft = new("classic.overrides.pick", "Pick an emote on the left.");
    public static readonly NoireString ClassicAddTarget = new("classic.overrides.target", "Add a target emote...");
    public static readonly NoireString ClassicLimited = new("classic.overrides.limited", "Limited to selected targets only");
    public static readonly NoireString ClassicNoTarget = new("classic.overrides.no_target", "No target yet.");

    public static readonly NoireString SilkOverridesIntro = new("silk.overrides.intro",
        "This tab makes sure a locked emote always uses one or more specific target emotes, tried in order.");
    public static readonly NoireString SilkLimited = new("silk.overrides.limited", "Limited to these targets only");
    public static readonly NoireString SilkOverrideEmote = new("silk.overrides.source", "+ Override an emote...");
    public static readonly NoireString SilkAddTarget = new("silk.overrides.target", "+ Add a target emote...");
    public static readonly NoireString SilkLockedEmote = new("silk.overrides.locked_emote", "Locked emote");
    public static readonly NoireString SilkPickLeft = new("silk.overrides.pick", "Pick an emote on the left");
    public static readonly NoireString SilkPlaysThrough = new("silk.overrides.plays_through", "{emote} plays through");
    public static readonly NoireString SilkRemove = new("silk.overrides.remove", "Remove");
    public static readonly NoireString NoResult = new("silk.picker.no_result", "No result");
    public static readonly NoireString OwnedMark = new("silk.picker.owned", "OWNED");
}
