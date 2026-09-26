using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using Lumina.Excel.Sheets;
using NoireLib.Enums;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Numerics;
using EmoteCategory = NoireLib.Enums.EmoteCategory;

namespace BypassEmote.UI.Silk.Main;

internal enum SilkTab
{
    All,
    General,
    Special,
    Expressions,
    Other,
    Favourites,
    Blocked,
}

internal sealed class SilkEmoteEntry
{
    private static readonly (string Type, string Text)[] NoSources = [];

    private EmoteCondition conditions;
    private bool conditionsRead;
    private (string Type, string Text)[] sources = NoSources;
    private string? patch;
    private string? ownedShare;
    private bool sourcesFound;

    internal SilkEmoteEntry(Emote row)
    {
        Row = row;
        RowId = row.RowId;
        IconId = CommonHelper.GetEmoteIcon(row);
        var text = SheetLanguage.Display(row);
        var name = CommonHelper.GetEmoteName(text);
        unnamed = string.IsNullOrWhiteSpace(name);
        this.name = unnamed ? L.UnknownName.Source : name;
        IdText = row.RowId.ToString();
        Category = EmoteHelper.GetEmoteCategory(row);
        Invalid = !CommonHelper.IsEmoteDisplayable(row);
        Assignable = CommonHelper.IsEmoteAssignableToHotbar(row);
        IsDefault = row.UnlockLink == 0;

        var commands = new List<string>(4);
        var tc = text.TextCommand.ValueNullable;
        AddCommand(commands, tc?.Command.ExtractText());
        AddCommand(commands, tc?.ShortCommand.ExtractText());
        AddCommand(commands, tc?.Alias.ExtractText());
        AddCommand(commands, tc?.ShortAlias.ExtractText());
        Commands = commands.ToArray();
        CommandsJoined = string.Join(", ", Commands);
        SearchKey = (Name + " " + string.Join(" ", Commands) + " " + IdText).ToLowerInvariant();
    }

    internal Emote Row { get; }
    internal uint RowId { get; }
    internal uint IconId { get; }
    private readonly string name;
    private readonly bool unnamed;

    internal string Name => unnamed ? L.UnknownName.Text : name;
    internal string IdText { get; }
    private LiveText? idLabel;
    internal string IdLabel => (idLabel ??= new(L.EmoteId, "id", IdText)).Display;
    internal string[] Commands { get; }
    internal string CommandsJoined { get; }
    internal string SearchKey { get; }
    internal EmoteCategory Category { get; }
    internal string CategoryName => Category switch
    {
        EmoteCategory.General => L.TabGeneral.Text,
        EmoteCategory.Special => L.TabSpecial.Text,
        EmoteCategory.Expressions => L.TabExpressions.Text,
        _ => L.TabOther.Text,
    };
    internal bool Invalid { get; }
    internal bool Assignable { get; }
    internal bool IsDefault { get; }
    internal bool Owned { get; set; }
    internal bool Locked { get; set; }

    internal Vector3 Color => SilkVivid.Get(IconId);

    internal bool HasSources
    {
        get
        {
            ReadSources();
            return sources.Length > 0 || patch != null;
        }
    }

    internal (string Type, string Text)[] Sources
    {
        get
        {
            ReadSources();
            return sources;
        }
    }

    internal string? Patch
    {
        get
        {
            ReadSources();
            return patch;
        }
    }

    internal string? OwnedShare
    {
        get
        {
            ReadSources();
            return ownedShare;
        }
    }

    internal EmoteCondition Conditions
    {
        get
        {
            if (!conditionsRead)
            {
                conditionsRead = EmoteHelper.TryGetEmoteDetails(RowId, out var details);
                conditions = details?.Conditions ?? EmoteCondition.None;
            }

            return conditions;
        }
    }

    private LiveText? patchLabel;
    internal string PatchLabel => (patchLabel ??= new(L.EmotePatch, "patch", Patch ?? string.Empty)).Display;

    private LiveText? addedLabel;
    internal string AddedLabel => Patch is { } value ? (addedLabel ??= new(L.AddedInPatch, "patch", value)).Display : L.BaseGame.Text;

    private void ReadSources()
    {
        if (sourcesFound)
            return;

        lock (Service.EmoteSources)
        {
            if (!Service.EmoteSources.TryGetValue(RowId, out var found))
                return;

            sources = found.Sources.Count > 0 ? found.Sources.ToArray() : NoSources;
            patch = string.IsNullOrWhiteSpace(found.Patch) ? null : found.Patch;
            ownedShare = string.IsNullOrWhiteSpace(found.Owned) ? null : found.Owned;
            sourcesFound = true;
        }
    }

    private static void AddCommand(List<string> commands, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var command = text.StartsWith('/') ? text : "/" + text;

        foreach (var existing in commands)
        {
            if (string.Equals(existing, command, StringComparison.OrdinalIgnoreCase))
                return;
        }

        commands.Add(command);
    }
}

internal sealed class SilkEmoteModel
{
    private SilkEmoteEntry[] all = [];
    private readonly Dictionary<uint, SilkEmoteEntry> byId = new();
    private readonly HashSet<uint> lockedIds = new();
    private readonly List<SilkEmoteEntry> view = new(512);
    private readonly int[] counts = new int[7];

