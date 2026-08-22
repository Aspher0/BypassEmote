using BypassEmote.IPC;
using NoireLib;
using System;
using System.IO;

namespace BypassEmote.EmoteSwap;

/// <summary>
/// Determines Penumbra's mod version. V3 for stable, V4 for testing.
/// </summary>
internal sealed class ModLayoutDetector
{
    private const string LogPrefix = "[ModLayoutDetector] ";

    private readonly IPCCaller_Penumbra _gateway;
    private readonly object _gate = new();

    private volatile int _layout = ModLayout.V3;
    private volatile bool _settled;
    private volatile bool _probing;

    internal ModLayoutDetector(IPCCaller_Penumbra gateway)
        => _gateway = gateway;

    internal int Layout => _layout;
    internal bool Settled => _settled;

    internal void Invalidate()
        => _settled = false;

    internal int Ensure(string? modDirectory, string modDirectoryName)
    {
        if (_settled || _probing)
            return _layout;

        lock (_gate)
        {
            if (_settled || _probing)
                return _layout;

            _probing = true;

            try
            {
                Probe(modDirectory, modDirectoryName);
            }
            finally
            {
                _probing = false;
            }

            return _layout;
        }
    }

    internal void Observe(string? modDirectory)
    {
        if (_probing || _layout >= ModLayout.V4 || modDirectory == null
            || !File.Exists(Path.Combine(modDirectory, ModLayout.MetaFileName)))
        {
            return;
        }

        lock (_gate)
        {
            if (_layout >= ModLayout.V4 || ModLayout.OnDisk(modDirectory) < ModLayout.V4)
                return;

            Adopt(ModLayout.V4, "Penumbra migrated the mod it was just handed");

            ModLayout.RemoveV3Files(modDirectory);
        }
    }

    private void Probe(string? modDirectory, string modDirectoryName)
    {
        try
        {
            if (!_gateway.Available || modDirectory == null || modDirectoryName.Length == 0)
                return;

            if (!File.Exists(Path.Combine(modDirectory, ModLayout.MetaFileName)))
            {
                NoireLogger.LogDebug("No generated mod to probe with yet; writing V3 until Penumbra says otherwise.", LogPrefix);
                return;
            }

            var before = ModLayout.OnDisk(modDirectory);

            if (before >= ModLayout.V4 && !ModLayout.ToV3(modDirectory))
            {
                Adopt(ModLayout.V4, "the mod is V4 and could not be rewritten for the probe");
                return;
            }

            if (_gateway.ReloadMod(modDirectoryName) is ModReadResult.NotHeld or ModReadResult.Refused)
            {
                NoireLogger.LogDebug($"Penumbra would not read '{modDirectoryName}', so its layout stays unknown.", LogPrefix);
                return;
            }

            var after = ModLayout.OnDisk(modDirectory);

            Adopt(after, after >= ModLayout.V4
                ? "Penumbra rewrote the probe in its own layout"
                : "Penumbra left the probe alone");

            if (after >= ModLayout.V4)
                ModLayout.RemoveV3Files(modDirectory);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not work out Penumbra's mod layout; keeping the last one that held.", LogPrefix);
        }
    }

    private void Adopt(int layout, string because)
    {
        var changed = _layout != layout;

        _layout = layout >= ModLayout.V4 ? ModLayout.V4 : ModLayout.V3;
        _settled = true;

        if (changed)
            NoireLogger.LogDebug($"Penumbra writes mods in the V{_layout} layout: {because}.", LogPrefix);
    }
}
