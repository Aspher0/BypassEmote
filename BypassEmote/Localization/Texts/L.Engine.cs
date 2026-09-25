using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString NoName = new("emote.no_name", "No name");

    public static readonly NoireString GateApprovedEarlier = new("gate.approved_earlier", "Game build {build} was approved earlier.");
    public static readonly NoireString GateReadingList = new("gate.reading_list", "Reading the approval list.");
    public static readonly NoireString GateApprovalDropped = new("gate.approval_dropped", "The approval recorded for game build {build} was dropped.");
    public static readonly NoireString GateBuildUnreadable = new("gate.build_unreadable", "The installed game build could not be read.");
    public static readonly NoireString GateListUnreachable = new("gate.list_unreachable", "The approval list could not be reached.");
    public static readonly NoireString GateNotApprovedYet = new("gate.not_approved_yet", "Game build {build} has not been approved yet.");
    public static readonly NoireString GateUnreadableVersion = new("gate.unreadable_version",
        "Game build {build} names a plugin version the plugin cannot read.");
    public static readonly NoireString GateNeedsVersion = new("gate.needs_version",
        "Game build {build} needs Bypass Emote {minimum} or newer. Installed: {installed}.");
    public static readonly NoireString GateUnknownVersion = new("gate.unknown_version", "unknown");
    public static readonly NoireString GateApproved = new("gate.approved", "Game build {build} is approved.");
    public static readonly NoireString GateUntestedClient = new("gate.untested_client",
        "The {client} client has not and can not be tested. This plugin might not work and might be unstable/unusable. "
        + "Please don't use it if it does not work well.");
    public static readonly NoireString GateUntestedOther = new("gate.untested_other",
        "This game client is not the Global one, and has not and can not be tested. This plugin might not work and might be "
        + "unstable/unusable. Please don't use it if it does not work well.");

    public static readonly NoireString BuildFailed = new("mod_builder.failed", "Something went wrong. Nothing was created. The log has the details.");
    public static readonly NoireString BuildPickRace = new("mod_builder.pick_race", "Pick at least one race for the mod to cover.");
    public static readonly NoireString BuildNameFirst = new("mod_builder.name_first", "Give the mod a name first.");
    public static readonly NoireString BuildNoModFolder = new("mod_builder.no_mod_folder", "Penumbra's mod folder could not be read.");
    public static readonly NoireString BuildNoEngine = new("mod_builder.no_engine", "The swap engine is not running.");
    public static readonly NoireString BuildFolderTaken = new("mod_builder.folder_taken",
        "Penumbra already holds a mod folder called '{folder}'. Pick another name.");
    public static readonly NoireString BuildNoPosture = new("mod_builder.no_posture",
        "/{source} cannot be played over /{target}. They share no posture.");
    public static readonly NoireString BuildNotWritten = new("mod_builder.not_written", "The mod's files could not be written. The log has the details.");
    public static readonly NoireString BuildNotLoaded = new("mod_builder.not_loaded",
        "'{mod}' was written to '{folder}'. Penumbra did not load it. Rediscover mods in Penumbra to pick it up.");
    public static readonly NoireString BuildCreatedOff = new("mod_builder.created_off", "'{mod}' was created. Enable it in Penumbra when you want it.");
    public static readonly NoireString BuildCreatedNoCollection = new("mod_builder.created_no_collection",
        "'{mod}' was created. No collection is assigned to your character. The mod is off.");
    public static readonly NoireString BuildCreatedNotEnabled = new("mod_builder.created_not_enabled",
        "'{mod}' was created. Penumbra could not enable it in {collection}.");
    public static readonly NoireString BuildCreatedOn = new("mod_builder.created_on", "'{mod}' was created and switched on in {collection}.");

    public static readonly NoireString AdviceBeingBypassed = new("advice.being_bypassed", "{target} is the emote being bypassed.");
    public static readonly NoireString AdviceNeverTarget = new("advice.never_target",
        "{target} can never be a swap target (pose, facial expression, per-job emote or weapon drawn). It is always skipped.");
    public static readonly NoireString AdviceNotUnlocked = new("advice.not_unlocked", "You have not unlocked {target}. It is skipped.");
    public static readonly NoireString AdviceBlocked = new("advice.blocked", "{target} is on your blocked targets list. This override uses it anyway.");
    public static readonly NoireString AdviceModChanges = new("advice.mod_changes",
        "Your mod \"{mod}\" already changes {target}. Your \"Emotes your mods change\" setting still applies.");
    public static readonly NoireString AdviceNoPosture = new("advice.no_posture", "{target} and {source} share no posture. The swap will not happen.");
    public static readonly NoireString AdviceMissingPosture = new("advice.missing_posture",
        "{target} has no {posture} animation. The swap will not happen when you play {source} in that posture.");
    public static readonly NoireString AdviceStopsOnTurn = new("advice.stops_on_turn", "{target} will stop when you turn your character.");
    public static readonly NoireString AdviceAdjust = new("advice.adjust",
        "{target} has an adjust variant for use on someone. {source} has none. Targeting someone keeps {target}'s animation.");
    public static readonly NoireString AdvicePlaysOnce = new("advice.plays_once",
        "{target} plays once while {source} loops. The animation will stop instead of looping.");
    public static readonly NoireString AdviceLoops = new("advice.loops", "{target} loops while {source} plays once. Weird behavior might happen.");
    public static readonly NoireString AdviceTurn = new("advice.turn", "When you target someone, {target} {target_turn} while {source} {source_turn}.");
    public static readonly NoireString AdviceVoiceLine = new("advice.voice_line", "{target} emits a voice line sound. Everyone around you will hear it.");
    public static readonly NoireString AdviceSound = new("advice.sound",
        "{target} emits a sound that {source} does not. Everyone around you will hear it.");
    public static readonly NoireString AdviceIntroDropped = new("advice.intro_dropped", "{target} has no intro and {source} has one. The intro will not play.");
    public static readonly NoireString AdviceIntroAdded = new("advice.intro_added", "{target} has an intro and {source} does not.");
    public static readonly NoireString TurnNone = new("advice.turn.none", "does not turn at all");
    public static readonly NoireString TurnEyes = new("advice.turn.eyes", "only follows with the eyes");
    public static readonly NoireString TurnHead = new("advice.turn.head", "turns the head");
    public static readonly NoireString TurnBody = new("advice.turn.body", "turns the whole body");
    public static readonly NoireString TurnUnknown = new("advice.turn.unknown", "turns in a way the plugin could not read");
    public static readonly NoireString PostureStanding = new("advice.posture.standing", "standing");
    public static readonly NoireString PostureChairSitting = new("advice.posture.chair_sitting", "chair sitting");
    public static readonly NoireString PostureGroundSitting = new("advice.posture.ground_sitting", "ground sitting");
    public static readonly NoireString PostureMounted = new("advice.posture.mounted", "mounted");
    public static readonly NoireString PostureMatching = new("advice.posture.matching", "matching");
    public static readonly NoireString PostureOr = new("advice.posture.or", "{first} or {second}");

    public static string Fill(NoireString text, string name1, string value1, string name2, string value2, string name3, string value3,
        string name4, string value4)
        => Fill(text, name1, value1, name2, value2, name3, value3).Replace("{" + name4 + "}", value4);
}
