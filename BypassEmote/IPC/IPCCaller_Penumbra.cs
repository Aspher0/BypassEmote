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
    private readonly GetCollectionForObject _getCollectionForObject;
    private readonly GetAllModSettings _getAllModSettings;
    private readonly GetModList _getModList;
    private readonly OpenMainWindow _openMainWindow;
    private readonly TrySetMod _trySetMod;
    private readonly TrySetModPriority _trySetModPriority;
    private readonly AddMod _addMod;
    private readonly ReloadMod _reloadMod;
    private readonly GetModPath _getModPath;
    private readonly GetModDirectory _getModDirectory;
    private readonly AddTemporaryMod _addTemporaryMod;
    private readonly RemoveTemporaryMod _removeTemporaryMod;
    private readonly RedrawObject _redrawObject;

    private readonly EventSubscriber _initialized;
    private readonly EventSubscriber _disposed;
    private readonly EventSubscriber<bool> _enabledChange;
    private readonly EventSubscriber<ModSettingChange, Guid, string, bool> _modSettingChanged;
    private readonly EventSubscriber<string> _modDeleted;
    private readonly EventSubscriber<string, bool> _modDirectoryChanged;
    private readonly EventSubscriber<nint, int> _gameObjectRedrawn;
    private readonly EventSubscriber<nint, string, string> _resourcePathResolved;


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
        _getCollectionForObject = new GetCollectionForObject(pluginInterface);
        _getAllModSettings = new GetAllModSettings(pluginInterface);
        _getModList = new GetModList(pluginInterface);
        _openMainWindow = new OpenMainWindow(pluginInterface);
        _trySetMod = new TrySetMod(pluginInterface);
        _trySetModPriority = new TrySetModPriority(pluginInterface);
        _addMod = new AddMod(pluginInterface);
        _reloadMod = new ReloadMod(pluginInterface);
        _getModPath = new GetModPath(pluginInterface);
        _getModDirectory = new GetModDirectory(pluginInterface);
        _addTemporaryMod = new AddTemporaryMod(pluginInterface);
        _removeTemporaryMod = new RemoveTemporaryMod(pluginInterface);
        _redrawObject = new RedrawObject(pluginInterface);

        _initialized = Initialized.Subscriber(pluginInterface);
        _disposed = Disposed.Subscriber(pluginInterface);
        _enabledChange = EnabledChange.Subscriber(pluginInterface);
        _modSettingChanged = ModSettingChanged.Subscriber(pluginInterface);
        _modDeleted = ModDeleted.Subscriber(pluginInterface);
        _modDirectoryChanged = ModDirectoryChanged.Subscriber(pluginInterface);
        _gameObjectRedrawn = Penumbra.Api.IpcSubscribers.GameObjectRedrawn.Subscriber(pluginInterface);
        _resourcePathResolved = GameObjectResourcePathResolved.Subscriber(pluginInterface);

        _initialized.Event += OnPenumbraInitialized;
        _disposed.Event += OnPenumbraDisposed;
        _enabledChange.Event += OnEnabledChange;
        _modSettingChanged.Event += OnModSettingChanged;
        _modDeleted.Event += OnModDeleted;
        _modDirectoryChanged.Event += OnModDirectoryChanged;
        _gameObjectRedrawn.Event += OnGameObjectRedrawn;
        _resourcePathResolved.Event += OnResourcePathResolved;

        _initialized.Enable();
        _disposed.Enable();
        _enabledChange.Enable();
        _modSettingChanged.Enable();
        _modDeleted.Enable();
        _modDirectoryChanged.Enable();
        _gameObjectRedrawn.Enable();
        _resourcePathResolved.Enable();

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

    public bool ReloadMod(string modDirectory)
    {
        try
        {
            return _reloadMod.Invoke(modDirectory, string.Empty) == PenumbraApiEc.Success;
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(ReloadMod), ex);
            return false;
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

    public PenumbraApiEc AddTemporaryMod(string tag, Guid collectionId, Dictionary<string, string> paths, int priority)
    {
        try
        {
            return _addTemporaryMod.Invoke(tag, collectionId, paths, string.Empty, priority);
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(AddTemporaryMod), ex);
            return PenumbraApiEc.UnknownError;
        }
    }

    public PenumbraApiEc RemoveTemporaryMod(string tag, Guid collectionId, int priority)
    {
        try
        {
            return _removeTemporaryMod.Invoke(tag, collectionId, priority);
        }
        catch (Exception ex)
        {
            LogFailureOnce(nameof(RemoveTemporaryMod), ex);
            return PenumbraApiEc.UnknownError;
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
            // Logging must never interfere with a resolve.
        }

        NoireLogger.LogWarning(
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

        AvailabilityChanged = null;
        OwnModSettingChanged = null;
        ExternalModChanged = null;
        OwnModDeleted = null;
        ModRootChanged = null;
        GameObjectRedrawn = null;
    }
}
