using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace BypassEmote.UI;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow() : base("Bypass Emote##BypassEmoteConfig", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(250, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        var pluginEnabled = Service.Configuration!.PluginEnabled;
        ImGui.Checkbox("Enable Bypass Emote", ref pluginEnabled);
        Service.Configuration.UpdateConfiguration(() => Service.Configuration.PluginEnabled = pluginEnabled);

        var autoFaceTarget = Service.Configuration!.AutoFaceTarget;
        ImGui.Checkbox("Automatically Face Target On Emote", ref autoFaceTarget);
        Service.Configuration.UpdateConfiguration(() => Service.Configuration.AutoFaceTarget = autoFaceTarget);

        //var interruptEmoteOnRotate = Service.Configuration!.InterruptEmoteOnRotate;
        //ImGui.Checkbox("[WIP] Interrupt Emote On Character Rotation", ref interruptEmoteOnRotate);
        //Service.Configuration.UpdateConfiguration(() => Service.Configuration.InterruptEmoteOnRotate = interruptEmoteOnRotate);
    }

    public void Dispose() { }
}
