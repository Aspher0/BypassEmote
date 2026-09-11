using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BypassEmote.Helpers;

internal static class SessionLog
{
    private const int MaxEntries = 120_000;

    private const long MaxCharacters = 16L * 1024 * 1024;

    private const string StampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    private readonly record struct Entry(DateTimeOffset At, string Level, string Text);

    private static readonly object Gate = new();

    private static readonly Queue<Entry> Entries = new();

    private static long _characters;

    private static int _dropped;

    private static int _written;

    internal static DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.Now;

    internal static void Start(string openingLine)
    {
        lock (Gate)
        {
            Entries.Clear();
            _characters = 0;
            _dropped = 0;
            _written = 0;
            StartedAt = DateTimeOffset.Now;
        }

        Write("INF", openingLine);
    }

    internal static void Write(string level, string text)
    {
        if (text == null)
            return;

        var entry = new Entry(DateTimeOffset.Now, level, text);

        lock (Gate)
        {
            Entries.Enqueue(entry);
            _characters += text.Length + 48;
            _written++;

            while (Entries.Count > MaxEntries || _characters > MaxCharacters)
            {
                if (Entries.Count <= 1)
                    break;

                _characters -= Entries.Dequeue().Text.Length + 48;
                _dropped++;
            }
        }
    }

    internal static void Write(string level, string text, Exception? exception)
    {
        if (exception == null)
        {
            Write(level, text);
            return;
        }

        Write(level, text + Environment.NewLine + exception);
    }

    internal readonly record struct Dump(int Kept, int Dropped, int Total);

    internal static Dump WriteTo(string path)
    {
        Entry[] snapshot;
        int dropped;
        int total;
        DateTimeOffset startedAt;

        lock (Gate)
        {
            snapshot = [.. Entries];
            dropped = _dropped;
            total = _written;
            startedAt = StartedAt;
        }

        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

        writer.WriteLine($"BypassEmote session log, opened {startedAt.ToString(StampFormat, CultureInfo.InvariantCulture)}");
        writer.WriteLine($"{total} line(s) recorded, {dropped} dropped, {snapshot.Length} kept below.");
        writer.WriteLine();

        if (dropped > 0)
            writer.WriteLine($"... the first {dropped} line(s) of this session were dropped to keep this file small ...");

        foreach (var entry in snapshot)
        {
            writer.WriteLine(
                $"{entry.At.ToString(StampFormat, CultureInfo.InvariantCulture)} [{entry.Level}] {entry.Text}");
        }

        return new Dump(snapshot.Length, dropped, total);
    }
}
