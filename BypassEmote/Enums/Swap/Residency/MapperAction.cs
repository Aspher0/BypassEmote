namespace BypassEmote.Enums;

internal enum MapperAction
{
    KeepGameAnswer,

    // Hand back the game's answer and stop correcting for the rest of the session.
    Disarm,

    InstallNone,
    InstallFound,
}
