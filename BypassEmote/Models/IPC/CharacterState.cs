using BypassEmote.Helpers;
using Newtonsoft.Json;
using NoireLib.Helpers;
using System;

namespace BypassEmote.Models;

[Serializable]
public class CharacterState
{
    public ExecutedAction ExecutedAction { get; set; }
    public CurrentState CurrentState { get; set; }
    public uint BaseId { get; set; }
    public ulong Cid { get; set; }
    public uint EmoteId { get; set; }


    [JsonIgnore]
    public nint CharacterAddress => CommonHelper.GetCharacterFromBaseIdOrCid(BaseId, Cid)?.Address ?? nint.Zero;

    [JsonIgnore]
    public bool IsPlayerCharacter => CommonHelper.IsPlayerCharacter(CharacterAddress);

    [JsonIgnore]
    public nint OwningPlayerAddress => CommonHelper.GetOwningPlayerAddress(CharacterAddress); // If nint.Zero, it means it's not owned by any player, but it can still be a player or a npc

    [JsonIgnore]
    public bool IsOwnedObject => OwningPlayerAddress != nint.Zero; // Is it owned by any player? (i.e. is it a companion/pet/etc.)

    [JsonIgnore]
    public bool IsLocallyOwnedObject => CharacterHelper.IsCharacterOwnedByLocalPlayer(CharacterAddress); // Is it owned by the local player? (i.e. is it the local player's companion/pet/etc.)

    [JsonIgnore]
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

    [JsonIgnore]
    public bool IsLooping => IsLoopedEmote();

    [JsonIgnore]
    public bool IsStopped => EmoteId == 0;

    [JsonIgnore]
    public bool IsCacheable => CurrentState == CurrentState.PlayingEmote && EmoteId != 0 && IsLooping;

    public bool IsLoopedEmote()
    {
        var emote = EmoteHelper.GetEmoteById(EmoteId);
        return emote == null ? false : CommonHelper.GetEmotePlayType(emote.Value) == Models.EmotePlayType.Looped;
    }

    public bool IsCharacterOrBelongsToIt(nint characterAddress)
    {
        return CharacterAddress == characterAddress || OwningPlayerAddress == characterAddress;
    }

    public CharacterState Clone()
    {
        return new CharacterState(ExecutedAction, CurrentState, BaseId, Cid, EmoteId);
    }

    public CharacterState ToStoppedState(ExecutedAction executedAction = ExecutedAction.None)
    {
        return new CharacterState(executedAction, CurrentState.Stopped, BaseId, Cid, 0);
    }

    public CharacterState ToCacheableState()
    {
        return IsCacheable
            ? new CharacterState(ExecutedAction.None, CurrentState.PlayingEmote, BaseId, Cid, EmoteId)
            : ToStoppedState();
    }

    public void ApplyState(bool shouldThrow = false, IpcData? ipcData = null)
    {
        if (CharacterAddress == nint.Zero)
            return;

        var castChar = CharacterHelper.GetCharacterFromAddress(CharacterAddress);

        if (castChar == null)
            return;

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(CharacterAddress);

        switch (CurrentState)
        {
            case CurrentState.PlayingEmote:
                {
                    if (trackedCharacter == null)
                    {
                        // Character is not tracked yet, start emoting
                        EmotePlayer.ProcessCharacterState(castChar, this, ipcData);
                        break;
                    }

                    // From this point onwards, the character is already tracked,
                    // we just need to check the ExecutedAction

                    if (ExecutedAction == ExecutedAction.StartedEmote)
                    {
                        // Restart the emote
                        EmotePlayer.ProcessCharacterState(castChar, this, ipcData);
                        break;
                    }
                    else if (ExecutedAction == ExecutedAction.StoppedEmote)
                    {
                        // Inconsistency
                        if (shouldThrow)
                            throw new InvalidOperationException("Inconsistent state in CharacterState: Character's CurrentState is PlayingEmote but ExecutedAction is StoppedEmote");

                        break;
                    }

                    // If ExecutedAction is None, do nothing

                    break;
                }
            case CurrentState.Stopped:
                {
                    if (trackedCharacter != null)
                    {
                        // Character is tracked, stop emoting
                        EmotePlayer.ProcessCharacterState(castChar, this, ipcData);
                        break;
                    }

                    // From this point onwards, the character is not tracked yet

                    if (ExecutedAction == ExecutedAction.StartedEmote)
                    {
                        // Check if it's an inconsitency
                        if (EmoteId == 0)
                        {
                            // Inconsitency
                            if (shouldThrow)
                                throw new InvalidOperationException("Inconsistent state in CharacterState: Character's CurrentState is Stopped but ExecutedAction is StartedEmote with EmoteId 0");

                            break;
                        }

                        // Otherwise, it's just that the character started an emote that is not looping, we play the emote
                        EmotePlayer.ProcessCharacterState(castChar, this, ipcData);
                        break;
                    }

                    // If ExecutedAction is StoppedEmote or None, do nothing

                    break;
                }
            case CurrentState.Unknown:
            default:
                {
                    if (shouldThrow)
                        throw new InvalidOperationException("Unknown CurrentState in CharacterState");

                    break;
                }
        }
    }

    [JsonConstructor]
    public CharacterState(ExecutedAction executedAction, CurrentState currentState, uint baseId, ulong cid, uint emoteId)
    {
        ExecutedAction = executedAction;
        CurrentState = currentState;
        BaseId = baseId;
        Cid = cid;
        EmoteId = emoteId;
    }

    public CharacterState(string json)
    {
        var data = JsonConvert.DeserializeObject<CharacterState>(json);

        if (data == null)
            throw new ArgumentException("Invalid JSON for CharacterState");

        ExecutedAction = data.ExecutedAction;
        CurrentState = data.CurrentState;
        BaseId = data.BaseId;
        Cid = data.Cid;
        EmoteId = data.EmoteId;
    }

    public CharacterState()
    {
        ExecutedAction = ExecutedAction.None;
        CurrentState = CurrentState.Stopped;
        BaseId = 0;
        Cid = 0;
        EmoteId = 0;
    }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
}
