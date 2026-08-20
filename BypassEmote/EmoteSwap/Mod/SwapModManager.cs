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
    public const string TempTag = "BypassEmoteSwap";
    public const int CurrentManifestSchemaVersion = 1;

    private const string LogPrefix = "[SwapModManager] ";
    private const string ManifestFileName = "swap_manifest.json";
    private const string TempStorageFolderName = "swap";
    private const string CharactersFolderName = "characters";
    private const string SwapsSubfolderName = "swaps";
    private const string SwapFileSearchPattern = "swap_*.*";
    private const string SwapFilePrefix = "swap_";

    private static readonly ContentAddressedStore Store =
        new(SwapFilePrefix, SwapFileTagLength, SwapFileSearchPattern);

    private const string GeneratedModDescription =
        "Made by BypassEmote automatically. Safe to disable or delete. BypassEmote will recreate it when needed.";

    internal static ModMeta MetaFor(string modName)
        => new(modName, Service.PenumbraModAuthor, GeneratedModDescription, Service.PenumbraModVersion,
            Service.PenumbraModWebsite);

    private readonly IPCCaller_Penumbra _gateway;
    private readonly SwapModIdentity _identity;
    private readonly string _configDirectory;

    private readonly ReentrancyGuard _ownMutations = new();

    public SwapModManager(IPCCaller_Penumbra gateway, SwapModIdentity identity, string configDirectory)
    {
        _gateway = gateway;
        _identity = identity;
        _configDirectory = configDirectory;

        gateway.OwnModSettingChanged += HandleExternalChange;
        gateway.OwnModDeleted += HandleExternalChange;
        gateway.ExternalModChanged += HandleCompetingModChange;

        identity.Changed += HandleIdentityChanged;

        if (identity.Names != null)
            Current = LoadManifestFromDisk();
    }

    private string ModDirectoryName => _identity.Names?.Directory ?? string.Empty;

    private string GeneratedModName => _identity.Names?.Display ?? "BypassEmote Generated";

    private string? CharacterDirectory
        => _identity.Names is { } names ? CharacterDirectoryCore(_configDirectory, names.CharacterKey) : null;

    internal static string CharacterDirectoryCore(string configDirectory, string characterKey)
        => Path.Combine(configDirectory, CharactersFolderName, characterKey);

    private void HandleIdentityChanged(SwapModNames? previous)
    {
        if (previous != null && Current != null && !TurnedOffSinceLastOn)
            DeactivateUnder(previous);

        TurnedOffSinceLastOn = true;
        Current = LoadManifestFromDisk();
    }

    public SwapManifest? Current
    {
        get => _current;
        private set
        {
            _current = value;
            PushRedirectScope();
        }
    }

    private SwapManifest? _current;

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
            var current = _current;

            var uniqueNames = current != null && RedirectsLive(current) ? current.UniqueNameByKey : null;

            _residencyProbe?.SetRedirectedScope(current?.TargetEmote ?? 0, current?.RedirectedPaths.Keys,
                uniqueNames, current?.InternalNames,
                current == null ? null : $"{current.ResolvedSourcePath}:{current.SourceStampTicks}");
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not push the redirect scope; the probe keeps its previous scope.", LogPrefix);
        }
    }

    private bool RedirectsLive(SwapManifest current)
    {
        try
        {
            return !TurnedOffSinceLastOn && AnyPathRedirected(current.RedirectedPaths.Keys);
        }
        catch
        {
            return false;
        }
    }

    private bool AnyPathRedirected(IEnumerable<string> gamePaths)
    {
        var probePath = gamePaths.FirstOrDefault(UniqueNamePlanner.IsComposedPapPath)
            ?? gamePaths.FirstOrDefault();

        return probePath != null && _gateway.ResolvePlayerPath(probePath) != probePath;
    }

    public bool IsOwnPath(string resolvedDiskPath)
        => IsOwnPathCore(resolvedDiskPath, _gateway.GetModRootDirectory(), _configDirectory);

    public bool CanReuse(uint sourceEmote, uint targetEmote, string resolvedSourcePath, long stampTicks,
        Guid collectionId, string skeleton)
    {
        var flavor = Configuration.SwapModFlavor;
        return CanReuseCore(Current, sourceEmote, targetEmote, resolvedSourcePath, stampTicks, collectionId,
            skeleton, (int)flavor, FlavorStorageDirectory(flavor));
    }

    public bool TurnedOffSinceLastOn { get; private set; } = true;

    private const int SwapFileTagLength = 8;

    private static readonly JsonSerializerSettings IndentedJson = new() { Formatting = Formatting.Indented };

    public static string DeriveFileName(byte[] papBytes)
        => DeriveFileName(papBytes, ".pap");

    public static string DeriveFileName(byte[] bytes, string extension)
        => Store.NameFor(bytes, extension);

    internal static string FileExtensionFor(string gamePath)
    {
        var dot = gamePath.LastIndexOf('.');
        return dot < 0 ? ".pap" : gamePath[dot..];
    }

    public bool Apply(SwapManifest manifest, IReadOnlyDictionary<string, byte[]> gamePathToPapBytes)
    {
        var prepared = PrepareFiles(BeginPrepare(), gamePathToPapBytes);
        return prepared != null && Register(manifest, prepared);
    }

    public SwapFilePlan BeginPrepare()
    {
        var flavor = Configuration.SwapModFlavor;

        return new SwapFilePlan(flavor, flavor == SwapModFlavor.RealMod ? _gateway.GetModRootDirectory() : null,
            _identity.Names);
    }

    public PreparedSwapFiles? PrepareFiles(SwapFilePlan plan, IReadOnlyDictionary<string, byte[]> gamePathToPapBytes)
        => PrepareFilesCore(plan, _configDirectory, gamePathToPapBytes);

    public bool Register(SwapManifest manifest, PreparedSwapFiles prepared)
    {
        var previous = Current;

        bool ok;

        using (_ownMutations.Enter())
        {
            ok = prepared.Flavor == SwapModFlavor.RealMod
                ? RegisterRealMod(manifest, prepared)
                : RegisterTemporaryMod(manifest, previous, prepared);
        }

        if (!ok)
            return false;

        var finalManifest = manifest with
        {
            RedirectedPaths = prepared.RedirectedPaths,
            FlavorUsed = (int)prepared.Flavor,
        };

        PersistManifest(finalManifest);

        TurnedOffSinceLastOn = false;

        Current = finalManifest;
        DeleteUnreferencedSwapFiles(prepared.SwapFileDirectory, prepared.RedirectedPaths.Values);

        if (prepared.Flavor == SwapModFlavor.RealMod
            && Path.GetDirectoryName(prepared.SwapFileDirectory) is { Length: > 0 } appliedModDirectory)
        {
            DeleteUnreferencedSwapFiles(appliedModDirectory, Enumerable.Empty<string>());
        }

        return true;
    }

    public bool Reactivate()
    {
        if (Current is not { } current || _identity.Names == null)
            return false;

        bool live;

        using (_ownMutations.Enter())
        {
            live = (SwapModFlavor)current.FlavorUsed == SwapModFlavor.RealMod
                ? ReactivateRealMod(current)
                : ReactivateTemporaryMod(current);
        }

        if (live)
            TurnedOffSinceLastOn = false;

        PushRedirectScope();

        if (live)
            ReconcilePriority();

        return live;
    }

    private bool ReactivateRealMod(SwapManifest current)
    {
        var modRoot = _gateway.GetModRootDirectory();
        if (string.IsNullOrEmpty(modRoot))
        {
            NoireLogger.LogDebug("Penumbra's mod root is unavailable; the swap is built again rather than reused.", LogPrefix);
            return false;
        }

        var modDirectory = Path.Combine(modRoot, ModDirectoryName);

        if (!AllRedirectedFilesExist(current.RedirectedPaths, modDirectory))
        {
            NoireLogger.LogDebug($"A pap '{ModDirectoryName}' redirects is gone; the swap is built again rather than reused.", LogPrefix);
            Current = null;
            return false;
        }

        if (PrepareGeneratedModFolder(modDirectory, GeneratedModName) == GeneratedModFolder.Unusable)
        {
            NoireLogger.LogWarning($"'{ModDirectoryName}' could not be put back under '{modRoot}'; the swap is built again.", LogPrefix);
            Current = null;
            return false;
        }

        if (WriteRealModJsons(modDirectory, GeneratedModName, current.RedirectedPaths) is not { } mapRewritten)
            return false;

        var holdsMod = _gateway.HoldsMod(ModDirectoryName);

        if ((mapRewritten || holdsMod != true) && !EnsurePenumbraReadsTheMod(holdsMod == false))
            return false;

        var collection = current.EnabledInCollection;

        if (!_gateway.TrySetModEnabled(collection, ModDirectoryName, true))
        {
            NoireLogger.LogWarning($"Penumbra would not re-enable '{ModDirectoryName}' in collection {collection}; the swap is built again.", LogPrefix);
            return false;
        }

        if (!_gateway.TrySetModPriority(collection, ModDirectoryName, current.AppliedPriority))
        {
            NoireLogger.LogWarning($"Penumbra would not restore the priority of '{ModDirectoryName}' in collection {collection}; the swap is built again.", LogPrefix);
            return false;
        }

        return true;
    }

    private bool ReactivateTemporaryMod(SwapManifest current)
    {
        var storageDirectory = CharacterDirectory is { } characterDirectory
            ? Path.Combine(characterDirectory, TempStorageFolderName)
            : Path.Combine(_configDirectory, TempStorageFolderName);

        if (!AllRedirectedFilesExist(current.RedirectedPaths, storageDirectory))
        {
            NoireLogger.LogDebug("A pap the temporary swap redirects is gone; the swap is built again rather than reused.", LogPrefix);
            Current = null;
            return false;
        }

        var fullPaths = ToFullPaths(current.RedirectedPaths, storageDirectory);

        var ec = _gateway.AddTemporaryMod(TempTag, current.EnabledInCollection, fullPaths, current.AppliedPriority);
        if (IsSuccess(ec))
            return true;

        NoireLogger.LogWarning($"Penumbra would not re-register the temporary swap mod (ec={ec}); the swap is built again.", LogPrefix);
        return false;
    }

    public void Deactivate()
    {
        if (_identity.Names is { } names)
            DeactivateUnder(names);
    }

    private void DeactivateUnder(SwapModNames names)
    {
        if (Current == null)
            return;

        TurnedOffSinceLastOn = true;

        var flavor = (SwapModFlavor)Current.FlavorUsed;

        using (_ownMutations.Enter())
        {
            if (flavor == SwapModFlavor.RealMod)
                DisableRealMod(Current.EnabledInCollection, names.Directory);
            else
            {
                var ec = _gateway.RemoveTemporaryMod(TempTag, Current.EnabledInCollection, Current.AppliedPriority);
                if (!IsSuccess(ec))
                    NoireLogger.LogError($"Failed to remove the temporary swap mod (ec={ec}).", LogPrefix);
            }
        }

        PushRedirectScope();
    }

    public void RecordServedSkeleton(string skeleton)
    {
        if (Current is not { } current || current.Skeleton == skeleton)
            return;

        Current = current with { Skeleton = skeleton };
        PersistManifest(Current);
    }

    public void HandleExternalChange()
    {
        if (_ownMutations.IsInside)
            return;

        Current = null;
    }

    public void HandleCompetingModChange(Guid collectionId)
    {
        if (Current is not { } current || _ownMutations.IsInside || collectionId != current.EnabledInCollection)
            return;

        ReconcilePriority();
    }

    private const int MaxPriorityPasses = 4;

    public void ReconcilePriority()
    {
        for (var pass = 0; pass < MaxPriorityPasses; pass++)
        {
            if (Current is not { } current)
                return;

            var competitors = CompetingMods(current, out var target);
            var listChanged = !SameMods(current.CompetingMods, competitors);

            if (target == current.AppliedPriority)
            {
                if (!listChanged)
                    return;

                Current = current with { CompetingMods = competitors };
                PersistManifest(Current);
                return;
            }

            bool moved;

            using (_ownMutations.Enter())
            {
                moved = (SwapModFlavor)current.FlavorUsed == SwapModFlavor.RealMod
                    ? MovePriorityOfRealMod(current, target)
                    : MovePriorityOfTemporaryMod(current, target);
            }

            if (!moved)
                return;

            NoireLogger.LogDebug($"Swap mod priority moved {current.AppliedPriority} -> {target}"
                + $" against {competitors.Count} competing mod(s).", LogPrefix);

            Current = current with { AppliedPriority = target, CompetingMods = competitors };
            PersistManifest(Current);
        }
    }

    private IReadOnlyList<string> CompetingMods(SwapManifest current, out int priority)
    {
        var modRoot = _gateway.GetModRootDirectory();
        var winners = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unattributable = false;

        foreach (var gamePath in current.RedirectedPaths.Keys)
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

        var recorded = current.CompetingMods ?? [];

        if (winners.Count == 0 && recorded.Count == 0)
        {
            priority = unattributable ? 1 : 0;
            return winners;
        }

        var states = _gateway.GetAllModStates(current.EnabledInCollection);

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

    private bool MovePriorityOfRealMod(SwapManifest current, int newPriority)
    {
        if (_gateway.TrySetModPriority(current.EnabledInCollection, ModDirectoryName, newPriority))
            return true;

        NoireLogger.LogError($"Failed to rank '{ModDirectoryName}' at {newPriority} in collection {current.EnabledInCollection}.", LogPrefix);
        return false;
    }

    private bool MovePriorityOfTemporaryMod(SwapManifest current, int newPriority)
    {
        var removeEc = _gateway.RemoveTemporaryMod(TempTag, current.EnabledInCollection, current.AppliedPriority);

        if (removeEc == PenumbraApiEc.NothingChanged)
            return true;

        if (removeEc != PenumbraApiEc.Success)
        {
            NoireLogger.LogError($"Failed to remove the temporary swap mod to move its priority (ec={removeEc}).", LogPrefix);
            return false;
        }

        var fullPaths = ToFullPaths(current.RedirectedPaths, FlavorStorageDirectory(SwapModFlavor.TemporaryMod) ?? string.Empty);

        var addEc = _gateway.AddTemporaryMod(TempTag, current.EnabledInCollection, fullPaths, newPriority);
        if (IsSuccess(addEc))
            return true;

        NoireLogger.LogError($"Failed to re-add the temporary swap mod at its new priority (ec={addEc}).", LogPrefix);
        return false;
    }

    public void StartupSweep(SwapLifetime lifetime)
    {
        if (lifetime == SwapLifetime.Ephemeral && Current != null)
            Deactivate();

        if (FlavorStorageDirectory(SwapModFlavor.TemporaryMod) is not { } tempDirectory)
            return;

        DeleteUnreferencedSwapFiles(tempDirectory, Current?.RedirectedPaths.Values ?? Enumerable.Empty<string>());
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

    internal static bool CanReuseCore(SwapManifest? current, uint sourceEmote, uint targetEmote,
        string resolvedSourcePath, long stampTicks, Guid collectionId, string skeleton, int currentFlavor,
        string? flavorStorageDirectory)
    {
        if (current == null)
            return false;

        var fieldsMatch = current.SchemaVersion == CurrentManifestSchemaVersion
            && current.SourceEmote == sourceEmote
            && current.TargetEmote == targetEmote
            && current.ResolvedSourcePath == resolvedSourcePath
            && current.SourceStampTicks == stampTicks
            && current.EnabledInCollection == collectionId
            && current.FlavorUsed == currentFlavor
            // An output carries the bone data of the body it was built from, so another skeleton rebuilds.
            && current.Skeleton == skeleton;

        return fieldsMatch && AllRedirectedFilesExist(current.RedirectedPaths, flavorStorageDirectory);
    }

    private static bool AllRedirectedFilesExist(IReadOnlyDictionary<string, string> redirectedPaths, string? storageDirectory)
    {
        if (string.IsNullOrEmpty(storageDirectory))
            return false;

        foreach (var relativePath in redirectedPaths.Values)
        {
            if (!File.Exists(Path.Combine(storageDirectory, relativePath)))
                return false;
        }

        return true;
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

        return normalizedPath.StartsWith(OwnPrefix(configDirectory, CharactersFolderName), StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(OwnPrefix(configDirectory, TempStorageFolderName), StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ModDirectoryFromDiskPath(string resolvedDiskPath, string? modRoot)
        => FirstSegmentUnder(modRoot, resolvedDiskPath);

    internal static string RedirectedPathValue(string fileName, SwapModFlavor flavor)
        => flavor == SwapModFlavor.RealMod ? $"{SwapsSubfolderName}/{fileName}" : fileName;

    public sealed record SwapFilePlan(SwapModFlavor Flavor, string? ModRootDirectory, SwapModNames? Names);

    public sealed record PreparedSwapFiles(SwapModFlavor Flavor, string SwapFileDirectory,
        IReadOnlyDictionary<string, string> RedirectedPaths, bool IsFirstCreation, SwapModNames Names);

    internal static PreparedSwapFiles? PrepareFilesCore(SwapFilePlan plan, string configDirectory,
        IReadOnlyDictionary<string, byte[]> gamePathToPapBytes)
    {
        var prepareClock = Stopwatch.StartNew();

        var redirectedPaths = new Dictionary<string, string>(gamePathToPapBytes.Count);
        foreach (var (gamePath, bytes) in gamePathToPapBytes)
            redirectedPaths[gamePath] = RedirectedPathValue(DeriveFileName(bytes, FileExtensionFor(gamePath)), plan.Flavor);

        var elapsedAtNames = prepareClock.ElapsedMilliseconds;

        if (plan.Names is not { } names)
        {
            NoireLogger.LogError("Cannot apply a swap: no character is loaded, so the mod has no name.", LogPrefix);
            return null;
        }

        if (plan.Flavor != SwapModFlavor.RealMod)
        {
            var tempDirectory = TempStorageDirectoryFor(configDirectory, names);

            if (!FileHelper.EnsureDirectoryExists(tempDirectory) || !WriteNewPapFiles(tempDirectory, gamePathToPapBytes, redirectedPaths))
                return null;

            NoireLogger.LogDebug(
                $"Prepare timings (temp): names {elapsedAtNames}ms, files {prepareClock.ElapsedMilliseconds - elapsedAtNames}ms.",
                LogPrefix);

            return new PreparedSwapFiles(plan.Flavor, tempDirectory, redirectedPaths, IsFirstCreation: false, names);
        }

        if (string.IsNullOrEmpty(plan.ModRootDirectory))
        {
            NoireLogger.LogError("Cannot apply a real-mod swap: Penumbra's mod root directory is unavailable.", LogPrefix);
            return null;
        }

        var modDirectory = Path.Combine(plan.ModRootDirectory, names.Directory);
        var swapsDirectory = Path.Combine(modDirectory, SwapsSubfolderName);

        var isFirstCreation = !Directory.Exists(modDirectory);

        if (!FileHelper.EnsureDirectoryExists(swapsDirectory) || !WriteNewPapFiles(modDirectory, gamePathToPapBytes, redirectedPaths))
            return null;

        NoireLogger.LogDebug(
            $"Prepare timings (real): names {elapsedAtNames}ms, files {prepareClock.ElapsedMilliseconds - elapsedAtNames}ms.",
            LogPrefix);

        return new PreparedSwapFiles(plan.Flavor, swapsDirectory, redirectedPaths, isFirstCreation, names);
    }

    private static string TempStorageDirectoryFor(string configDirectory, SwapModNames names)
        => Path.Combine(CharacterDirectoryCore(configDirectory, names.CharacterKey), TempStorageFolderName);

    private void DisableRealMod(Guid collection, string modDirectoryName)
    {
        var ec = _gateway.SetModEnabled(collection, modDirectoryName, false);

        if (IsSuccess(ec))
            return;

        if (ec is PenumbraApiEc.ModMissing or PenumbraApiEc.CollectionMissing)
        {
            NoireLogger.LogDebug(
                $"Penumbra no longer holds '{modDirectoryName}' in collection {collection} ({ec}), so there was "
                + "nothing left to turn off.", LogPrefix);
            return;
        }

        var retry = _gateway.SetModEnabled(collection, modDirectoryName, false);

        if (IsSuccess(retry))
        {
            NoireLogger.LogDebug($"'{modDirectoryName}' turned off on the second attempt (first said {ec}).", LogPrefix);
            return;
        }

        NoireLogger.LogWarning(
            $"Penumbra would not turn '{modDirectoryName}' off in collection {collection} ({ec}, then {retry}). "
            + "The next swap rewrites and re-enables it from scratch.", LogPrefix);
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

    private bool RegisterRealMod(SwapManifest manifest, PreparedSwapFiles prepared)
    {
        var registerClock = Stopwatch.StartNew();

        if (!WriteRealModJsons(prepared))
            return false;

        if (!EnsurePenumbraReadsTheMod(prepared.IsFirstCreation))
            return false;

        var elapsedAtRegister = registerClock.ElapsedMilliseconds;

        var collection = manifest.EnabledInCollection;

        if (!_gateway.TrySetModEnabled(collection, ModDirectoryName, true))
        {
            NoireLogger.LogError($"Failed to enable '{ModDirectoryName}' in collection {collection}.", LogPrefix);
            return false;
        }

        if (!_gateway.TrySetModPriority(collection, ModDirectoryName, manifest.AppliedPriority))
        {
            NoireLogger.LogError($"Failed to set priority for '{ModDirectoryName}' in collection {collection}.", LogPrefix);
            return false;
        }

        if (!Serves(prepared.RedirectedPaths) && !RepairAndVerify(manifest, prepared))
            return false;

        NoireLogger.LogDebug(
            $"Apply timings (real): {(prepared.IsFirstCreation ? "add" : "reload")} {elapsedAtRegister}ms, " +
            $"enable {registerClock.ElapsedMilliseconds - elapsedAtRegister}ms.", LogPrefix);

        return true;
    }

    private bool Serves(IReadOnlyDictionary<string, string> redirectedPaths)
        => redirectedPaths.Count == 0 || AnyPathRedirected(redirectedPaths.Keys);

    private bool RepairAndVerify(SwapManifest manifest, PreparedSwapFiles prepared)
    {
        var modDirectory = Path.GetDirectoryName(prepared.SwapFileDirectory);

        if (string.IsNullOrEmpty(modDirectory)
            || PrepareGeneratedModFolder(modDirectory, prepared.Names.Display) == GeneratedModFolder.Unusable)
        {
            return false;
        }

        NoireLogger.LogDebug($"'{ModDirectoryName}' redirects none of its paths; writing and registering it again.", LogPrefix);

        if (WriteRealModJsons(modDirectory, prepared.Names.Display, prepared.RedirectedPaths) == null)
            return false;

        var collection = manifest.EnabledInCollection;

        if (!EnsurePenumbraReadsTheMod(isFirstCreation: true)
            || !_gateway.TrySetModEnabled(collection, ModDirectoryName, true)
            || !_gateway.TrySetModPriority(collection, ModDirectoryName, manifest.AppliedPriority)
            || !Serves(prepared.RedirectedPaths))
        {
            NoireLogger.LogWarning(
                $"Penumbra still redirects nothing for '{ModDirectoryName}' in collection {collection}. Check that "
                + "the mod is present and enabled in Penumbra, then try the emote again.", LogPrefix);

            return false;
        }

        return true;
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

    private bool RegisterTemporaryMod(SwapManifest manifest, SwapManifest? previous, PreparedSwapFiles prepared)
    {
        var registerClock = Stopwatch.StartNew();

        if (previous != null && (SwapModFlavor)previous.FlavorUsed == SwapModFlavor.TemporaryMod)
        {
            var removeEc = _gateway.RemoveTemporaryMod(TempTag, previous.EnabledInCollection, previous.AppliedPriority);
            if (!IsSuccess(removeEc))
            {
                NoireLogger.LogError($"Failed to remove the previous temporary swap mod (ec={removeEc}).", LogPrefix);
                return false;
            }
        }

        var fullPaths = ToFullPaths(prepared.RedirectedPaths, prepared.SwapFileDirectory);

        var addEc = _gateway.AddTemporaryMod(TempTag, manifest.EnabledInCollection, fullPaths, manifest.AppliedPriority);
        if (!IsSuccess(addEc))
        {
            NoireLogger.LogError($"Failed to add the temporary swap mod (ec={addEc}).", LogPrefix);
            return false;
        }

        NoireLogger.LogDebug($"Apply timings (temp): remove+add {registerClock.ElapsedMilliseconds}ms.", LogPrefix);

        return true;
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

    private static Dictionary<string, string> ToFullPaths(IReadOnlyDictionary<string, string> gamePathToFileName, string storageDirectory)
    {
        var result = new Dictionary<string, string>(gamePathToFileName.Count);
        foreach (var (gamePath, fileName) in gamePathToFileName)
            result[gamePath] = Path.Combine(storageDirectory, fileName);

        return result;
    }

    private static bool IsSuccess(PenumbraApiEc ec) => ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;

    private string? FlavorStorageDirectory(SwapModFlavor flavor)
    {
        if (_identity.Names is not { } names)
            return null;

        if (flavor != SwapModFlavor.RealMod)
            return TempStorageDirectoryFor(_configDirectory, names);

        var modRoot = _gateway.GetModRootDirectory();
        return string.IsNullOrEmpty(modRoot) ? null : Path.Combine(modRoot, names.Directory);
    }

    private string? ManifestPath
        => CharacterDirectory is { } characterDirectory ? Path.Combine(characterDirectory, ManifestFileName) : null;

    private SwapManifest? LoadManifestFromDisk()
    {
        if (ManifestPath is not { } path || !File.Exists(path))
            return null;

        try
        {
            return FileHelper.ReadJsonFromFile<SwapManifest>(path);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to read the swap manifest at '{path}'; treating as no active swap.", LogPrefix);
            return null;
        }
    }

    private void PersistManifest(SwapManifest manifest)
    {
        if (ManifestPath is not { } path)
            return;

        try
        {
            FileHelper.WriteJsonToFile(path, manifest, atomic: true, IndentedJson);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Failed to persist the swap manifest to '{path}'.", LogPrefix);
        }
    }

    private static void DeleteUnreferencedSwapFiles(string storageDirectory, IEnumerable<string> referencedPaths)
        => Store.RemoveUnreferenced(storageDirectory, referencedPaths);

    private static string OwnPrefix(string directory, string subfolder) => NormalizeSlashes(Path.Combine(directory, subfolder)) + "/";

    /// <summary>Turns every backslash into a forward slash.</summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The path with forward slashes, or an empty string when <paramref name="path"/> is null.</returns>
    private static string NormalizeSlashes(string? path)
        => path == null ? string.Empty : path.Replace('\\', '/');

    /// <summary>The first path segment below a root, ignoring slash style and case.</summary>
    /// <param name="root">The directory the path should be under.</param>
    /// <param name="path">The path to take apart.</param>
    /// <returns>The first segment below the root, or null when the path is not under it or is the root itself.</returns>
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

        // Must be a segment boundary: "mods2/x" is not under "mods".
        if (normalizedPath[normalizedRoot.Length] != '/')
            return null;

        var start = normalizedRoot.Length + 1;
        var end = normalizedPath.IndexOf('/', start);

        var segment = end < 0 ? normalizedPath[start..] : normalizedPath[start..end];

        return segment.Length == 0 ? null : segment;
    }
}
