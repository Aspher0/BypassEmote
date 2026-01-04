using BypassEmote.Data;
using BypassEmote.IPC;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Linq;
using System.Text;

namespace BypassEmote.Helpers;

public static class CommonHelper
{
    public static ICharacter? TryGetCharacterFromTrackedCharacter(TrackedCharacter? trackedCharacter)
    {
        if (trackedCharacter == null) return null;

        return GetCharacterFromBaseIdAndObjectIndexOrCid(trackedCharacter.BaseId, trackedCharacter.ObjectIndex, trackedCharacter.CID);
    }

    public static ICharacter? GetCharacterFromBaseIdAndObjectIndexOrCid(uint? baseId, ushort? objectIndex, ulong? cid)
    {
        if (cid != null)
            return CharacterHelper.TryGetCharacterFromCID(cid.Value);
        else if (baseId != null && objectIndex != null)
            return TryGetCharacterFromBaseIdAndObjectIndex(baseId.Value, objectIndex.Value);

        return null;
    }

    public static ICharacter? GetCharacterFromBaseIdOrCid(uint baseId, ulong cid)
    {
        if (cid != 0)
            return CharacterHelper.TryGetCharacterFromCID(cid);
        else if (baseId != 0)
            return TryGetCharacterFromBaseId(baseId);

        return null;
    }

