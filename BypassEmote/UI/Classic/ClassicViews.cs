using NoireLib;
using NoireLib.Changelog;
using NoireLib.HistoryLogger;
using NoireLib.UI;

namespace BypassEmote.UI.Classic;

internal sealed class ClassicMainView : NoireView<MainWindow>
{
    private static readonly ClassicMainPainter Painter = new();

    protected override void Draw(MainWindow target) => Painter.Draw();
}

internal sealed class ClassicSettingsView : NoireView<SettingsWindow>
{
    private static readonly ClassicSettingsPainter Painter = new();

    protected override void Draw(SettingsWindow target) => Painter.Draw(target);
}

internal sealed class ClassicCreateModView : NoireView<CreateModWindow>
{
    private static readonly ClassicCreateModPainter Painter = new();

    protected override void Draw(CreateModWindow target) => Painter.Draw(target);
}

internal sealed class ClassicChangelogView : NoireView<BeChangelogWindow>
{
    private static ChangelogWindow? content;

    protected override void Draw(BeChangelogWindow target)
    {
        if (content == null && NoireLibMain.GetModule<NoireChangelogManager>() is { } manager)
            content = new ChangelogWindow(manager);

        content?.Draw();
    }
}

internal sealed class ClassicLogsView : NoireView<BeLogsWindow>
{
    private static HistoryLoggerWindow? content;

    protected override void Draw(BeLogsWindow target)
    {
        if (content == null && NoireLibMain.GetModule<NoireHistoryLogger>() is { } logger)
            content = new HistoryLoggerWindow(logger);

        content?.Draw();
    }
}
