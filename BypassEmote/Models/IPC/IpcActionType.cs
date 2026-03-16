namespace BypassEmote.Models;

public enum IpcActionType : int
{
    /// <summary>
    /// The character started playing a looped or non-looped emote.
    /// </summary>
    StartedEmote = 0,
    /// <summary>
    /// The character stopped playing a looped emote.
    /// </summary>
    StoppedEmote = 1,
    /// <summary>
    /// The configuration of the plugin has changed.
    /// </summary>
    ChangedConfiguration = 2
}
