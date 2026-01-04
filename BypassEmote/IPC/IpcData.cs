using BypassEmote.Helpers;
using Newtonsoft.Json;
using NoireLib.Helpers;
using System;

namespace BypassEmote.IPC;

[Serializable]
public class IpcData
{
    public ActionType ActionType { get; set; }
    public uint BaseId { get; set; }
    public ulong Cid { get; set; }
    public uint EmoteId { get; set; }
    public bool StopOwnedObjectEmoteOnMove { get; set; }


    [JsonIgnore]
    public nint CharacterAddress => CommonHelper.GetCharacterFromBaseIdOrCid(BaseId, Cid)?.Address ?? nint.Zero;


    public bool IsPlayerCharacter => CommonHelper.IsPlayerCharacter(CharacterAddress);
    public nint OwningPlayerAddress => CommonHelper.GetOwningPlayerAddress(CharacterAddress); // If nint.Zero, it means it's not owned by any player, but it can still be a player or a npc
    public bool IsOwnedObject => OwningPlayerAddress != nint.Zero; // Is it owned by any player? (i.e. is it a companion/pet/etc.)
    public bool IsLocallyOwnedObject => CommonHelper.IsObjectOwnedByLocalPlayer(CharacterAddress); // Is it owned by the local player? (i.e. is it the local player's companion/pet/etc.)
    public string? EmoteName
    {
        get
        {
            var emote = EmoteHelper.GetEmoteById(EmoteId);
            if (emote == null) return null;
            var name = CommonHelper.GetEmoteName(emote.Value);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
    public bool IsLooping => IsLoopedEmote();
    public bool IsStopped => EmoteId == 0;


    [JsonConstructor]
    public IpcData(ActionType ipcActionType, uint baseId, ushort objectIndex, ulong cid, uint emoteId)
    {
        ActionType = ipcActionType;
        BaseId = baseId;
        Cid = cid;
        EmoteId = emoteId;
        StopOwnedObjectEmoteOnMove = Configuration.Instance.StopOwnedObjectEmoteOnMove;

    }

    public IpcData(string json)
    {
        var data = JsonConvert.DeserializeObject<IpcData>(json);

        ActionType = data?.ActionType ?? ActionType.Unknown;
        BaseId = data?.BaseId ?? 0;
        Cid = data?.Cid ?? 0;
        EmoteId = data?.EmoteId ?? 0;
        StopOwnedObjectEmoteOnMove = data?.StopOwnedObjectEmoteOnMove ?? Configuration.Instance.StopOwnedObjectEmoteOnMove;
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

    public bool IsCharacterOrBelongsToIt(nint characterAddress)
    {
        return CharacterAddress == characterAddress || OwningPlayerAddress == characterAddress;
    }

    public void UpdateConfig(IpcData newIpcDataConfig)
    {
        StopOwnedObjectEmoteOnMove = newIpcDataConfig.StopOwnedObjectEmoteOnMove;
    }
}

public enum ActionType : int
{
    Unknown = 0,
    PlayEmote = 1,
    StopEmote = 2,
    ConfigUpdate = 3,
}
