namespace BypassEmote.Models;

public enum ExecutedAction : int
{
    /// <summary>
    /// Unchanged
    /// </summary>
    None = 0,
    /// <summary>
    /// Started playing any emote, including looped and non-looped ones.
    /// </summary>
    StartedEmote = 1,
    /// <summary>
    /// Stopped a looped emote.
    /// </summary>
    StoppedEmote = 2,
}
