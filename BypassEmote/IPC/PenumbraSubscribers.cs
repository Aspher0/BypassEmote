using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Penumbra.Api.Enums;
using System;
using System.Collections.Generic;

namespace BypassEmote.IPC;

internal sealed class ApiVersion(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<(int, int)> _gate = pi.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersion.V5");

    public (int Breaking, int Features) Invoke() => _gate.InvokeFunc();
}

internal sealed class ResolvePlayerPath(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string, string> _gate
        = pi.GetIpcSubscriber<string, string>("Penumbra.ResolvePlayerPath");

    public string Invoke(string gamePath) => _gate.InvokeFunc(gamePath);
}

internal sealed class ResolvePlayerPaths(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string[], string[], (string[], string[][])> _gate
        = pi.GetIpcSubscriber<string[], string[], (string[], string[][])>("Penumbra.ResolvePlayerPaths");

    public (string[], string[][]) Invoke(string[] forward, string[] reverse) => _gate.InvokeFunc(forward, reverse);
}

internal sealed class GetCollectionForObject(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<int, (bool, bool, (Guid, string))> _gate
        = pi.GetIpcSubscriber<int, (bool, bool, (Guid, string))>("Penumbra.GetCollectionForObject.V5");

    public (bool ObjectValid, bool IndividualSet, (Guid Id, string Name) EffectiveCollection) Invoke(int gameObjectIndex)
        => _gate.InvokeFunc(gameObjectIndex);
}

internal sealed class GetCollection(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<byte, (Guid, string)?> _gate
        = pi.GetIpcSubscriber<byte, (Guid, string)?>("Penumbra.GetCollection");

    public (Guid Id, string Name)? Invoke(ApiCollectionType type) => _gate.InvokeFunc((byte)type);
}

internal sealed class GetCollections(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Dictionary<Guid, string>> _gate
        = pi.GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections.V5");

    public Dictionary<Guid, string> Invoke() => _gate.InvokeFunc();
}

internal sealed class GetAllModSettings(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, bool, bool, int,
        (int, Dictionary<string, (bool, int, Dictionary<string, List<string>>, bool, bool)>?)> _gate
        = pi.GetIpcSubscriber<Guid, bool, bool, int,
            (int, Dictionary<string, (bool, int, Dictionary<string, List<string>>, bool, bool)>?)>("Penumbra.GetAllModSettings");

    public (PenumbraApiEc, Dictionary<string, (bool, int, Dictionary<string, List<string>>, bool, bool)>?) Invoke(
        Guid collectionId, bool ignoreInheritance = false, bool ignoreTemporary = false, int key = 0)
    {
        var (ec, settings) = _gate.InvokeFunc(collectionId, ignoreInheritance, ignoreTemporary, key);
        return ((PenumbraApiEc)ec, settings);
    }
}

internal sealed class GetModList(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Dictionary<string, string>> _gate
        = pi.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");

    public Dictionary<string, string> Invoke() => _gate.InvokeFunc();
}

