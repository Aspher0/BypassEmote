using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString ShowAllEmotes = new("main.show_all", "Show all emotes");
    public static readonly NoireString ShowInvalidEmotes = new("main.show_invalid", "Show invalid emotes");
    public static readonly NoireString ShowInvalidEmotesClassic = new("main.show_invalid.classic", "Show Invalid Emotes");
    public static readonly NoireString ShowIds = new("main.show_ids", "Show IDs");
    public static readonly NoireString SearchEmotes = new("main.search", "Search emotes...");
    public static readonly NoireString TabAll = new("main.tab.all", "All");
    public static readonly NoireString TabGeneral = new("main.tab.general", "General");
    public static readonly NoireString TabSpecial = new("main.tab.special", "Special");
    public static readonly NoireString TabExpressions = new("main.tab.expressions", "Expressions");
    public static readonly NoireString TabOther = new("main.tab.other", "Other");
    public static readonly NoireString TabFavourites = new("main.tab.favourites", "Fav");
    public static readonly NoireString TabBlocked = new("main.tab.blocked", "Blocked");
    public static readonly NoireString BlockedTooltip = new("main.row.blocked", "Blocked: no swap will land on this emote");
    public static readonly NoireString BlockTooltip = new("main.row.block", "Block this emote as a swap target");
    public static readonly NoireString PatchLine = new("main.row.patch", "Patch: {patch}");
    public static readonly NoireString LeftClickApply = new("main.row.left_click", "Left-click to apply to yourself");
    public static readonly NoireString RightClickOptions = new("main.row.right_click", "Right-click for more options");
    public static readonly NoireString DragToHotbar = new("main.row.drag", "Drag onto a hotbar slot to assign it");
    public static readonly NoireString ForceSwap = new("main.menu.force_swap", "Force swap");
    public static readonly NoireString OnlyEmoteSwapSwaps = new("main.menu.only_emote_swap", "Only for Emote Swap mode.");
    public static readonly NoireString ApplyOnMinionClassic = new("main.menu.minion.classic", "Apply emote on Minion");
    public static readonly NoireString ApplyOnPetClassic = new("main.menu.pet.classic", "Apply emote on Pet");
    public static readonly NoireString ApplyOnChocoboClassic = new("main.menu.chocobo.classic", "Apply emote on Chocobo");
    public static readonly NoireString AddOverrideClassic = new("main.menu.override.classic", "Add an override...");
    public static readonly NoireString AssignToHotbarClassic = new("main.menu.hotbar.classic", "Assign emote to Hotbar...");
    public static readonly NoireString CreateModFromEmote = new("main.menu.create_mod", "Create a mod from this emote...");
    public static readonly NoireString SupportOnKofi = new("main.kofi", "Support me on Ko-fi");
    public static readonly NoireString KofiTooltip = new("main.kofi.tooltip", "This plugin is free and always will be, donations are appreciated.");
    public static readonly NoireString Discord = new("main.discord", "Discord");
    public static readonly NoireString DiscordTooltip = new("main.discord.tooltip", "Help, bug reports and updates.");
    public static readonly NoireString AddFavoriteClassic = new("main.quick.favourite.classic", "Add an emote to your favorites...");
    public static readonly NoireString MarkedFavoriteClassic = new("main.quick.favourite.marked.classic", "(favorite)");
    public static readonly NoireString BlockEmotePlaceholder = new("main.quick.blocked", "Block an emote as a swap target...");
    public static readonly NoireString MarkedBlocked = new("main.quick.blocked.marked", "(blocked)");
    public static readonly NoireString NoFavoritedClassic = new("main.empty.favourites.classic", "No favorited emote");
    public static readonly NoireString NoBlockedEmote = new("main.empty.blocked", "No blocked emote");
    public static readonly NoireString RefreshLockedEmotes = new("main.toolbar.refresh.tooltip", "Refresh Locked Emotes");
    public static readonly NoireString CreateMod = new("main.toolbar.create_mod", "Create a mod");
    public static readonly NoireString SyncMenu = new("main.toolbar.sync_menu", "Sync...");
    public static readonly NoireString SyncEveryoneCommand = new("main.sync.everyone", "Sync everyone (/be sync)");
    public static readonly NoireString SyncDirectCommand = new("main.sync.direct", "Sync Direct Play only (/be syncdirect)");
}
