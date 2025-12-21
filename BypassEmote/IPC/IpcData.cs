using BypassEmote.Helpers;
using Newtonsoft.Json;
using NoireLib.Helpers;
using System;

namespace BypassEmote.IPC;

[Serializable]
public class IpcData
{
    public uint EmoteId;
    public string? EmoteName
    {
        get
        {
            var name = EmoteHelper.GetEmoteById(EmoteId)?.Name.ExtractText();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
    public bool IsLooping => IsLoopedEmote();

    [JsonConstructor]
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

    public bool IsLoopedEmote()
    {
        var emote = EmoteHelper.GetEmoteById(EmoteId);
        return emote == null ? false : CommonHelper.GetEmotePlayType(emote.Value) == Models.EmotePlayType.Looped;
    }
}
