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
using System.Threading;

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
    private readonly ModLayoutDetector _layout;

    private string? _pressedKey;

    public SwapModManager(IPCCaller_Penumbra gateway, SwapModIdentity identity, string configDirectory)
    {
        _gateway = gateway;
        _identity = identity;
        _configDirectory = configDirectory;

        _layout = new ModLayoutDetector(gateway);

        gateway.AvailabilityChanged += HandleAvailabilityChanged;
        gateway.OwnModSettingChanged += HandleExternalChange;
        gateway.OwnModDeleted += HandleOwnModDeleted;
        gateway.ExternalModChanged += HandleCompetingModChange;

        identity.Changed += HandleIdentityChanged;

        Registry = LoadRegistryFromDisk();
    }

    public SwapRegistry Registry { get; private set; }

    public string ModDirectoryName => _identity.Names?.Directory ?? string.Empty;

    internal int Layout => _layout.Layout;

    internal int EnsureLayout()
    {
        if (_layout.Settled)
            return _layout.Layout;

        using (_ownMutations.Enter())
            return _layout.Ensure(ModDirectory, ModDirectoryName);
    }

    private void HandleAvailabilityChanged(bool available)
    {
        _layout.Invalidate();

        if (available)
            EnsureLayout();
    }

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
                if (ModStore.WriteMeta(EnsureLayout(), modDirectory, MetaFor(display), NoRedirects))
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
        if (_identity.Names is not { } names)
            return false;

        var collection = CollectionForSelection();

        using (_ownMutations.Enter())
        {
            var enableEc = _gateway.SetModEnabled(collection, names.Directory, true);

            if (!IsSuccess(enableEc))
            {
                NoireLogger.LogError(
                    $"Penumbra would not enable '{names.Directory}' in collection {collection} (ec={enableEc}).", LogPrefix);

                return false;
            }

            var selectEc = _gateway.SelectOption(collection, names.Directory, entry.GroupName, entry.OptionName);

            if (!IsSuccess(selectEc))
            {
                var version = _gateway.ReportedApiVersion();

                NoireLogger.LogError(
                    $"Penumbra refused '{entry.OptionName}' in '{entry.GroupName}' (ec={selectEc}, "
                    + $"mod '{names.Directory}', collection {collection}, Penumbra api "
                    + $"{(version is { } v ? $"{v.Breaking}.{v.Feature}" : "unknown")}). "
                    + $"Penumbra holds: {PenumbraOptionsLine(entry.GroupName, entry.OptionName)}", LogPrefix);

                return false;
            }
        }

        _pressedKey = entry.ContentKey;

        foreach (var sibling in Registry.Entries
            .Where(other => other.SelectedByUs && other.GroupName == entry.GroupName && other.ContentKey != entry.ContentKey)
            .ToList())
        {
            UpdateEntry(sibling with { SelectedByUs = false });
        }

        UpdateEntry(entry with { SelectedByUs = true, LastUsedStamp = NextStamp() });

        if (Configuration.SwapBehavior == SwapBehavior.OneAtATime)
            DeselectAllExcept(entry.GroupName);

        PushRedirectScope();
        ReconcilePriority();

        return true;
    }

    public void DeselectEntry(SwapOptionEntry entry)
    {
        if (_identity.Names is not { } names)
            return;

        using (_ownMutations.Enter())
            _gateway.TrySelectOption(Registry.CollectionId, names.Directory, entry.GroupName, OptionNaming.NoneOptionName);

        if (_pressedKey == entry.ContentKey)
            _pressedKey = null;

        UpdateEntry(entry with { SelectedByUs = false });

        DisableModIfNothingSelected();
        PushRedirectScope();
    }

    public void DeselectAll()
    {
        foreach (var entry in Registry.Entries.Where(entry => entry.SelectedByUs).ToList())
            DeselectEntry(entry);
    }

    private void DeselectAllExcept(string groupName)
    {
        foreach (var entry in Registry.Entries.Where(entry => entry.SelectedByUs && entry.GroupName != groupName).ToList())
            DeselectEntry(entry);
    }

    private void DeselectAllUnder(SwapModNames names)
    {
        var selected = Registry.Entries.Where(entry => entry.SelectedByUs).ToList();

        if (selected.Count == 0)
            return;

        using (_ownMutations.Enter())
        {
            foreach (var entry in selected)
            {
                _gateway.TrySelectOption(Registry.CollectionId, names.Directory, entry.GroupName,
                    OptionNaming.NoneOptionName);
            }

            _gateway.TrySetModEnabled(Registry.CollectionId, names.Directory, false);
        }
    }

    private void DisableModIfNothingSelected()
    {
        if (_identity.Names is not { } names || Registry.Entries.Any(entry => entry.SelectedByUs))
            return;

        using (_ownMutations.Enter())
            _gateway.TrySetModEnabled(Registry.CollectionId, names.Directory, false);
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

        using var batch = BeginRegistryBatch();

        var clock = Stopwatch.StartNew();

        if (Registry.Skeleton != race && !RewriteForSkeleton(SkeletonRewritePlanner.For(Registry, race), race))
            return false;

        var folder = PrepareGeneratedModFolder(modDirectory, GeneratedModName, EnsureLayout());

        if (folder == GeneratedModFolder.Unusable || !WriteSwapFiles(modDirectory, entry, filesToWrite))
            return false;

        var atFiles = clock.ElapsedMilliseconds;

        var groups = ReadGroups();
        var known = groups.TryGetValue(entry.GroupName, out var existing) ? existing : ReusableSlot(groups);

        var group = known is { } slot && slot.Group.Name == entry.GroupName
            ? slot.Group
            : ModGroupFile.NewGroup(entry.GroupName);

        var isNewOption = !group.Options.Any(option => option.Name == entry.OptionName);
        var freedFiles = false;

        if (isNewOption
            && RegistryDecisions.EvictionCandidate(Registry, entry.GroupName, Configuration.MaxKeptSwapsPerTarget) is { } evicted)
        {
            group = ModGroupFile.Remove(group, evicted.OptionName);
            RemoveEntry(evicted);
            freedFiles = true;

            NoireLogger.LogDebug($"'{evicted.OptionName}' was dropped from '{entry.GroupName}' to stay under the cap.", LogPrefix);
        }

        group = isNewOption
            ? ModGroupFile.Add(group, new ModGroupOption(entry.OptionName, entry.FilesByRace[race]))
            : ModGroupFile.WithFiles(group, entry.OptionName, entry.FilesByRace[race]);

        var atGroups = clock.ElapsedMilliseconds;
        long atWrite;

        using (_ownMutations.Enter())
        {
            if (!WriteGroup(group, known?.Index ?? IndexFor(groups, entry.GroupName), known?.Files))
                return false;

            atWrite = clock.ElapsedMilliseconds;

            if (!EnsurePenumbraReadsTheMod(folder == GeneratedModFolder.Created))
                return false;
        }

        var atReload = clock.ElapsedMilliseconds;

        UpdateEntry(entry);

        if (freedFiles)
            SweepUnreferencedFiles();

        FlushRegistry();

        var atRegistry = clock.ElapsedMilliseconds;

        var selected = SelectExisting(entry);

        NoireLogger.LogDebug($"Apply steps: files {atFiles}ms, groups {atGroups - atFiles}ms, "
            + $"group write {atWrite - atGroups}ms, penumbra reload {atReload - atWrite}ms, "
            + $"registry {atRegistry - atReload}ms, select {clock.ElapsedMilliseconds - atRegistry}ms.", LogPrefix);

        if (selected && _gateway.RefreshOwnPanel())
            NoireLogger.LogDebug("The mod's panel was on screen, so its option list was refreshed.", LogPrefix);

        return selected;
    }

    internal int ApplyRulesPlan(string stamp, string sourceKey, uint sourceEmote, uint keptTarget)
    {
        var plan = RegistryDecisions.PlanForSwap(Registry, stamp, sourceKey, sourceEmote, keptTarget, _pressedKey,
            Configuration.MaxKeptSwapsPerTarget);

        var rewritten = plan.Dropped.Count > 0
            || plan.Entries.Count != Registry.Entries.Count
            || plan.Entries.Where((entry, index) => !ReferenceEquals(entry, Registry.Entries[index])).Any();

        if (!rewritten)
            return 0;

        Registry = Registry with { Entries = plan.Entries };
        PersistRegistry();

        if (plan.Dropped.Count == 0)
            return 0;

        NoireLogger.LogDebug($"{plan.Dropped.Count} kept swap(s) the settings would no longer make were dropped: "
            + string.Join(", ", plan.Dropped.Select(entry => $"'{entry.OptionName}' in '{entry.GroupName}'")), LogPrefix);

        DeselectDropped(plan.Dropped);
        DropOptionsFromDisk(plan.Dropped);

        SweepUnreferencedFiles();
        ReassertSelections();
        DisableModIfNothingSelected();
        PushRedirectScope();

        if (_gateway.RefreshOwnPanel())
            NoireLogger.LogDebug("The mod's panel was on screen, so its option list was refreshed.", LogPrefix);

        return plan.Dropped.Count;
    }

    private void DeselectDropped(IReadOnlyList<SwapOptionEntry> dropped)
    {
        if (_identity.Names is not { } names)
            return;

        var armed = dropped.Where(entry => entry.SelectedByUs).ToList();

        if (armed.Count == 0)
            return;

        using (_ownMutations.Enter())
        {
            foreach (var entry in armed)
            {
                _gateway.TrySelectOption(Registry.CollectionId, names.Directory, entry.GroupName,
                    OptionNaming.NoneOptionName);
            }
        }
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
                WriteGroup(groups[groupName].Group, groups[groupName].Index, groups[groupName].Files);

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

    private static bool WriteSwapFiles(string modDirectory, SwapOptionEntry entry,
        IReadOnlyDictionary<string, byte[]> filesToWrite)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var files in entry.FilesByRace.Values)
        {
            foreach (var (gamePath, relativePath) in files)
            {
                if (!seen.Add(relativePath))
                    continue;

                var fullPath = Path.Combine(modDirectory, relativePath);

                if (File.Exists(fullPath))
                    continue;

                if (!filesToWrite.TryGetValue(gamePath, out var bytes))
                {
                    NoireLogger.LogError($"'{relativePath}' is missing and this swap does not carry it.", LogPrefix);
                    return false;
                }

                if (!Store.WriteAt(fullPath, bytes))
                    return false;
            }
        }

        return true;
    }

    private Dictionary<string, GroupOnDisk> ReadGroups()
        => ModDirectory is { } modDirectory
            ? ModStore.ReadGroups(EnsureLayout(), modDirectory)
            : new Dictionary<string, GroupOnDisk>(StringComparer.Ordinal);

    private static GroupOnDisk? ReusableSlot(Dictionary<string, GroupOnDisk> groups)
        => groups.Values
            .Where(onDisk => ModGroupFile.IsEmpty(onDisk.Group))
            .OrderBy(onDisk => onDisk.Index)
            .FirstOrDefault();

    private bool WriteGroup(ModGroup group, int index, IReadOnlyList<string>? knownFiles = null)
        => ModDirectory is { } modDirectory
            && ModStore.WriteGroup(EnsureLayout(), modDirectory, group, index, knownFiles);

    internal static int IndexInFileName(string path) => ModStore.IndexInFileName(path);

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
                if (!WriteGroup(onDisk.Group, onDisk.Index, onDisk.Files))
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
        EnsureLayout();

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

        EmptyGroupFiles();

        SweepUnreferencedFiles();

        if (hadModFolder)
        {
            EnsureEmptyDefaultMap();

            using (_ownMutations.Enter())
                EnsurePenumbraReadsTheMod(isFirstCreation: false);
        }

        PushRedirectScope();
    }

    private void EmptyGroupFiles()
    {
        foreach (var onDisk in ReadGroups().Values)
        {
            if (!ModGroupFile.IsEmpty(onDisk.Group))
                WriteGroup(ModGroupFile.NewGroup(onDisk.Group.Name), onDisk.Index, onDisk.Files);
        }
    }

    public void ShutDown()
    {
        if (ModDirectory is not { } modDirectory || !Directory.Exists(modDirectory))
            return;

        ModStore.DropEmptyGroups(Layout, modDirectory);
    }

    private void ReconcileWithDisk()
    {
        if (_identity.Names is not { } names)
            return;

        var groups = ReadGroups();

        var optionsOnDisk = groups.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Group.Options.Select(option => option.Name).ToList(),
            StringComparer.Ordinal);

        var selectedOnDisk = _gateway.GetSelectedOptions(Registry.CollectionId, names.Directory)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var plan = RegistryDecisions.Reconcile(Registry, optionsOnDisk, selectedOnDisk);

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
                WriteGroup(onDisk.Group, onDisk.Index, onDisk.Files);
        }

        ReloadAndReassert();
    }

    private void ReloadAndReassert()
    {
        using (_ownMutations.Enter())
            EnsurePenumbraReadsTheMod(isFirstCreation: false);

        ReassertSelections();
    }

    private void ReassertSelections(string? onlyGroupName = null)
    {
        if (_identity.Names is not { } names)
            return;

        var armed = Registry.Entries
            .Where(entry => entry.SelectedByUs && (onlyGroupName == null || entry.GroupName == onlyGroupName))
            .ToList();

        if (armed.Count == 0)
            return;

        using (_ownMutations.Enter())
        {
            foreach (var entry in armed)
                _gateway.TrySelectOption(Registry.CollectionId, names.Directory, entry.GroupName, entry.OptionName);
        }
    }

    private bool EnsureEmptyDefaultMap()
    {
        if (ModDirectory is not { } modDirectory || !Directory.Exists(modDirectory))
            return false;

        return WriteRealModJsons(modDirectory, GeneratedModName, NoRedirects, EnsureLayout()) == true;
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
        ForgetModStates();

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
        ForgetModStates();

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

            bool moved;

            using (_ownMutations.Enter())
                moved = _gateway.TrySetModPriority(Registry.CollectionId, names.Directory, target);

            if (!moved)
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

    private IReadOnlyDictionary<string, ModState>? _modStates;
    private Guid _modStatesCollection;

    private IReadOnlyDictionary<string, ModState>? ModStates()
    {
        if (_modStates != null && _modStatesCollection == Registry.CollectionId)
            return _modStates;

        _modStates = _gateway.GetAllModStates(Registry.CollectionId);
        _modStatesCollection = Registry.CollectionId;

        return _modStates;
    }

    private void ForgetModStates() => _modStates = null;

    private IReadOnlyList<string> CompetingMods(IReadOnlyList<string> servedPaths, out int priority)
    {
        var modRoot = _gateway.GetModRootDirectory();
        var winners = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unattributable = false;

        var resolvedPaths = _gateway.ResolvePlayerPaths(servedPaths);

        for (var index = 0; index < servedPaths.Count; index++)
        {
            var gamePath = servedPaths[index];
            var resolved = resolvedPaths is { } batch ? batch[index] : _gateway.ResolvePlayerPath(gamePath);

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

        var states = ModStates();

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

    internal static GeneratedModFolder PrepareGeneratedModFolder(string modDirectory, string modName,
        int layout = ModLayout.V3)
    {
        var existed = Directory.Exists(modDirectory);

        if (!FileHelper.EnsureDirectoryExists(Path.Combine(modDirectory, SwapsSubfolderName)))
            return GeneratedModFolder.Unusable;

        try
        {
            ModStore.WriteMetaMissing(layout, modDirectory, MetaFor(modName));
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the mod files under '{modDirectory}'.", LogPrefix);
            return GeneratedModFolder.Unusable;
        }

        return existed ? GeneratedModFolder.AlreadyThere : GeneratedModFolder.Created;
    }

    private bool EnsurePenumbraReadsTheMod(bool isFirstCreation)
    {
        var read = EnsureReadCore(isFirstCreation);

        if (read)
            _layout.Observe(ModDirectory);

        return read;
    }

    private bool EnsureReadCore(bool isFirstCreation)
    {
        if (isFirstCreation)
        {
            if (_gateway.AddMod(ModDirectoryName))
                return true;

            NoireLogger.LogWarning(
                $"Penumbra would not register '{ModDirectoryName}'; trying the other call.", LogPrefix);

            if (Reloaded())
                return true;
        }
        else
        {
            if (Reloaded())
                return true;

            NoireLogger.LogWarning(
                $"Penumbra would not reload '{ModDirectoryName}'; trying the other call.", LogPrefix);

            if (_gateway.AddMod(ModDirectoryName))
                return true;
        }

        NoireLogger.LogError(
            $"Penumbra would neither register nor reload '{ModDirectoryName}'; this swap cannot be applied.",
            LogPrefix);

        return false;

        bool Reloaded()
        {
            var read = _gateway.ReloadMod(ModDirectoryName);

            if (read != ModReadResult.ReadThenThrew)
                return read == ModReadResult.Read;

            NoireLogger.LogError(
                $"Penumbra threw while telling its collections that '{ModDirectoryName}' had changed. It read the "
                + "mod, so the swap goes on, but its own settings bookkeeping for that reload did not finish, and "
                + "it will throw the same way on every reload until Penumbra is restarted.", LogPrefix);

            return true;
        }
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

    internal static bool WriteRealModJsons(PreparedSwapFiles prepared, int layout = ModLayout.V3)
    {
        var modDirectory = Path.GetDirectoryName(prepared.SwapFileDirectory);

        if (string.IsNullOrEmpty(modDirectory))
        {
            NoireLogger.LogError($"Cannot place the mod jsons: no parent for '{prepared.SwapFileDirectory}'.", LogPrefix);
            return false;
        }

        return WriteRealModJsons(modDirectory, prepared.Names.Display, prepared.RedirectedPaths, layout) != null;
    }

    internal static bool? WriteRealModJsons(string modDirectory, string modName,
        IReadOnlyDictionary<string, string> redirectedPaths, int layout = ModLayout.V3)
    {
        try
        {
            return ModStore.WriteMeta(layout, modDirectory, MetaFor(modName), redirectedPaths);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to write the mod files under '{modDirectory}'.", LogPrefix);
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

    private const string RegistryWriteOperationName = "BypassEmote.SwapRegistryWrite";

    private readonly object _registryWriteLock = new();

    private long _registryWriteTicket;

    private int _registryBatchDepth;
    private bool _registryDirty;

    private IDisposable BeginRegistryBatch() => new RegistryBatch(this);

    private sealed class RegistryBatch : IDisposable
    {
        private readonly SwapModManager _owner;

        internal RegistryBatch(SwapModManager owner)
        {
            _owner = owner;
            _owner._registryBatchDepth++;
        }

        public void Dispose()
        {
            if (--_owner._registryBatchDepth > 0 || !_owner._registryDirty)
                return;

            _owner._registryDirty = false;
            _owner.WriteRegistry();
        }
    }

    private void FlushRegistry()
    {
        if (!_registryDirty)
            return;

        _registryDirty = false;
        WriteRegistry();
    }

    private void PersistRegistry()
    {
        if (_registryBatchDepth > 0)
        {
            _registryDirty = true;
            return;
        }

        WriteRegistry();
    }

    private void WriteRegistry()
    {
        if (RegistryPath is not { } path)
            return;

        var snapshot = Registry;
        var ticket = Interlocked.Increment(ref _registryWriteTicket);

        AsyncHelper.RunInBackgroundAsync(() =>
        {
            lock (_registryWriteLock)
            {
                if (Interlocked.Read(ref _registryWriteTicket) != ticket)
                    return;

                try
                {
                    FileHelper.WriteJsonToFile(path, snapshot, atomic: true, IndentedJson);
                }
                catch (Exception ex)
                {
                    NoireLogger.LogError(ex, $"Failed to persist the swap registry to '{path}'.", LogPrefix);
                }
            }
        }, RegistryWriteOperationName);
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
