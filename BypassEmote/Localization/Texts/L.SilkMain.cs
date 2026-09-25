using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString AddFavouritePlaceholder = new("silk.main.quick.favourite", "Add an emote to your favourites...");
    public static readonly NoireString Refresh = new("silk.main.toolbar.refresh", "Refresh");
    public static readonly NoireString SyncEveryone = new("silk.main.toolbar.sync", "Sync everyone");
    public static readonly NoireString TabTip = new("silk.main.tab.tip", "{tab} ({count})");
    public static readonly NoireString HintClick = new("silk.main.hint.click", "Click");
    public static readonly NoireString HintDoubleClick = new("silk.main.hint.double_click", "Double-click");
    public static readonly NoireString HintRightClick = new("silk.main.hint.right_click", "Right-click");
    public static readonly NoireString HintDrag = new("silk.main.hint.drag", "Drag");
    public static readonly NoireString HintDetails = new("silk.main.hint.details", "details");
    public static readonly NoireString HintPlay = new("silk.main.hint.play", "play");
    public static readonly NoireString HintMore = new("silk.main.hint.more", "more");
    public static readonly NoireString HintHotbar = new("silk.main.hint.hotbar", "hotbar");
    public static readonly NoireString HintPlaySelf = new("silk.main.hint.play_self", "play on yourself");
    public static readonly NoireString RemoveFavourite = new("silk.main.row.unfavourite", "Remove from favourites");
    public static readonly NoireString AddFavourite = new("silk.main.row.favourite", "Add to favourites");
    public static readonly NoireString AllowTarget = new("silk.main.row.allow", "Allow as a swap target");
    public static readonly NoireString BlockTarget = new("silk.main.row.block", "Block as a swap target");
    public static readonly NoireString NoFavourited = new("silk.main.empty.favourites", "No favourited emote");
    public static readonly NoireString NoFavouritedHint = new("silk.main.empty.favourites.hint", "Star an emote, or add one above.");
    public static readonly NoireString NoBlockedHint = new("silk.main.empty.blocked.hint", "Blocked emotes are never used as swap targets.");
    public static readonly NoireString NothingMatches = new("silk.main.empty.search", "Nothing matches \"{query}\"");
    public static readonly NoireString NothingMatchesHint = new("silk.main.empty.search.hint", "Search by name, command or ID.");
    public static readonly NoireString EmotePlayed = new("silk.main.pill.played", "Emote played!");
    public static readonly NoireString EmoteSwapped = new("silk.main.pill.swapped", "Emote swapped!");
    public static readonly NoireString NoEmoteMatches = new("silk.main.quick.none", "No emote matches.");
    public static readonly NoireString QuickMarkedFavourite = new("silk.main.quick.marked_favourite", "favourite");
    public static readonly NoireString QuickMarkedBlocked = new("silk.main.quick.marked_blocked", "blocked");

    public static readonly NoireString UnknownName = new("emote.unknown", "Unknown name");
    public static readonly NoireString EmoteId = new("emote.id", "ID {id}");
    public static readonly NoireString EmotePatch = new("emote.patch", "Patch {patch}");
    public static readonly NoireString AddedInPatch = new("emote.added", "Added in patch {patch}");
    public static readonly NoireString BaseGame = new("emote.base_game", "Base game");
    public static readonly NoireString PlayableWhile = new("silk.card.playable_while", "Playable while");
    public static readonly NoireString Sources = new("silk.card.sources", "Sources");
    public static readonly NoireString DefaultTag = new("silk.card.default", "Default");
    public static readonly NoireString DefaultText = new("silk.card.default.text", "Every character has this emote.");
    public static readonly SplitText OwnedBy = new(new("silk.card.owned_by", "Owned by {percent} of players"), "percent");
    public static readonly NoireString OpenPage = new("silk.card.open_page", "Open page");
    public static readonly NoireString FfxivCollect = new("silk.card.ffxiv_collect", "FFXIV Collect");

    public static readonly NoireString PlayOnYourself = new("silk.menu.play", "Play on yourself");
    public static readonly NoireString AssignToHotbarSlot = new("silk.menu.hotbar", "Assign to a hotbar slot");
    public static readonly NoireString ApplyOnMinion = new("silk.menu.minion", "Apply emote on minion");
    public static readonly NoireString ApplyOnPet = new("silk.menu.pet", "Apply emote on pet");
    public static readonly NoireString ApplyOnChocobo = new("silk.menu.chocobo", "Apply emote on chocobo");
    public static readonly NoireString AddOverride = new("silk.menu.override", "Add an override");

    public static readonly NoireString LoopExact = new("silk.dock.match.loop_exact", "Loop matches exactly");
    public static readonly NoireString LoopLenient = new("silk.dock.match.loop_lenient", "Loop matches leniently");
    public static readonly NoireString SoundExact = new("silk.dock.match.sound_exact", "Sound matches exactly");
    public static readonly NoireString SoundLenient = new("silk.dock.match.sound_lenient", "Sound matches leniently");
    public static readonly NoireString TurnExact = new("silk.dock.match.turn_exact", "Turn matches exactly");
    public static readonly NoireString TurnLenient = new("silk.dock.match.turn_lenient", "Turn matches leniently");
    public static readonly NoireString Close = new("common.close", "Close");
    public static readonly NoireString PreviewPopup = new("silk.dock.preview_popup", "Preview popup");
    public static readonly NoireString PreviewPopupTip = new("silk.dock.preview_popup.tip", "You can enable this again in the general settings at any time.");
    public static readonly NoireString NeverTarget = new("silk.dock.never_target", "Never use this emote as a swap target");
    public static readonly NoireString NoTextCommand = new("silk.dock.no_command", "No text command");
    public static readonly NoireString Locked = new("silk.dock.locked", "Locked");
    public static readonly NoireString SwapPreview = new("silk.dock.swap_preview", "Swap preview");
    public static readonly NoireString OverrideTag = new("silk.dock.tag.override", "Override");
    public static readonly NoireString IdlePoseTag = new("silk.dock.tag.idle_pose", "Idle pose");
    public static readonly NoireString YourIdlePoseLower = new("silk.dock.idle_pose.lower", "your idle pose");
    public static readonly NoireString YourIdlePose = new("silk.dock.idle_pose", "Your idle pose");
    public static readonly NoireString PlaysThrough = new("silk.dock.plays_through", "Plays through {emote}");
    public static readonly NoireString PoseNumber = new("silk.dock.pose", "Pose {pose}");
    public static readonly NoireString Loop = new("silk.dock.loop", "Loop");
    public static readonly NoireString Sound = new("silk.dock.sound", "Sound");
    public static readonly NoireString Turn = new("silk.dock.turn", "Turn");
    public static readonly NoireString Favourited = new("silk.dock.favourited", "Favourited");
    public static readonly NoireString Favourite = new("silk.dock.favourite", "Favourite");
    public static readonly NoireString BlockedAsTarget = new("silk.dock.blocked", "Blocked as target");
    public static readonly NoireString BlockAsTarget = new("silk.dock.block", "Block as target");
    public static readonly NoireString HotbarButton = new("silk.dock.hotbar", "Hotbar");
    public static readonly NoireString PlayOnCompanion = new("silk.dock.companion", "Play on a companion");
    public static readonly NoireString Minion = new("silk.dock.minion", "Minion");
    public static readonly NoireString Pet = new("silk.dock.pet", "Pet");
    public static readonly NoireString Chocobo = new("silk.dock.chocobo", "Chocobo");
    public static readonly NoireString PlayEmote = new("silk.dock.play", "Play emote");
    public static readonly NoireString OrDoubleClick = new("silk.dock.play.small", "or double-click");
}
