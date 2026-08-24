using BypassEmote.EmoteSwap;
using BypassEmote.Enums;
using BypassEmote.Helpers;
using Dalamud.Bindings.ImGui;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Threading.Tasks;

namespace BypassEmote.UI;

/// <summary> One-time popup to pick a bypass mode. </summary>
public class SwapPromptWindow : IDisposable
{
    private const float DialogWidth = 520f;

    public static bool IsShowing { get; private set; }

    public async Task ShowAsync()
    {
        IsShowing = true;

        int choice;

        try
        {
            choice = await NoireModal.ChoiceAsync("Choose how Bypass Emote plays locked emotes", BuildMessage(), ["Use Emote Swap", "Keep Direct Play"],
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

                    Service.Plugin.OpenSettings();
                    ConfigWindow.SwitchToBypassMode();
                    ConfigWindow.ShowUnsafeToggleAttention();
                    break;

                default:
                    if (Configuration.SwapPromptPending)
                    {
                        ModeSwitcher.Apply(SelfBypassMode.EmoteSwap);
                        Configuration.SwapPromptPending = false;
                        LogHelper.Error("Emote Swap was enabled because no choice was made.");
                    }

                    break;
            }
        });
    }

    private static NoireContent BuildMessage()
    {
        var theme = NoireTheme.Current;

        var ok = ColorHelper.HexToVector4("#009DFF");
        var warning = ColorHelper.HexToVector4("#FF9800");
        var muted = ColorHelper.HexToVector4("#9E9E9E");

        return new NoireContent()
            .AddCustom(() => NoireText.Wrapped(ImGui.GetContentRegionAvail().X, "A safer way to play locked emotes", TextSize.Heading))
            .AddSeparator()
            .AddText("Until now Bypass Emote forced the animation onto your character from your own client. Nothing was ever "
                + "sent to the server, but in specific cases, where your character would be in any pose other than the base one, the "
                + "game client would send duplicate change pose packets to the server. This is not caused by the plugin itself, but rather "
                + "by how the game handles pose changes. The \"Idle Animation Delay\" setting in the game's "
                + "Character Configuration > Control Settings > Character tab is what causes this.")
            .AddNewLine()
            .AddNewLine()
            .AddText("The new mode uses Penumbra to swap locked emotes onto unlocked ones. "
                + "The game itself does the playing, and nothing mismatches between the game and the server anymore.")
            .AddNewLine()
            .AddNewLine()
            .AddText("You can change this at any time in the settings.", muted);
    }

    public void Dispose() { }
}
