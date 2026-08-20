using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BypassEmote.EmoteSwap;

/// <summary>
/// A directory of generated files named after their content hash, plus the sweep that removes the ones nothing
/// refers to any more. The game caches loaded resources by path, so a new name is the only way to make it reload;
/// a name that already exists already holds the right bytes and is left untouched.
/// </summary>
public sealed class ContentAddressedStore
{
    private const string LogPrefix = "[ContentStore] ";

    private readonly string _prefix;
    private readonly int _tagLength;

    /// <summary>
    /// Creates a store whose files share a name prefix, which the sweep uses to tell its own files from anything
    /// else in the same directory.
    /// </summary>
    /// <param name="fileNamePrefix">The prefix every generated name starts with, such as "swap_".</param>
    /// <param name="contentTagLength">How many hex characters of the content hash the name carries.</param>
    /// <param name="searchPattern">
    /// What the sweep enumerates, defaulting to everything under the prefix. Narrow it when the directory holds a
    /// file sharing the prefix that must never be swept.
    /// </param>
    public ContentAddressedStore(string fileNamePrefix, int contentTagLength = 8, string? searchPattern = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentTagLength);

        _prefix = fileNamePrefix;
        _tagLength = contentTagLength;
        SearchPattern = searchPattern ?? $"{fileNamePrefix}*";
    }

    /// <summary>The pattern the sweep enumerates.</summary>
    public string SearchPattern { get; }

    /// <summary>The name a piece of content gets, which is the same for the same bytes.</summary>
    /// <param name="bytes">The content.</param>
    /// <param name="extension">The extension to append, including its dot.</param>
    /// <returns>The generated file name.</returns>
    public string NameFor(byte[] bytes, string extension = "")
        => $"{_prefix}{EncryptionHelper.ShortTag(bytes, _tagLength)}{extension}";

    /// <summary>
    /// Writes content into the store unless a file of that name is already there, which already holds these bytes.
    /// </summary>
    /// <param name="directory">The store's directory.</param>
    /// <param name="bytes">The content.</param>
    /// <param name="extension">The extension to append, including its dot.</param>
    /// <param name="fileName">The name the content has, whether it was written now or was already there.</param>
    /// <returns>True when the content is in the store afterwards.</returns>
    public bool Write(string directory, byte[] bytes, string extension, out string fileName)
    {
        fileName = NameFor(bytes, extension);

        return WriteAt(Path.Combine(directory, fileName), bytes);
    }

    /// <summary>
    /// Writes content to an exact path unless a file is already there, for a caller deciding the layout itself.
    /// The file name must still come from <see cref="NameFor"/>, since the skip relies on the name naming the bytes.
    /// </summary>
    /// <param name="filePath">Where the content goes.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>True when the content is at that path afterwards.</returns>
    public bool WriteAt(string filePath, byte[] bytes)
    {
        if (File.Exists(filePath))
            return true;

        if (FileHelper.ReplaceFileAtomically(filePath, bytes))
            return true;

        NoireLogger.LogError($"Failed to write '{filePath}'.", LogPrefix);
        return false;
    }

    /// <summary>
    /// Removes every file of this store's that nothing in <paramref name="referenced"/> names. Files that do
    /// not carry the store's prefix are never touched, so a store can share a directory with other content.
    /// </summary>
    /// <param name="directory">The store's directory.</param>
    /// <param name="referenced">The names still in use, as bare names or paths; only the file name part is compared.</param>
    /// <returns>How many files were removed.</returns>
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
            NoireLogger.LogError(ex, $"Failed to list files under '{directory}' for cleanup.", LogPrefix);
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
                NoireLogger.LogError(ex, $"Failed to delete stale file '{path}'.", LogPrefix);
            }
        }

        return removed;
    }
}
