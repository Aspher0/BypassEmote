using BypassEmote.Helpers;
using Newtonsoft.Json;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;

namespace BypassEmote.Models;

[Serializable]
public class IpcData
{
    public IpcConfiguration Configuration { get; set; }

    public CharacterState PlayerData { get; set; }
    public CharacterState CompanionData { get; set; }
    public CharacterState PetData { get; set; }
    public CharacterState BuddyData { get; set; }


    public void ApplyAll(bool applyOwnedObjects)
    {
        if (PlayerData.CharacterAddress == nint.Zero || !PlayerData.IsPlayerCharacter)
            return;

        PlayerData.ApplyState(true, this);

        if (!applyOwnedObjects)
            return;

        var playerAddress = PlayerData.CharacterAddress;

        if (CompanionData.OwningPlayerAddress == playerAddress)
            CompanionData.ApplyState(true, this);

        if (PetData.OwningPlayerAddress == playerAddress)
            PetData.ApplyState(true, this);

        if (BuddyData.OwningPlayerAddress == playerAddress)
            BuddyData.ApplyState(true, this);
    }

    public IEnumerable<CharacterState> GetCharacterStates(bool includeOwnedObjects = true)
    {
        yield return PlayerData;

        if (!includeOwnedObjects)
            yield break;

        yield return CompanionData;
        yield return PetData;
        yield return BuddyData;
    }

    public bool HasAnyCacheableState()
    {
        foreach (var characterState in GetCharacterStates())
        {
            if (characterState.IsCacheable)
                return true;
        }

        return false;
    }

    public IpcData ToCacheableData()
    {
        return new IpcData(
            Configuration.Clone(),
            PlayerData.ToCacheableState(),
            CompanionData.ToCacheableState(),
            BuddyData.ToCacheableState(),
            PetData.ToCacheableState());
    }

    public bool TrySetCharacterState(nint characterAddress, CharacterState characterState)
    {
        if (PlayerData.CharacterAddress == characterAddress)
        {
            PlayerData = characterState;
            return true;
        }

        if (CompanionData.CharacterAddress == characterAddress)
        {
            CompanionData = characterState;
            return true;
        }

        if (PetData.CharacterAddress == characterAddress)
        {
            PetData = characterState;
            return true;
        }

        if (BuddyData.CharacterAddress == characterAddress)
        {
            BuddyData = characterState;
            return true;
        }

        return false;
    }


    [JsonConstructor]
    public IpcData(IpcConfiguration configuration, CharacterState playerData, CharacterState companionData, CharacterState buddyData, CharacterState petData)
    {
        Configuration = configuration;
        PlayerData = playerData;
        CompanionData = companionData;
        BuddyData = buddyData;
        PetData = petData;
    }

    public IpcData(string json)
    {
        var data = JsonConvert.DeserializeObject<IpcData>(json);

        if (data == null)
            throw new ArgumentException("Invalid JSON for IpcData");

        Configuration = data.Configuration;
        PlayerData = data.PlayerData;
        CompanionData = data.CompanionData;
        BuddyData = data.BuddyData;
        PetData = data.PetData;
    }

    public IpcData()
    {
        Configuration = new IpcConfiguration();

        var localPlayer = NoireService.ObjectTable.LocalPlayer;

        if (localPlayer == null)
        {
            PlayerData = new CharacterState();
            CompanionData = new CharacterState();
            BuddyData = new CharacterState();
            PetData = new CharacterState();
            return;
        }

        var companionAddress = CharacterHelper.GetCompanionAddress(localPlayer);
        var petAddress = CharacterHelper.GetPetAddress(localPlayer);
        var buddyAddress = CharacterHelper.GetBuddyAddress(localPlayer);

        PlayerData = CommonHelper.GetCharacterState(localPlayer.Address);
        CompanionData = CommonHelper.GetCharacterState(companionAddress);
        PetData = CommonHelper.GetCharacterState(petAddress);
        BuddyData = CommonHelper.GetCharacterState(buddyAddress);
    }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
}
