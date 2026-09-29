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
    public static readonly NoireString DisableModOnExit = new("settings.disable_mod_on_exit", "Disable the mod on logout and unload");
    public static readonly NoireString DisableModOnExitHelp = new("settings.disable_mod_on_exit.help",
        "When on, the generated Penumbra mod is disabled when you log out or unload the plugin.");
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
    public static readonly NoireString ClassicLifetimeEnds = new("classic.settings.lifetime.ends", "When the emote ends");
    public static readonly NoireString ClassicLifetimeTarget = new("classic.settings.lifetime.target", "When you play the target emote");
    public static readonly NoireString ClassicBehaviorMultiple = new("classic.settings.behavior.multiple", "Multiple swaps");
    public static readonly NoireString ClassicBehaviorOne = new("classic.settings.behavior.one", "One swap at a time");
    public static readonly NoireString ClassicWhenNecessary = new("classic.settings.when_necessary", "Only when necessary");
    public static readonly NoireString ClassicAnything = new("classic.settings.anything", "Anything allowed");
    public static readonly NoireString ClassicNewInterface = new("classic.settings.new_interface", "Use the new interface");
    public static readonly NoireString ClassicNewInterfaceHelp = new("classic.settings.new_interface.help",
        "Switches back to the new interface."
        + "\nThe classic interface is the one you are looking at, exactly as in previous versions.");
    public static readonly NoireString ClassicGposeWindows = new("classic.settings.gpose", "Show windows in GPose");
    public static readonly NoireString ClassicGposeWindowsHelp = new("classic.settings.gpose.help", "Keeps this plugin's windows visible while you are in GPose.");
    public static readonly NoireString ClassicHiddenUiWindows = new("classic.settings.hidden_ui", "Show windows while the game UI is hidden");
    public static readonly NoireString ClassicHiddenUiWindowsHelp = new("classic.settings.hidden_ui.help", "Keeps this plugin's windows visible when you hide the game UI.");
    public static readonly NoireString ClassicSectionWindows = new("classic.settings.section.windows", "Windows");
    public static readonly NoireString ClassicNotApproved = new("classic.settings.gate.not_approved", "Bypass Emote has not been approved for this game build.");
    public static readonly NoireString ClassicNotApprovedText = new("classic.settings.gate.not_approved.text",
        "Emote swaps may behave oddly until the build is approved.\nThe plugin will automatically fetch updates every 10 minutes to check if it was approved.");
    public static readonly NoireString ClassicCheckedAt = new("classic.settings.gate.checked_at", "Checked at {time}.");
    public static readonly NoireString ClassicCountdown = new("classic.settings.gate.countdown", "{label} ({seconds})");
    public static readonly NoireString ClassicForceApprovalOn = new("classic.settings.gate.forced", "Force approval is on.");
    public static readonly NoireString ClassicEnableForceApproval = new("classic.settings.gate.force_enable", "Enable Force Approval");
    public static readonly NoireString ClassicDisableForceApproval = new("classic.settings.gate.force_disable", "Disable Force Approval");
    public static readonly NoireString ClassicUntested = new("classic.settings.gate.untested", "Bypass Emote cannot be tested on this game client.");

    public static readonly NoireString Language = new("settings.language", "Language");
    public static readonly NoireString LanguageHelp = new("settings.language.help", "The language of the plugin's windows and chat messages.");
    public static readonly NoireString Translation = new("settings.translation", "Translation");
    public static readonly NoireString Translate = new("settings.translate", "Translate...");
    public static readonly NoireString TranslationHelp = new("settings.translation.help", "Opens the translation editor. Your translations are saved in the plugin's configuration folder. Translations or improvements are very much welcome: open an issue or a pull request on the plugin's GitHub to share yours.");
    public static readonly NoireString LockedInWindow = new("settings.locked_in_window", "Show locked emotes in the game emote window");
    public static readonly NoireString PreviewPopupSetting = new("silk.settings.preview_popup", "Enable preview popup");
    public static readonly NoireString PreviewPopupSettingHelp = new("silk.settings.preview_popup.help",
        "On: clicking an emote opens a panel next to the window, and a double-click plays it. In Emote Swap the panel also "
        + "previews the swap. Off: no panel, a single click plays the emote.");
    public static readonly NoireString NewInterfaceText = new("silk.settings.new_interface.text", "The one you are looking at.");
    public static readonly NoireString ClassicInterfaceText = new("silk.settings.classic_interface.text", "The old version.");
    public static readonly NoireString SectionEmoteList = new("silk.settings.section.emote_list", "Emote list");
    public static readonly NoireString CompactRows = new("silk.settings.compact_rows", "Compact rows");
    public static readonly NoireString CompactRowsHelp = new("silk.settings.compact_rows.help", "Much smaller rows in the emote list, to see more emotes at once.");
    public static readonly NoireString AdoptMalou = new("silk.settings.malou", "Adopt Malou, the orange cat");
    public static readonly NoireString AdoptMalouHelp = new("silk.settings.malou.help", "He's really cute.");

    public static readonly NoireString Recommended = new("silk.settings.recommended", "Recommended");
    public static readonly NoireString Strict = new("settings.strict", "Strict");
    public static readonly NoireString Lenient = new("settings.lenient", "Lenient");
    public static readonly NoireString VeryStrict = new("settings.very_strict", "Very strict");
    public static readonly NoireString Off = new("settings.off", "Off");
    public static readonly NoireString SilkWhenNeeded = new("silk.settings.when_needed", "When needed");
    public static readonly NoireString On = new("settings.on", "On");
    public static readonly NoireString SameRank = new("settings.same_rank", "Same rank");
    public static readonly NoireString OneBelow = new("settings.one_below", "One below");
    public static readonly NoireString SilkAny = new("silk.settings.any", "Any");
    public static readonly NoireString Allowed = new("settings.allowed", "Allowed");
    public static readonly NoireString LastResort = new("settings.last_resort", "Last resort");
    public static readonly NoireString Blocked = new("settings.blocked", "Blocked");
    public static readonly NoireString Never = new("settings.never", "Never");
    public static readonly NoireString Allow = new("settings.allow", "Allow");
    public static readonly NoireString SilkWhenItEnds = new("silk.settings.when_it_ends", "When it ends");
    public static readonly NoireString SilkOnTheTarget = new("silk.settings.on_the_target", "On the target");
    public static readonly NoireString SilkMultiple = new("silk.settings.multiple", "Multiple");
    public static readonly NoireString SilkOneAtATime = new("silk.settings.one_at_a_time", "One at a time");
    public static readonly NoireString UnsafeToggleHelp = new("settings.unsafe.help",
        "Lets Direct Play bypass an emote in any pose."
        + "\n\nLeave it off unless you know what you are doing. When it is off, the plugin only plays emotes from "
        + "states where nothing can be noticed.");
    public static readonly NoireString LoopMatchingHelp = new("settings.loop_matching.help",
        "\"Strict\" only puts a looping emote on another looping one."
        + "\n\"Lenient\" lets a looping emote play once on a one time emote when no better match exists."
        + "\n\nRecommended: \"Strict\". Pick \"Lenient\" when you own few emotes.");
    public static readonly NoireString TurnMatchingHelp = new("settings.turn_matching.help",
        "Emotes turn your character differently when you have a target. Some turn the torso (/hum). Some turn only the "
        + "head (/stepdance). Some only move the eyes (/beesknees). Some do not move at all (/guard)."
        + "\n\n\"Very strict\" only picks emotes that behave the same way."
        + "\n\"Strict\" keeps the head and body turn, and allows a different eye behaviour."
        + "\n\"Lenient\" allows any turn behaviour."
        + "\n\nThe best match is always tried first."
        + "\n\nRecommended: \"Lenient\".");
    public static readonly NoireString SoundMatchingHelp = new("settings.sound_matching.help",
        "Decides whether an emote can be swapped onto one that makes a sound, which players around you would hear."
        + "\n\"Strict\" only uses silent targets."
        + "\n\"Lenient\" keeps silent emotes on silent targets. An emote that makes a sound itself can use any target."
        + "\n\"Off\" allows any target, so a silent emote can end up playing another emote's sound."
        + "\n\nWhenever a silent target fits, it is picked first."
        + "\n\nRecommended: \"Lenient\".");
    public static readonly NoireString CachedDispatchHelp = new("settings.cached_dispatch.help",
        "Gives each bypassed emote a target emote of its own. Useful when you bypass several emotes quickly."
        + "\n\n\"Off\" redraws your character on every bypass."
        + "\n\"When needed\" spreads emotes only after a game patch breaks the cache-breaker."
        + "\n\"On\" always spreads emotes."
        + "\n\nRecommended: \"On\". \"Off\" is not recommended.");
    public static readonly NoireString DispatchFidelityHelp = new("settings.dispatch_fidelity.help",
        "Which emotes a source emote may spread over. Takes effect while \"Spread swaps over several emotes\" is on. "
        + "A rank is a group of emotes with the same characteristics (turn behaviour and so on)."
        + "\n\n\"Same rank\" only picks targets of the same rank."
        + "\n\"One below\" also accepts targets one rank below."
        + "\n\"Any\" picks any target it can find."
        + "\n\nNone of them breaks your other rules."
        + "\n\nRecommended: \"One below\". Pick \"Same rank\" for behaviour accuracy.");
    public static readonly NoireString ModdedTargetsHelp = new("settings.modded_targets.help",
        "Whether an unlocked emote that one of your mods changes may be used as a target. Other players can see that "
        + "modded emote for a moment before the swap takes place."
        + "\n\n\"Allowed\" uses them freely."
        + "\n\"Last resort\" uses one only when nothing else fits."
        + "\n\"Blocked\" never uses one. The chat error then offers to disable the mod or open it in Penumbra."
        + "\n\nRecommended: \"Last resort\". Pick \"Blocked\" to keep your modded emotes out of sight.");
    public static readonly NoireString IdlePoseLoopsHelp = new("settings.idle_pose_loops.help",
        "When no unlocked looped emote fits as a target, your current idle pose can carry the emote instead. "
        + "Using the pose redraws your character."
        + "\n\n\"Never\" blocks idle poses as targets."
        + "\n\"Allow\" falls back to your idle pose whenever no looped emote fits."
        + "\n\nRecommended: \"Allow\".");
    public static readonly NoireString LifetimeHelp = new("settings.lifetime.help",
        "\"When it ends\" puts your real emote back as soon as the animation stops."
        + "\n\"On the target\" keeps the swap enabled until the next time you play the target emote."
        + "\n\"Never\" keeps the swap live until another swap claims the same target emote."
        + "\n\n\"When it ends\" redraws your character after every swap."
        + "\n\nRecommended: \"On the target\".");
    public static readonly NoireString BehaviorHelp = new("settings.behavior.help",
        "\"Multiple\" keeps multiple swaps active at the same time in the mod."
        + "\n\"One at a time\" turns the previous swaps off as soon as a new one starts."
        + "\n\nRecommended: \"Multiple\".");
    public static readonly NoireString KeptSwapsHelp = new("settings.kept_swaps.help",
        "How many swaps are kept per target emote. 0 keeps them all."
        + "\n\nA kept swap stays as an option of the generated Penumbra mod. Playing that emote again enables it "
        + "without rebuilding the mod. Kept swaps make the mod bigger.");
    public static readonly NoireString CacheBreakHelp = new("settings.cache_break.help",
        "Applies the cache-break mechanism to every emote, even the ones you own. An animation change then applies "
        + "without a redraw and without stopping the emote. Experimental.");
    public static readonly NoireString ErrorThrottleHelp = new("settings.error_throttle.help",
        "How long the same error stays quiet after you have seen it."
        + "\n\nSet to 0 to see every one of them.");
    public static readonly NoireString WarningMessagesHelp = new("settings.warning_messages.help",
        "Shows a warning in chat when a swap did not happen the way you would expect.");
    public static readonly NoireString WarningThrottleHelp = new("settings.warning_throttle.help",
        "How long the same warning stays quiet after you have seen it."
        + "\n\nSet to 0 to disable.");
    public static readonly NoireString CachedDispatchAlarm = new("settings.cached_dispatch.alarm",
        "Leave this on. Bypassing several animations in a row can look broken while it is off.");

    public static readonly NoireString SafeModeNotAPromise = new("settings.safe_mode_not_a_promise",
        "Safe mode is not a guarantee either. It keeps Direct Play out of the states where it was proved to go wrong. "
        + "Emote Swap is the safe option and behaves almost the same way.");
    public static readonly NoireString UnsafeReassurance = new("settings.unsafe.reassurance",
        "In practice it is a non-issue. Other tools and plugins have done this for years without trouble. Go back to "
        + "safe mode, or to Emote Swap, if you are uncomfortable with it.");

    public static readonly NoireString GateWaitingTitle = new("silk.gate.waiting", "Waiting for this game patch to be approved");
    public static readonly NoireString GateWaitingText = new("silk.gate.waiting.text",
        "Bypass Emote stays off until the current game version is checked. It usually takes a few hours.");
    public static readonly NoireString GateUntestedTitle = new("silk.gate.untested", "Bypass Emote cannot be tested on this game client");
    public static readonly NoireString ForceApproval = new("silk.gate.force", "Force approval");
    public static readonly NoireString ForceApprovalOn = new("silk.gate.forced", "Force approval is on.");
    public static readonly NoireString DisableForceApproval = new("silk.gate.force_disable", "Disable force approval");
    public static readonly NoireString GateDetail = new("silk.gate.detail",
        "{reason}\nEmote swaps may behave oddly until the build is approved. The plugin fetches updates "
        + "every 10 minutes to check if it was approved.{notice}\nChecked at {time}.");
    public static readonly NoireString CountdownLabel = new("silk.gate.countdown", "{label} ({seconds})");
}
