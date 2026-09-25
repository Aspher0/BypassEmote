using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal sealed class SplitText
{
    private readonly string placeholder;
    private string before = string.Empty;
    private string after = string.Empty;
    private int revision = -1;

    public SplitText(NoireString text, string name)
    {
        Text = text;
        placeholder = "{" + name + "}";
    }

    public NoireString Text { get; }

    public string Before
    {
        get
        {
            Refresh();
            return before;
        }
    }

    public string After
    {
        get
        {
            Refresh();
            return after;
        }
    }

    public (string Before, string After) Source => Cut(Text.Source);

    private void Refresh()
    {
        if (revision == NoireLanguages.Revision)
            return;

        revision = NoireLanguages.Revision;
        (before, after) = Cut(Text.Text);
    }

    private (string Before, string After) Cut(string text)
    {
        var at = text.IndexOf(placeholder, System.StringComparison.Ordinal);
        return at < 0 ? (text, string.Empty) : (text[..at], text[(at + placeholder.Length)..]);
    }
}
