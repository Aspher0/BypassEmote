namespace BypassEmote.Enums;

internal enum MapperSourceOutcome
{
    // A guarded read refused, or the descent ran deeper than a real tree can be.
    Unreadable,

    // The animation belongs to this skeleton, so nothing needs retargeting.
    Native,

    Missing,

    Found,
}
