using Dalamud.Configuration;
using System;

namespace BypassEmote;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool PluginEnabled { get; set; } = false;
    public bool AutoFaceTarget { get; set; } = false;

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
