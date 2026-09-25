using BypassEmote.UI.Skins;
using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed class SilkMainView : NoireView<MainWindow>
{
    private static SilkMainPainter? painter;

    private MainWindow? bound;

    internal static SilkMainPainter Painter => painter ??= new SilkMainPainter();

    protected override void Draw(MainWindow target)
    {
        Bind(target);
        DrawBody(target, target.BodyMin, target.BodyMax);
    }

    public override void Dispose()
    {
        if (bound != null)
        {
            bound.AfterWindow -= DrawOverlays;
            bound.Closed -= OnClose;
            BeSkins.Silk.ChromeOf(bound).AlsoInside = null;
            bound = null;
        }

        base.Dispose();
    }

    internal static void Connect(MainWindow window)
    {
        var view = Painter;
        var backdrop = BeSkins.Silk.ChromeOf(window).Backdrop;

        view.Lean = color => backdrop.Lean(color);
        view.Wave = (x, color) => backdrop.Wave(x, color);
        view.Dock.PaintBackdrop = (list, min, max, radius) =>
        {
            backdrop.Paint(list, min, max, radius, SilkUi.Opacity);
            return true;
        };
        view.Dock.Opacity = static () => SilkUi.Opacity;
        view.Dock.ClickThrough = static () => SilkUi.Settings.ClickThrough;
        view.Dock.MoveGroup = delta =>
        {
            if (!SilkUi.Settings.LockPosition)
                ImGui.SetWindowPos(window.WindowName, window.WindowMin + delta);
        };
    }

    internal static void DisposePainter()
    {
        painter?.Dispose();
        painter = null;
    }

    private void Bind(MainWindow window)
    {
        if (ReferenceEquals(bound, window))
            return;

        bound = window;
        window.AfterWindow += DrawOverlays;
        window.Closed += OnClose;
        BeSkins.Silk.ChromeOf(window).AlsoInside = Painter.Dock.MouseInside;
    }

    private static void DrawBody(MainWindow window, Vector2 min, Vector2 max)
    {
        var view = Painter;

        view.MainFocusTick(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));

        if (view.GroupFrontPending && !SilkUi.Settings.AlwaysOnTop && !SilkMainPainter.PopupBlocking())
            ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());

        view.Collapsed = window.IsCollapsed;
        view.PassingClicks = window.PassingClicks;
        view.Draw(window.WindowMin, window.WindowMax);
    }

    private void DrawOverlays()
    {
        if (!SilkUi.Active || bound == null)
            return;

        SilkUi.Enter(bound);

        try
        {
            Painter.DrawOverlays();
        }
        finally
        {
            SilkUi.Leave();
        }
    }

    private void OnClose() => Painter.CloseDock();
}
