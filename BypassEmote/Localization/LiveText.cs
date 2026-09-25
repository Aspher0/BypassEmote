using NoireLib.Localizer;

namespace BypassEmote.Localization;

public sealed class LiveText
{
    private readonly NoireString text;
    private readonly string? name1;
    private readonly string? value1;
    private readonly string? name2;
    private readonly string? value2;
    private readonly string? name3;
    private readonly string? value3;
    private string display = string.Empty;
    private int revision = -1;

    public LiveText(NoireString text, string? name1 = null, string? value1 = null, string? name2 = null, string? value2 = null,
        string? name3 = null, string? value3 = null)
    {
        this.text = text;
        this.name1 = name1;
        this.value1 = value1;
        this.name2 = name2;
        this.value2 = value2;
        this.name3 = name3;
        this.value3 = value3;
    }

    public string Display
    {
        get
        {
            if (revision != NoireLanguages.Revision)
            {
                revision = NoireLanguages.Revision;
                display = Chat.Display;
            }

            return display;
        }
    }

    public string Record => Chat.Record;

    public ChatText Chat => name3 != null
        ? ChatText.Of(text, name1!, value1!, name2!, value2!, name3, value3!)
        : name2 != null
            ? ChatText.Of(text, name1!, value1!, name2, value2!)
            : name1 != null ? ChatText.Of(text, name1, value1!) : text;

    public override string ToString() => Record;
}
