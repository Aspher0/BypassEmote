using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace BypassEmote;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = Service.ConfigVersion;

    public bool PluginEnabled { get; set; } = true;
    public bool AutoFaceTarget { get; set; } = true;
    public bool ShowAllEmotes { get; set; } = false;
    public bool ShowUpdateNotification { get; set; } = true;
    public List<uint> FavoriteEmotes { get; set; } = new List<uint>();
    public bool ShowChangelogOnUpdate { get; set; } = true;
    public bool ShowEmoteIds { get; set; } = false;
    public bool ShowInvalidEmotes { get; set; } = false;

    public void UpdateConfiguration(Action updateAction)
    {
        updateAction();
        Save();
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
