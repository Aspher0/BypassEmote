using NoireLib;
using System;
using System.Collections.Generic;

namespace BypassEmote.Helpers;

internal static class Log
{
    private static readonly HashSet<string> ReportedOnce = new(StringComparer.Ordinal);

    internal static void Debug(string message, string? prefix = null)
    {
        SessionLog.Write("DBG", Compose(message, prefix));
        NoireLogger.LogDebug(message, prefix);
    }

    internal static void Info(string message, string? prefix = null)
    {
        SessionLog.Write("INF", Compose(message, prefix));
        NoireLogger.LogInfo(message, prefix);
    }

    internal static void Warning(string message, string? prefix = null)
    {
        SessionLog.Write("WRN", Compose(message, prefix));
        NoireLogger.LogWarning(message, prefix);
    }

    internal static void Error(string message, string? prefix = null)
    {
        SessionLog.Write("ERR", Compose(message, prefix));
        NoireLogger.LogError(message, prefix);
    }

    internal static void Error(Exception? ex, string message, string? prefix = null)
    {
        SessionLog.Write("ERR", Compose(message, prefix), ex);
        NoireLogger.LogError(ex, message, prefix);
    }

    internal static bool ErrorOnce(string key, Exception? ex, string message, string? prefix = null)
    {
        lock (ReportedOnce)
        {
            if (!ReportedOnce.Add(key))
                return false;
        }

        Error(ex, message, prefix);
        return true;
    }

    internal static bool ErrorOnce(string key, string message, string? prefix = null)
        => ErrorOnce(key, null, message, prefix);

    private static string Compose(string message, string? prefix)
        => string.IsNullOrEmpty(prefix) ? message : prefix + message;
}
