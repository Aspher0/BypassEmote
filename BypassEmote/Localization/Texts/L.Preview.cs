using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString PreviewSwapOff = new("preview.swap_off", "Emote Swap is off.");
    public static readonly NoireString PreviewGroupPose = new("preview.group_pose", "You are in group pose.");
    public static readonly NoireString PreviewOwned = new("preview.owned", "You already own this emote.");
    public static readonly NoireString PreviewNoOverrideTarget = new("preview.no_override_target", "No override target of /{command} can be played.");
    public static readonly NoireString PreviewDetail = new("preview.detail", "{emote}: {reason}");
    public static readonly NoirePlural PreviewMoreSkipped = new("preview.more_skipped", "{n} more target(s) skipped.", "{n} more target(s) skipped.");
    public static readonly NoireString PreviewNotUnlocked = new("preview.refusal.locked", "not unlocked.");
    public static readonly NoireString PreviewNeverTarget = new("preview.refusal.never_target", "never a swap target.");
    public static readonly NoireString PreviewNotHere = new("preview.refusal.not_here", "not playable in your current state.");
    public static readonly NoireString PreviewChangedByMod = new("preview.refusal.changed_by_mod", "changed by another mod.");
    public static readonly NoireString PreviewNoAnimation = new("preview.refusal.no_animation", "no animation data.");
    public static readonly NoireString PreviewUnavailable = new("preview.refusal.unavailable", "unavailable.");
    public static readonly NoireString PreviewOwnNone = new("preview.own_none", "You own no emote that can carry this one.");
    public static readonly NoireString PreviewAllUnavailable = new("preview.all_unavailable", "Every emote that can carry this one is unavailable.");
    public static readonly NoireString PreviewAllBlocked = new("preview.all_blocked", "Every emote that fits is on your blocked targets list.");
    public static readonly NoireString PreviewAllModded = new("preview.all_modded", "One of your mods changes every emote that fits.");
    public static readonly NoireString PreviewNoLoop = new("preview.no_loop", "No loop is free. Idle-pose loops are off.");
    public static readonly NoireString PreviewNoMatch = new("preview.no_match", "No emote you own matches /{command}.");
    public static readonly NoireString PreviewMiss = new("preview.miss", "/{command}: {reason}");
    public static readonly NoirePlural PreviewBlockedFit = new("preview.blocked_fit",
        "{n} emote(s) that fit: on your blocked targets list.", "{n} emote(s) that fit: on your blocked targets list.");
    public static readonly NoirePlural PreviewModdedFit = new("preview.modded_fit",
        "{n} emote(s) that fit: changed by another mod.", "{n} emote(s) that fit: changed by another mod.");
    public static readonly NoireString MissBlocked = new("preview.miss.blocked", "on your blocked targets list.");
    public static readonly NoireString MissNamedMod = new("preview.miss.named_mod", "changed by your mod \"{mod}\".");
    public static readonly NoireString MissLoop = new("preview.miss.loop", "different loop kind.");
    public static readonly NoireString MissTurn = new("preview.miss.turn", "turn does not match.");
    public static readonly NoireString MissSound = new("preview.miss.sound", "makes a sound.");
    public static readonly NoireString MissOther = new("preview.miss.other", "{rule} does not match.");
    public static readonly NoirePlural PoolLocked = new("preview.pool.locked", "{n} emote(s) not unlocked.", "{n} emote(s) not unlocked.");
    public static readonly NoirePlural PoolNeverTarget = new("preview.pool.never_target",
        "{n} emote(s) never a swap target.", "{n} emote(s) never a swap target.");
    public static readonly NoirePlural PoolNotHere = new("preview.pool.not_here",
        "{n} emote(s) not playable in your current state.", "{n} emote(s) not playable in your current state.");
    public static readonly NoirePlural PoolGameGate = new("preview.pool.game_gate",
        "{n} emote(s) refused by the game.", "{n} emote(s) refused by the game.");
    public static readonly NoirePlural PoolTargeted = new("preview.pool.targeted",
        "{n} emote(s) play another row with a target.", "{n} emote(s) play another row with a target.");
    public static readonly NoirePlural PoolOther = new("preview.pool.other", "{n} emote(s): {reason}.", "{n} emote(s): {reason}.");
}
