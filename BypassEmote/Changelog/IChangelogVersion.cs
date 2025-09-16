using BypassEmote.Models;

namespace BypassEmote.Changelog;

/// <summary>
/// Interface for changelog version files
/// </summary>
public interface IChangelogVersion
{
    /// <summary>
    /// Gets the changelog version data
    /// </summary>
    ChangelogVersion GetVersion();
}
