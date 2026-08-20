#if DEBUG
using FFXIVClientStructs.FFXIV.Client.Game;
using NoireLib;
using NoireLib.Hooking;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.EmoteSwap;

public sealed class ClientTriggerMonitor
{
    private const string LogPrefix = "[PacketTrail] ";
    private const string HookGroup = "BypassEmote.TriggerTrail";

    private static readonly Dictionary<int, string> KnownCommandNames = new()
    {
        [0x1F6] = "emote stop notify",
        [0x1F7] = "emote loop exit, in place",
        [0x1F8] = "emote loop exit, with rotation",
        [0x1F9] = "idle pose update A",
        [0x1FA] = "idle pose update B",
        [0x1FB] = "idle pose exit",
    };

    private readonly NoireHook<GameMain.Delegates.ExecuteCommand>? _executeCommand;
    private readonly NoireHook<GameMain.Delegates.ExecuteLocationCommand>? _executeLocationCommand;

    public unsafe ClientTriggerMonitor()
    {
        _executeCommand = new(ExecuteCommandDetour, false, "GameMain.ExecuteCommand") { Group = HookGroup };
        _executeLocationCommand = new(ExecuteLocationCommandDetour, false, "GameMain.ExecuteLocationCommand") { Group = HookGroup };
    }

    private static void LogCommand(int command, string details)
    {
        try
        {
            var name = KnownCommandNames.TryGetValue(command, out var known) ? $" {known}" : string.Empty;
            NoireLogger.LogDebug($"-> 0x{command:X}{name} | {details}", LogPrefix);
        }
        catch
        {
            // Logging must never break the passthrough.
        }
    }

    private bool ExecuteCommandDetour(int command, int first, int second, int third, int fourth)
    {
        LogCommand(command, $"args {first}, {second}, {third}, {fourth}");
        return _executeCommand!.Original(command, first, second, third, fourth);
    }

    private unsafe bool ExecuteLocationCommandDetour(int command, Vector3* location, int first, int second, int third, int fourth)
    {
        LogCommand(command, location == null
            ? $"no location, args {first}, {second}, {third}, {fourth}"
            : $"location {location->X:0.##}, {location->Y:0.##}, {location->Z:0.##}, args {first}, {second}, {third}, {fourth}");

        return _executeLocationCommand!.Original(command, location, first, second, third, fourth);
    }
}
#endif
