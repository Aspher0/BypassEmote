using NoireLib;
using NoireLib.Helpers;
using NoireLib.HistoryLogger;
using System.Numerics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BypassEmote.Tests")]

namespace BypassEmote.Helpers;

public static class LogHelper
{
    public const string ChatTag = "[BypassEmote] ";

    private static readonly Logger Channel = new(ChatTag, "BypassEmote", "BypassEmote.Feedback");

    internal static readonly Vector3 ErrorColor = ColorHelper.HexToVector3("#E81313");
    internal static readonly Vector3 SuccessColor = ColorHelper.HexToVector3("#4CAF50");
    internal static readonly Vector3 InfoColor = ColorHelper.HexToVector3("#E6E6E6");
    internal static readonly Vector3 WarningColor = ColorHelper.HexToVector3("#FF8C1A");
    internal static readonly Vector3 SwapLineColor = ColorHelper.HexToVector3("#B3B3B3");

    internal static string ThrottleKeyFor(string channelKey, string kind) => channelKey + kind;

    public static void Error(string message, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.Say(message, ErrorColor, "Error", HistoryLogLevel.Error,
            Configuration.ShowErrorMessages, Configuration.ThrottleTimeErrors,
            kind == null ? null : ThrottleKeyFor("Error", kind), chat);

    public static void Notice(string message, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.Say(message, WarningColor, "Warning", HistoryLogLevel.Warning,
            Configuration.ShowWarningMessages, Configuration.ThrottleTimeWarnings,
            kind == null ? null : ThrottleKeyFor("Warning", kind), chat);

    public static void NoticeAlways(string message, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.SayAlways(message, WarningColor, "Warning", HistoryLogLevel.Warning, chat);

    public static void Success(string message)
        => Channel.SayAlways(message, SuccessColor, "Info");

    public static void Info(string message)
        => Channel.SayAlways(message, InfoColor, "Info");

    public static void SwapLine(string sourceCommand, string targetCommand)
    {
        Channel.Record($"{sourceCommand} -> {targetCommand}", "Swap", HistoryLogLevel.Info);

        if (!Configuration.ShowSwapMessages || !ShouldShowSwapLine(sourceCommand, targetCommand))
            return;

        Channel.SayAlways($"{sourceCommand} -> {targetCommand}", SwapLineColor, "Swap");
    }

    public static void DebugLine(string message)
    {
        NoireLogger.LogDebug(message, "[SwapTrail] ");
    }

    internal static bool ShouldShowSwapLine(string sourceCommand, string targetCommand)
        => Channel.ShouldShowChange(sourceCommand, targetCommand);

    internal static void ResetSwapLineMemoryForTests() => Channel.ForgetShownChanges();
}
