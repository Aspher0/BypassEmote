using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString PageGeneral = new("settings.page.general", "General settings");
    public static readonly NoireString PageBypassMode = new("settings.page.mode", "Bypass Mode");
    public static readonly NoireString PageOverrides = new("settings.page.overrides", "Emote overrides");
    public static readonly NoireString SectionInterface = new("settings.section.interface", "Interface");
    public static readonly NoireString SectionPlugin = new("settings.section.plugin", "Plugin");
    public static readonly NoireString SectionUpdates = new("settings.section.updates", "Updates");
    public static readonly NoireString SectionSafety = new("settings.section.safety", "Safety");
    public static readonly NoireString SectionDirectPlay = new("settings.section.direct_play", "Direct play");
    public static readonly NoireString SectionMatching = new("settings.section.matching", "Matching");
    public static readonly NoireString SectionPenumbra = new("settings.section.penumbra", "Penumbra");
    public static readonly NoireString SectionChatMessages = new("settings.section.chat", "Chat messages");
    public static readonly NoireString EmoteSwap = new("settings.mode.emote_swap", "Emote Swap");
    public static readonly NoireString DirectPlay = new("settings.mode.direct_play", "Direct Play");
    public static readonly NoireString Mode = new("settings.mode", "Mode");
    public static readonly NoireString UnsafeToggle = new("settings.unsafe", "Unsafe toggle");
    public static readonly NoireString FaceTarget = new("settings.face_target", "Face your target automatically");
    public static readonly NoireString FaceTargetHelp = new("settings.face_target.help", "Turns your character toward your target when you bypass an emote.");
    public static readonly NoireString LoopMatching = new("settings.loop_matching", "Loop matching");
    public static readonly NoireString TurnMatching = new("settings.turn_matching", "Turn matching");
    public static readonly NoireString SoundMatching = new("settings.sound_matching", "Sound matching");
    public static readonly NoireString CachedDispatch = new("settings.cached_dispatch", "Spread swaps over several emotes");
    public static readonly NoireString MaxTargets = new("settings.max_targets", "Emotes used per behaviour");
    public static readonly NoireString MaxTargetsHelp = new("settings.max_targets.help", "How many different target emotes one kind of emote may swap to.");
    public static readonly NoireString DispatchFidelity = new("settings.dispatch_fidelity", "How close a spread emote must fit");
    public static readonly NoireString ModdedTargets = new("settings.modded_targets", "Emotes your mods change");
    public static readonly NoireString IdlePoseLoops = new("settings.idle_pose_loops", "Loops on idle poses");
    public static readonly NoireString Lifetime = new("settings.lifetime", "Turn the swap off");
    public static readonly NoireString Behavior = new("settings.behavior", "Swaps at the same time");
    public static readonly NoireString KeptSwaps = new("settings.kept_swaps", "Kept swaps per emote");
    public static readonly NoireString AnonymizeMod = new("settings.anonymize", "Hide your name on the mod");
    public static readonly NoireString AnonymizeModHelp = new("settings.anonymize.help", "Names the generated mod after your initials instead of your full name and world.");
    public static readonly NoireString AlwaysCacheBreak = new("settings.cache_break", "Always cache-break");
    public static readonly NoireString SwapMessages = new("settings.swap_messages", "Show swap messages");
    public static readonly NoireString SwapMessagesHelp = new("settings.swap_messages.help", "Shows a message in chat when an emote is swapped.");
    public static readonly NoireString ErrorMessages = new("settings.error_messages", "Show error messages");
    public static readonly NoireString ErrorMessagesHelp = new("settings.error_messages.help", "Shows an error in chat when a swap could not be done.");
    public static readonly NoireString ErrorThrottle = new("settings.error_throttle", "Repeat an error at most every");
    public static readonly NoireString WarningMessages = new("settings.warning_messages", "Show warning messages");
    public static readonly NoireString WarningThrottle = new("settings.warning_throttle", "Repeat a warning at most every");
    public static readonly NoireString PluginEnabled = new("settings.plugin_enabled", "Enable the plugin");
    public static readonly NoireString PluginEnabledHelp = new("settings.plugin_enabled.help",
        "Lets you bypass emotes with the vanilla emote commands (/beesknees, /tea, and so on) and with hotbar slots."
        + "\nThe main window and the /be command work regardless of this setting.");
    public static readonly NoireString HotbarBypass = new("settings.hotbar_bypass", "Bypass emotes from locked hotbar slots");
    public static readonly NoireString HotbarBypassHelp = new("settings.hotbar_bypass.help",
        "Bypasses a locked emote when you press its hotbar slot."
        + "\nRight-click an emote in the main window to assign it to a slot.");
    public static readonly NoireString LockedInWindowHelp = new("settings.locked_in_window.help",
        "Lists the emotes you have not unlocked in the game's own emote window."
        + "\nClose and reopen the emote window for changes to appear.");
    public static readonly NoireString LockedAsUsable = new("settings.locked_as_usable", "Do not grey out locked emotes");
    public static readonly NoireString LockedAsUsableHelp = new("settings.locked_as_usable.help",
        "Keeps the emotes you have not unlocked lit in the game's emote window and on your hotbars."
        + "\nPurely cosmetic.");
    public static readonly NoireString StopOnMove = new("settings.stop_on_move", "Stop companion emotes when they move");
    public static readonly NoireString StopOnMoveHelp = new("settings.stop_on_move.help", "Stops your minion, pet or chocobo's looped emote when they move.");
    public static readonly NoireString UpdateNotification = new("settings.update_notification", "Show update notifications");
    public static readonly NoireString UpdateNotificationHelp = new("settings.update_notification.help", "Tells you in chat and on screen when a new version of the plugin has been installed.");
    public static readonly NoireString ChangelogOnUpdate = new("settings.changelog_on_update", "Show the changelog after an update");
    public static readonly NoireString ChangelogOnUpdateHelp = new("settings.changelog_on_update.help", "Opens the changelog window after an update.");
    public static readonly NoireString ModeHelp = new("settings.mode.help",
        "\"Emote Swap\" plays your emote over one your character owns, through a Penumbra mod. Other players see it "
        + "over any sync service."
        + "\n\"Direct Play\" sends the emote to the game itself. Not every sync service supports it.");
    public static readonly NoireString SyncServicesLine = new("settings.sync_services", "Not all sync services support Direct Play.");
    public static readonly NoireString SafeModeLimitLine = new("settings.safe_mode_limit",
        "In safe mode you can only bypass emotes from the base pose (pose 0) of your current stance.");
    public static readonly NoireString UnsafeHeadline = new("settings.unsafe.headline",
        "This is unsafe. Forcing an emote outside your base pose is, in theory, detectable by the server.");
    public static readonly NoireString SafeDirectPlayTooltip = new("settings.direct_play.safe_tooltip", "Not all sync services support it. {limit}");
    public static readonly NoireString UnsafeDirectPlayTooltip = new("settings.direct_play.unsafe_tooltip",
        "Not recommended. Not all sync services support it. Emote Swap is safer and works over any sync service.");
    public static readonly NoireString SwitchToDirectPlay = new("settings.confirm.direct_play", "Switch to Direct Play");
    public static readonly NoireString SwitchToDirectPlayTitle = new("settings.confirm.direct_play.title", "Switch to Direct Play?");
    public static readonly NoireString EnableUnsafe = new("settings.confirm.unsafe", "Enable unsafe mode");
    public static readonly NoireString EnableUnsafeTitle = new("settings.confirm.unsafe.title", "Enable unsafe mode?");
    public static readonly NoireString Cancel = new("common.cancel", "Cancel");
    public static readonly NoireString CheckNow = new("settings.gate.check_now", "Check now");
    public static readonly NoireString NotYet = new("settings.gate.not_yet", "not yet");
    public static readonly NoireString CacheBreakNotApproved = new("settings.cache_break.not_approved", "This game build is not approved yet, this will not work.");
    public static readonly NoireString NotRunning = new("settings.not_running", "Not running: {fault}.");

    public static readonly NoireString ClassicStrict = new("classic.settings.strict", "Strict");
    public static readonly NoireString ClassicLenient = new("classic.settings.lenient", "Lenient");
    public static readonly NoireString ClassicOff = new("classic.settings.off", "Off");
    public static readonly NoireString ClassicVeryStrict = new("classic.settings.very_strict", "Very strict");
    public static readonly NoireString ClassicLifetimeEnds = new("classic.settings.lifetime.ends", "When the emote ends");
    public static readonly NoireString ClassicLifetimeTarget = new("classic.settings.lifetime.target", "When you play the target emote");
    public static readonly NoireString ClassicNever = new("classic.settings.never", "Never");
    public static readonly NoireString ClassicBehaviorMultiple = new("classic.settings.behavior.multiple", "Multiple swaps");
    public static readonly NoireString ClassicBehaviorOne = new("classic.settings.behavior.one", "One swap at a time");
    public static readonly NoireString ClassicAllowed = new("classic.settings.allowed", "Allowed");
    public static readonly NoireString ClassicLastResort = new("classic.settings.last_resort", "Last resort");
    public static readonly NoireString ClassicBlocked = new("classic.settings.blocked", "Blocked");
    public static readonly NoireString ClassicNothingElseFits = new("classic.settings.nothing_else_fits", "Only when nothing else fits");
    public static readonly NoireString ClassicAllow = new("classic.settings.allow", "Allow");
    public static readonly NoireString ClassicWhenNecessary = new("classic.settings.when_necessary", "Only when necessary");
    public static readonly NoireString ClassicOn = new("classic.settings.on", "On");
    public static readonly NoireString ClassicSameRank = new("classic.settings.same_rank", "Same rank only");
    public static readonly NoireString ClassicOneBelow = new("classic.settings.one_below", "One rank below");
    public static readonly NoireString ClassicAnything = new("classic.settings.anything", "Anything allowed");
    public static readonly NoireString ClassicLockedInWindow = new("classic.settings.locked_in_window", "Show locked emotes in the game's Emote window");
    public static readonly NoireString ClassicNewInterface = new("classic.settings.new_interface", "Use the new interface");
    public static readonly NoireString ClassicNewInterfaceHelp = new("classic.settings.new_interface.help",
        "Switches back to the new interface."
        + "\nThe classic interface is the one you are looking at, exactly as in previous versions.");
    public static readonly NoireString ClassicGposeWindows = new("classic.settings.gpose", "Show windows in GPose");
    public static readonly NoireString ClassicGposeWindowsHelp = new("classic.settings.gpose.help", "Keeps this plugin's windows visible while you are in GPose.");
    public static readonly NoireString ClassicHiddenUiWindows = new("classic.settings.hidden_ui", "Show windows while the game UI is hidden");
    public static readonly NoireString ClassicHiddenUiWindowsHelp = new("classic.settings.hidden_ui.help", "Keeps this plugin's windows visible when you hide the game UI.");
    public static readonly NoireString ClassicSectionWindows = new("classic.settings.section.windows", "Windows");
    public static readonly NoireString ClassicSafeModeNotAPromise = new("classic.settings.safe_mode_not_a_promise",
        "Safe mode is not 100% guaranteed to be safe either. It prevents Direct Play from being used in states where it was proved to "
        + "go wrong, but I can not prove the absence of issues. Use Emote Swap if you want to be 100% safe, it will behave almost the same way.");
    public static readonly NoireString ClassicUnsafeReassurance = new("classic.settings.unsafe.reassurance",
        "In practice it is a non-issue. This has been a thing in other tools and plugins (and still is in some of them), "
        + "which people have used for years without trouble. Go back to safe mode, or even better, to emote swap, if you are uncomfy with this.");
    public static readonly NoireString ClassicUnsafeToggleHelp = new("classic.settings.unsafe.help",
        "Lets Direct Play bypass an emote in any pose."
        + "\n\nLeave it off unless you know what you are doing. When off, the plugin only plays emotes from states where "
        + "nothing can be noticed.");
    public static readonly NoireString ClassicNotApproved = new("classic.settings.gate.not_approved", "Bypass Emote has not been approved for this game build.");
    public static readonly NoireString ClassicNotApprovedText = new("classic.settings.gate.not_approved.text",
        "Emote swaps may behave oddly until the build is approved.\nThe plugin will automatically fetch updates every 10 minutes to check if it was approved.");
    public static readonly NoireString ClassicCheckedAt = new("classic.settings.gate.checked_at", "Checked at {time}.");
    public static readonly NoireString ClassicCountdown = new("classic.settings.gate.countdown", "{label} ({seconds})");
    public static readonly NoireString ClassicForceApprovalOn = new("classic.settings.gate.forced", "Force approval is on.");
    public static readonly NoireString ClassicEnableForceApproval = new("classic.settings.gate.force_enable", "Enable Force Approval");
    public static readonly NoireString ClassicDisableForceApproval = new("classic.settings.gate.force_disable", "Disable Force Approval");
    public static readonly NoireString ClassicUntested = new("classic.settings.gate.untested", "Bypass Emote cannot be tested on this game client.");
    public static readonly NoireString ClassicLoopMatchingHelp = new("classic.settings.loop_matching.help",
        "\"Strict\" only puts a looping emote on another looping one."
        + "\n\"Lenient\" lets a looping emote play once on a one time emote when no better match exists."
        + "\n\nRecommended: \"Strict\", or \"Lenient\" if you really don't have many emotes.");
    public static readonly NoireString ClassicTurnMatchingHelp = new("classic.settings.turn_matching.help",
        "Emotes have different turn behaviors when you target someone. Some emotes will make your torso turn (i.e: /hum), some only your head (i.e: /stepdance),"
        + "some will only make your eyes follow your target (i.e: /beesknees) while others will not move at all (i.e:/guard)."
        + "\n\n\"Very strict\" only picks emotes that behaves the same way."
        + "\n\"Strict\" allows eye following differences but keeps emotes head and body turn behaviors."
        + "\n\"Lenient\" allows any turn behavior."
        + "\n\nThe plugin will still always try to find the best match first, regardless of the selected rule."
        + "\n\nRecommended: \"Lenient\".");
    public static readonly NoireString ClassicSoundMatchingHelp = new("classic.settings.sound_matching.help",
        "\"Strict\" never puts an emote on one that makes sound."
        + "\n\"Lenient\" allows matching emotes that make sounds together."
        + "\n\"Off\" will let emotes play regardless of sound."
        + "\n\nThis is to prevent vanilla people from seeing you play fume which could annoy other vanilla players, for example."
        + "\n\nRecommended: \"Lenient\".");
    public static readonly NoireString ClassicCachedDispatchHelp = new("classic.settings.cached_dispatch.help",
        "Gives each bypassed emote a target emote of its own. This is useful when you want to bypass multiple emotes quickly."
        + "\n\n\"Off\" would make it so other people on your sync service would see you redraw constantly."
        + "\n\"Only when necessary\" spreads emotes only after a game patch breaks the cache-breaker feature."
        + "\n\"On\" always spreads emotes."
        + "\n\nRecommended: \"On\" if you want other people on your sync service to always see you properly without "
        + "redrawing all the time, otherwise highly recommended to leave it on \"Only when necessary\" and not \"Off\".");
    public static readonly NoireString ClassicDispatchFidelityHelp = new("classic.settings.dispatch_fidelity.help",
        "Takes effect when \"Spread swaps over several emote\" is enabled. This determines which emotes become available for a source emote. "
        + "Basically, if you want to spread swaps over 5 emotes, and you try to bypass an emote but you only have 2 same-rank targets available, "
        + "this is how it will determine what to do in this scenario. A rank is basically a category of emotes with similar characteristics (same turn behaviour, etc)."
        + "\n\n\"Same rank only\" strictly picks targets of the same rank."
        + "\n\"One rank below\" also accepts targets one rank below."
        + "\n\"Anything allowed\" picks any target it can find, regardless of the rank."
        + "\n\nNone of them ever breaks your other rules."
        + "\n\nRecommended: \"One rank below\", or \"Same rank only\" if you want behavior accuracy.");
    public static readonly NoireString ClassicModdedTargetsHelp = new("classic.settings.modded_targets.help",
        "Determines whether to block unlocked emotes from being picked when they are modified by at least one of your mods. "
        + "This prevents other people from seeing other modded emotes you might have before the swap takes place."
        + "\nAs an example, you have a mod on beesknees, and you try to bypass /conduct which happens to land on beesknees: "
        + "other players might or might not see the modded beesknees for a moment."
        + "\n\n\"Allowed\" allows using unlocked emotes that are modified by one of your mods."
        + "\n\"Last resort\" uses one only when nothing else fits."
        + "\n\"Blocked\" never uses one, and show an error message in the chat with options to disable or open the mod in penumbra."
        + "\n\nRecommended: \"Last resort\", or \"Blocked\" if you absolutely don't want your modded emotes to accidentaly be seen.");
    public static readonly NoireString ClassicIdlePoseLoopsHelp = new("classic.settings.idle_pose_loops.help",
        "When no unlocked looped emote fits as a target, your current idle pose may be eligible instead. "
        + "The emote you try to bypass will then be targeted onto your current idle pose. This will cause a redraw of your character when triggered."
        + "\n\n\"Never\" blocks idle poses from being used as targets."
        + "\n\"Only when nothing else fits\" uses the pose only when literally no unlocked looped emote could have played here at all. "
        + "This will not use your idle pose if any other emote would have been available if it wasn't blocked."
        + "\n\"Allow\" always falls back to your idle pose when no other options are available."
        + "\n\nRecommended: \"Only when nothing else fits\".");
    public static readonly NoireString ClassicLifetimeHelp = new("classic.settings.lifetime.help",
        "\"When the emote ends\" puts your real emote back as soon as the animation stops."
        + "\n\"When you play the target emote\" keeps the swap enabled until the next time you play the target emote."
        + "\n\"Never\" keeps the swap live until another swap claims the same target emote."
        + "\n\nRecommended: \"When you play the target emote\". \"When the emote ends\" is not recommended, as people will "
        + "see you redraw constantly after swapping.");
    public static readonly NoireString ClassicBehaviorHelp = new("classic.settings.behavior.help",
        "\"Multiple swaps\" keeps multiple swaps active at the same time in the mod."
        + "\n\"One swap at a time\" turns the previous swaps off as soon as a new one starts."
        + "\n\nRecommended: \"Multiple swaps\", unless for some reason you want to only keep one swap at a time.");
    public static readonly NoireString ClassicKeptSwapsHelp = new("classic.settings.kept_swaps.help",
        "How many swaps are kept per target emote. 0 keeps them all."
        + "\n\nEach kept swap stays as an option of the generated Penumbra mod, so playing an emote "
        + "again enables it again without rebuilding it. The only cost is that the mod gets bigger.");
    public static readonly NoireString ClassicCacheBreakHelp = new("classic.settings.cache_break.help",
        "Applies the cache-break mechanism to every emote, even those you own. This is a bonus feature and "
        + "is experimental. This will allow the client to always \"refresh\" animations so you never have "
        + "to redraw yourself or stop emoting to apply an animation change.");
    public static readonly NoireString ClassicErrorThrottleHelp = new("classic.settings.error_throttle.help",
        "How long the same error stays quiet after you have seen it."
        + "\n\nAccepts time format: 5m, 300s, 5m10s, 1h."
        + "\nSet to 0 to see every one of them.");
    public static readonly NoireString ClassicWarningMessagesHelp = new("classic.settings.warning_messages.help",
        "Shows a warning in chat when a swap happened, but not the way you would expect.");
    public static readonly NoireString ClassicWarningThrottleHelp = new("classic.settings.warning_throttle.help",
        "How long the same warning stays quiet after you have seen it."
        + "\n\nAccepts time format: 5m, 300s, 5m10s, 1h."
        + "\nSet to 0 to disable.");
    public static readonly NoireString ClassicCachedDispatchAlarm = new("classic.settings.cached_dispatch.alarm",
        "Leave this on, otherwise bypassing several animations in a row may look broken.");

    public static readonly NoireString Language = new("settings.language", "Language");
    public static readonly NoireString LanguageHelp = new("settings.language.help", "The language of the plugin's windows and chat messages.");
    public static readonly NoireString Translation = new("settings.translation", "Translation");
    public static readonly NoireString Translate = new("settings.translate", "Translate...");
    public static readonly NoireString TranslationHelp = new("settings.translation.help", "Opens the translation editor. Your translations are saved in the plugin's configuration folder. Translations or improvements are very much welcome: open an issue or a pull request on the plugin's GitHub to share yours.");
}
