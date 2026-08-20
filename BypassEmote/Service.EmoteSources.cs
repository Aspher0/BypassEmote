using BypassEmote.Helpers;
using Dalamud.Utility;
using NoireLib;
using NoireLib.Helpers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BypassEmote;

public partial class Service
{
    // Dictionary: Emote RowId -> (patch, List of (source type, source text)) from ffxivcollect
    public static Dictionary<uint, (string? Patch, List<(string Type, string Text)> Sources)> EmoteSources { get; } = new();

    private static async Task FetchAndBuildEmoteSourcesAsync(CancellationToken token)
    {
        var entries = await FfxivCollectHelper.GetEmotesAsync(token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return;

        if (entries.Count == 0)
        {
            NoireLogger.LogWarning<Service>("FFXIVCollect emotes API returned no results.");
            return;
        }

        lock (EmoteSources)
            EmoteSources.Clear();

        foreach (var entry in entries)
        {
            var matched = false;

            foreach (var command in entry.Commands())
            {
                if (EmoteHelper.GetEmoteByCommand(command) is { } emote)
                {
                    MergeEmoteSources(emote.RowId, entry);
                    matched = true;
                }
            }

            if (!matched && entry.Id.HasValue && EmoteHelper.GetEmoteById((uint)entry.Id.Value) is { } byId)
                MergeEmoteSources(byId.RowId, entry);
        }

        NoireLogger.LogInfo<Service>($"Built EmoteSources for {EmoteSources.Count} emotes from FFXIVCollect.");
    }

    private static void MergeEmoteSources(uint rowId, FfxivCollectEntry entry)
    {
        var entries = new HashSet<(string Type, string Text)>();

        if (entry.Sources != null)
        {
            foreach (var source in entry.Sources)
            {
                if (!source.Text.IsNullOrWhitespace())
                {
                    var type = source.Type.IsNullOrWhitespace() ? "Unknown" : source.Type!;
                    entries.Add((type, source.Text!));
                }
            }
        }

        if (entries.Count == 0 && entry.Patch.IsNullOrWhitespace())
            return;

        lock (EmoteSources)
        {
            if (!EmoteSources.TryGetValue(rowId, out var existing))
            {
                existing = (entry.Patch, new List<(string Type, string Text)>());
                EmoteSources[rowId] = existing;
            }
            else if (existing.Patch.IsNullOrWhitespace() && !entry.Patch.IsNullOrWhitespace())
            {
                existing.Patch = entry.Patch;
                EmoteSources[rowId] = existing;
            }

            foreach (var source in entries)
            {
                if (!existing.Sources.Contains(source))
                    existing.Sources.Add(source);
            }
        }
    }
}
