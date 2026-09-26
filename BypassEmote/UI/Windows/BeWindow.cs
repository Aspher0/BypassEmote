using BypassEmote.UI.Classic;
using BypassEmote.UI.Skins;
using NoireLib.Localizer;
using NoireLib.UI;
using System;

namespace BypassEmote.UI;

internal abstract class BeWindow : NoireSkinnedWindow
{
    protected BeWindow(string id, NoireString title, NoireString subtitle)
        : base(id, title)
    {
        Subtitle = subtitle;
        OptionsKey = "BypassEmote";
    }

    internal event Action? Closed;

    internal event Action? AfterWindow;

#if DEBUG
    public override bool DrawConditions()
    {
        using var profile = NoireUI.Profiler.Measure("BeWindow.DrawConditions");
        return base.DrawConditions();
    }
#endif

    public override void OnClose()
    {
        base.OnClose();
        Closed?.Invoke();
    }

#if DEBUG
    public override void PreDraw()
    {
        using var profile = NoireUI.Profiler.Measure("BeWindow.PreDraw");
        base.PreDraw();
    }
#endif

    public override void PostDraw()
    {
#if DEBUG
        using var profile = NoireUI.Profiler.Measure("BeWindow.PostDraw");
#endif

        base.PostDraw();
        AfterWindow?.Invoke();
    }

    protected override void SetUpNativeWindow() => ClassicSkin.SetUpNative(this);

    protected override string WindowNameFor(NoireSkin skin)
    {
        var id = "###BypassEmoteSilk" + Id["BypassEmote".Length..];

        if (ReferenceEquals(skin, BeSkins.Silk))
            return "Bypass Emote - " + Subtitle?.Text + id;

        return this switch
        {
            BeChangelogWindow => ClassicSkin.ChangelogTitle + id,
            BeLogsWindow => ClassicSkin.LogsTitle + id,
            _ => Title.Text + id,
        };
    }
}
