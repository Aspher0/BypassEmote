using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.UI;
using System.Threading.Tasks;

namespace BypassEmote.UI;

internal sealed class SwapPrompt
{
    private const float DialogWidth = 520f;

    public static bool IsShowing { get; private set; }

    public async Task ShowAsync()
    {
        IsShowing = true;

        int choice;

        try
        {
            choice = await NoireModal.ChoiceAsync(L.PromptTitle.Text, BuildMessage(), [L.PromptUseEmoteSwap.Text, L.PromptKeepDirectPlay.Text],
                new ModalOptions { Width = DialogWidth });
        }
        finally
        {
            IsShowing = false;
        }

        if (!NoireService.IsInitialized())
            return;

        await AsyncHelper.RunOnFrameworkThreadAsync(() =>
        {
            if (!NoireService.IsInitialized())
                return;

            switch (choice)
            {
                case 0:
                    ModeSwitcher.Apply(SelfBypassMode.EmoteSwap);
                    Configuration.SwapPromptPending = false;
                    break;

                case 1:
                    ModeSwitcher.Apply(SelfBypassMode.DirectPlay);
                    Configuration.SwapPromptPending = false;

                    Service.Plugin.OpenBypassModeWithUnsafeAttention();
                    break;

                default:
                    if (Configuration.SwapPromptPending)
                    {
                        ModeSwitcher.Apply(SelfBypassMode.EmoteSwap);
                        Configuration.SwapPromptPending = false;
                        LogHelper.Error(L.NoEmoteChoiceMade);
                    }

                    break;
            }
        });
    }

    private static NoireContent BuildMessage()
    {
        var muted = ColorHelper.HexToVector4("#9E9E9E");

        return new NoireContent()
            .AddCustom(() => NoireText.Wrapped(ImGui.GetContentRegionAvail().X, L.PromptHeading.Text, TextSize.Heading))
            .AddSeparator()
            .AddText(L.PromptBefore.Text)
            .AddNewLine()
            .AddNewLine()
            .AddText(L.PromptNow.Text)
            .AddNewLine()
            .AddNewLine()
            .AddText(L.PromptChangeLater.Text, muted);
    }
}
