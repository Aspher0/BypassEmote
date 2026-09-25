using Dalamud.Bindings.ImGui;
using NoireLib.UI;
using System;
using System.Numerics;

namespace BypassEmote.UI.Silk.Main;

internal static class SilkMainIcons
{
    internal static readonly SilkIconShape Star = SilkIcons.Get(SilkIcon.Star);
    internal static readonly SilkIconShape Ban = SilkIcons.Define(16f, SilkIcons.Circle(8f, 8f, 5.6f, 1.9f), SilkIcons.Path("M4 12l8-8", 1.9f, false, false));
    internal static readonly SilkIconShape BanThin = SilkIcons.Get(SilkIcon.Block);
    internal static readonly SilkIconShape Info = SilkIcons.Define(16f, SilkIcons.Circle(8f, 8f, 6f, 1.5f), SilkIcons.Path("M8 7.2v4M8 4.8v.2", 1.5f));
    internal static readonly SilkIconShape All = SilkIcons.Define(16f,
        SilkIcons.Rect(2f, 2f, 5f, 5f, 1.3f, 0f, true), SilkIcons.Rect(9f, 2f, 5f, 5f, 1.3f, 0f, true),
        SilkIcons.Rect(2f, 9f, 5f, 5f, 1.3f, 0f, true), SilkIcons.Rect(9f, 9f, 5f, 5f, 1.3f, 0f, true));
    internal static readonly SilkIconShape General = SilkIcons.Define(16f, SilkIcons.Circle(8f, 5f, 2.6f, 1.6f),
        SilkIcons.Path("M2.8 14C2.8 11 5.1 9 8 9C10.9 9 13.2 11 13.2 14", 1.6f));
    internal static readonly SilkIconShape Special = SilkIcons.Define(16f,
        SilkIcons.Path("M8 1.5l1.4 4.1L13.5 7l-4.1 1.4L8 12.5 6.6 8.4 2.5 7l4.1-1.4z", 0f, true), SilkIcons.Circle(13f, 12.5f, 1.3f, 0f, true));
    internal static readonly SilkIconShape Expressions = SilkIcons.Define(16f, SilkIcons.Circle(8f, 8f, 6f, 1.5f),
        SilkIcons.Path("M5.5 9.6c1.3 1.5 3.7 1.5 5 0", 1.5f), SilkIcons.Path("M6 6.4v.3M10 6.4v.3", 2f));
    internal static readonly SilkIconShape Other = SilkIcons.Define(16f, SilkIcons.Circle(3.5f, 8f, 1.4f, 0f, true),
        SilkIcons.Circle(8f, 8f, 1.4f, 0f, true), SilkIcons.Circle(12.5f, 8f, 1.4f, 0f, true));
    internal static readonly SilkIconShape OwnedCheck = SilkIcons.Stroked(12f, 1.9f, true, "M3 6.2l2 2 4-4.4");
    internal static readonly SilkIconShape Arrow = SilkIcons.Stroked(16f, 1.8f, true, "M13 8H3M6.5 4.5L3 8l3.5 3.5");
    internal static readonly SilkIconShape ArrowThin = SilkIcons.Get(SilkIcon.ArrowLeft);
    internal static readonly SilkIconShape Close = SilkIcons.Get(SilkIcon.Close);
    internal static readonly SilkIconShape Play = SilkIcons.Get(SilkIcon.Play);
    internal static readonly SilkIconShape Swap = SilkIcons.Get(SilkIcon.Swap);
    internal static readonly SilkIconShape Hotbar = SilkIcons.Get(SilkIcon.Hotbar);
    internal static readonly SilkIconShape Overrides = SilkIcons.Get(SilkIcon.Overrides);
    internal static readonly SilkIconShape Cube = SilkIcons.Get(SilkIcon.Cube);
    internal static readonly SilkIconShape Sync = SilkIcons.Get(SilkIcon.Sync);
    internal static readonly SilkIconShape Refresh = SilkIcons.Get(SilkIcon.Refresh);
    internal static readonly SilkIconShape Search = SilkIcons.Get(SilkIcon.Search);
    internal static readonly SilkIconShape Check = SilkIcons.Get(SilkIcon.Check);
    internal static readonly SilkIconShape BlockDock = SilkIcons.Define(16f, SilkIcons.Circle(8f, 8f, 5.6f, 1.7f), SilkIcons.Path("M4 12l8-8", 1.7f, false, false));

}
