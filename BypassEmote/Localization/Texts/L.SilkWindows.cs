
using NoireLib.Localizer;
using System.Globalization;

namespace BypassEmote.Localization;

internal static partial class L
{
    public static readonly NoireString NewBadge = new("silk.changelog.new", "NEW");

    public static readonly NoireString LogsSearch = new("silk.logs.search", "Search messages, categories, sources...");
    public static readonly NoireString AllCategories = new("silk.logs.all_categories", "All categories");
    public static readonly NoirePlural CategoriesCount = new("silk.logs.categories", "{n} categories", "{n} categories");
    public static readonly NoireString Options = new("silk.logs.options", "Options");
    public static readonly NoireString Add = new("silk.logs.add", "Add");
    public static readonly NoireString AddEntry = new("silk.logs.add_entry", "Add entry");
    public static readonly NoireString CategoryHint = new("silk.logs.category_hint", "Category");
    public static readonly NoireString MessageHint = new("silk.logs.message_hint", "What happened?");
    public static readonly NoireString Export = new("silk.logs.export", "Export");
    public static readonly NoireString HoldToClear = new("silk.logs.hold", "Hold to clear");
    public static readonly NoireString ExportTip = new("silk.logs.export.tip", "Same as /belogs: exports everything since the plugin loaded");
    public static readonly NoireString HoldTipMemory = new("silk.logs.hold.memory", "Hold to clear the in-memory entries");
    public static readonly NoireString HoldTipDatabase = new("silk.logs.hold.database", "Hold to clear the database entries");
    public static readonly NoireString PersistToDatabase = new("silk.logs.persist", "Persist to database");
    public static readonly NoireString ShowColors = new("silk.logs.colors", "Show colors");
    public static readonly NoireString SplitLines = new("silk.logs.split", "Split lines");
    public static readonly NoireString HideCategory = new("silk.logs.hide_category", "Hide category");
    public static readonly NoireString HideSource = new("silk.logs.hide_source", "Hide source");
    public static readonly NoirePlural ShowingEntries = new("silk.logs.showing",
        "Showing {first}-{last} of {n} entries ({total} total)", "Showing {first}-{last} of {n} entries ({total} total)");
    public static readonly NoirePlural NoEntries = new("silk.logs.none_of", "0 of {n} entries", "0 of {n} entries");
    public static readonly NoireString NoEntryMatches = new("silk.logs.empty", "No entry matches these filters.");
    public static readonly NoireString TimeHead = new("silk.logs.head.time", "Time");
    public static readonly NoireString LevelHead = new("silk.logs.head.level", "Level");
    public static readonly NoireString CategoryHead = new("silk.logs.head.category", "Category");
    public static readonly NoireString MessageHead = new("silk.logs.head.message", "Message");
    public static readonly NoireString SourceHead = new("silk.logs.head.source", "Source");
    public static readonly NoireString LevelTrace = new("silk.logs.level.trace", "Trace");
    public static readonly NoireString LevelDebug = new("silk.logs.level.debug", "Debug");
    public static readonly NoireString LevelInfo = new("silk.logs.level.info", "Info");
    public static readonly NoireString LevelWarning = new("silk.logs.level.warning", "Warning");
    public static readonly NoireString LevelError = new("silk.logs.level.error", "Error");
    public static readonly NoireString LevelCritical = new("silk.logs.level.critical", "Critical");
    public static readonly NoirePlural DeleteSelected = new("silk.logs.menu.delete_selected", "Delete selected entries ({n})", "Delete selected entries ({n})");
    public static readonly NoireString DeleteEntry = new("silk.logs.menu.delete", "Delete entry");
    public static readonly NoireString HoldCtrlToDelete = new("silk.logs.menu.delete.hint", "Hold CTRL to delete");
    public static readonly NoirePlural CopyLinesMultiple = new("silk.logs.menu.copy_lines_multiple",
        "Copy selected lines from multiple entries to clipboard ({n})", "Copy selected lines from multiple entries to clipboard ({n})");
    public static readonly NoireString CopyLine = new("silk.logs.menu.copy_line", "Copy selected line to clipboard");
    public static readonly NoireString CopyLines = new("silk.logs.menu.copy_lines", "Copy selected lines to clipboard");
    public static readonly NoirePlural CopyEntries = new("silk.logs.menu.copy_entries", "Copy selected entries to clipboard ({n})", "Copy selected entries to clipboard ({n})");
    public static readonly NoireString CopyEntry = new("silk.logs.menu.copy_entry", "Copy entry to clipboard");
    public static readonly NoirePlural CopyMessages = new("silk.logs.menu.copy_messages", "Copy only messages to clipboard ({n})", "Copy only messages to clipboard ({n})");
    public static readonly NoireString CopyMessage = new("silk.logs.menu.copy_message", "Copy only message to clipboard");

    public static string Count(NoirePlural text, int count)
        => text.For(count).Replace("{n}", count.ToString(CultureInfo.InvariantCulture));

    public static ChatText CountChat(NoirePlural text, int count)
    {
        var number = count.ToString(CultureInfo.InvariantCulture);
        var english = text.Forms[count == 1 ? PluralCategory.One : PluralCategory.Other];
        return new(Count(text, count), english.Replace("{n}", number));
    }
}
