using NoireLib.Configuration;
using System.Collections.Generic;

namespace BypassEmote;

[NoireConfig("Configuration")]
public class ConfigurationInstance : NoireConfigBase
{
    public override string GetConfigFileName() => "Configuration";
    public override int Version { get; set; } = 1;

    [AutoSave]
    public bool PluginEnabled { get; set; } = true;

    [AutoSave]
    public bool AutoFaceTarget { get; set; } = true;

    [AutoSave]
    public bool ShowAllEmotes { get; set; } = false;

    [AutoSave]
    public bool ShowUpdateNotification { get; set; } = true;

    [AutoSave]
    public List<uint> FavoriteEmotes { get; set; } = new List<uint>();

    [AutoSave]
    public bool ShowChangelogOnUpdate { get; set; } = true;

    [AutoSave]
    public bool ShowEmoteIds { get; set; } = false;

    [AutoSave]
    public bool ShowInvalidEmotes { get; set; } = false;

    [AutoSave]
    public bool StopOwnedObjectEmoteOnMove { get; set; } = false;

    [AutoSave]
    public bool BypassOnHotbarSlotTriggered { get; set; } = true;

#if DEBUG
    [AutoSave]
    public int AutoRegister { get; set; } = 0;
#endif
}
