using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Models;
using BypassEmote.Safety;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using NoireLib;
using NoireLib.Helpers;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote.IPC;

public enum ModReadResult
{
    Read,
    ReadThenThrew,
    NotHeld,
    Refused,
}

public enum PenumbraReadiness
{
    Ready,
    Missing,
    TooOld,
}

public sealed class IPCCaller_Penumbra : IDisposable
{
    private const string LogPrefix = "[IPCCaller_Penumbra] ";
    private const string LogOnceScope = "BypassEmote.Penumbra.";
    private const int MinimumBreakingVersion = 5;
    private const int LocalPlayerObjectIndex = 0;

    private static readonly TimeSpan ReprobeInterval = TimeSpan.FromSeconds(1);

    private readonly SwapModIdentity _identity;

    private string? OwnModDirectoryName => _identity.DirectoryName;

    private readonly ApiVersion _apiVersion;
    private readonly ResolvePlayerPath _resolvePlayerPath;
    private readonly ResolvePlayerPaths _resolvePlayerPaths;
    private readonly GetCollectionForObject _getCollectionForObject;
    private readonly GetCollection _getCollection;
    private readonly GetCollections _getCollections;
    private readonly GetAllModSettings _getAllModSettings;
    private readonly GetModList _getModList;
    private readonly OpenMainWindow _openMainWindow;
    private readonly TrySetMod _trySetMod;
    private readonly TrySetModPriority _trySetModPriority;
    private readonly TrySetModSettings _trySetModSettings;
    private readonly TryInheritMod _tryInheritMod;
    private readonly RemoveTemporaryModSettings _removeTemporaryModSettings;
    private readonly QueryTemporaryModSettings _queryTemporaryModSettings;
    private readonly GetCurrentModSettings _getCurrentModSettings;
    private readonly GetAvailableModSettings _getAvailableModSettings;
    private readonly AddMod _addMod;
    private readonly ReloadMod _reloadMod;
    private readonly GetModPath _getModPath;
    private readonly GetModDirectory _getModDirectory;
    private readonly RedrawObject _redrawObject;
    private readonly AddTemporaryMod _addTemporaryMod;
    private readonly RemoveTemporaryMod _removeTemporaryMod;

    private readonly EventSubscriber _initialized;
    private readonly EventSubscriber _disposed;
    private readonly EventSubscriber<ModSettingChange, Guid, string, bool> _modSettingChanged;
    private readonly EventSubscriber<string> _modDeleted;
    private readonly EventSubscriber<string, bool> _modDirectoryChanged;
    private readonly EventSubscriber<nint, int> _gameObjectRedrawn;
    private readonly EventSubscriber<nint, string, string> _resourcePathResolved;
    private readonly EventSubscriber<string> _preSettingsDraw;

    private long _ownPanelDrawnAt;

    private const long PanelFreshnessMilliseconds = 250;


    private bool _available;
    private PenumbraReadiness _readiness = PenumbraReadiness.Missing;
    private int _reportedBreaking;
    private DateTime _lastProbeUtc = DateTime.MinValue;

    public event Action<bool>? AvailabilityChanged;
    public event Action? OwnModSettingChanged;
    public event Action<Guid>? ExternalModChanged;
    public event Action? OwnModDeleted;
    public event Action? ModRootChanged;
    public event Action<int>? GameObjectRedrawn;

