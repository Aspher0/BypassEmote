using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal sealed class IdLabel
{
    private readonly NoireString text;
    private readonly string id;
    private string cache = string.Empty;
    private int revision = -1;

    public IdLabel(NoireString text, string id)
    {
        this.text = text;
        this.id = id;
    }

    public string Id => id;

    public string Text
    {
        get
        {
            if (revision != NoireLanguages.Revision)
            {
                cache = text.Text + id;
                revision = NoireLanguages.Revision;
            }

            return cache;
        }
    }
}
