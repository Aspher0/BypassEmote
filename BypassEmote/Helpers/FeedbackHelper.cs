using NoireLib;
using NoireLib.Feedback;
using NoireLib.Helpers;
using NoireLib.HistoryLogger;
using System.Numerics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BypassEmote.Tests")]

namespace BypassEmote.Helpers;

public static class FeedbackHelper
{
    public const string ChatTag = "[BypassEmote] ";

    private static readonly NoireFeedback Channel = new(ChatTag, "BypassEmote", "BypassEmote.Feedback");

    private static readonly Vector3 ErrorColor = ColorHelper.HexToVector3("#E81313");
    private static readonly Vector3 InfoColor = ColorHelper.HexToVector3("#E6E6E6");
    private static readonly Vector3 WarningColor = ColorHelper.HexToVector3("#FF8C1A");
    private static readonly Vector3 SwapLineColor = ColorHelper.HexToVector3("#B3B3B3");

    private const string WarningCategory = "Warning";
    private const string ErrorCategory = "Error";
    private const string SwapCategory = "Swap";
    private const string InfoCategory = "Info";

    private const string ErrorChannel = "Error";
    private const string WarningChannel = "Warning";

    internal static string ThrottleKeyFor(string channelKey, string kind) => channelKey + kind;

    public static void Error(string message, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.Say(message, ErrorColor, ErrorCategory, HistoryLogLevel.Error,
            Configuration.ShowErrorMessages, Configuration.ThrottleTimeErrors,
            kind == null ? null : ThrottleKeyFor(ErrorChannel, kind), chat);

    public static void Notice(string message, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.Say(message, WarningColor, WarningCategory, HistoryLogLevel.Warning,
            Configuration.ShowWarningMessages, Configuration.ThrottleTimeWarnings,
            kind == null ? null : ThrottleKeyFor(WarningChannel, kind), chat);

    public static void Info(string message)
        => Channel.SayAlways(message, InfoColor, InfoCategory);

    public static void SwapLine(string sourceCommand, string targetCommand)
    {
        Channel.Record($"{sourceCommand} -> {targetCommand}", SwapCategory, HistoryLogLevel.Info);

        if (!Configuration.ShowSwapMessages || !ShouldShowSwapLine(sourceCommand, targetCommand))
            return;

        Channel.SayAlways($"{sourceCommand} -> {targetCommand}", SwapLineColor, SwapCategory);
    }

    public static void DebugLine(string message)
    {
        NoireLogger.LogWarning(message, "[SwapTrail] ");
    }

    internal static bool ShouldShowSwapLine(string sourceCommand, string targetCommand)
        => Channel.ShouldShowChange(sourceCommand, targetCommand);

    internal static void ResetSwapLineMemoryForTests() => Channel.ForgetShownChanges();
}
