using NoireLib.Localizer;

namespace BypassEmote.Localization;

internal sealed class TextList
{
    private readonly NoireString[] texts;
    private string[] cache = [];
    private int revision = -1;
    private string[]? sources;

    public TextList(params NoireString[] texts) => this.texts = texts;

    public int Length => texts.Length;

    public string this[int index] => Array[index];

    public string Source(int index) => texts[index].Source;

    public string[] Sources => sources ??= System.Array.ConvertAll(texts, text => text.Source);

    public string[] Array
    {
        get
        {
            if (revision == NoireLanguages.Revision && cache.Length == texts.Length)
                return cache;

            var built = new string[texts.Length];

            for (var i = 0; i < texts.Length; i++)
                built[i] = texts[i].Text;

            cache = built;
            revision = NoireLanguages.Revision;
            return cache;
        }
    }
}
