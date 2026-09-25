using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal sealed partial class SilkMainPainter
{
    private bool headerPressPending;
    private Vector2 headerPressAt;

    private bool OverRow(Vector2 point)
    {
        if (point.X < listScreenMin.X || point.X >= listScreenMax.X || point.Y < listScreenMin.Y || point.Y >= listScreenMax.Y)
            return false;

        var rowH = RowHeight * s;
        var index = (int)MathF.Floor((point.Y - rowLeftTop.Y) / rowH);
        return index >= 0 && index < model.View.Count;
    }

    private bool KeepsDock(Vector2 point)
        => dock.MouseInside(point) || OverRow(point) || menu.MouseInside(point) || favAdd.MouseInside(point)
        || blockAdd.MouseInside(point);

    private void TickDockDismiss()
    {
        var osPressed = NoireDismiss.PressedInGame;

        if (!dock.IsOpen)
        {
            headerPressPending = false;
            return;
        }

        var point = ImGui.GetMousePos();
        var pressed = osPressed || ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right);

        if (pressed)
        {
            if (KeepsDock(point))
            {
                headerPressPending = false;
                return;
            }

            if (InHeader(point))
            {
                headerPressPending = true;
                headerPressAt = point;
                return;
            }

            CancelDeferredOpen();
            CloseDock();
            return;
        }

        if (!headerPressPending)
            return;

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            var moved = Vector2.Distance(point, headerPressAt) > 3f * s;
            headerPressPending = false;

            if (!moved)
            {
                CancelDeferredOpen();
                CloseDock();
            }
        }
    }

    private bool InHeader(Vector2 point)
        => point.X >= winMin.X && point.X < winMax.X && point.Y >= winMin.Y && point.Y < winMin.Y + 50f * s;
}
