using Newtonsoft.Json;
using System;

namespace BypassEmote.IPC;

[Serializable]
public class IpcData
{
    public uint EmoteId;

    public IpcData(uint emoteId)
    {
        EmoteId = emoteId;
    }

    public IpcData(string json)
    {
        var data = JsonConvert.DeserializeObject<IpcData>(json);
        EmoteId = data?.EmoteId ?? 0;
    }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
}
