using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace BypassEmote.UI;

/// <summary> One-time popup to pick a bypass mode. </summary>
public class SwapPromptWindow : IDisposable
{
    private const string Title = "Choose how Bypass Emote plays locked emotes";

    private const string Headline = "A safer way to play locked emotes";

    private const string WhyItChanged =
        "Until now Bypass Emote forced the animation onto your character from your own client. Nothing was ever "
        + "sent to the server, but in specific cases, where your character would be in any pose other than the base one, the "
        + "game client would send duplicate change pose packets to the server. This is not caused by the plugin itself, but rather "
        + "by how the game handles pose changes. The \"Idle Animation Delay\" setting in the game's "
        + "Character Configuration > Control Settings > Character tab is what causes this.";

    private const string WhatItDoesNow =
        "The new mode uses Prenumbra to swap locked emotes onto unlocked ones. "
        + "The game itself does the playing, and nothing mismatches between the game and the server anymore.";

    private const string Footer = "You can change this at any time in the settings.";

    private const float PointIndent = 10f;
    private const float MarkGap = 6f;

    private const float DialogWidth = 520f;

    public const string WaitingForAChoiceMessage =
        "Bypass Emote is waiting for you to choose how it should play locked emotes.";

    public const string WaitingForAChoiceKind = "prompt.pending";

    public static bool IsShowing { get; private set; }

    public async Task ShowAsync()
    {
        IsShowing = true;

        int choice;

        try
        {
            choice = await NoireModal.ChoiceAsync(Title, BuildMessage(), ["Use Emote Swap", "Keep Direct Play"],
                new ModalOptions { Width = DialogWidth });
        }
        finally
        {
            IsShowing = false;
        }

        await AsyncHelper.RunOnFrameworkThreadAsync(() =>
        {
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
                        FeedbackHelper.Error("Emote Swap was enabled because no choice was made.");
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
            .AddCustom(() => NoireText.Wrapped(ImGui.GetContentRegionAvail().X, Headline, TextSize.Heading))
            .AddSeparator()
            .AddText(WhyItChanged)
            .AddNewLine()
            .AddNewLine()
            .AddText(WhatItDoesNow)
            .AddNewLine()
            .AddNewLine()
            .AddText(Footer, muted);
    }

    private static void Mode(
        NoireContent content, FontAwesomeIcon icon, Vector4 color, string name, string aside,
        Vector4 asideColor)
    {
        content
            .AddIcon(icon, color)
            .AddSpacing(MarkGap)
            .AddText(name, color)
            .AddSpacing(MarkGap * 2f)
            .AddText(aside, asideColor)
            .AddNewLine();
    }

    private static void Point(
        NoireContent content, bool isPro, Vector4 favorColor,
        Vector4 againstColor, string text)
    {
        content
            .AddSpacing(PointIndent)
            .AddIcon(isPro ? FontAwesomeIcon.Plus : FontAwesomeIcon.Minus, isPro ? favorColor : againstColor)
            .AddSpacing(MarkGap)
            .AddText(text, isPro ? null : againstColor)
            .AddNewLine();
    }

    public void Dispose() { }
}
