using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString PenumbraNotRunning = new("create_mod.penumbra_off", "Penumbra is not running.");
    public static readonly NoireString EmoteToPlay = new("create_mod.source", "Emote to play");
    public static readonly NoireString PlayedOver = new("create_mod.target", "Played over");
    public static readonly NoireString EmoteToPlayHelp = new("create_mod.source.help", "The animation the mod plays. The one you have not unlocked.");
    public static readonly NoireString PlayedOverHelp = new("create_mod.target.help", "The emote you will actually use in game. The one you have unlocked.");
    public static readonly NoireString PickEmoteToPlay = new("create_mod.source.placeholder", "Pick the emote to play...");
    public static readonly NoireString PickEmoteToPlayOver = new("create_mod.target.placeholder", "Pick the emote to play over...");
    public static readonly NoireString RacesCovered = new("create_mod.races", "Races covered");
    public static readonly NoireString EnableOnCreation = new("create_mod.enable", "Enable on creation");
    public static readonly NoireString EnableOnCreationHelp = new("create_mod.enable.help", "Switches the mod on in your character's collection as soon as it exists.");
    public static readonly NoireString HighestPriority = new("create_mod.priority", "Highest priority");
    public static readonly NoireString ModNamePlaceholder = new("create_mod.name.placeholder", "My emote mod");
    public static readonly NoireString VanillaAnimation = new("create_mod.vanilla", "Vanilla animation");
    public static readonly NoireString PickBoth = new("create_mod.pick_both", "Pick both emotes and name the mod.");
    public static readonly NoireString NotOverItself = new("create_mod.not_over_itself", "An emote cannot be played over itself.");
    public static readonly NoireString PickRace = new("create_mod.pick_race", "Pick at least one race.");
    public static readonly NoireString EmoteDataLoading = new("create_mod.loading", "Emote data is still loading. Try again in a moment.");
    public static readonly NoireString NoReadableAnimation = new("create_mod.no_animation", "One of those emotes has no animation this can read.");
    public static readonly NoireString NotUnlockedTarget = new("create_mod.warning.not_unlocked", "You have not unlocked {target}.");
    public static readonly NoireString ModChangesTarget = new("create_mod.warning.modded", "Your mod \"{mod}\" already changes {target}.");
    public static readonly NoireString SharedAnimationFile = new("create_mod.warning.shared",
        "{losers} read the same animation file as {winner}. {winner}'s version plays for all of them.");
    public static readonly NoireString AlsoReached = new("create_mod.warning.also_reached",
        "This also changes the emote for {races}. They read a file the mod writes.");

    public static readonly NoireString ClassicCreateModIntro = new("classic.create_mod.intro",
        "This window allows you to create a permanent swap mod, and it stays unaffected by Bypass Emote. You own it, and you manage it, like any other mod.");
    public static readonly NoireString RacesCoveredHelp = new("classic.create_mod.races.help", "Which bodies the mod is written for.");
    public static readonly NoireString ModName = new("classic.create_mod.name", "Mod name");
    public static readonly NoireString ModNameHelp = new("classic.create_mod.name.help", "The name of the generated mod.");
    public static readonly NoireString ClassicHighestPriorityHelp = new("classic.create_mod.priority.help",
        "Makes the mod have the highest priority. When off, it is created at priority 0.");
    public static readonly NoireString NoRaceCovered = new("classic.create_mod.no_race", "No race covered");
    public static readonly NoireString SearchRaces = new("classic.create_mod.search_races", "Search races...");
    public static readonly NoireString ModdedAnimation = new("create_mod.modded", "Modded animation: {mod}");
    public static readonly NoireString Create = new("classic.create_mod.create", "Create");

    public static readonly NoireString SilkCreateModIntro = new("silk.create_mod.intro",
        "Creates a permanent swap mod in Penumbra. Bypass Emote never touches it afterwards. You own it and manage it "
        + "like any other mod.");
    public static readonly NoireString SilkHighestPriorityHelp = new("silk.create_mod.priority.help",
        "Gives the mod the highest priority. When it is off, the mod is created at priority 0.");
    public static readonly NoireString CreateTheMod = new("silk.create_mod.create", "Create the mod");
    public static readonly NoireString NoAnimationForBody = new("silk.create_mod.no_body", "This emote has no animation for this body");
    public static readonly NoireString Owned = new("common.owned", "Owned");
    public static readonly NoireString NotUnlocked = new("silk.create_mod.not_unlocked", "Not unlocked");
    public static readonly NoireString ModLabel = new("silk.create_mod.mod", "Mod");
    public static readonly NoireString All = new("common.all", "All");
    public static readonly NoireString None = new("common.none", "None");
    public static readonly NoireString AutoModName = new("silk.create_mod.auto_name", "{source} over {target}");
    public static readonly NoireString ModCreated = new("silk.create_mod.done", "\"{mod}\" is in Penumbra, enabled at the highest priority.");
}
