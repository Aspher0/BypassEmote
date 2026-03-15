using BypassEmote.Helpers;
using Newtonsoft.Json;
using NoireLib;
using NoireLib.Helpers;
using System;

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

        EmotePlayer.ProcessIpcData(this);

        //// TODO
        //// Update every object owned by the player to use new configuration

        //PlayerData.ApplyState(true);

        //if (applyOwnedObjects)
        //{
        //    if (CompanionData.OwningPlayerAddress == PlayerData.CharacterAddress)
        //        CompanionData.ApplyState(true);

        //    if (PetData.OwningPlayerAddress == PlayerData.CharacterAddress)
        //        PetData.ApplyState(true);

        //    if (BuddyData.OwningPlayerAddress == PlayerData.CharacterAddress)
        //        BuddyData.ApplyState(true);
        //}
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
