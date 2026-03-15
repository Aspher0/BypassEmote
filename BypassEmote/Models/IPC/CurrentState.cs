namespace BypassEmote.Models;

public enum CurrentState : int
{
    /// <summary>
    /// Unknown state
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Currently playing a looped emote
    /// </summary>
    PlayingEmote = 1,
    /// <summary>
    /// Is not bypassing a looped emote
    /// </summary>
    Stopped = 2,
}
