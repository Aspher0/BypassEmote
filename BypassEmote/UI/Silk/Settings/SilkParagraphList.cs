using NoireLib.Localizer;

namespace BypassEmote.UI.Silk.Settings;

internal sealed class SilkParagraphList
{
    private readonly (NoireString Text, SilkParagraphTone Tone)[] paragraphs;
    private SilkParagraph[] cache = [];
    private int revision = -1;

    public SilkParagraphList(params (NoireString Text, SilkParagraphTone Tone)[] paragraphs) => this.paragraphs = paragraphs;

    public SilkParagraph[] Array
    {
        get
        {
            if (revision == NoireLanguages.Revision && cache.Length == paragraphs.Length)
                return cache;

            var built = new SilkParagraph[paragraphs.Length];

            for (var i = 0; i < paragraphs.Length; i++)
                built[i] = new SilkParagraph(paragraphs[i].Text.Text, paragraphs[i].Tone);

            cache = built;
            revision = NoireLanguages.Revision;
            return cache;
        }
    }
}