    private object? lockedSource;
    private bool showAll;
    private bool showInvalid;
    private SilkTab tab;
    private string query = string.Empty;
    private long favSignature = -1;
    private long blockSignature = -1;
    private bool dirty = true;
    private bool built;
    private int builtLanguage;

    internal IReadOnlyList<SilkEmoteEntry> View => view;

    internal IReadOnlyList<SilkEmoteEntry> All => all;

    internal SilkTab Tab => tab;

    internal string Query => query;

    internal int Version { get; private set; }

    internal int Count(SilkTab t) => counts[(int)t];

    internal SilkEmoteEntry? Get(uint rowId) => byId.TryGetValue(rowId, out var entry) ? entry : null;

    internal void SetTab(SilkTab value)
    {
        if (tab == value)
            return;

        tab = value;
        dirty = true;
    }

    internal void SetQuery(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed == query)
            return;

        query = trimmed;
        dirty = true;
    }

    internal void Invalidate() => dirty = true;

    internal void Update()
    {
        if (!built || builtLanguage != SheetLanguage.Version)
            BuildAll();

        var locked = Service.LockedEmotes;

        if (!ReferenceEquals(locked, lockedSource))
        {
            lockedSource = locked;
            lockedIds.Clear();

            foreach (var (emote, _) in locked)
                lockedIds.Add(emote.RowId);

            foreach (var entry in all)
            {
                entry.Locked = lockedIds.Contains(entry.RowId);
                entry.Owned = EmoteHelper.IsEmoteUnlocked(entry.RowId);
            }

            dirty = true;
        }

        if (Configuration.ShowAllEmotes != showAll)
        {
            showAll = Configuration.ShowAllEmotes;
            dirty = true;
        }

        if (Configuration.ShowInvalidEmotes != showInvalid)
        {
            showInvalid = Configuration.ShowInvalidEmotes;
            dirty = true;
        }

        var fav = Signature(Configuration.FavoriteEmotes);

        if (fav != favSignature)
        {
            favSignature = fav;
            dirty = true;
        }

        var blk = Signature(Configuration.BlockedTargetEmotesEmoteSwap);

        if (blk != blockSignature)
        {
            blockSignature = blk;
            dirty = true;
        }

        if (dirty)
            Rebuild();
    }

    internal bool IsFavourite(uint rowId) => Configuration.FavoriteEmotes.Contains(rowId);

    internal bool IsBlocked(uint rowId) => Configuration.BlockedTargetEmotesEmoteSwap.Contains(rowId);

    internal void ToggleFavourite(uint rowId)
    {
        EmoteActions.ToggleFavourite(rowId);
        dirty = true;
    }

    internal void ToggleBlocked(uint rowId)
    {
        EmoteActions.ToggleBlocked(rowId);
        dirty = true;
    }

    private static long Signature(List<uint> ids)
    {
        long sum = ids.Count;

        foreach (var id in ids)
            sum = sum * 31 + id;

        return sum;
    }

    private void BuildAll()
    {
        built = true;
        builtLanguage = SheetLanguage.Version;
        dirty = true;

        var sheet = ExcelSheetHelper.GetSheet<Emote>();

        if (sheet == null)
        {
            built = false;
            return;
        }

        var list = new List<SilkEmoteEntry>(sheet.Count);
        byId.Clear();

        foreach (var row in sheet)
        {
            if (CommonHelper.GetEmotePlayType(row) == EmotePlayType.DoNotPlay)
                continue;

            var entry = new SilkEmoteEntry(row);
            list.Add(entry);
            byId[entry.RowId] = entry;
        }

        list.Sort(static (a, b) => b.RowId.CompareTo(a.RowId));
        all = list.ToArray();
        lockedSource = null;
    }

    private bool Listed(SilkEmoteEntry entry, SilkTab t)
    {
        var keep = t is SilkTab.Favourites or SilkTab.Blocked;
        return (keep || showAll || entry.Locked) && (keep || showInvalid || !entry.Invalid);
    }

    private bool InTab(SilkEmoteEntry entry, SilkTab t) => t switch
    {
        SilkTab.All => true,
        SilkTab.Favourites => IsFavourite(entry.RowId),
        SilkTab.Blocked => IsBlocked(entry.RowId),
        SilkTab.General => entry.Category == EmoteCategory.General,
        SilkTab.Special => entry.Category == EmoteCategory.Special,
        SilkTab.Expressions => entry.Category == EmoteCategory.Expressions,
        _ => entry.Category == EmoteCategory.Unknown,
    };

    private void Rebuild()
    {
        dirty = false;
        Version++;
        view.Clear();
        Array.Clear(counts);

        foreach (var entry in all)
        {
            for (var t = 0; t < counts.Length; t++)
            {
                if (Listed(entry, (SilkTab)t) && InTab(entry, (SilkTab)t))
                    counts[t]++;
            }

            if (!Listed(entry, tab) || !InTab(entry, tab))
                continue;

            if (query.Length > 0 && !entry.SearchKey.Contains(query, StringComparison.Ordinal))
                continue;

            view.Add(entry);
        }
    }
}
