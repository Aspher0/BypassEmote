using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

public sealed class ContentAddressedStore
{
    private const string LogPrefix = "[ContentStore] ";

    private readonly string _prefix;
    private readonly int _tagLength;

    public ContentAddressedStore(string fileNamePrefix, int contentTagLength = 8, string? searchPattern = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentTagLength);

        _prefix = fileNamePrefix;
        _tagLength = contentTagLength;
        SearchPattern = searchPattern ?? $"{fileNamePrefix}*";
    }

    public string SearchPattern { get; }

    public string NameFor(byte[] bytes, string extension = "")
        => $"{_prefix}{EncryptionHelper.ShortTag(bytes, _tagLength)}{extension}";

    public bool Write(string directory, byte[] bytes, string extension, out string fileName)
    {
        fileName = NameFor(bytes, extension);

        return WriteAt(Path.Combine(directory, fileName), bytes);
    }

    public bool WriteAt(string filePath, byte[] bytes)
    {
        if (File.Exists(filePath))
            return true;

        if (FileHelper.ReplaceFileAtomically(filePath, bytes))
            return true;

        Log.Error($"Failed to write '{filePath}'.", LogPrefix);
        return false;
    }

    public int RemoveUnreferenced(string directory, IEnumerable<string> referenced)
    {
        if (!Directory.Exists(directory))
            return 0;

        var keep = new HashSet<string>(
            referenced?.Select(entry => Path.GetFileName(entry) ?? entry) ?? [],
            StringComparer.OrdinalIgnoreCase);

        List<string> present;

        try
        {
            present = Directory.EnumerateFiles(directory, SearchPattern).ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to list files under '{directory}' for cleanup.", LogPrefix);
            return 0;
        }

        var removed = 0;

        foreach (var path in present)
        {
            if (keep.Contains(Path.GetFileName(path)))
                continue;

            try
            {
                File.Delete(path);
                removed++;
            }
            catch (Exception ex)
            {
                // One undeletable file does not stop the rest of the sweep.
                Log.Error(ex, $"Failed to delete stale file '{path}'.", LogPrefix);
            }
        }

        return removed;
    }
}
