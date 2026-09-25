using BypassEmote.Localization;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.HistoryLogger;
using System.Numerics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BypassEmote.Tests")]

namespace BypassEmote.Helpers;

public static class LogHelper
{
    public const string ChatTag = "[Bypass Emote] ";

    private static readonly Logger Channel = new(ChatTag, "BypassEmote", "BypassEmote.Feedback");

    internal static readonly Vector3 ErrorColor = ColorHelper.HexToVector3("#E81313");
    internal static readonly Vector3 SuccessColor = ColorHelper.HexToVector3("#4CAF50");
    internal static readonly Vector3 InfoColor = ColorHelper.HexToVector3("#E6E6E6");
    internal static readonly Vector3 WarningColor = ColorHelper.HexToVector3("#FF8C1A");
    internal static readonly Vector3 SwapLineColor = ColorHelper.HexToVector3("#B3B3B3");

    internal static string ThrottleKeyFor(string channelKey, string kind) => channelKey + kind;

    internal static int ErrorCount { get; private set; }

    public static void Error(ChatText message, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
    {
        ErrorCount++;
        Channel.Say(message, ErrorColor, "Error", HistoryLogLevel.Error,
            Configuration.ShowErrorMessages, Configuration.ThrottleTimeErrors,
            kind == null ? null : ThrottleKeyFor("Error", kind), chat);
    }

    public static void Notice(ChatText message, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.Say(message, WarningColor, "Warning", HistoryLogLevel.Warning,
            Configuration.ShowWarningMessages, Configuration.ThrottleTimeWarnings,
            kind == null ? null : ThrottleKeyFor("Warning", kind), chat);

    public static void NoticeAlways(ChatText message, NoireLogger.ChatMessageBuilder? chat = null)
        => Channel.SayAlways(message, WarningColor, "Warning", HistoryLogLevel.Warning, chat);

    public static void Success(ChatText message)
        => Channel.SayAlways(message, SuccessColor, "Info");

    public static void Info(ChatText message)
        => Channel.SayAlways(message, InfoColor, "Info");

    public static void SwapLine(string sourceCommand, string targetCommand)
        => SwapLine(sourceCommand, ChatText.Plain(targetCommand));

    public static void SwapLine(string sourceCommand, ChatText target)
    {
        var line = new ChatText($"{sourceCommand} -> {target.Display}", $"{sourceCommand} -> {target.Record}");

        if (!Configuration.ShowSwapMessages || !ShouldShowSwapLine(sourceCommand, target.Record))
        {
            Channel.Record(line, "Swap", HistoryLogLevel.Info);
            return;
        }

        Channel.SayAlways(line, SwapLineColor, "Swap");
    }

    public static void DebugLine(string message)
    {
        Log.Debug(message, "[SwapTrail] ");
    }

    internal static bool ShouldShowSwapLine(string sourceCommand, string targetCommand)
        => Channel.ShouldShowChange(sourceCommand, targetCommand);

    internal static void ResetSwapLineMemoryForTests() => Channel.ForgetShownChanges();
}