    public IPCCaller_Penumbra(IDalamudPluginInterface pluginInterface, SwapModIdentity identity)
    {
        _identity = identity;

        _apiVersion = new ApiVersion(pluginInterface);
        _resolvePlayerPath = new ResolvePlayerPath(pluginInterface);
        _resolvePlayerPaths = new ResolvePlayerPaths(pluginInterface);
        _getCollectionForObject = new GetCollectionForObject(pluginInterface);
        _getCollection = new GetCollection(pluginInterface);
        _getCollections = new GetCollections(pluginInterface);
        _getAllModSettings = new GetAllModSettings(pluginInterface);
        _getModList = new GetModList(pluginInterface);
        _openMainWindow = new OpenMainWindow(pluginInterface);
        _trySetMod = new TrySetMod(pluginInterface);
        _trySetModPriority = new TrySetModPriority(pluginInterface);
        _trySetModSettings = new TrySetModSettings(pluginInterface);
        _tryInheritMod = new TryInheritMod(pluginInterface);
        _removeTemporaryModSettings = new RemoveTemporaryModSettings(pluginInterface);
        _queryTemporaryModSettings = new QueryTemporaryModSettings(pluginInterface);
        _getCurrentModSettings = new GetCurrentModSettings(pluginInterface);
        _getAvailableModSettings = new GetAvailableModSettings(pluginInterface);
        _addMod = new AddMod(pluginInterface);
        _reloadMod = new ReloadMod(pluginInterface);
        _getModPath = new GetModPath(pluginInterface);
        _getModDirectory = new GetModDirectory(pluginInterface);
        _redrawObject = new RedrawObject(pluginInterface);
        _addTemporaryMod = new AddTemporaryMod(pluginInterface);
        _removeTemporaryMod = new RemoveTemporaryMod(pluginInterface);

        _initialized = Initialized.Subscriber(pluginInterface);
        _disposed = Disposed.Subscriber(pluginInterface);
        _modSettingChanged = ModSettingChanged.Subscriber(pluginInterface);
        _modDeleted = ModDeleted.Subscriber(pluginInterface);
        _modDirectoryChanged = ModDirectoryChanged.Subscriber(pluginInterface);
        _gameObjectRedrawn = Penumbra.Api.IpcSubscribers.GameObjectRedrawn.Subscriber(pluginInterface);
        _resourcePathResolved = GameObjectResourcePathResolved.Subscriber(pluginInterface);
        _preSettingsDraw = PreSettingsDraw.Subscriber(pluginInterface);

        _initialized.Event += OnPenumbraInitialized;
        _disposed.Event += OnPenumbraDisposed;
        _modSettingChanged.Event += OnModSettingChanged;
        _modDeleted.Event += OnModDeleted;
        _modDirectoryChanged.Event += OnModDirectoryChanged;
        _gameObjectRedrawn.Event += OnGameObjectRedrawn;
        _resourcePathResolved.Event += OnResourcePathResolved;
        _preSettingsDraw.Event += OnPreSettingsDraw;

        _initialized.Enable();
        _disposed.Enable();
        _modSettingChanged.Enable();
        _modDeleted.Enable();
        _modDirectoryChanged.Enable();
        _gameObjectRedrawn.Enable();
        _resourcePathResolved.Enable();
        _preSettingsDraw.Enable();

        Probe();
    }

    public bool Available => Readiness == PenumbraReadiness.Ready;

    public PenumbraReadiness Readiness
    {
        get
        {
            if (!_available && DateTime.UtcNow - _lastProbeUtc >= ReprobeInterval)
                Probe();

            return _readiness;
        }
    }

    public string UnavailableReason => Readiness switch
    {
        PenumbraReadiness.Ready => string.Empty,
        PenumbraReadiness.TooOld => $"Penumbra answers over interface version {_reportedBreaking}, and Bypass Emote "
            + $"needs version {MinimumBreakingVersion} or newer. Update Penumbra.",
        _ => "Penumbra is not running. Emote Swap needs it installed.",
    };