internal sealed class OpenMainWindow(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<int, string, string, int> _gate
        = pi.GetIpcSubscriber<int, string, string, int>("Penumbra.OpenMainWindow.V5");

    public PenumbraApiEc Invoke(TabType tab, string modDirectory = "", string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc((int)tab, modDirectory, modName);
}

internal sealed class TrySetMod(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, bool, int> _gate
        = pi.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TrySetMod.V5");

    public PenumbraApiEc Invoke(Guid collectionId, string modDirectory, bool enabled, string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc(collectionId, modDirectory, modName, enabled);
}

internal sealed class TrySetModPriority(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, int, int> _gate
        = pi.GetIpcSubscriber<Guid, string, string, int, int>("Penumbra.TrySetModPriority.V5");

    public PenumbraApiEc Invoke(Guid collectionId, string modDirectory, int priority, string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc(collectionId, modDirectory, modName, priority);
}

internal sealed class TrySetModSettings(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, string, IReadOnlyList<string>, int> _gate
        = pi.GetIpcSubscriber<Guid, string, string, string, IReadOnlyList<string>, int>("Penumbra.TrySetModSettings.V5");

    public PenumbraApiEc Invoke(Guid collectionId, string modDirectory, string optionGroupName,
        IReadOnlyList<string> optionNames, string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc(collectionId, modDirectory, modName, optionGroupName, optionNames);
}

internal sealed class TryInheritMod(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, bool, int> _gate
        = pi.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TryInheritMod.V5");

    public PenumbraApiEc Invoke(Guid collectionId, string modDirectory, bool inherit, string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc(collectionId, modDirectory, modName, inherit);
}

internal sealed class RemoveTemporaryModSettings(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, int, int> _gate
        = pi.GetIpcSubscriber<Guid, string, string, int, int>("Penumbra.RemoveTemporaryModSettings.V5");

    public PenumbraApiEc Invoke(Guid collectionId, string modDirectory, int key = 0, string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc(collectionId, modDirectory, modName, key);
}

internal sealed class QueryTemporaryModSettings(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, int,
        (int, (bool, bool, int, Dictionary<string, List<string>>)?, string)> _gate
        = pi.GetIpcSubscriber<Guid, string, string, int,
            (int, (bool, bool, int, Dictionary<string, List<string>>)?, string)>("Penumbra.QueryTemporaryModSettings.V5");

    public PenumbraApiEc Invoke(Guid collectionId, string modDirectory,
        out (bool ForceInherit, bool Enabled, int Priority, Dictionary<string, List<string>> Settings)? settings,
        out string source, int key = 0, string modName = "")
    {
        var (ec, held, heldBy) = _gate.InvokeFunc(collectionId, modDirectory, modName, key);

        settings = held;
        source = heldBy;

        return (PenumbraApiEc)ec;
    }
}

internal sealed class GetCurrentModSettings(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<Guid, string, string, bool,
        (int, (bool, int, Dictionary<string, List<string>>, bool)?)> _gate
        = pi.GetIpcSubscriber<Guid, string, string, bool,
            (int, (bool, int, Dictionary<string, List<string>>, bool)?)>("Penumbra.GetCurrentModSettings.V5");

    public (PenumbraApiEc, (bool, int, Dictionary<string, List<string>>, bool)?) Invoke(Guid collectionId,
        string modDirectory, string modName = "", bool ignoreInheritance = false)
    {
        var (ec, settings) = _gate.InvokeFunc(collectionId, modDirectory, modName, ignoreInheritance);
        return ((PenumbraApiEc)ec, settings);
    }
}

internal sealed class GetAvailableModSettings(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string, string, IReadOnlyDictionary<string, (string[], int)>?> _gate
        = pi.GetIpcSubscriber<string, string, IReadOnlyDictionary<string, (string[], int)>?>(
            "Penumbra.GetAvailableModSettings.V5");

    public IReadOnlyDictionary<string, (string[], int)>? Invoke(string modDirectory, string modName = "")
        => _gate.InvokeFunc(modDirectory, modName);
}

internal sealed class AddMod(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string, int> _gate = pi.GetIpcSubscriber<string, int>("Penumbra.AddMod.V5");

    public PenumbraApiEc Invoke(string modDirectory) => (PenumbraApiEc)_gate.InvokeFunc(modDirectory);
}

internal sealed class ReloadMod(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string, string, int> _gate
        = pi.GetIpcSubscriber<string, string, int>("Penumbra.ReloadMod.V5");

    public PenumbraApiEc Invoke(string modDirectory, string modName = "")
        => (PenumbraApiEc)_gate.InvokeFunc(modDirectory, modName);
}

internal sealed class GetModDirectory(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string> _gate = pi.GetIpcSubscriber<string>("Penumbra.GetModDirectory");

    public string Invoke() => _gate.InvokeFunc();
}

internal sealed class RedrawObject(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<int, int, object> _gate
        = pi.GetIpcSubscriber<int, int, object>("Penumbra.RedrawObject.V5");

    public void Invoke(int gameObjectIndex, RedrawType setting = RedrawType.Redraw)
        => _gate.InvokeAction(gameObjectIndex, (int)setting);
}

internal sealed class AddTemporaryMod(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string, Guid, Dictionary<string, string>, string, int, int> _gate
        = pi.GetIpcSubscriber<string, Guid, Dictionary<string, string>, string, int, int>("Penumbra.AddTemporaryMod.V5");

    public PenumbraApiEc Invoke(string tag, Guid collectionId, Dictionary<string, string> paths, string manipString,
        int priority)
        => (PenumbraApiEc)_gate.InvokeFunc(tag, collectionId, paths, manipString, priority);
}

internal sealed class RemoveTemporaryMod(IDalamudPluginInterface pi)
{
    private readonly ICallGateSubscriber<string, Guid, int, int> _gate
        = pi.GetIpcSubscriber<string, Guid, int, int>("Penumbra.RemoveTemporaryMod.V5");

    public PenumbraApiEc Invoke(string tag, Guid collectionId, int priority)
        => (PenumbraApiEc)_gate.InvokeFunc(tag, collectionId, priority);
}

internal sealed class PenumbraEvent : IDisposable
{
    private readonly ICallGateSubscriber<object> _gate;
    private readonly Action _raise;
    private readonly string _label;
    private bool _enabled;

    public PenumbraEvent(IDalamudPluginInterface pi, string label)
    {
        _label = label;
        _gate = pi.GetIpcSubscriber<object>(label);
        _raise = Raise;
    }

    public event Action? Event;

    private void Raise()
    {
        try
        {
            Event?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Handler for '{_label}' failed.", "[Penumbra] ");
        }
    }

    public void Enable()
    {
        if (_enabled)
            return;

        _gate.Subscribe(_raise);
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _gate.Unsubscribe(_raise);
        _enabled = false;
    }

    public void Dispose() => Disable();
}

internal sealed class PenumbraEvent<T1> : IDisposable
{
    private readonly ICallGateSubscriber<T1, object> _gate;
    private readonly Action<T1> _raise;
    private readonly string _label;
    private bool _enabled;

    public PenumbraEvent(IDalamudPluginInterface pi, string label)
    {
        _label = label;
        _gate = pi.GetIpcSubscriber<T1, object>(label);
        _raise = Raise;
    }

    public event Action<T1>? Event;

    private void Raise(T1 a1)
    {
        try
        {
            Event?.Invoke(a1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Handler for '{_label}' failed.", "[Penumbra] ");
        }
    }

    public void Enable()
    {
        if (_enabled)
            return;

        _gate.Subscribe(_raise);
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _gate.Unsubscribe(_raise);
        _enabled = false;
    }

    public void Dispose() => Disable();
}

internal sealed class PenumbraEvent<T1, T2, T3> : IDisposable
{
    private readonly ICallGateSubscriber<T1, T2, T3, object> _gate;
    private readonly Action<T1, T2, T3> _raise;
    private readonly string _label;
    private bool _enabled;

    public PenumbraEvent(IDalamudPluginInterface pi, string label)
    {
        _label = label;
        _gate = pi.GetIpcSubscriber<T1, T2, T3, object>(label);
        _raise = Raise;
    }

    public event Action<T1, T2, T3>? Event;

    private void Raise(T1 a1, T2 a2, T3 a3)
    {
        try
        {
            Event?.Invoke(a1, a2, a3);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Handler for '{_label}' failed.", "[Penumbra] ");
        }
    }

    public void Enable()
    {
        if (_enabled)
            return;

        _gate.Subscribe(_raise);
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _gate.Unsubscribe(_raise);
        _enabled = false;
    }

    public void Dispose() => Disable();
}

internal sealed class PenumbraEvent<T1, T2, T3, T4> : IDisposable
{
    private readonly ICallGateSubscriber<T1, T2, T3, T4, object> _gate;
    private readonly Action<T1, T2, T3, T4> _raise;
    private readonly string _label;
    private bool _enabled;

    public PenumbraEvent(IDalamudPluginInterface pi, string label)
    {
        _label = label;
        _gate = pi.GetIpcSubscriber<T1, T2, T3, T4, object>(label);
        _raise = Raise;
    }

    public event Action<T1, T2, T3, T4>? Event;

    private void Raise(T1 a1, T2 a2, T3 a3, T4 a4)
    {
        try
        {
            Event?.Invoke(a1, a2, a3, a4);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Handler for '{_label}' failed.", "[Penumbra] ");
        }
    }

    public void Enable()
    {
        if (_enabled)
            return;

        _gate.Subscribe(_raise);
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _gate.Unsubscribe(_raise);
        _enabled = false;
    }

    public void Dispose() => Disable();
}
