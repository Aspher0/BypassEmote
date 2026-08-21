using BypassEmote.Helpers;
using BypassEmote.IPC;
using BypassEmote.Models;
using Newtonsoft.Json;
using NoireLib;
using NoireLib.Helpers;
using Penumbra.Api.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed class SwapModManager
{
    public const string LegacySharedModDirectoryName = "_BypassEmoteGenerated";

    private const string LogPrefix = "[SwapModManager] ";
    private const string RegistryFileName = "swap_registry.json";
    private const string StaleManifestFileName = "swap_manifest.json";
    private const string CharactersFolderName = "characters";
    private const string SwapsSubfolderName = "swaps";
    private const string SwapFileSearchPattern = "swap_*.*";
    private const string SwapFilePrefix = "swap_";

    private const int SwapFileTagLength = 8;
    private const int CurrentRegistrySchemaVersion = 1;
    private const int MaxPriorityPasses = 4;

    private static readonly ContentAddressedStore Store =
        new(SwapFilePrefix, SwapFileTagLength, SwapFileSearchPattern);

    private static readonly JsonSerializerSettings IndentedJson = new() { Formatting = Formatting.Indented };

    private static readonly IReadOnlyDictionary<string, string> NoRedirects = new Dictionary<string, string>();

    private const string GeneratedModDescription =
        "Made by BypassEmote automatically. Safe to disable or delete. BypassEmote will recreate it when needed.";

    internal static ModMeta MetaFor(string modName)
        => new(modName, Service.PenumbraModAuthor, GeneratedModDescription, Service.PenumbraModVersion,
            Service.PenumbraModWebsite);

    private readonly IPCCaller_Penumbra _gateway;
    private readonly SwapModIdentity _identity;
    private readonly string _configDirectory;

    private readonly ReentrancyGuard _ownMutations = new();

    private string? _pressedKey;

    public SwapModManager(IPCCaller_Penumbra gateway, SwapModIdentity identity, string configDirectory)
    {
        _gateway = gateway;
        _identity = identity;
        _configDirectory = configDirectory;

        gateway.OwnModSettingChanged += HandleExternalChange;
        gateway.OwnModDeleted += HandleOwnModDeleted;
        gateway.ExternalModChanged += HandleCompetingModChange;

        identity.Changed += HandleIdentityChanged;

        Registry = LoadRegistryFromDisk();
    }

    public SwapRegistry Registry { get; private set; }

    public string ModDirectoryName => _identity.Names?.Directory ?? string.Empty;

    private string GeneratedModName => _identity.Names?.Display ?? "BypassEmote Generated";

    private string? CharacterDirectory
        => _identity.Names is { } names ? CharacterDirectoryCore(_configDirectory, names.CharacterKey) : null;

    private string? ModDirectory
        => _gateway.GetModRootDirectory() is { Length: > 0 } modRoot && _identity.Names is { } names
            ? Path.Combine(modRoot, names.Directory)
            : null;

    internal static string CharacterDirectoryCore(string configDirectory, string characterKey)
        => Path.Combine(configDirectory, CharactersFolderName, characterKey);

    private void HandleIdentityChanged(SwapModNames? previous)
    {
        if (previous != null && _identity.Names is { } names
            && string.Equals(previous.Directory, names.Directory, StringComparison.OrdinalIgnoreCase))
        {
            RenameModInPlace(names.Display);
            return;
        }

        if (previous != null)
            DeselectAllUnder(previous);

        Registry = LoadRegistryFromDisk();
        PushRedirectScope();
    }

    public void SaveDispatch(IReadOnlyList<DispatchRecord> dispatch)
    {
        Registry = Registry with { Dispatch = dispatch };
        PersistRegistry();
    }

    private void RenameModInPlace(string display)
    {
        if (ModDirectory is not { } modDirectory || !Directory.Exists(modDirectory))
            return;

        try
        {
            using (_ownMutations.Enter())
            {
                if (SimpleV3ModWriter.Write(modDirectory, MetaFor(display), new Dictionary<string, string>()))
                    EnsurePenumbraReadsTheMod(isFirstCreation: false);
            }

            NoireLogger.LogDebug($"The generated mod is now named '{display}'.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Could not rename the generated mod to '{display}'.", LogPrefix);
        }
    }

    public SchedulerResidencyProbe? ResidencyProbe
    {
        get => _residencyProbe;
        set
        {
            _residencyProbe = value;
            PushRedirectScope();
        }
    }

    private SchedulerResidencyProbe? _residencyProbe;

    private void PushRedirectScope()
    {
        try
        {
            var selected = Registry.Entries.Where(entry => entry.SelectedByUs).ToList();
            var paths = selected.SelectMany(ServedPathsOf).ToList();

            var pressed = _pressedKey is { } key ? selected.FirstOrDefault(entry => entry.ContentKey == key) : null;

            _residencyProbe?.SetRedirectedScope(pressed?.TargetEmote ?? 0, paths,
                pressed?.UniqueNameByKey, pressed?.InternalNames, pressed?.ContentKey);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not push the redirect scope; the probe keeps its previous scope.", LogPrefix);
        }
    }

    private IEnumerable<string> ServedPathsOf(SwapOptionEntry entry)
        => Registry.Skeleton is { } skeleton && entry.FilesByRace.TryGetValue(skeleton, out var files)
            ? files.Keys
            : [];

    public bool IsOwnPath(string resolvedDiskPath)
        => IsOwnPathCore(resolvedDiskPath, _gateway.GetModRootDirectory(), _configDirectory);

    public static string DeriveFileName(byte[] papBytes)
        => DeriveFileName(papBytes, ".pap");

    public static string DeriveFileName(byte[] bytes, string extension)
        => Store.NameFor(bytes, extension);

    internal static string FileExtensionFor(string gamePath)
    {
        var dot = gamePath.LastIndexOf('.');
        return dot < 0 ? ".pap" : gamePath[dot..];
    }

    public SwapOptionEntry? FindReusable(string contentKey)
    {
        if (KeptWithKey(contentKey) is not { } entry)
            return null;

        return AllFilesExist(entry) ? entry : null;
    }

    public SwapOptionEntry? KeptWithKey(string contentKey) => RegistryDecisions.FindByKey(Registry, contentKey);

    public SwapOptionEntry? ArmedFor(uint targetEmote) => RegistryDecisions.FindArmedByTarget(Registry, targetEmote);

    public bool SelectExisting(SwapOptionEntry entry)
    {
        if (_identity.Names == null)
            return false;

        foreach (var sibling in Registry.Entries
            .Where(other => other.SelectedByUs && other.GroupName == entry.GroupName && other.ContentKey != entry.ContentKey)
            .ToList())
        {
            UpdateEntry(sibling with { SelectedByUs = false });
        }

        UpdateEntry(entry with { SelectedByUs = true, LastUsedStamp = NextStamp() });

        if (Configuration.SwapBehavior == SwapBehavior.OneAtATime)
        {
            foreach (var other in Registry.Entries
                .Where(other => other.SelectedByUs && other.GroupName != entry.GroupName).ToList())
            {
                UpdateEntry(other with { SelectedByUs = false });
            }
        }

        _pressedKey = entry.ContentKey;

        if (!PushSelections())
        {
            UpdateEntry(entry with { SelectedByUs = false });
            _pressedKey = null;

            return false;
        }

        PushRedirectScope();

        return true;
    }

    public void DeselectEntry(SwapOptionEntry entry)
    {
        if (_pressedKey == entry.ContentKey)
            _pressedKey = null;

        UpdateEntry(entry with { SelectedByUs = false });

        PushSelections();
        PushRedirectScope();
    }

    public void DeselectAll()
    {
        var selected = Registry.Entries.Where(entry => entry.SelectedByUs).ToList();

        if (selected.Count == 0)
            return;

        foreach (var entry in selected)
            UpdateEntry(entry with { SelectedByUs = false });

        _pressedKey = null;

        PushSelections();
        PushRedirectScope();
    }

    private bool PushSelections()
    {
        if (_identity.Names is not { } names)
            return false;

        var collection = CollectionForSelection();
        var armed = Registry.Entries.Where(entry => entry.SelectedByUs).ToList();

        using (_ownMutations.Enter())
        {
            if (armed.Count == 0)
                return _gateway.RemoveTemporarySettings(collection, names.Directory);

            var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (var entry in Registry.Entries)
                selections[entry.GroupName] = [OptionNaming.NoneOptionName];

            foreach (var entry in armed)
                selections[entry.GroupName] = [entry.OptionName];

            var ec = _gateway.SetTemporarySettings(collection, names.Directory, enabled: true,
                Registry.AppliedPriority, selections);

            if (IsSuccess(ec))
                return true;

            var version = _gateway.ReportedApiVersion();

            NoireLogger.LogError(
                $"Penumbra refused the settings of '{names.Directory}' in collection {collection} (ec={ec}, "
                + $"Penumbra api {(version is { } v ? $"{v.Breaking}.{v.Feature}" : "unknown")}). "
                + $"Penumbra holds: {PenumbraOptionsLine(armed[0].GroupName, armed[0].OptionName)}", LogPrefix);

            return false;
        }
    }

    private void DeselectAllUnder(SwapModNames names)
    {
        if (!Registry.Entries.Any(entry => entry.SelectedByUs))
            return;

        using (_ownMutations.Enter())
            _gateway.RemoveTemporarySettings(Registry.CollectionId, names.Directory);
    }

    public bool AddAndSelect(SwapOptionEntry entry, IReadOnlyDictionary<string, byte[]> filesToWrite, string drawnRace)
    {
        if (ModDirectory is not { } modDirectory || _identity.Names == null)
            return false;

        if (RaceForNewOption(entry, drawnRace) is not { } race)
        {
            NoireLogger.LogError($"'{entry.OptionName}' carries no files for any body, so it cannot be kept.", LogPrefix);
            return false;
        }

        if (Registry.Skeleton != race && !RewriteForSkeleton(SkeletonRewritePlanner.For(Registry, race), race))
            return false;

        var folder = PrepareGeneratedModFolder(modDirectory, GeneratedModName);

        if (folder == GeneratedModFolder.Unusable || !WriteSwapFiles(modDirectory, filesToWrite))
            return false;

        var groups = ReadGroups();

        var group = groups.TryGetValue(entry.GroupName, out var existing)
            ? existing.Group
            : ModGroupFile.NewGroup(entry.GroupName);

        var isNewOption = !group.Options.Any(option => option.Name == entry.OptionName);

        if (isNewOption
            && RegistryDecisions.EvictionCandidate(Registry, entry.GroupName, Configuration.MaxKeptSwapsPerTarget) is { } evicted)
        {
            group = ModGroupFile.Remove(group, evicted.OptionName);
            RemoveEntry(evicted);

            NoireLogger.LogDebug($"'{evicted.OptionName}' was dropped from '{entry.GroupName}' to stay under the cap.", LogPrefix);
        }

        group = isNewOption
            ? ModGroupFile.Add(group, new ModGroupOption(entry.OptionName, entry.FilesByRace[race]))
            : ModGroupFile.WithFiles(group, entry.OptionName, entry.FilesByRace[race]);

        using (_ownMutations.Enter())
        {
            if (!WriteGroup(group, IndexFor(groups, entry.GroupName))
                || !EnsurePenumbraReadsTheMod(folder == GeneratedModFolder.Created))
            {
                return false;
            }
        }

        UpdateEntry(entry);
        ReassertSelections();
        SweepUnreferencedFiles();

        var selected = SelectExisting(entry);

        if (selected && _gateway.RefreshOwnPanel())
            NoireLogger.LogDebug("The mod's panel was on screen, so its option list was refreshed.", LogPrefix);

        return selected;
    }

    private const int MaxJudgementsPerSwap = 8;

    internal int ApplyRulesPlan(string stamp, Func<SwapOptionEntry, RegistryDecisions.RulesVerdict> judge)
    {
        var plan = RegistryDecisions.JudgeAgainstRules(Registry, stamp, _pressedKey, judge,
            Configuration.MaxKeptSwapsPerTarget, MaxJudgementsPerSwap);

        var restamped = plan.Entries.Count(entry => entry.RulesStamp == stamp)
            != Registry.Entries.Count(entry => entry.RulesStamp == stamp);

        if (plan.Dropped.Count == 0 && !restamped)
            return 0;

        Registry = Registry with { Entries = plan.Entries };
        PersistRegistry();

        if (plan.Dropped.Count == 0)
            return 0;

        NoireLogger.LogDebug($"{plan.Dropped.Count} kept swap(s) the settings would no longer make were dropped: "
            + string.Join(", ", plan.Dropped.Select(entry => $"'{entry.OptionName}' in '{entry.GroupName}'")), LogPrefix);

        DropOptionsFromDisk(plan.Dropped);

        SweepUnreferencedFiles();
        ReassertSelections();
        PushRedirectScope();

        if (_gateway.RefreshOwnPanel())
            NoireLogger.LogDebug("The mod's panel was on screen, so its option list was refreshed.", LogPrefix);

        return plan.Dropped.Count;
    }

    private void DropOptionsFromDisk(IReadOnlyList<SwapOptionEntry> dropped)
    {
        if (ModDirectory is not { } modDirectory)
            return;

        var groups = ReadGroups();
        var touched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in dropped)
        {
            if (!groups.TryGetValue(entry.GroupName, out var onDisk))
                continue;

            groups[entry.GroupName] = onDisk with { Group = ModGroupFile.Remove(onDisk.Group, entry.OptionName) };
            touched.Add(entry.GroupName);
        }

        using (_ownMutations.Enter())
        {
            foreach (var groupName in touched)
            {
                // A group left with nothing but its empty option is a row of the mod panel that serves no swap.
                if (ModGroupFile.IsEmpty(groups[groupName].Group))
                    RemoveRivalGroupFiles(modDirectory, groupName, keptFileName: string.Empty);
                else
                    WriteGroup(groups[groupName].Group, groups[groupName].Index);
            }

            EnsurePenumbraReadsTheMod(isFirstCreation: false);
        }
    }

    private static bool IsSuccess(PenumbraApiEc ec) => ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;

    private string PenumbraOptionsLine(string wantedGroup, string wantedOption)
    {
        if (_identity.Names is not { } names)
            return "no mod name yet";

        if (_gateway.GetAvailableOptions(names.Directory) is not { } available)
            return "nothing at all, so Penumbra does not hold the mod";

        if (available.Count == 0)
            return "the mod, but not one group";

        var groups = string.Join("; ", available.Select(group => $"{group.Key} [{string.Join(", ", group.Value)}]"));

        var exact = available.Keys.Any(name => string.Equals(name, wantedGroup, StringComparison.Ordinal));

        var loose = available.Keys.FirstOrDefault(name => string.Equals(name, wantedGroup, StringComparison.OrdinalIgnoreCase));

        var groupVerdict = exact
            ? "the group name matches exactly"
            : loose is { } near
                ? $"no exact group match; closest is {Spell(near)} against {Spell(wantedGroup)}"
                : $"no group match at all for {Spell(wantedGroup)}";

        var optionVerdict = loose is { } matched && available[matched].Any(name => string.Equals(name, wantedOption, StringComparison.Ordinal))
            ? "the option name matches exactly"
            : $"no exact option match for {Spell(wantedOption)}";

        return $"{groups} | {groupVerdict} | {optionVerdict}";
    }

    private static string Spell(string value)
        => $"'{value}' ({value.Length} chars: {string.Join(" ", value.Select(character => ((int)character).ToString("x2")))})";

    private string? RaceForNewOption(SwapOptionEntry entry, string drawnRace)
    {
        if (entry.FilesByRace.ContainsKey(drawnRace))
            return drawnRace;

        return Registry.Skeleton is { } skeleton && entry.FilesByRace.ContainsKey(skeleton)
            ? skeleton
            : entry.FilesByRace.Keys.FirstOrDefault();
    }

    private static bool WriteSwapFiles(string modDirectory, IReadOnlyDictionary<string, byte[]> filesToWrite)
    {
        foreach (var (gamePath, bytes) in filesToWrite)
        {
            var relativePath = RedirectedPathValue(DeriveFileName(bytes, FileExtensionFor(gamePath)));

            if (!Store.WriteAt(Path.Combine(modDirectory, relativePath), bytes))
                return false;
        }

        return true;
    }

    private static void RemoveRivalGroupFiles(string modDirectory, string groupName, string keptFileName)
    {
        if (!Directory.Exists(modDirectory))
            return;

        foreach (var path in Directory.GetFiles(modDirectory, ModGroupFile.FileNamePrefix + "*.json"))
        {
            if (string.Equals(Path.GetFileName(path), keptFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (ModGroupFile.Deserialize(File.ReadAllText(path)) is { } rival && rival.Name == groupName)
                {
                    File.Delete(path);
                    NoireLogger.LogDebug(keptFileName.Length == 0
                        ? $"Removed '{Path.GetFileName(path)}', the last file of group '{groupName}'."
                        : $"Removed '{Path.GetFileName(path)}', a second file for group '{groupName}'.", LogPrefix);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NoireLogger.LogDebug($"Could not read or remove '{path}' ({ex.Message}).", LogPrefix);
            }
        }
    }

    private sealed record GroupOnDisk(ModGroup Group, int Index);

    private Dictionary<string, GroupOnDisk> ReadGroups()
    {
        var groups = new Dictionary<string, GroupOnDisk>(StringComparer.Ordinal);

        if (ModDirectory is not { } modDirectory || !Directory.Exists(modDirectory))
            return groups;

        foreach (var path in Directory.EnumerateFiles(modDirectory, ModGroupFile.FileNamePrefix + "*.json"))
        {
            try
            {
                if (ModGroupFile.Deserialize(File.ReadAllText(path)) is not { } group)
                    continue;

                if (!groups.TryGetValue(group.Name, out var seen) || group.Options.Count > seen.Group.Options.Count)
                    groups[group.Name] = new GroupOnDisk(group, IndexInFileName(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NoireLogger.LogDebug($"Could not read the group file '{path}' ({ex.Message}).", LogPrefix);
            }
        }

        return groups;
    }

    private bool WriteGroup(ModGroup group, int index)
    {
        if (ModDirectory is not { } modDirectory)
            return false;

        var fileName = ModGroupFile.FileNameFor(group.Name, index);

        try
        {
            RemoveRivalGroupFiles(modDirectory, group.Name, fileName);

            AtomicFile.WriteAllText(Path.Combine(modDirectory, fileName), ModGroupFile.Serialize(group));

            return true;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the group file for '{group.Name}'.", LogPrefix);
            return false;
        }
    }

    internal static int IndexInFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        if (name.Length <= ModGroupFile.FileNamePrefix.Length)
            return 0;

        var digits = new string(name[ModGroupFile.FileNamePrefix.Length..].TakeWhile(char.IsAsciiDigit).ToArray());

        return digits.Length > 0 && int.TryParse(digits, out var index) ? index : 0;
    }

    private static int IndexFor(Dictionary<string, GroupOnDisk> groups, string groupName)
        => groups.TryGetValue(groupName, out var existing) && existing.Index > 0
            ? existing.Index
            : groups.Values.Select(entry => entry.Index).DefaultIfEmpty(0).Max() + 1;

    public string? GroupNameForTarget(uint targetEmote)
        => Registry.Entries.FirstOrDefault(entry => entry.TargetEmote == targetEmote)?.GroupName;

    public IReadOnlySet<string> TakenGroupNames()
        => Registry.Entries.Select(entry => entry.GroupName).ToHashSet(StringComparer.Ordinal);

    public IReadOnlySet<string> TakenOptionNames(string groupName)
        => Registry.Entries.Where(entry => entry.GroupName == groupName)
            .Select(entry => entry.OptionName)
            .ToHashSet(StringComparer.Ordinal);

    public ModState? PenumbraState()
    {
        if (_identity.Names is not { } names)
            return null;

        var states = _gateway.GetAllModStates(Registry.CollectionId);

        return states != null && states.TryGetValue(names.Directory, out var state) ? state : null;
    }
    public long SwapFilesSize()
    {
        if (ModDirectory is not { } modDirectory)
            return 0;

        var swapsDirectory = Path.Combine(modDirectory, SwapsSubfolderName);

        if (!Directory.Exists(swapsDirectory))
            return 0;

        try
        {
            return Directory.EnumerateFiles(swapsDirectory, SwapFileSearchPattern)
                .Sum(path => new FileInfo(path).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoireLogger.LogDebug($"Could not measure '{swapsDirectory}' ({ex.Message}).", LogPrefix);
            return 0;
        }
    }

    internal bool RewriteForSkeleton(SkeletonRewritePlanner.RewritePlan plan, string newSkeleton)
    {
        if (plan.Rewrites.Count == 0)
        {
            Registry = Registry with { Skeleton = newSkeleton };
            PersistRegistry();

            PushRedirectScope();

            return true;
        }

        var groups = ReadGroups();

        foreach (var rewrite in plan.Rewrites)
        {
            if (groups.TryGetValue(rewrite.GroupName, out var onDisk))
            {
                groups[rewrite.GroupName] =
                    onDisk with { Group = ModGroupFile.WithFiles(onDisk.Group, rewrite.OptionName, rewrite.Files) };
            }
        }

        using (_ownMutations.Enter())
        {
            foreach (var onDisk in groups.Values)
            {
                if (!WriteGroup(onDisk.Group, onDisk.Index))
                    return false;
            }

            if (!EnsurePenumbraReadsTheMod(isFirstCreation: false))
                return false;
        }

        Registry = Registry with { Skeleton = newSkeleton };
        PersistRegistry();

        ReassertSelections();
        PushRedirectScope();

        return true;
    }

    public void StartupSweep()
    {
        Registry = LoadRegistryFromDisk();

        DeselectAll();
        ReconcileWithDisk();
        SweepUnreferencedFiles();
    }

    public void ForgetAll()
    {
        DeselectAll();

        NoireLogger.LogDebug($"Dropping {Registry.Entries.Count} kept swap(s) and everything they name.", LogPrefix);

        Registry = Registry with { Entries = [] };
        _pressedKey = null;
        PersistRegistry();

        var hadModFolder = ModDirectory is { } modDirectory && Directory.Exists(modDirectory);

        DeleteGroupFiles();

        SweepUnreferencedFiles();

        if (hadModFolder)
        {
            EnsureEmptyDefaultMap();

            using (_ownMutations.Enter())
                EnsurePenumbraReadsTheMod(isFirstCreation: false);
        }

        PushRedirectScope();
    }

    private void DeleteGroupFiles()
    {
        if (ModDirectory is not { } modDirectory || !Directory.Exists(modDirectory))
            return;

        foreach (var path in Directory.GetFiles(modDirectory, ModGroupFile.FileNamePrefix + "*.json"))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NoireLogger.LogDebug($"Could not delete the group file '{path}' ({ex.Message}).", LogPrefix);
            }
        }
    }

    private void ReconcileWithDisk()
    {
        if (_identity.Names is not { } names)
            return;

        var groups = ReadGroups();

        var optionsOnDisk = groups.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Group.Options.Select(option => option.Name).ToList(),
            StringComparer.Ordinal);

        var plan = RegistryDecisions.Reconcile(Registry, optionsOnDisk,
            new Dictionary<string, string>(StringComparer.Ordinal));

        Registry = Registry with { Entries = plan.Entries };
        PersistRegistry();

        var mapEmptied = EnsureEmptyDefaultMap();

        if (plan.OrphanOptions.Count == 0)
        {
            if (mapEmptied)
                ReloadAndReassert();

            return;
        }

        foreach (var (groupName, optionName) in plan.OrphanOptions)
        {
            if (groups.TryGetValue(groupName, out var onDisk))
                groups[groupName] = onDisk with { Group = ModGroupFile.Remove(onDisk.Group, optionName) };
        }

        NoireLogger.LogDebug($"{plan.OrphanOptions.Count} option(s) nothing knows about were dropped.", LogPrefix);

        using (_ownMutations.Enter())
        {
            foreach (var onDisk in groups.Values)
                WriteGroup(onDisk.Group, onDisk.Index);
        }

        ReloadAndReassert();
    }

    private void ReloadAndReassert()
    {
        using (_ownMutations.Enter())
            EnsurePenumbraReadsTheMod(isFirstCreation: false);

        ReassertSelections();
    }

    private void ReassertSelections() => PushSelections();

    private bool EnsureEmptyDefaultMap()
    {
        if (ModDirectory is not { } modDirectory || !Directory.Exists(modDirectory))
            return false;

        return WriteRealModJsons(modDirectory, GeneratedModName, NoRedirects) == true;
    }

    private void SweepUnreferencedFiles()
    {
        if (ModDirectory is not { } modDirectory)
            return;

        var referenced = Registry.Entries
            .SelectMany(entry => entry.FilesByRace.Values)
            .SelectMany(files => files.Values);

        Store.RemoveUnreferenced(Path.Combine(modDirectory, SwapsSubfolderName), referenced);
    }

    public void HandleExternalChange()
    {
        if (_ownMutations.IsInside)
            return;

        ReconcileWithDisk();
        PushRedirectScope();
    }

    private void HandleOwnModDeleted()
    {
        if (_ownMutations.IsInside)
            return;

        NoireLogger.LogDebug("Penumbra no longer holds the generated mod; the registry is emptied.", LogPrefix);

        Registry = Registry with { Entries = [] };
        _pressedKey = null;

        PersistRegistry();
        PushRedirectScope();
    }

    public void HandleCompetingModChange(Guid collectionId)
    {
        if (_ownMutations.IsInside || collectionId != Registry.CollectionId)
            return;

        ReconcilePriority();
    }

    public void ReconcilePriority()
    {
        if (_identity.Names is not { } names)
            return;

        for (var pass = 0; pass < MaxPriorityPasses; pass++)
        {
            var servedPaths = SelectedGamePaths();

            if (servedPaths.Count == 0)
                return;

            var competitors = CompetingMods(servedPaths, out var target);
            var listChanged = !SameMods(Registry.CompetingMods, competitors);

            if (target == Registry.AppliedPriority)
            {
                if (!listChanged)
                    return;

                Registry = Registry with { CompetingMods = competitors };
                PersistRegistry();
                return;
            }

            Registry = Registry with { AppliedPriority = target };

            if (!PushSelections())
            {
                NoireLogger.LogError(
                    $"Failed to rank '{names.Directory}' at {target} in collection {Registry.CollectionId}.", LogPrefix);
                return;
            }

            NoireLogger.LogDebug($"Swap mod priority moved {Registry.AppliedPriority} -> {target}"
                + $" against {competitors.Count} competing mod(s).", LogPrefix);

            Registry = Registry with { AppliedPriority = target, CompetingMods = competitors };
            PersistRegistry();
        }
    }

    private IReadOnlyList<string> SelectedGamePaths()
        => Registry.Entries.Where(entry => entry.SelectedByUs)
            .SelectMany(ServedPathsOf)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private IReadOnlyList<string> CompetingMods(IReadOnlyList<string> servedPaths, out int priority)
    {
        var modRoot = _gateway.GetModRootDirectory();
        var winners = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unattributable = false;

        foreach (var gamePath in servedPaths)
        {
            var resolved = _gateway.ResolvePlayerPath(gamePath);
            if (resolved == gamePath || IsOwnPathCore(resolved, modRoot, _configDirectory))
                continue;

            if (ModDirectoryFromDiskPath(resolved, modRoot) is { } directory)
            {
                if (seen.Add(directory))
                    winners.Add(directory);
            }
            else
            {
                unattributable = true;
            }
        }

        var recorded = Registry.CompetingMods ?? [];

        if (winners.Count == 0 && recorded.Count == 0)
        {
            priority = unattributable ? 1 : 0;
            return winners;
        }

        var states = _gateway.GetAllModStates(Registry.CollectionId);

        return RankAgainst(winners, recorded, unattributable,
            directory => states != null && states.TryGetValue(directory, out var state) ? state : null, out priority);
    }

    internal static IReadOnlyList<string> RankAgainst(IReadOnlyList<string> winners, IReadOnlyList<string>? recorded,
        bool beatsAnUnattributableRedirect, Func<string, ModState?> stateOf, out int priority)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>(winners.Count + (recorded?.Count ?? 0));

        int? highest = beatsAnUnattributableRedirect ? 0 : null;

        foreach (var directory in winners)
        {
            if (!seen.Add(directory))
                continue;

            kept.Add(directory);
            Raise(ref highest, stateOf(directory)?.Priority ?? 0);
        }

        foreach (var directory in recorded ?? [])
        {
            if (!seen.Add(directory))
                continue;

            if (stateOf(directory) is not { } state)
                continue;

            kept.Add(directory);

            if (state.Enabled)
                Raise(ref highest, state.Priority);
        }

        priority = highest is { } winner ? Math.Max(0, winner + 1) : 0;
        return kept;
    }

    private static void Raise(ref int? highest, int priority)
    {
        if (highest is not { } max || priority > max)
            highest = priority;
    }

    private static bool SameMods(IReadOnlyList<string>? left, IReadOnlyList<string> right)
    {
        if (left == null)
            return right.Count == 0;

        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    internal enum GeneratedModFolder
    {
        Created,
        AlreadyThere,
        Unusable,
    }

    internal static GeneratedModFolder PrepareGeneratedModFolder(string modDirectory, string modName)
    {
        var existed = Directory.Exists(modDirectory);

        if (!FileHelper.EnsureDirectoryExists(Path.Combine(modDirectory, SwapsSubfolderName)))
            return GeneratedModFolder.Unusable;

        try
        {
            SimpleV3ModWriter.WriteMissing(modDirectory, MetaFor(modName));
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the V3 mod files under '{modDirectory}'.", LogPrefix);
            return GeneratedModFolder.Unusable;
        }

        return existed ? GeneratedModFolder.AlreadyThere : GeneratedModFolder.Created;
    }

    private bool EnsurePenumbraReadsTheMod(bool isFirstCreation)
    {
        if (isFirstCreation ? _gateway.AddMod(ModDirectoryName) : _gateway.ReloadMod(ModDirectoryName))
            return true;

        NoireLogger.LogWarning(
            $"Penumbra would not {(isFirstCreation ? "register" : "reload")} '{ModDirectoryName}'; "
            + "trying the other call.", LogPrefix);

        if (isFirstCreation ? _gateway.ReloadMod(ModDirectoryName) : _gateway.AddMod(ModDirectoryName))
            return true;

        NoireLogger.LogError(
            $"Penumbra would neither register nor reload '{ModDirectoryName}'; this swap cannot be applied.",
            LogPrefix);

        return false;
    }

    internal static bool IsOwnPathCore(string resolvedDiskPath, string? modRoot, string configDirectory)
    {
        if (string.IsNullOrEmpty(resolvedDiskPath))
            return false;

        var normalizedPath = NormalizeSlashes(resolvedDiskPath);

        if (ModDirectoryFromDiskPath(resolvedDiskPath, modRoot) is { } modDirectory
            && (modDirectory.StartsWith(SwapModIdentity.DirectoryPrefix, StringComparison.OrdinalIgnoreCase)
                || modDirectory.Equals(LegacySharedModDirectoryName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return normalizedPath.StartsWith(OwnPrefix(configDirectory, CharactersFolderName), StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ModDirectoryFromDiskPath(string resolvedDiskPath, string? modRoot)
        => FirstSegmentUnder(modRoot, resolvedDiskPath);

    internal static string RedirectedPathValue(string fileName)
        => $"{SwapsSubfolderName}/{fileName}";

    public sealed record SwapFilePlan(string? ModRootDirectory, SwapModNames? Names);

    public sealed record PreparedSwapFiles(string SwapFileDirectory,
        IReadOnlyDictionary<string, string> RedirectedPaths, bool IsFirstCreation, SwapModNames Names);

    public SwapFilePlan BeginPrepare()
        => new(_gateway.GetModRootDirectory(), _identity.Names);

    public PreparedSwapFiles? PrepareFiles(SwapFilePlan plan, IReadOnlyDictionary<string, byte[]> gamePathToPapBytes)
        => PrepareFilesCore(plan, gamePathToPapBytes);

    internal static PreparedSwapFiles? PrepareFilesCore(SwapFilePlan plan,
        IReadOnlyDictionary<string, byte[]> gamePathToPapBytes)
    {
        var prepareClock = Stopwatch.StartNew();

        var redirectedPaths = new Dictionary<string, string>(gamePathToPapBytes.Count);
        foreach (var (gamePath, bytes) in gamePathToPapBytes)
            redirectedPaths[gamePath] = RedirectedPathValue(DeriveFileName(bytes, FileExtensionFor(gamePath)));

        var elapsedAtNames = prepareClock.ElapsedMilliseconds;

        if (plan.Names is not { } names)
        {
            NoireLogger.LogError("Cannot apply a swap: no character is loaded, so the mod has no name.", LogPrefix);
            return null;
        }

        if (string.IsNullOrEmpty(plan.ModRootDirectory))
        {
            NoireLogger.LogError("Cannot apply a swap: Penumbra's mod root directory is unavailable.", LogPrefix);
            return null;
        }

        var modDirectory = Path.Combine(plan.ModRootDirectory, names.Directory);
        var swapsDirectory = Path.Combine(modDirectory, SwapsSubfolderName);

        var isFirstCreation = !Directory.Exists(modDirectory);

        if (!FileHelper.EnsureDirectoryExists(swapsDirectory) || !WriteNewPapFiles(modDirectory, gamePathToPapBytes, redirectedPaths))
            return null;

        NoireLogger.LogDebug(
            $"Prepare timings: names {elapsedAtNames}ms, files {prepareClock.ElapsedMilliseconds - elapsedAtNames}ms.",
            LogPrefix);

        return new PreparedSwapFiles(swapsDirectory, redirectedPaths, isFirstCreation, names);
    }

    internal static bool WriteRealModJsons(PreparedSwapFiles prepared)
    {
        var modDirectory = Path.GetDirectoryName(prepared.SwapFileDirectory);

        if (string.IsNullOrEmpty(modDirectory))
        {
            NoireLogger.LogError($"Cannot place the V3 mod jsons: no parent for '{prepared.SwapFileDirectory}'.", LogPrefix);
            return false;
        }

        return WriteRealModJsons(modDirectory, prepared.Names.Display, prepared.RedirectedPaths) != null;
    }

    internal static bool? WriteRealModJsons(string modDirectory, string modName,
        IReadOnlyDictionary<string, string> redirectedPaths)
    {
        try
        {
            return SimpleV3ModWriter.Write(modDirectory, MetaFor(modName), redirectedPaths);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the V3 mod files under '{modDirectory}'.", LogPrefix);
            return null;
        }
    }

    private static bool WriteNewPapFiles(string storageDirectory, IReadOnlyDictionary<string, byte[]> gamePathToPapBytes,
        Dictionary<string, string> redirectedPaths)
    {
        foreach (var (gamePath, bytes) in gamePathToPapBytes)
        {
            if (!Store.WriteAt(Path.Combine(storageDirectory, redirectedPaths[gamePath]), bytes))
                return false;
        }

        return true;
    }

    private string? RegistryPath
        => CharacterDirectory is { } characterDirectory ? Path.Combine(characterDirectory, RegistryFileName) : null;

    private SwapRegistry EmptyRegistry()
        => new(CurrentRegistrySchemaVersion, _gateway.GetPlayerCollection()?.Id ?? Guid.Empty, null, 0, []);

    private SwapRegistry LoadRegistryFromDisk()
    {
        DeleteStaleManifest();

        if (RegistryPath is not { } path || !File.Exists(path))
            return EmptyRegistry();

        try
        {
            var read = FileHelper.ReadJsonFromFile<SwapRegistry>(path);
            return read is { SchemaVersion: CurrentRegistrySchemaVersion } ? read : EmptyRegistry();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to read the swap registry at '{path}'; starting from an empty one.", LogPrefix);
            return EmptyRegistry();
        }
    }

    private void DeleteStaleManifest()
    {
        if (CharacterDirectory is not { } characterDirectory)
            return;

        var stale = Path.Combine(characterDirectory, StaleManifestFileName);

        try
        {
            if (File.Exists(stale))
                File.Delete(stale);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoireLogger.LogDebug($"Could not delete the stale manifest at '{stale}' ({ex.Message}).", LogPrefix);
        }
    }

    private void PersistRegistry()
    {
        if (RegistryPath is not { } path)
            return;

        try
        {
            FileHelper.WriteJsonToFile(path, Registry, atomic: true, IndentedJson);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to persist the swap registry to '{path}'.", LogPrefix);
        }
    }

    private Guid CollectionForSelection()
    {
        if (Registry.CollectionId != Guid.Empty)
            return Registry.CollectionId;

        if (_gateway.GetPlayerCollection() is not { } collection)
            return Guid.Empty;

        Registry = Registry with { CollectionId = collection.Id };
        PersistRegistry();

        return collection.Id;
    }

    private void UpdateEntry(SwapOptionEntry entry)
    {
        var entries = Registry.Entries.ToList();
        var index = entries.FindIndex(candidate => candidate.ContentKey == entry.ContentKey);

        if (index < 0)
            entries.Add(entry);
        else
            entries[index] = entry;

        Registry = Registry with { Entries = entries };
        PersistRegistry();
    }

    private void RemoveEntry(SwapOptionEntry entry)
    {
        Registry = Registry with
        {
            Entries = Registry.Entries.Where(candidate => candidate.ContentKey != entry.ContentKey).ToList(),
        };

        PersistRegistry();
    }

    private long NextStamp()
        => Registry.Entries.Count == 0 ? 1 : Registry.Entries.Max(entry => entry.LastUsedStamp) + 1;

    private bool AllFilesExist(SwapOptionEntry entry)
    {
        if (ModDirectory is not { } modDirectory)
            return false;

        foreach (var files in entry.FilesByRace.Values)
        {
            foreach (var relativePath in files.Values)
            {
                if (!File.Exists(Path.Combine(modDirectory, relativePath)))
                    return false;
            }
        }

        return true;
    }

    private static string OwnPrefix(string directory, string subfolder) => NormalizeSlashes(Path.Combine(directory, subfolder)) + "/";

    private static string NormalizeSlashes(string? path)
        => path == null ? string.Empty : path.Replace('\\', '/');

    private static string? FirstSegmentUnder(string? root, string? path)
    {
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
            return null;

        var normalizedRoot = NormalizeSlashes(root).TrimEnd('/');
        var normalizedPath = NormalizeSlashes(path);

        if (normalizedRoot.Length == 0 || normalizedPath.Length <= normalizedRoot.Length)
            return null;

        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        if (normalizedPath[normalizedRoot.Length] != '/')
            return null;

        var start = normalizedRoot.Length + 1;
        var end = normalizedPath.IndexOf('/', start);

        var segment = end < 0 ? normalizedPath[start..] : normalizedPath[start..end];

        return segment.Length == 0 ? null : segment;
    }
}
