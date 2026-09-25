using NoireLib.Localizer;
using System.Collections.Generic;

namespace BypassEmote.Localization;

public readonly record struct ChatText(string Display, string Record)
{
    public static readonly ChatText Empty = new(string.Empty, string.Empty);

    public bool IsEmpty => Record.Length == 0 && Display.Length == 0;

    public static ChatText Plain(string text) => new(text, text);

    public static implicit operator ChatText(NoireString text) => From(new NoireMessage(text));

    public static ChatText Of(NoireString text, string name, string value)
        => From(new NoireMessage(text, name, value));

    public static ChatText Of(NoireString text, string name1, string value1, string name2, string value2)
        => From(new NoireMessage(text, name1, value1, name2, value2));

    public static ChatText Of(NoireString text, string name1, string value1, string name2, string value2, string name3, string value3)
    {
        var message = Of(text, name1, value1, name2, value2);
        var placeholder = "{" + name3 + "}";
        return new(message.Display.Replace(placeholder, value3), message.Record.Replace(placeholder, value3));
    }

    public static ChatText Of(NoireString text, string name, ChatText value)
        => new(new NoireMessage(text, name, value.Display).Display, new NoireMessage(text, name, value.Record).Record);

    public static ChatText Of(NoireString text, string name1, ChatText value1, string name2, string value2)
        => new(new NoireMessage(text, name1, value1.Display, name2, value2).Display,
            new NoireMessage(text, name1, value1.Record, name2, value2).Record);

    public static ChatText Join(string separator, IEnumerable<ChatText> parts)
    {
        var display = new List<string>();
        var record = new List<string>();

        foreach (var part in parts)
        {
            display.Add(part.Display);
            record.Add(part.Record);
        }

        return new(string.Join(separator, display), string.Join(separator, record));
    }

    public override string ToString() => Record;

    private static ChatText From(NoireMessage message) => new(message.Display, message.Record);
}
