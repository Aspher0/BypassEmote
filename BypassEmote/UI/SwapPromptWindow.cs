using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.Models;
using NoireLib.Helpers;
using NoireLib.UI;
using System;
using System.Threading.Tasks;

namespace BypassEmote.UI;

/// <summary> One-time popup to pick a bypass mode. </summary>
public class SwapPromptWindow : IDisposable
{
    private const string Title = "Choose how BypassEmote plays locked emotes";

    /// <summary> Dismissing without picking (Escape, clicking away, plugin unload) falls back to Emote Swap and says so in chat. </summary>
    public async Task ShowAsync()
    {
        var choice = await NoireModal.ChoiceAsync(Title, BuildMessage(), ["Use Emote Swap", "Keep Direct Play"]);

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
        var pro = ColorHelper.HexToVector4("#2FCC39");
        var con = theme.Resolve(ThemeColor.TextMuted);
        var orange = ColorHelper.HexToVector4("#FFA500");

        return new NoireContent()
            .AddText("A new update is available for BypassEmote, and it comes with a major change.")
            .AddNewLine()
            .AddText("BypassEmote can now use Penumbra to redirect locked emotes onto emotes you own. That is the 100% safe way to bypass emotes.")
            .AddNewLine()
            .AddNewLine()
            .AddText("- Emote Swap (new, recommended)", orange)
            .AddNewLine()
            .AddText("+ Completely Safe", pro)
            .AddNewLine()
            .AddText("+ Works over any sync service", pro)
            .AddNewLine()
            .AddText("- Needs Penumbra installed", con)
            .AddNewLine()
            .AddNewLine()
            .AddText("- Direct Play (the old method)", orange)
            .AddNewLine()
            .AddText("+ No Penumbra needed", pro)
            .AddNewLine()
            .AddText("- Not 100% safe, but the risk is negligible.", con)
            .AddNewLine()
            .AddText("- Not all sync services support it", con)
            .AddNewLine()
            .AddNewLine()
            .AddText("You can change this anytime in settings.");
    }

    public void Dispose() { }
}
