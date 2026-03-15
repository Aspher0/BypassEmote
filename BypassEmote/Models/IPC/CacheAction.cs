namespace BypassEmote.Models;

public enum CacheAction : int
{
    /// <summary>
    /// Indicated you should update your cache with the provided data.
    /// </summary>
    ShouldAddOrUpdateCache = 0,
    /// <summary>
    /// You can safely remove the cached data as no local object is bypassing any emote anymore.
    /// </summary>
    ShouldRemoveFromCache = 1
}
