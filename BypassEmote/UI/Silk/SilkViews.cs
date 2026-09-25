using BypassEmote.UI.Silk.Settings;
using BypassEmote.UI.Silk.Windows;
using NoireLib.UI;

namespace BypassEmote.UI.Silk;

internal sealed class SilkSettingsView : NoireView<SettingsWindow>
{
    private static SilkSettingsPainter? painter;

    private SettingsWindow? bound;

    internal static void Connect(SettingsWindow window) => painter = new SilkSettingsPainter(window);

    protected override void Draw(SettingsWindow target)
    {
        if (bound == null)
        {
            bound = target;
            target.Closed += OnClose;
        }

        painter!.DrawBody(target.BodyMin, target.BodyMax);
    }

    protected override void Overlay(SettingsWindow target) => painter!.DrawOverlay(target.WindowMin, target.WindowMax);

    public override void Dispose()
    {
        if (bound != null)
            bound.Closed -= OnClose;

        bound = null;
        base.Dispose();
    }

    private static void OnClose() => painter?.OnClose();
}

internal sealed class SilkCreateModView : NoireView<CreateModWindow>
{
    private static SilkCreateModPainter? painter;

    internal static void Connect(CreateModWindow window) => painter = new SilkCreateModPainter(window);

    protected override void Draw(CreateModWindow target) => painter!.DrawBody(target.BodyMin, target.BodyMax);
}

internal sealed class SilkHotbarView : NoireView<HotbarWindow>
{
    private static SilkHotbarPainter? painter;

    internal static void Connect(HotbarWindow window) => painter = new SilkHotbarPainter(window);

    protected override void Draw(HotbarWindow target) => painter!.DrawBody(target.BodyMin, target.BodyMax);
}

internal sealed class SilkChangelogView : NoireView<BeChangelogWindow>
{
    private static SilkChangelogPainter? painter;

    internal static void Connect(BeChangelogWindow window) => painter = new SilkChangelogPainter(window);

    protected override void Draw(BeChangelogWindow target) => painter!.DrawBody(target.BodyMin, target.BodyMax);
}

internal sealed class SilkLogsView : NoireView<BeLogsWindow>
{
    private static SilkLogsPainter? painter;

    internal static void Connect(BeLogsWindow window) => painter = new SilkLogsPainter(window);

    protected override void Draw(BeLogsWindow target) => painter!.DrawBody(target.BodyMin, target.BodyMax);

    protected override void Overlay(BeLogsWindow target) => painter!.DrawOverlay(target.WindowMin, target.WindowMax);
}
