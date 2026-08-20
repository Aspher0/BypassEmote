using Dalamud.Game.Text;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.HistoryLogger;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.Helpers;


public sealed class Feedback
{
    private readonly string _tag;
    private readonly string _source;
    private readonly string _throttleScope;
    private readonly Dictionary<string, string> _lastShownByKey = new(StringComparer.Ordinal);

    public Feedback(string chatTag, string? sourceName = null, string? throttleScope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatTag);

        _tag = chatTag;
        _source = sourceName ?? chatTag.Trim().Trim('[', ']', ' ');
        _throttleScope = throttleScope ?? _source;
    }

    public void Record(string message, string category, HistoryLogLevel level)
        => NoireLibMain.GetModule<NoireHistoryLogger>()?.AddEntry(message, category, level, _source);

    public void Say(
        string message, Vector3 color, string category, HistoryLogLevel level,
        bool shown, TimeSpan window, string? kind = null, NoireLogger.ChatMessageBuilder? chat = null)
    {
        Record(message, category, level);

        if (!shown)
            return;

        Throttled(window, kind ?? message, () => Print(message, color, chat));
    }

    public void SayAlways(string message, Vector3 color, string category, HistoryLogLevel level = HistoryLogLevel.Info)
    {
        Record(message, category, level);
        Print(message, color);
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

    private void Print(string message, Vector3 color, NoireLogger.ChatMessageBuilder? chat = null)
    {
        chat ??= NoireLogger.CreateChatMessageBuilder().AddText(message, color);

        NoireLogger.PrintToChat(XivChatType.Debug, chat, prefix: _tag);
    }
}
