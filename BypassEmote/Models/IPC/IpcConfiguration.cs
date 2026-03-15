using Newtonsoft.Json;
using System;

namespace BypassEmote.Models;

[Serializable]
public class IpcConfiguration
{
    public bool StopOwnedObjectEmoteOnMove { get; set; }

    [JsonConstructor]
    public IpcConfiguration(bool stopOwnedObjectEmoteOnMove)
    {
        StopOwnedObjectEmoteOnMove = stopOwnedObjectEmoteOnMove;
    }

    public IpcConfiguration()
    {
        StopOwnedObjectEmoteOnMove = Configuration.StopOwnedObjectEmoteOnMove;
    }

    public IpcConfiguration(string json)
    {
        var data = JsonConvert.DeserializeObject<IpcConfiguration>(json);

        if (data == null)
            throw new ArgumentException("Invalid JSON for IpcConfiguration");

        StopOwnedObjectEmoteOnMove = data.StopOwnedObjectEmoteOnMove;
    }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }

    public IpcConfiguration Clone()
    {
        return new IpcConfiguration(StopOwnedObjectEmoteOnMove);
    }
}
