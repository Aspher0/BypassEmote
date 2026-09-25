using BypassEmote.Localization;
using Dalamud.Game.Text;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.HistoryLogger;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.Helpers;

public sealed class Logger
{
    private readonly string _tag;
    private readonly string _source;
    private readonly string _throttleScope;
    private readonly Dictionary<string, string> _lastShownByKey = new(StringComparer.Ordinal);

    public Logger(string chatTag, string? sourceName = null, string? throttleScope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatTag);

        _tag = chatTag;
        _source = sourceName ?? chatTag.Trim().Trim('[', ']', ' ');
        _throttleScope = throttleScope ?? _source;
    }

    public void Record(ChatText message, string category, HistoryLogLevel level)
        => Record(message, category, level, null);

    private void Record(ChatText message, string category, HistoryLogLevel level, string? note)
    {
        SessionLog.Write(LevelTag(level),
            $"{_source} chat {category}: {message.Record}" + (note == null ? string.Empty : $" [{note}]"));

        NoireLibMain.GetModule<NoireHistoryLogger>()?.AddEntry(message.Record, category, level, _source);
    }

    private static string LevelTag(HistoryLogLevel level) => level switch
    {
        HistoryLogLevel.Error => "ERR",
        HistoryLogLevel.Warning => "WRN",
        _ => "INF",
    };

    public void Say(
        ChatText message, Vector3 color, string category, HistoryLogLevel level,
        bool shown, TimeSpan window, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
    {
        Record(message, category, level, shown ? null : "hidden by the chat settings");

        if (!shown)
            return;

        Throttled(window, kind ?? message.Record, () => Print(message, color, chat));
    }

    public void SayAlways(ChatText message, Vector3 color, string category, HistoryLogLevel level = HistoryLogLevel.Info,
        NoireLogger.ChatMessageBuilder? chat = null)
    {
        Record(message, category, level, null);
        Print(message, color, chat);
    }

    public bool ShouldShowChange(string key, string value)
    {
        lock (_lastShownByKey)
        {
            if (_lastShownByKey.TryGetValue(key, out var previous) && previous == value)
                return false;

            _lastShownByKey[key] = value;
            return true;
        }
    }

    public void ForgetShownChanges()
    {
        lock (_lastShownByKey)
            _lastShownByKey.Clear();
    }

    private void Throttled(TimeSpan window, string kind, Action print)
    {
        if (window <= TimeSpan.Zero)
        {
            print();
            return;
        }

        ThrottleHelper.Throttle($"{_throttleScope}.{kind}", window, print);
    }

    private void Print(ChatText message, Vector3 color, NoireLogger.ChatMessageBuilder? chat = null)
    {
        chat ??= NoireLogger.CreateChatMessageBuilder().AddText(message.Display, color);

        NoireLogger.PrintToChat(XivChatType.Debug, chat, prefix: _tag);
    }
}
