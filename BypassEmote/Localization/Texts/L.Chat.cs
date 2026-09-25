using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString PromptPending = new("chat.prompt_pending",
        "Bypass Emote is waiting for you to choose how it should play locked emotes.");
    public static readonly NoireString CannotBypassNow = new("chat.cannot_bypass_now", "You cannot bypass this emote right now.");
    public static readonly NoireString SafeModeRefusal = new("chat.safe_mode",
        "Due to detectability, you need to be in the base pose (pose 0) of your current stance to bypass emotes in safe mode.");
    public static readonly NoireString NoEmoteChoiceMade = new("chat.no_choice_made", "Emote Swap was enabled because no choice was made.");

    public static readonly NoireString RefusedCarrying = new("chat.refused.carrying", "/{command} cannot be played while carrying your {ornament}.");
    public static readonly NoireString RefusedStanding = new("chat.refused.standing", "/{command} cannot be played while standing.");
    public static readonly NoireString RefusedSwimming = new("chat.refused.swimming", "/{command} cannot be played while swimming.");
    public static readonly NoireString RefusedDiving = new("chat.refused.diving", "/{command} cannot be played while diving.");
    public static readonly NoireString RefusedGroundSitting = new("chat.refused.ground_sitting", "/{command} cannot be played while sitting on the ground.");
    public static readonly NoireString RefusedChairSitting = new("chat.refused.chair_sitting", "/{command} cannot be played while sitting in a chair.");
    public static readonly NoireString RefusedMounted = new("chat.refused.mounted", "/{command} cannot be played while mounted.");
    public static readonly NoireString RefusedUmbrella = new("chat.refused.umbrella", "/{command} cannot be played while holding an umbrella.");
    public static readonly NoireString RefusedTorch = new("chat.refused.torch", "/{command} cannot be played while holding a torch.");
    public static readonly NoireString RefusedAccessory = new("chat.refused.accessory", "/{command} cannot be played while wearing a fashion accessory.");
    public static readonly NoireString RefusedFishing = new("chat.refused.fishing", "/{command} cannot be played while fishing.");
    public static readonly NoireString RefusedRightNow = new("chat.refused.right_now", "/{command} cannot be played right now.");

    public static readonly NoireString CatalogLoading = new("chat.swap.catalog_loading", "Still loading emote data. Try again in a moment.");
    public static readonly NoireString SwapFailed = new("chat.swap.failed", "Something went wrong. Emote not swapped.");
    public static readonly NoireString SwapNoCollection = new("chat.swap.no_collection",
        "No Penumbra collection is assigned to your character. Emote not swapped.");
    public static readonly NoireString SwapPenumbraUnavailable = new("chat.swap.penumbra_unavailable", "{reason} Emote not swapped.");
    public static readonly NoireString SwapNoCharacter = new("chat.swap.no_character",
        "Penumbra could not say which collection your character uses. Emote not swapped.");
    public static readonly NoireString NeedsEnvironment = new("chat.swap.needs_environment", "/{command} needs {requirement}.");
    public static readonly NoireString CouldNotSwap = new("chat.swap.could_not", "Could not swap /{command}.");
    public static readonly NoireString FoundBut = new("chat.swap.found_but", "Found /{command} but {reason}");
    public static readonly NoireString AlsoFoundBut = new("chat.swap.also_found_but", "Also found /{command} but {reason}");
    public static readonly NoireString NearBlockedList = new("chat.near.blocked_list", "it is on your blocked targets list.");
    public static readonly NoireString NearOtherMod = new("chat.near.other_mod", "another of your mods targets it. Your configuration blocked it.");
    public static readonly NoireString NearNamedMod = new("chat.near.named_mod", "your mod \"{mod}\" targets it. Your configuration blocked it.");
    public static readonly NoireString NearLoopKinds = new("chat.near.loop_kinds", "the loop kinds do not match.");
    public static readonly NoireString NearTurnVeryStrict = new("chat.near.turn_very_strict", "turn matching is very strict.");
    public static readonly NoireString NearSoundStrict = new("chat.near.sound_strict",
        "it makes a sound, and your sound rule avoids every target that does.");
    public static readonly NoireString NearSoundLenient = new("chat.near.sound_lenient",
        "it makes a sound, and your sound rule only allows that for an emote that makes one too.");
    public static readonly NoireString NearSound = new("chat.near.sound", "it makes a sound.");
    public static readonly NoireString NearStrict = new("chat.near.strict", "{rule} matching is strict.");
    public static readonly NoireString IntroDroppedLoop = new("chat.swap.intro_dropped.loop",
        "This emote landed on a loop only target with no intro. You will not see the intro play.");
    public static readonly NoireString IntroDroppedOneShot = new("chat.swap.intro_dropped.one_shot",
        "This emote landed on a one shot target with no intro. You will not see the intro play.");
    public static readonly NoireString ChangedTarget = new("chat.swap.changed_target",
        "This emote landed on /{command}, which your mod \"{mod}\" changes. Players around you may briefly see that mod's animation "
        + "before yours reaches them. If you don't want this to happen, head over to the configuration window and block emotes that "
        + "are changed by other mods.");
    public static readonly NoireString NoOverrideTarget = new("chat.swap.no_override_target",
        "Could not swap /{command}. None of its override targets can be played right now.");
    public static readonly NoireString OverrideSkipped = new("chat.swap.override_skipped", "{emote} was skipped because {reason}.");
    public static readonly NoirePlural MoreSkipped = new("chat.swap.more_skipped",
        "{n} more target(s) were skipped for the same reason.", "{n} more target(s) were skipped for the same reason.");
    public static readonly NoireString EmoteNumber = new("chat.swap.emote_number", "Emote #{id}");

    public static readonly NoireString ReasonLocked = new("chat.reason.locked", "you have not unlocked it");
    public static readonly NoireString ReasonNeverATarget = new("chat.reason.never_a_target", "it can never be a swap target");
    public static readonly NoireString ReasonNotHere = new("chat.reason.not_here", "it cannot be played in your current state");
    public static readonly NoireString ReasonChangedByAMod = new("chat.reason.changed_by_mod", "a mod changes it and your settings block modded emotes");
    public static readonly NoireString ReasonNotConfigured = new("chat.reason.not_configured", "there is no animation data for it");
    public static readonly NoireString ReasonAvailable = new("chat.reason.available", "it is available");

    public static readonly NoireString IdlePoseFailed = new("chat.idle.failed", "Could not use your idle pose for this emote. {cause}");
    public static readonly NoireString IdleStillInEmote = new("chat.idle.still_in_emote",
        "Your character is still in another emote. Move, or change pose, then try again.");
    public static readonly NoireString IdleMounted = new("chat.idle.mounted", "Your character is mounted. There is no idle pose to borrow.");
    public static readonly NoireString IdlePoseUnchangeable = new("chat.idle.pose_unchangeable", "This pose cannot be changed.");
    public static readonly NoireString IdlePoseNotFound = new("chat.idle.pose_not_found", "Your pose animation could not be found.");
    public static readonly NoireString IdleNothingToLend = new("chat.idle.nothing_to_lend", "That emote has no animation to lend.");
    public static readonly NoireString IdleSourceNotFound = new("chat.idle.source_not_found", "That emote's animation could not be found.");
    public static readonly NoireString IdlePoseNotRebuilt = new("chat.idle.pose_not_rebuilt", "Your pose animation could not be rebuilt.");
    public static readonly NoireString IdleNoCollection = new("chat.idle.no_collection", "Penumbra could not say which collection your character uses.");
    public static readonly NoireString IdleModNotOn = new("chat.idle.mod_not_on", "The swap mod could not be turned on.");
    public static readonly NoireString IdleNotRefreshed = new("chat.idle.not_refreshed", "Your character could not be refreshed.");
    public static readonly NoireString IdleSomethingWrong = new("chat.idle.something_wrong", "Something went wrong.");
    public static readonly NoireString IdleZeroNoIntro = new("chat.idle.zero_no_intro",
        "Your idle 0 pose has no intro. This emote's intro will not play. Try changing pose.");
    public static readonly NoireString IdlePoseSwapTarget = new("chat.idle.swap_target", "idle pose");

    public static readonly NoireString OpenLink = new("chat.mod.open", "[Open]");
    public static readonly NoireString DisableLink = new("chat.mod.disable", "[Disable]");
    public static readonly NoireString ModNotOpened = new("chat.mod.not_opened", "{reason} Mod not opened.");
    public static readonly NoireString ModNotDisabled = new("chat.mod.not_disabled", "{reason} Mod not disabled.");
    public static readonly NoireString PenumbraWouldNotOpen = new("chat.mod.would_not_open", "Penumbra would not open '{mod}'.");
    public static readonly NoireString NoCollectionModNotDisabled = new("chat.mod.no_collection",
        "No Penumbra collection is assigned to your character. Mod not disabled.");
    public static readonly NoireString PenumbraWouldNotSwitchOff = new("chat.mod.would_not_switch_off",
        "Penumbra would not switch '{mod}' off in {collection}.");
    public static readonly NoireString ModNowOff = new("chat.mod.now_off", "'{mod}' is now off in {collection}.");

    public static readonly NoireString PenumbraTooOld = new("chat.penumbra.too_old",
        "Bypass Emote needs Penumbra {version} or newer. Update Penumbra (its interface reads {api}).");
    public static readonly NoireString PenumbraMissing = new("chat.penumbra.missing", "Penumbra is not running. Emote Swap needs it installed.");

    public static readonly NoireString ExportRunning = new("chat.export.running", "A debug log export is already running.");
    public static readonly NoireString Exporting = new("chat.export.exporting", "Exporting logs...");
    public static readonly NoireString ExportFailed = new("chat.export.failed", "Debug logs could not be exported. The Dalamud log has the reason.");
    public static readonly SplitText ExportedTo = new(new("chat.export.done",
        "Debug logs exported to {path}. Send this file to the developer. Feel free to check the content of the zip and if you need to "
        + "anonymize any information, please do so before sending it."), "path");
    public static readonly NoireString ExportPersonal = new("chat.export.personal",
        "Personal information appears in it, DO NOT send this in a public channel.");
    public static readonly NoireString ExportAskWhere = new("chat.export.ask_where", "Ask the developer where to send this file to be extra safe.");
    public static readonly NoireString OpenFolder = new("chat.export.open_folder", "[Open folder]");

    public static readonly NoireString CommandFailed = new("chat.command.failed", "Error trying to process command");
    public static readonly NoireString NoNpcTargeted = new("chat.command.no_npc", "No NPC targeted.");
    public static readonly NoireString OnlyOwnCompanion = new("chat.command.only_own", "You can only target your own minion, pet, chocobo.");
    public static readonly NoireString NoMinion = new("chat.command.no_minion", "No minion summoned.");
    public static readonly NoireString NoPet = new("chat.command.no_pet", "No pet summoned.");
    public static readonly NoireString NoChocobo = new("chat.command.no_chocobo", "No chocobo summoned.");
    public static readonly NoireString EmoteNotFound = new("chat.command.not_found", "Emote or command not found: {arg}");
    public static readonly NoireString PosesCannotSwap = new("chat.command.poses", "Poses cannot be swapped.");
    public static readonly NoireString NotInEmoteSwap = new("chat.command.not_in_swap", "This emote cannot be played in Emote Swap mode.");

    public static readonly NoireString ApprovedAnnouncement = new("chat.gate.announcement",
        "The plugin has been approved for this patch. If you noticed weird behaviors prior to this message, try again and it should "
        + "be fixed now.");
}