    public string ResolvePlayerPath(string gamePath)
    {
        try
        {
            return _resolvePlayerPath.Invoke(gamePath) ?? gamePath;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(ResolvePlayerPath), ex);
            return gamePath;
        }
    }

    public IReadOnlyList<string>? ResolvePlayerPaths(IReadOnlyList<string> gamePaths)
    {
        if (gamePaths.Count == 0)
            return [];

        try
        {
            var forward = gamePaths as string[] ?? [.. gamePaths];
            var (resolved, _) = _resolvePlayerPaths.Invoke(forward, []);

            return resolved.Length == gamePaths.Count ? resolved : null;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(ResolvePlayerPaths), ex);
            return null;
        }
    }

    public static bool PretendIdentifierRejected { get; set; }

    public string? PlayerCollectionFallbackSource { get; private set; }

    private string? _announcedFallbackSource;

    public (Guid Id, string Name)? GetPlayerCollection()
    {
        try
        {
            var result = _getCollectionForObject.Invoke(LocalPlayerObjectIndex);

            if (result.ObjectValid && !PretendIdentifierRejected)
            {
                PlayerCollectionFallbackSource = null;
                return result.EffectiveCollection;
            }

            return PlayerAssignmentFallback();
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetPlayerCollection), ex);
            return null;
        }
    }

    private (Guid Id, string Name)? PlayerAssignmentFallback()
    {
        PlayerCollectionFallbackSource = null;

        if (GameClientHelper.Current() is not (GameClient.Korean or GameClient.Chinese))
            return null;

        if (NoireService.ObjectTable.LocalPlayer is not { } localPlayer)
            return null;

        var (source, collection) = ProbeServingCollection() is { } served
            ? ($"Probe -> '{served.Name}'", ((Guid Id, string Name)?)served)
            : ReadPlayerAssignments(localPlayer.Customize);

        if (collection is null)
            return null;

        PlayerCollectionFallbackSource = source;

        if (_announcedFallbackSource != source)
        {
            _announcedFallbackSource = source;
            NoireLogger.LogDebug($"Penumbra rejects the local player identifier on this client, '{source}' assignment used instead.", LogPrefix);
        }

        return collection;
    }

    private (string Source, (Guid Id, string Name)? Collection) ReadPlayerAssignments(ReadOnlySpan<byte> customize)
    {
        if (_getCollection.Invoke(ApiCollectionType.Yourself) is { } yourself)
            return ("Your Character", yourself);

        if (customize.Length > (int)CustomizeIndex.Tribe && customize[(int)CustomizeIndex.Race] != 0)
        {
            var gender = customize[(int)CustomizeIndex.Gender];
            var tribe = customize[(int)CustomizeIndex.Tribe];

            if (gender <= 1)
            {
                if (tribe is >= 1 and <= 16)
                {
                    var racial = (ApiCollectionType)((int)ApiCollectionType.MaleMidlander + 2 * (tribe - 1) + gender);

                    if (_getCollection.Invoke(racial) is { } racialCollection)
                        return ($"{racial}", racialCollection);
                }

                var group = gender == 0 ? ApiCollectionType.MalePlayerCharacter : ApiCollectionType.FemalePlayerCharacter;

                if (_getCollection.Invoke(group) is { } genderCollection)
                    return ($"{group}", genderCollection);
            }
        }

        return ("Base", _getCollection.Invoke(ApiCollectionType.Default));
    }

    private const int ProbePriority = int.MaxValue;
    private const long ProbeFreshnessMilliseconds = 5000;

    private long _probeStamp;
    private bool _probeCached;
    private (Guid Id, string Name)? _probedServing;

    private (Guid Id, string Name)? ProbeServingCollection()
    {
        if (_probeCached && Environment.TickCount64 - _probeStamp < ProbeFreshnessMilliseconds)
            return _probedServing;

        _probedServing = ProbeServingCollectionCore();
        _probeCached = true;
        _probeStamp = Environment.TickCount64;

        return _probedServing;
    }

    private (Guid Id, string Name)? ProbeServingCollectionCore()
    {
        if (GetAllCollections() is not { Count: > 0 } collections)
            return null;

        var marked = new List<Guid>(collections.Count);

        try
        {
            foreach (var (id, _) in collections)
            {
                var redirect = new Dictionary<string, string> { ["bypassemote/probe/serving_collection.tex"] = $"bypassemoteprobe_{id:N}" };

                if (_addTemporaryMod.Invoke("BypassEmote.ServingCollectionProbe", id, redirect, string.Empty, ProbePriority)
                    is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged)
                {
                    marked.Add(id);
                }
            }

            if (marked.Count == 0)
                return null;

            var resolved = _resolvePlayerPath.Invoke("bypassemote/probe/serving_collection.tex") ?? string.Empty;
            var markerAt = resolved.IndexOf("bypassemoteprobe_", StringComparison.OrdinalIgnoreCase);

            if (markerAt < 0)
                return (Guid.Empty, "None");

            var hex = resolved[(markerAt + "bypassemoteprobe_".Length)..];

            if (hex.Length >= 32
                && Guid.TryParseExact(hex[..32], "N", out var servingId)
                && collections.TryGetValue(servingId, out var servingName))
            {
                return (servingId, servingName);
            }

            return null;
        }
        finally
        {
            foreach (var id in marked)
                _removeTemporaryMod.Invoke("BypassEmote.ServingCollectionProbe", id, ProbePriority);
        }
    }

    public string DescribePlayerAssignmentChain()
    {
        try
        {
            var lines = new List<string>
            {
                $"Serving collection (probe): {(ProbeServingCollection() is { } served
                    ? served.Id == Guid.Empty ? "vanilla, no mods served" : $"'{served.Name}'"
                    : "probe failed, the chain below decides")}",
                $"Your Character: {DescribeAssignment(_getCollection.Invoke(ApiCollectionType.Yourself))}",
            };

            if (NoireService.ObjectTable.LocalPlayer is { } localPlayer)
            {
                var customize = localPlayer.Customize;

                if (customize.Length > (int)CustomizeIndex.Tribe)
                {
                    var gender = customize[(int)CustomizeIndex.Gender];
                    var tribe = customize[(int)CustomizeIndex.Tribe];

                    if (gender <= 1 && tribe is >= 1 and <= 16)
                    {
                        var racial = (ApiCollectionType)((int)ApiCollectionType.MaleMidlander + 2 * (tribe - 1) + gender);
                        lines.Add($"{racial}: {DescribeAssignment(_getCollection.Invoke(racial))}");

                        var group = gender == 0 ? ApiCollectionType.MalePlayerCharacter : ApiCollectionType.FemalePlayerCharacter;
                        lines.Add($"{group}: {DescribeAssignment(_getCollection.Invoke(group))}");
                    }
                }
            }
            else
            {
                lines.Add("No local player to read the racial and gender groups from.");
            }

            lines.Add($"Base: {DescribeAssignment(_getCollection.Invoke(ApiCollectionType.Default))}");

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return $"Assignment probe failed: {ex.Message}";
        }
    }

    private static string DescribeAssignment((Guid Id, string Name)? assignment)
    {
        if (assignment is not { } value)
            return "not assigned";

        return value.Id == Guid.Empty ? $"'{value.Name}' (the empty collection, swaps refused)" : $"'{value.Name}'";
    }

    private void OnPreSettingsDraw(string modDirectory)
    {
        if (string.Equals(modDirectory, OwnModDirectoryName, StringComparison.OrdinalIgnoreCase))
            _ownPanelDrawnAt = Environment.TickCount64;
    }

    public bool OwnPanelOnScreen
        => _ownPanelDrawnAt > 0 && Environment.TickCount64 - _ownPanelDrawnAt <= PanelFreshnessMilliseconds;

    public bool RefreshOwnPanel()
    {
        if (OwnModDirectoryName is not { Length: > 0 } ownDirectory || !OwnPanelOnScreen)
            return false;

        return OpenMod(ownDirectory, string.Empty);
    }

    public bool OpenMod(string modDirectory, string modName)
    {
        try
        {
            return _openMainWindow.Invoke(TabType.Mods, modDirectory, modName) == PenumbraApiEc.Success;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(OpenMod), ex);
            return false;
        }
    }

    public IReadOnlyDictionary<string, string>? GetModNames()
    {
        try
        {
            var mods = _getModList.Invoke();
            return mods == null ? null : new Dictionary<string, string>(mods, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetModNames), ex);
            return null;
        }
    }

    public IReadOnlyDictionary<string, ModState>? GetAllModStates(Guid collectionId)
    {
        try
        {
            var (ec, settings) = _getAllModSettings.Invoke(collectionId);
            if (ec != PenumbraApiEc.Success || settings == null)
                return null;

            var states = new Dictionary<string, ModState>(settings.Count, StringComparer.OrdinalIgnoreCase);

            // Unnamed tuple: (Enabled, Priority, Settings, Inherited, Temporary)
            foreach (var (modDirectory, entry) in settings)
                states[modDirectory] = new ModState(entry.Item1, entry.Item2);

            return states;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetAllModStates), ex);
            return null;
        }
    }

    public bool TrySetModEnabled(Guid collectionId, string modDirectory, bool enabled)
        => SetModEnabled(collectionId, modDirectory, enabled) is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;

    public PenumbraApiEc SetModEnabled(Guid collectionId, string modDirectory, bool enabled)
    {
        try
        {
            return _trySetMod.Invoke(collectionId, modDirectory, enabled);
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(SetModEnabled), ex);
            return PenumbraApiEc.UnknownError;
        }
    }

    public bool TrySetModPriority(Guid collectionId, string modDirectory, int priority)
    {
        try
        {
            var ec = _trySetModPriority.Invoke(collectionId, modDirectory, priority);
            return ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(TrySetModPriority), ex);
            return false;
        }
    }

    public bool TrySelectOption(Guid collectionId, string modDirectory, string groupName, string optionName)
        => SelectOption(collectionId, modDirectory, groupName, optionName)
            is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;

    public PenumbraApiEc SelectOption(Guid collectionId, string modDirectory, string groupName, string optionName)
    {
        try
        {
            return _trySetModSettings.Invoke(collectionId, modDirectory, optionGroupName: groupName,
                optionNames: [optionName]);
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(SelectOption), ex);
            return PenumbraApiEc.UnknownError;
        }
    }

    public IReadOnlyDictionary<Guid, string>? GetAllCollections()
    {
        try
        {
            return _getCollections.Invoke();
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetAllCollections), ex);
            return null;
        }
    }

    public bool TryDropOwnSettings(Guid collectionId, string modDirectory)
    {
        try
        {
            var ec = _tryInheritMod.Invoke(collectionId, modDirectory, inherit: true);
            return ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(TryDropOwnSettings), ex);
            return false;
        }
    }

    public bool TryDropTempSettings(Guid collectionId, string modDirectory)
    {
        try
        {
            var ec = _removeTemporaryModSettings.Invoke(collectionId, modDirectory, key: 0);

            if (ec == PenumbraApiEc.TemporarySettingDisallowed)
            {
                NoireLogger.LogDebug(
                    $"Locked temporary settings on '{modDirectory}' in collection {collectionId} "
                    + $"({DescribeTempSettings(collectionId, modDirectory) ?? "unreadable"}).",
                    LogPrefix);
            }

            return ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(TryDropTempSettings), ex);
            return false;
        }
    }

    public string? DescribeTempSettings(Guid collectionId, string modDirectory)
    {
        try
        {
            var ec = _queryTemporaryModSettings.Invoke(collectionId, modDirectory, out var settings, out var source, key: 0);

            if (ec != PenumbraApiEc.Success)
                return $"query {ec}, source '{source}'";

            if (settings is not { } held)
                return null;

            var groups = held.Settings.Count == 0
                ? "no groups"
                : string.Join("; ", held.Settings.Select(pair => $"'{pair.Key}' -> [{string.Join(", ", pair.Value)}]"));

            return $"source '{source}', inherit {held.ForceInherit}, enabled {held.Enabled}, "
                + $"{held.Settings.Count} group(s): {groups}";
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(DescribeTempSettings), ex);
            return "query threw";
        }
    }

    public bool SetTemporaryRedirects(string tag, Guid collectionId, IReadOnlyDictionary<string, string> redirects,
        int priority)
    {
        try
        {
            var forward = new Dictionary<string, string>(redirects.Count);
            foreach (var (gamePath, file) in redirects)
                forward[gamePath] = file;

            var ec = _addTemporaryMod.Invoke(tag, collectionId, forward, string.Empty, priority);
            return ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(SetTemporaryRedirects), ex);
            return false;
        }
    }

    public bool ClearTemporaryRedirects(string tag, Guid collectionId, int priority)
    {
        try
        {
            var ec = _removeTemporaryMod.Invoke(tag, collectionId, priority);
            return ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(ClearTemporaryRedirects), ex);
            return false;
        }
    }

    public (int Breaking, int Feature)? ReportedApiVersion()
    {
        try
        {
            var version = _apiVersion.Invoke();
            return (version.Breaking, version.Features);
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(ReportedApiVersion), ex);
            return null;
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? GetAvailableOptions(string modDirectory)
    {
        try
        {
            if (_getAvailableModSettings.Invoke(modDirectory, modName: string.Empty) is not { } available)
                return null;

            var byGroup = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (var (groupName, group) in available)
                byGroup[groupName] = group.Item1;

            return byGroup;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetAvailableOptions), ex);
            return null;
        }
    }

    public IReadOnlyDictionary<string, string>? GetSelectedOptions(Guid collectionId, string modDirectory)
    {
        try
        {
            var (ec, settings) = _getCurrentModSettings.Invoke(collectionId, modDirectory, modName: string.Empty,
                ignoreInheritance: false);

            if (ec != PenumbraApiEc.Success || settings is not { } current)
                return null;

            var selected = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (groupName, options) in current.Item3)
            {
                if (options.Count > 0)
                    selected[groupName] = options[0];
            }

            return selected;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetSelectedOptions), ex);
            return null;
        }
    }

    public bool AddMod(string modDirectory)
    {
        try
        {
            return _addMod.Invoke(modDirectory) == PenumbraApiEc.Success;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(AddMod), ex);
            return false;
        }
    }

    public ModReadResult ReloadMod(string modDirectory)
    {
        try
        {
            return _reloadMod.Invoke(modDirectory, string.Empty) switch
            {
                PenumbraApiEc.Success => ModReadResult.Read,
                PenumbraApiEc.ModMissing => ModReadResult.NotHeld,
                _ => ModReadResult.Refused,
            };
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(ReloadMod), ex);
            return ModReadResult.ReadThenThrew;
        }
    }

    public bool? HoldsMod(string modDirectory)
    {
        try
        {
            var (ec, _, _, _) = _getModPath.Invoke(modDirectory, string.Empty);
            return ec != PenumbraApiEc.ModMissing;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(HoldsMod), ex);
            return null;
        }
    }

    public string? GetModRootDirectory()
    {
        try
        {
            return _getModDirectory.Invoke();
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetModRootDirectory), ex);
            return null;
        }
    }

    public bool RedrawLocalPlayer()
    {
        try
        {
            _redrawObject.Invoke(LocalPlayerObjectIndex, RedrawType.Redraw);
            return true;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(RedrawLocalPlayer), ex);
            return false;
        }
    }

    private void Probe()
    {
        _lastProbeUtc = DateTime.UtcNow;
        var wasAvailable = _available;
        var wasReadiness = _readiness;

        try
        {
            var (breaking, _) = _apiVersion.Invoke();

            _reportedBreaking = breaking;
            _readiness = breaking >= MinimumBreakingVersion ? PenumbraReadiness.Ready : PenumbraReadiness.TooOld;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(Probe), ex);
            _readiness = PenumbraReadiness.Missing;
        }

        _available = _readiness == PenumbraReadiness.Ready;

        if (_readiness != wasReadiness)
            NoireLogger.LogDebug($"Penumbra reads as {_readiness}.", LogPrefix);

        if (_available != wasAvailable)
            RaiseAvailabilityChanged();
    }

    private void OnPenumbraInitialized()
        => Probe();

    private void OnPenumbraDisposed()
    {
        var wasAvailable = _available;
        _readiness = PenumbraReadiness.Missing;
        _available = false;

        if (wasAvailable)
            RaiseAvailabilityChanged();
    }

    private void OnModSettingChanged(ModSettingChange type, Guid collectionId, string modDirectory, bool inherited)
    {
        if (modDirectory == OwnModDirectoryName)
            RaiseOnFrameworkThread(OwnModSettingChanged);
        else
            RaiseOnFrameworkThread(ExternalModChanged, collectionId);
    }

    // Logs only. Penumbra raises this on the game's actual resource request
    private static void OnResourcePathResolved(nint gameObject, string gamePath, string localPath)
    {
        var isPap = gamePath.EndsWith(".pap", StringComparison.OrdinalIgnoreCase);
        var isTmb = gamePath.EndsWith(".tmb", StringComparison.OrdinalIgnoreCase);

        if ((!isPap && !isTmb) || !gamePath.Contains("emote", StringComparison.Ordinal))
            return;

        var onLocalPlayer = gameObject != 0 && gameObject == (NoireService.ObjectTable.LocalPlayer?.Address ?? 0);

        var served = string.Equals(gamePath, localPath, StringComparison.Ordinal) ? "vanilla" : $"'{localPath}'";
        var size = "?";
        try
        {
            if (served != "vanilla" && System.IO.File.Exists(localPath))
                size = new System.IO.FileInfo(localPath).Length.ToString();
        }
        catch
        {
            // no-op
        }

#if DEBUG
        NoireLogger.LogDebug(
            $"{(isPap ? "Pap" : "Tmb")} requested by the game: '{gamePath}' [vanilla path] served {served} "
            + $"({size} bytes, object 0x{gameObject:X} {(onLocalPlayer ? "LOCAL PLAYER" : "other or none")}).",
            LogPrefix);
#endif
    }

    private void OnModDeleted(string modDirectory)
    {
        if (modDirectory == OwnModDirectoryName)
            RaiseOnFrameworkThread(OwnModDeleted);
    }

    private void OnModDirectoryChanged(string newPath, bool isValid)
        => RaiseOnFrameworkThread(ModRootChanged);

    private void OnGameObjectRedrawn(nint objectPtr, int objectTableIndex)
        => RaiseOnFrameworkThread(GameObjectRedrawn, objectTableIndex);

    private void RaiseAvailabilityChanged()
        => AsyncHelper.RunOnFramework(AvailabilityChanged, _available);

    private static void RaiseOnFrameworkThread(Action? handler)
        => AsyncHelper.RunOnFramework(handler);

    private static void RaiseOnFrameworkThread<T>(Action<T>? handler, T value)
        => AsyncHelper.RunOnFramework(handler, value);

    private void LogFailureOnce(string kind, Exception ex)
        => NoireLogger.LogErrorOnce($"{LogOnceScope}{kind}", ex,
            $"Penumbra IPC call failed ({kind}). Further failures of this kind are not logged again this session.",
            LogPrefix);

    public void Dispose()
    {
        _initialized.Event -= OnPenumbraInitialized;
        _disposed.Event -= OnPenumbraDisposed;
        _modSettingChanged.Event -= OnModSettingChanged;
        _modDeleted.Event -= OnModDeleted;
        _modDirectoryChanged.Event -= OnModDirectoryChanged;
        _gameObjectRedrawn.Event -= OnGameObjectRedrawn;
        _resourcePathResolved.Event -= OnResourcePathResolved;
        _preSettingsDraw.Event -= OnPreSettingsDraw;

        _initialized.Disable();
        _disposed.Disable();
        _modSettingChanged.Disable();
        _modDeleted.Disable();
        _modDirectoryChanged.Disable();
        _gameObjectRedrawn.Disable();
        _resourcePathResolved.Disable();

        _initialized.Dispose();
        _disposed.Dispose();
        _modSettingChanged.Dispose();
        _modDeleted.Dispose();
        _modDirectoryChanged.Dispose();
        _gameObjectRedrawn.Dispose();
        _resourcePathResolved.Dispose();
        _preSettingsDraw.Dispose();

        AvailabilityChanged = null;
        OwnModSettingChanged = null;
        ExternalModChanged = null;
        OwnModDeleted = null;
        ModRootChanged = null;
        GameObjectRedrawn = null;
    }
}
