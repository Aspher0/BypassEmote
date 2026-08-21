using BypassEmote.EmoteSwap;
using BypassEmote.Models;
using Dalamud.Plugin;
using NoireLib;
using NoireLib.Helpers;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using System;
using System.Collections.Generic;

namespace BypassEmote.IPC;

public enum ModReadResult
{
    Read,
    ReadThenThrew,
    NotHeld,
    Refused,
}

public sealed class IPCCaller_Penumbra : IDisposable
{
    private const string LogPrefix = "[IPCCaller_Penumbra] ";
    private const string LogOnceScope = "BypassEmote.Penumbra.";
    private const int RequiredBreakingVersion = 5;
    private const int LocalPlayerObjectIndex = 0;

    private static readonly TimeSpan ReprobeInterval = TimeSpan.FromSeconds(1);

    private readonly SwapModIdentity _identity;

    private string? OwnModDirectoryName => _identity.DirectoryName;

    private readonly ApiVersion _apiVersion;
    private readonly GetEnabledState _getEnabledState;
    private readonly ResolvePlayerPath _resolvePlayerPath;
    private readonly ResolvePlayerPaths _resolvePlayerPaths;
    private readonly GetCollectionForObject _getCollectionForObject;
    private readonly GetAllModSettings _getAllModSettings;
    private readonly GetModList _getModList;
    private readonly OpenMainWindow _openMainWindow;
    private readonly TrySetMod _trySetMod;
    private readonly TrySetModPriority _trySetModPriority;
    private readonly TrySetModSettings _trySetModSettings;
    private readonly GetCurrentModSettings _getCurrentModSettings;
    private readonly GetAvailableModSettings _getAvailableModSettings;
    private readonly AddMod _addMod;
    private readonly ReloadMod _reloadMod;
    private readonly GetModPath _getModPath;
    private readonly GetModDirectory _getModDirectory;
    private readonly RedrawObject _redrawObject;

    private readonly EventSubscriber _initialized;
    private readonly EventSubscriber _disposed;
    private readonly EventSubscriber<bool> _enabledChange;
    private readonly EventSubscriber<ModSettingChange, Guid, string, bool> _modSettingChanged;
    private readonly EventSubscriber<string> _modDeleted;
    private readonly EventSubscriber<string, bool> _modDirectoryChanged;
    private readonly EventSubscriber<nint, int> _gameObjectRedrawn;
    private readonly EventSubscriber<nint, string, string> _resourcePathResolved;
    private readonly EventSubscriber<string> _preSettingsDraw;

    private long _ownPanelDrawnAt;

    private string? _bounceTarget;

    private const long PanelFreshnessMilliseconds = 250;


    private bool _available;
    private bool _breakingVersionOk;
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
        _getEnabledState = new GetEnabledState(pluginInterface);
        _resolvePlayerPath = new ResolvePlayerPath(pluginInterface);
        _resolvePlayerPaths = new ResolvePlayerPaths(pluginInterface);
        _getCollectionForObject = new GetCollectionForObject(pluginInterface);
        _getAllModSettings = new GetAllModSettings(pluginInterface);
        _getModList = new GetModList(pluginInterface);
        _openMainWindow = new OpenMainWindow(pluginInterface);
        _trySetMod = new TrySetMod(pluginInterface);
        _trySetModPriority = new TrySetModPriority(pluginInterface);
        _trySetModSettings = new TrySetModSettings(pluginInterface);
        _getCurrentModSettings = new GetCurrentModSettings(pluginInterface);
        _getAvailableModSettings = new GetAvailableModSettings(pluginInterface);
        _addMod = new AddMod(pluginInterface);
        _reloadMod = new ReloadMod(pluginInterface);
        _getModPath = new GetModPath(pluginInterface);
        _getModDirectory = new GetModDirectory(pluginInterface);
        _redrawObject = new RedrawObject(pluginInterface);

        _initialized = Initialized.Subscriber(pluginInterface);
        _disposed = Disposed.Subscriber(pluginInterface);
        _enabledChange = EnabledChange.Subscriber(pluginInterface);
        _modSettingChanged = ModSettingChanged.Subscriber(pluginInterface);
        _modDeleted = ModDeleted.Subscriber(pluginInterface);
        _modDirectoryChanged = ModDirectoryChanged.Subscriber(pluginInterface);
        _gameObjectRedrawn = Penumbra.Api.IpcSubscribers.GameObjectRedrawn.Subscriber(pluginInterface);
        _resourcePathResolved = GameObjectResourcePathResolved.Subscriber(pluginInterface);
        _preSettingsDraw = PreSettingsDraw.Subscriber(pluginInterface);

        _initialized.Event += OnPenumbraInitialized;
        _disposed.Event += OnPenumbraDisposed;
        _enabledChange.Event += OnEnabledChange;
        _modSettingChanged.Event += OnModSettingChanged;
        _modDeleted.Event += OnModDeleted;
        _modDirectoryChanged.Event += OnModDirectoryChanged;
        _gameObjectRedrawn.Event += OnGameObjectRedrawn;
        _resourcePathResolved.Event += OnResourcePathResolved;
        _preSettingsDraw.Event += OnPreSettingsDraw;

