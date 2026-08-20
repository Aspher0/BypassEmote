using BypassEmote.Data;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace BypassEmote.Helpers;

public static class CommonHelper
{
    public const ulong NoTargetId = EmoteHelper.NoEmoteTargetId;

    public static ICharacter? GetCharacterFromTrackedCharacter(TrackedCharacter? trackedCharacter)
    {
        if (trackedCharacter == null) return null;

        var gameObject = GameObjectHelper.FindByIdentity(
            trackedCharacter.BaseId, trackedCharacter.ObjectIndex, trackedCharacter.CID);

        return gameObject as ICharacter;
    }

    public static TrackedCharacter? TryGetTrackedCharacterFromAddress(nint charaAddress)
    {
        var castChar = CharacterHelper.GetCharacterFromAddress(charaAddress);
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

        var castChar = CharacterHelper.GetCharacterFromAddress(charaAddress);

        if (castChar == null) return null;

        var existing = TryGetTrackedCharacterFromAddress(charaAddress);

        if (existing != null)
        {
            existing.IsLocalObject = CharacterHelper.IsLocalObject(castChar);

            if (castChar is INpc || castChar is IBattleNpc)
                existing.BaseId = castChar.BaseId;
            else if (castChar is IPlayerCharacter)
            {
                var CID = CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress);
                if (CID == null) return null;
                existing.CID = CID.Value;
            }

            existing.LastPosition = castChar.Position;
            existing.LastRotation = castChar.Rotation;
            existing.UpdatePlayingEmoteId(emote);

            if (receivedIpcData != null)
                existing.ReceivedIpcData = receivedIpcData;

            return existing;
        }
        else
        {
            if (castChar is not INpc && castChar is not IBattleNpc && castChar is not IPlayerCharacter) return null;

            var newTracked = new TrackedCharacter(
                CharacterHelper.IsLocalObject(castChar),
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

    public static bool HasAnyLocalLoopedEmote()
    {
        return EmotePlayer.TrackedCharacters.Any(tc => tc.IsLocalObject);
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

    public static bool IsCharacterInBypassedLoop(ICharacter chara)
    {
        var foundCharacter = TryGetTrackedCharacterFromAddress(chara.Address);
        return foundCharacter != null;
    }

    public static IGameObject? GetLocalTarget() => GameObjectHelper.GetLocalTarget();

    public static ulong GetPlayerTarget(ICharacter chara) => GameObjectHelper.GetTargetId(chara);

    public static ulong TargetIdFor(ICharacter chara, CharacterState? characterState = null)
        => characterState != null
            ? characterState.TargetObject?.GameObjectId ?? NoTargetId
            : GetPlayerTarget(chara);

    public static EmoteController.PlayEmoteOption EmoteOptionFor(ulong targetId)
        => EmoteHelper.EmoteOptionFor(targetId);

    public static EmoteController.PlayEmoteOption LocalPlayerEmoteOption()
        => EmoteOptionFor(NoireService.ObjectTable.LocalPlayer is { } player ? GetPlayerTarget(player) : NoTargetId);

    public static uint ResolveTargetedEmote(ICharacter chara, uint emoteRowId, CharacterState? characterState = null)
        => EmoteHelper.ResolveTargetedEmote(chara, emoteRowId, TargetIdFor(chara, characterState));

    public static void FaceTarget()
    {
        if (NoireService.ObjectTable.LocalPlayer is not ICharacter localCharacter ||
            GetLocalTarget() is not IGameObject targetObject)
            return;

        if (localCharacter.Address == targetObject.Address)
            return;

        CharacterHelper.RotateCharacterSafe(GameObjectHelper.Bearing(localCharacter, targetObject));
    }

    public static bool IsPlayerCharacter(nint characterAddress)
    {
        var castChar = CharacterHelper.GetCharacterFromAddress(characterAddress);
        if (castChar == null) return false;
        return castChar is IPlayerCharacter;
    }

    public static unsafe CharacterState GetCharacterState(nint characterAddress)
    {
        var castChar = CharacterHelper.GetCharacterFromAddress(characterAddress);

        var characterState = new CharacterState();

        if (castChar == null)
            return characterState;

        var native = CharacterHelper.GetCharacterAddress(castChar);

        var baseId = castChar.BaseId;
        var cid = castChar is IPlayerCharacter ? native->ContentId : 0UL;

        characterState = new CharacterState(ExecutedAction.None, CurrentState.Stopped, baseId, cid, 0, 0, 0, 0);

        var trackedCharacter = TryGetTrackedCharacterFromAddress(characterAddress);

        if (trackedCharacter == null)
            return characterState;

        characterState.EmoteId = trackedCharacter.PlayingEmoteId ?? 0;
        characterState.CurrentState = characterState.EmoteId != 0 ? CurrentState.PlayingEmote : CurrentState.Stopped;

        return characterState;
    }

    public static unsafe CharacterState CreateCharacterState(nint characterAddress, ExecutedAction executedAction, CurrentState currentState, uint emoteId)
    {
        var castChar = CharacterHelper.GetCharacterFromAddress(characterAddress);

        if (castChar == null)
            return new CharacterState(executedAction, currentState, 0, 0, emoteId, 0, 0, 0);

        var native = CharacterHelper.GetCharacterAddress(castChar);

        var charTargetId = GetPlayerTarget(castChar);
        var targetObject = GameObjectHelper.FindByGameObjectId(charTargetId);
        var targetCid = 0UL;
        if (targetObject is IPlayerCharacter)
        {
            var targetNative = CharacterHelper.GetCharacterAddress((IPlayerCharacter)targetObject);
            targetCid = targetNative->ContentId;
        }

        var cid = castChar is IPlayerCharacter ? native->ContentId : 0UL;
        return new CharacterState(executedAction, currentState, castChar.BaseId, cid, emoteId, targetObject?.BaseId ?? 0, targetObject?.ObjectIndex ?? 0, targetCid);
    }

    // From ReActionEx: https://github.com/Taurenkey/ReActionEX/blob/master/ReAction/Game.cs
    public static unsafe void AssignEmoteToHotbarSlot(int hotbar, int slot, uint emoteId)
    {
        if (hotbar is < 0 or > 17 || (hotbar < 10 ? slot is < 0 or > 11 : slot is < 0 or > 15)) return;
        Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->SetAndSaveSlot((uint)hotbar, (uint)slot, HotbarSlotType.Emote, emoteId, false, false);
    }

    public static unsafe HotbarSlot* GetHotbarSlot(int hotbar, int slot)
    {
        if (hotbar is < 0 or > 17 || (hotbar < 10 ? slot is < 0 or > 11 : slot is < 0 or > 15)) return null;
        return Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->GetSlotById((uint)hotbar, (uint)slot);
    }

    public static bool IsEmoteAssignableToHotbar(Emote emote)
    {
        var category = EmoteHelper.GetEmoteCategory(emote);
        if (category == NoireLib.Enums.EmoteCategory.Unknown)
            return false;

        if (!IsEmoteDisplayable(emote))
            return false;

        var specification = TryGetEmoteSpecification(emote);
        if (specification == null)
            return true;

        return specification.IsAssignableToHotbar;
    }
}
