using NoireLib.Configuration;
using System;
using System.Collections.Generic;

namespace BypassEmote;

[Serializable]
public class Configuration : NoireConfigBase<Configuration>
{
    public override string GetConfigFileName() => "Configuration";
    public override int Version { get; set; } = 1;

    [AutoSave] public virtual bool PluginEnabled { get; set; } = true;
    [AutoSave] public virtual bool AutoFaceTarget { get; set; } = true;
    [AutoSave] public virtual bool ShowAllEmotes { get; set; } = false;
    [AutoSave] public virtual bool ShowUpdateNotification { get; set; } = true;
    [AutoSave] public virtual List<uint> FavoriteEmotes { get; set; } = new List<uint>();
    [AutoSave] public virtual bool ShowChangelogOnUpdate { get; set; } = true;
    [AutoSave] public virtual bool ShowEmoteIds { get; set; } = false;
    [AutoSave] public virtual bool ShowInvalidEmotes { get; set; } = false;
    [AutoSave] public virtual bool StopCompanionEmoteOnCompanionMove { get; set; } = false;
}
