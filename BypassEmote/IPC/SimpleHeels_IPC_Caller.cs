using ECommons.EzIpcManager;
using NoireLib;
using NoireLib.Helpers;
using System;

namespace BypassEmote.IPC;

// Won't work yet, WIP

public class SimpleHeels_IPC_Caller
{
    public SimpleHeels_IPC_Caller()
    {
        EzIPC.Init(this, "SimpleHeels");
    }

    public bool IsValid()
    {
        if (InteropHelper.IsPluginAvailable("SimpleHeels", "0.11.0.19") != NoireLib.Enums.PluginAvailability.Available)
            return false;

        var version = ApiVersion(false);
        return version.Major >= 2 && version.Minor >= 5;
    }

    [EzIPC("ApiVersion")]
    public Func<(int Major, int Minor)> _apiVersion;

    public (int Major, int Minor) ApiVersion(bool logErrors = true)
    {
        try
        {
            return _apiVersion.Invoke();
        }
        catch (Exception ex)
        {
            if (logErrors)
                NoireLogger.LogError(this, ex, "Error calling ApiVersion IPC");
            return (-1, -1);
        }
    }

    [EzIPC("RegisterEmoteOverride")]
    public Action<int, uint, byte> _registerEmoteOverride;

    public void RegisterEmoteOverride(int gameObjectIndex, uint emoteModeId, byte cPose)
    {
        if (!IsValid())
            return;

        try
        {
            //NoireLogger.LogDebug($"Registering emote override on game obj index {gameObjectIndex}, emote mode {emoteModeId}, cpose {cPose}");
            _registerEmoteOverride?.Invoke(gameObjectIndex, emoteModeId, cPose);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "Error calling RegisterEmoteOverride IPC");
        }
    }

    [EzIPC("ClearEmoteOverride")]
    public Action<int> _clearEmoteOverride;

    public void ClearEmoteOverride(int gameObjectIndex)
    {
        if (!IsValid())
            return;

        try
        {
            //NoireLogger.LogDebug($"Clearing emote override on game obj index {gameObjectIndex}");
            _clearEmoteOverride?.Invoke(gameObjectIndex);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "Error calling ClearEmoteOverride IPC");
        }
    }
}
