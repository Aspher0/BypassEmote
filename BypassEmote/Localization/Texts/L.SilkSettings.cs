using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString SilkLockedInWindow = new("silk.settings.locked_in_window", "Show locked emotes in the game emote window");
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
    public static readonly NoireString SilkStrict = new("silk.settings.strict", "Strict");
    public static readonly NoireString SilkLenient = new("silk.settings.lenient", "Lenient");
    public static readonly NoireString SilkVeryStrict = new("silk.settings.very_strict", "Very strict");
    public static readonly NoireString SilkOff = new("silk.settings.off", "Off");
    public static readonly NoireString SilkWhenNeeded = new("silk.settings.when_needed", "When needed");
    public static readonly NoireString SilkOn = new("silk.settings.on", "On");
    public static readonly NoireString SilkSameRank = new("silk.settings.same_rank", "Same rank");
    public static readonly NoireString SilkOneBelow = new("silk.settings.one_below", "One below");
    public static readonly NoireString SilkAny = new("silk.settings.any", "Any");
    public static readonly NoireString SilkAllowed = new("silk.settings.allowed", "Allowed");
    public static readonly NoireString SilkLastResort = new("silk.settings.last_resort", "Last resort");
    public static readonly NoireString SilkBlocked = new("silk.settings.blocked", "Blocked");
    public static readonly NoireString SilkNever = new("silk.settings.never", "Never");
    public static readonly NoireString SilkIfNothingFits = new("silk.settings.if_nothing_fits", "If nothing fits");
    public static readonly NoireString SilkAllow = new("silk.settings.allow", "Allow");
    public static readonly NoireString SilkWhenItEnds = new("silk.settings.when_it_ends", "When it ends");
    public static readonly NoireString SilkOnTheTarget = new("silk.settings.on_the_target", "On the target");
    public static readonly NoireString SilkMultiple = new("silk.settings.multiple", "Multiple");
    public static readonly NoireString SilkOneAtATime = new("silk.settings.one_at_a_time", "One at a time");
    public static readonly NoireString SilkUnsafeToggleHelp = new("silk.settings.unsafe.help",
        "Lets Direct Play bypass an emote in any pose."
        + "\n\nLeave it off unless you know what you are doing. When it is off, the plugin only plays emotes from "
        + "states where nothing can be noticed.");
    public static readonly NoireString SilkLoopMatchingHelp = new("silk.settings.loop_matching.help",
        "\"Strict\" only puts a looping emote on another looping one."
        + "\n\"Lenient\" lets a looping emote play once on a one time emote when no better match exists."
        + "\n\nRecommended: \"Strict\". Pick \"Lenient\" when you own few emotes.");
    public static readonly NoireString SilkTurnMatchingHelp = new("silk.settings.turn_matching.help",
        "Emotes turn your character differently when you have a target. Some turn the torso (/hum). Some turn only the "
        + "head (/stepdance). Some only move the eyes (/beesknees). Some do not move at all (/guard)."
        + "\n\n\"Very strict\" only picks emotes that behave the same way."
        + "\n\"Strict\" keeps the head and body turn, and allows a different eye behaviour."
        + "\n\"Lenient\" allows any turn behaviour."
        + "\n\nThe best match is always tried first."
        + "\n\nRecommended: \"Lenient\".");
    public static readonly NoireString SilkSoundMatchingHelp = new("silk.settings.sound_matching.help",
        "\"Strict\" never puts an emote on one that makes sound."
        + "\n\"Lenient\" allows matching emotes that make sounds together."
        + "\n\"Off\" plays emotes regardless of sound."
        + "\n\nSound matching keeps other players from hearing an emote you did not play."
        + "\n\nRecommended: \"Lenient\".");
    public static readonly NoireString SilkCachedDispatchHelp = new("silk.settings.cached_dispatch.help",
        "Gives each bypassed emote a target emote of its own. Useful when you bypass several emotes quickly."
        + "\n\n\"Off\" redraws your character on every bypass."
        + "\n\"When needed\" spreads emotes only after a game patch breaks the cache-breaker."
        + "\n\"On\" always spreads emotes."
        + "\n\nRecommended: \"On\". \"Off\" is not recommended.");
    public static readonly NoireString SilkDispatchFidelityHelp = new("silk.settings.dispatch_fidelity.help",
        "Which emotes a source emote may spread over. Takes effect while \"Spread swaps over several emotes\" is on. "
        + "A rank is a group of emotes with the same characteristics (turn behaviour and so on)."
        + "\n\n\"Same rank\" only picks targets of the same rank."
        + "\n\"One below\" also accepts targets one rank below."
        + "\n\"Any\" picks any target it can find."
        + "\n\nNone of them breaks your other rules."
        + "\n\nRecommended: \"One below\". Pick \"Same rank\" for behaviour accuracy.");
    public static readonly NoireString SilkModdedTargetsHelp = new("silk.settings.modded_targets.help",
        "Whether an unlocked emote that one of your mods changes may be used as a target. Other players can see that "
        + "modded emote for a moment before the swap takes place."
        + "\n\n\"Allowed\" uses them freely."
        + "\n\"Last resort\" uses one only when nothing else fits."
        + "\n\"Blocked\" never uses one. The chat error then offers to disable the mod or open it in Penumbra."
        + "\n\nRecommended: \"Last resort\". Pick \"Blocked\" to keep your modded emotes out of sight.");
    public static readonly NoireString SilkIdlePoseLoopsHelp = new("silk.settings.idle_pose_loops.help",
        "When no unlocked looped emote fits as a target, your current idle pose can carry the emote instead. "
        + "Using the pose redraws your character."
        + "\n\n\"Never\" blocks idle poses as targets."
        + "\n\"If nothing fits\" uses the pose only when no unlocked looped emote could have played at all. "
        + "An emote you blocked yourself still counts as one that could have played."
        + "\n\"Allow\" falls back to your idle pose whenever nothing else is available."
        + "\n\nRecommended: \"If nothing fits\".");
    public static readonly NoireString SilkLifetimeHelp = new("silk.settings.lifetime.help",
        "\"When it ends\" puts your real emote back as soon as the animation stops."
        + "\n\"On the target\" keeps the swap enabled until the next time you play the target emote."
        + "\n\"Never\" keeps the swap live until another swap claims the same target emote."
        + "\n\n\"When it ends\" redraws your character after every swap."
        + "\n\nRecommended: \"On the target\".");
    public static readonly NoireString SilkBehaviorHelp = new("silk.settings.behavior.help",
        "\"Multiple\" keeps multiple swaps active at the same time in the mod."
        + "\n\"One at a time\" turns the previous swaps off as soon as a new one starts."
        + "\n\nRecommended: \"Multiple\".");
    public static readonly NoireString SilkKeptSwapsHelp = new("silk.settings.kept_swaps.help",
        "How many swaps are kept per target emote. 0 keeps them all."
        + "\n\nA kept swap stays as an option of the generated Penumbra mod. Playing that emote again enables it "
        + "without rebuilding the mod. Kept swaps make the mod bigger.");
    public static readonly NoireString SilkCacheBreakHelp = new("silk.settings.cache_break.help",
        "Applies the cache-break mechanism to every emote, even the ones you own. An animation change then applies "
        + "without a redraw and without stopping the emote. Experimental.");
    public static readonly NoireString SilkErrorThrottleHelp = new("silk.settings.error_throttle.help",
        "How long the same error stays quiet after you have seen it."
        + "\n\nSet to 0 to see every one of them.");
    public static readonly NoireString SilkWarningMessagesHelp = new("silk.settings.warning_messages.help",
        "Shows a warning in chat when a swap did not happen the way you would expect.");
    public static readonly NoireString SilkWarningThrottleHelp = new("silk.settings.warning_throttle.help",
        "How long the same warning stays quiet after you have seen it."
        + "\n\nSet to 0 to disable.");
    public static readonly NoireString SilkCachedDispatchAlarm = new("silk.settings.cached_dispatch.alarm",
        "Leave this on. Bypassing several animations in a row can look broken while it is off.");

    public static readonly NoireString SilkSafeModeNotAPromise = new("silk.settings.safe_mode_not_a_promise",
        "Safe mode is not a guarantee either. It keeps Direct Play out of the states where it was proved to go wrong. "
        + "Emote Swap is the safe option and behaves almost the same way.");
    public static readonly NoireString SilkUnsafeReassurance = new("silk.settings.unsafe.reassurance",
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