    public static TrackedCharacter? TryGetTrackedCharacterFromAddress(nint charaAddress)
    {
        var castChar = CharacterHelper.TryGetCharacterFromAddress(charaAddress);
        if (castChar == null) return null;

        if (castChar is INpc || castChar is IBattleNpc)
            return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.BaseId == castChar.BaseId);
        else if (castChar is IPlayerCharacter)
            return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.CID == CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress));
        else
            return null;
    }

    public static unsafe TrackedCharacter? AddOrUpdateCharacterInTrackedList(nint charaAddress, Emote emote, IpcData? receivedIpcData = null)
    {
        if (charaAddress == nint.Zero) return null;

        var castChar = CharacterHelper.TryGetCharacterFromAddress(charaAddress);

        if (castChar == null) return null;

        var existing = TryGetTrackedCharacterFromAddress(charaAddress);

        if (existing != null)
        {
            existing.IsLocalObject = IsLocalObject(castChar);

            if (castChar is INpc || castChar is IBattleNpc)
                existing.BaseId = castChar.BaseId;
            else if (castChar is IPlayerCharacter)
            {
                var CID = CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress);
                if (CID == null) return null;
                existing.CID = CID.Value;
            }

            existing.LastPlayerPosition = castChar.Position;
            existing.LastPlayerRotation = castChar.Rotation;
            existing.UpdatePlayingEmoteId(emote);

            if (receivedIpcData != null)
                existing.ReceivedIpcData = receivedIpcData;

            return existing;
        }
        else
        {
            if (castChar is not INpc && castChar is not IBattleNpc && castChar is not IPlayerCharacter) return null;

            var newTracked = new TrackedCharacter(
                IsLocalObject(castChar),
                (castChar is IPlayerCharacter ? CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress) : null),
                (castChar is INpc || castChar is IBattleNpc ? castChar.BaseId : null),
                (castChar is INpc || castChar is IBattleNpc ? castChar.ObjectIndex : null),
                castChar.Position,
                castChar.Rotation,
                CharacterHelper.IsCharacterWeaponDrawn(charaAddress),
                emote.RowId,
                receivedIpcData
            );
            EmotePlayer.TrackedCharacters.Add(newTracked);
            return newTracked;
        }
    }

    public static ICharacter? TryGetCharacterFromBaseIdAndObjectIndex(uint baseId, ushort objectIndex)
    {
        return (from o in NoireService.ObjectTable
                where o is ICharacter
                select o as ICharacter).FirstOrDefault(p => p != null && p.BaseId == baseId && p.ObjectIndex == objectIndex);
    }

    public static ICharacter? TryGetCharacterFromBaseId(uint baseId)
    {
        return (from o in NoireService.ObjectTable
                where o is ICharacter
                select o as ICharacter).FirstOrDefault(p => p != null && p.BaseId == baseId);
    }

    public static void RemoveCharacterFromTrackedListByUniqueID(string uniqueId)
    {
        EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.UniqueId == uniqueId);
    }

    public static bool IsEmoteDisplayable(Emote emote)
    {
        if (TryGetEmoteSpecification(emote) != null)
            return true;

        return !emote.Name.ToString().IsNullOrWhitespace();
    }

    public static string GetEmoteName(Emote emote)
    {
        var name = "";

        var specification = TryGetEmoteSpecification(emote);

        if (specification == null)
        {
            return emote.Name.ToString().IsNullOrWhitespace() ?
                (emote.TextCommand.ValueNullable?.Command.ExtractText() ?? $"No name")
                : name + $"{emote.Name.ToString()}";
        }

        if (specification.Name != null)
            name += specification.Name;
        else
            name += GetRealEmoteNameById(emote.RowId);

        return name;
    }

    public static uint GetEmoteIcon(Emote emote)
    {
        var specification = TryGetEmoteSpecification(emote);

        if (specification == null || specification.Icon == null)
            return emote.Icon;

        return specification.Icon.Value;
    }

    public static string GetRealEmoteNameById(uint emoteId)
    {
        var foundEmote = EmoteHelper.GetEmoteById(emoteId);
        return foundEmote?.Name.ToString() ?? $"No name";
    }

    public static uint? GetRealEmoteIconById(uint emoteId)
    {
        var foundEmote = EmoteHelper.GetEmoteById(emoteId);
        return foundEmote?.Icon ?? null;
    }

    public static EmotePlayType GetEmotePlayType(Emote emote)
    {
        var emoteSpecification = TryGetEmoteSpecification(emote);
        if (emoteSpecification != null) return emoteSpecification.PlayType;

        return (EmoteHelper.GetEmoteCategory(emote) == NoireLib.Enums.EmoteCategory.Special) ? EmotePlayType.Looped : EmotePlayType.OneShot;
    }

    public static EmoteSpecification? TryGetEmoteSpecification(Emote emote)
    {
        foreach (var specification in EmoteData.EmoteSpecifications)
        {
            if (specification.Matches(emote.RowId))
                return specification;
        }

        return null;
    }

    public static unsafe ReadOnlySpan<byte> GetUtf8Span(Utf8String* s) => s == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(s->StringPtr, (int)s->Length);

    public static string Utf8StringToPlainText(SeString se)
    {
        var sb = new StringBuilder();
        foreach (var p in se.Payloads)
        {
            switch (p)
            {
                case TextPayload t:
                    sb.Append(t.Text);
                    break;
                case AutoTranslatePayload a:
                    sb.Append(NoireService.SeStringEvaluator.Evaluate(new ReadOnlySeString(a.Encode()), default, NoireService.ClientState.ClientLanguage).ToString());
                    break;
            }
        }
        return sb.ToString();
    }

    public static float GetRotationToTarget(ICharacter from, IGameObject to)
    {
        return ECommons.MathHelpers.MathHelper.GetAngleBetweenPoints(new(from.Position.Z, from.Position.X), new(to.Position.Z, to.Position.X));
    }

    public unsafe static nint GetCompanionAddress(ICharacter ownerCharacter)
    {
        var native = CharacterHelper.GetCharacterAddress(ownerCharacter);
        return (nint)native->CompanionData.CompanionObject;
    }

    public unsafe static nint GetPetAddress(ICharacter ownerCharacter)
    {
        var native = CharacterHelper.GetCharacterAddress(ownerCharacter);
        var manager = CharacterManager.Instance();
        return (nint)manager->LookupPetByOwnerObject((BattleChara*)native);
    }

    public unsafe static nint GetBuddyAddress(ICharacter ownerCharacter)
    {
        var native = CharacterHelper.GetCharacterAddress(ownerCharacter);
        var manager = CharacterManager.Instance();
        return (nint)manager->LookupBuddyByOwnerObject((BattleChara*)native);
    }

    /// <summary>
    /// Determines whether the given object address belongs to the local player but *ISN'T* the local player.
    /// </summary>
    public unsafe static bool IsObjectOwnedByLocalPlayer(nint objectAddress)
    {
        var local = NoireService.ObjectTable.LocalPlayer;
        if (local == null)
            return false;
        return objectAddress == GetCompanionAddress(local) || objectAddress == GetPetAddress(local) || objectAddress == GetBuddyAddress(local);
    }

    public static bool IsLocalObject(ICharacter chara)
    {
        var localPlayer = NoireService.ObjectTable.LocalPlayer;

        if (localPlayer == null)
            return false;

        var playerAddress = localPlayer.Address;
        var companionAddress = GetCompanionAddress(localPlayer);
        var petAddress = GetPetAddress(localPlayer);
        var buddyAddress = GetBuddyAddress(localPlayer);

        return playerAddress == chara.Address ||
               companionAddress == chara.Address ||
               petAddress == chara.Address ||
               buddyAddress == chara.Address;
    }

    public static bool IsCharacterInBypassedLoop(ICharacter chara)
    {
        var foundCharacter = TryGetTrackedCharacterFromAddress(chara.Address);
        return foundCharacter != null;
    }

    public unsafe static void FaceTarget()
    {
        if (NoireService.ObjectTable.LocalPlayer is not ICharacter localCharacter ||
            NoireService.TargetManager.Target is not IGameObject targetObject)
            return;

        if (localCharacter.Address == targetObject.Address)
            return;

        if (CharacterHelper.IsCharacterChairSitting(localCharacter) ||
            CharacterHelper.IsCharacterGroundSitting(localCharacter) ||
            CharacterHelper.IsCharacterSleeping(localCharacter))
            return;

        var rotToTarget = GetRotationToTarget(localCharacter, targetObject);

        var character = CharacterHelper.GetCharacterAddress(localCharacter);
        character->SetRotation(rotToTarget);
    }

    public static unsafe nint GetOwningPlayerAddress(nint characterAddress)
    {
        var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
        if (castChar == null) return nint.Zero;
        uint ownerEntityId;

        if (castChar.ObjectKind == ObjectKind.Companion)
        {
            // Minion
            var native = CharacterHelper.GetCharacterAddress(castChar);
            ownerEntityId = native->CompanionOwnerId;
        }
        else if (castChar.SubKind == 2 || castChar.SubKind == 3)
        {
            // Pet or buddy
            ownerEntityId = castChar.OwnerId;
        }
        else
        {
            return nint.Zero;
        }

        var foundOwner = NoireService.ObjectTable.PlayerObjects.FirstOrDefault(p => p.EntityId == ownerEntityId);
        if (foundOwner == null) return nint.Zero;
        return foundOwner.Address;
    }

    internal static bool IsPlayerCharacter(nint characterAddress)
    {
        var castChar = CharacterHelper.TryGetCharacterFromAddress(characterAddress);
        if (castChar == null) return false;
        return castChar is IPlayerCharacter;
    }
}