        _initialized.Enable();
        _disposed.Enable();
        _enabledChange.Enable();
        _modSettingChanged.Enable();
        _modDeleted.Enable();
        _modDirectoryChanged.Enable();
        _gameObjectRedrawn.Enable();
        _resourcePathResolved.Enable();
        _preSettingsDraw.Enable();

        Probe();
    }

    public bool Available
    {
        get
        {
            if (!_available && DateTime.UtcNow - _lastProbeUtc >= ReprobeInterval)
                Probe();

            return _available;
        }
    }

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

    public (Guid Id, string Name)? GetPlayerCollection()
    {
        try
        {
            var result = _getCollectionForObject.Invoke(LocalPlayerObjectIndex);
            return result.ObjectValid ? result.EffectiveCollection : null;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(GetPlayerCollection), ex);
            return null;
        }
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

        if (BounceTarget(ownDirectory) is not { } other)
            return false;

        return OpenMod(other, string.Empty) && OpenMod(ownDirectory, string.Empty);
    }

    private string? BounceTarget(string ownDirectory)
    {
        var ownFolder = TreeFolderOf(ownDirectory);

        if (_bounceTarget is { } cached
            && !string.Equals(cached, ownDirectory, StringComparison.OrdinalIgnoreCase)
            && SameFolder(TreeFolderOf(cached), ownFolder))
        {
            return cached;
        }

        _bounceTarget = null;

        if (GetModNames() is not { } mods)
            return null;

        foreach (var directory in mods.Keys)
        {
            if (string.Equals(directory, ownDirectory, StringComparison.OrdinalIgnoreCase)
                || !SameFolder(TreeFolderOf(directory), ownFolder))
            {
                continue;
            }

            _bounceTarget = directory;

            NoireLogger.LogDebug(
                $"'{directory}' shares the folder '{(ownFolder.Length == 0 ? "<root>" : ownFolder)}' with the generated "
                + "mod, so the panel refresh bounces off it.", LogPrefix);

            return directory;
        }

        NoireLogger.LogDebug(
            $"No other mod sits in '{(ownFolder.Length == 0 ? "<root>" : ownFolder)}', so the panel is left as it is.",
            LogPrefix);

        return null;
    }

    private static bool SameFolder(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private string TreeFolderOf(string modDirectory)
    {
        try
        {
            var (ec, path, _, _) = _getModPath.Invoke(modDirectory, string.Empty);

            if (ec != PenumbraApiEc.Success || path.Length == 0)
                return string.Empty;

            var lastSeparator = path.LastIndexOf('/');

            return lastSeparator < 0 ? string.Empty : path[..lastSeparator];
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(TreeFolderOf), ex);
            return string.Empty;
        }
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

            // Unnamed tuple: (Enabled, Priority, Settings, Inherited, Temporary), so Item1 and Item2 are wanted.
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

        try
        {
            var (breaking, _) = _apiVersion.Invoke();
            _breakingVersionOk = breaking == RequiredBreakingVersion;
            _available = _breakingVersionOk && _getEnabledState.Invoke();
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(Probe), ex);
            _breakingVersionOk = false;
            _available = false;
        }

        if (_available != wasAvailable)
            RaiseAvailabilityChanged();
    }

    private void OnPenumbraInitialized()
        => Probe();

    private void OnPenumbraDisposed()
    {
        var wasAvailable = _available;
        _breakingVersionOk = false;
        _available = false;

        if (wasAvailable)
            RaiseAvailabilityChanged();
    }

    private void OnEnabledChange(bool enabled)
    {
        var wasAvailable = _available;
        _available = _breakingVersionOk && enabled;

        if (_available != wasAvailable)
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
        var key = UniqueNamePlanner.IsComposedPapPath(gamePath) ? "composed" : "vanilla path";

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

        NoireLogger.LogDebug(
            $"{(isPap ? "Pap" : "Tmb")} requested by the game: '{gamePath}' [{key}] served {served} "
            + $"({size} bytes, object 0x{gameObject:X} {(onLocalPlayer ? "LOCAL PLAYER" : "other or none")}).",
            LogPrefix);
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
        _enabledChange.Event -= OnEnabledChange;
        _modSettingChanged.Event -= OnModSettingChanged;
        _modDeleted.Event -= OnModDeleted;
        _modDirectoryChanged.Event -= OnModDirectoryChanged;
        _gameObjectRedrawn.Event -= OnGameObjectRedrawn;
        _resourcePathResolved.Event -= OnResourcePathResolved;
        _preSettingsDraw.Event -= OnPreSettingsDraw;

        _initialized.Disable();
        _disposed.Disable();
        _enabledChange.Disable();
        _modSettingChanged.Disable();
        _modDeleted.Disable();
        _modDirectoryChanged.Disable();
        _gameObjectRedrawn.Disable();
        _resourcePathResolved.Disable();

        _initialized.Dispose();
        _disposed.Dispose();
        _enabledChange.Dispose();
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
