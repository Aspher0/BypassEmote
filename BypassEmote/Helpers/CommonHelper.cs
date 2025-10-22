using BypassEmote.Data;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using ECommons.MathHelpers;
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

        if (trackedCharacter.CID != null)
            return CharacterHelper.TryGetCharacterFromCID(trackedCharacter.CID.Value);
        else if (trackedCharacter.BaseId != null)
            return CharacterHelper.TryGetCharacterFromBaseId(trackedCharacter.BaseId.Value);

        return null;
    }

    public static TrackedCharacter? TryGetTrackedCharacterFromAddress(nint charaAddress)
    {
        var castChar = CharacterHelper.TryGetCharacterFromAddress(charaAddress);
        if (castChar == null) return null;

        if (castChar is INpc)
            return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.BaseId == castChar.BaseId);
        else if (castChar is IPlayerCharacter)
            return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.CID == CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress));
        else
            return null;
    }

    public static TrackedCharacter? AddOrUpdateCharacterInTrackedList(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return null;

        var castChar = CharacterHelper.TryGetCharacterFromAddress(charaAddress);

        if (castChar == null) return null;

        var existing = TryGetTrackedCharacterFromAddress(charaAddress);

        if (existing != null)
        {
            if (castChar is INpc)
                existing.BaseId = castChar.BaseId;
            else if (castChar is IPlayerCharacter)
            {
                var CID = CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress);
                if (CID == null) return null;
                existing.CID = CID.Value;
            }

            existing.LastPlayerPosition = castChar.Position;
            existing.LastPlayerRotation = castChar.Rotation;
            return existing;
        }
        else
        {
            if (castChar is not INpc && castChar is not IPlayerCharacter) return null;

            var newTracked = new TrackedCharacter(
                (castChar is IPlayerCharacter ? CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress) : null),
                (castChar is INpc ? castChar.BaseId : null),
                castChar.Position,
                castChar.Rotation,
                CharacterHelper.IsCharacterWeaponDrawn(charaAddress)
            );
            EmotePlayer.TrackedCharacters.Add(newTracked);
            return newTracked;
        }
    }

    public static void RemoveCharacterFromTrackedListByAddress(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return;

        var castChar = CharacterHelper.TryGetCharacterFromAddress(charaAddress);

        if (castChar is INpc)
            EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.BaseId == castChar.BaseId);
        else if (castChar is IPlayerCharacter)
        {
            var CID = CharacterHelper.GetCIDFromPlayerCharacterAddress(charaAddress);
            if (CID == null) return;
            EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.CID == CID);
        }
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

        if (specification.Value.Name != null)
            name += specification.Value.Name;
        else
            name += GetRealEmoteNameById(emote.RowId);

        return name;
    }

    public static ushort GetEmoteIcon(Emote emote)
    {
        var specification = TryGetEmoteSpecification(emote);

        if (specification == null || specification.Value.Icon == null)
            return emote.Icon;

        return specification.Value.Icon.Value;
    }

    public static string GetRealEmoteNameById(uint emoteId)
    {
        var foundEmote = EmoteHelper.GetEmoteById(emoteId);
        return foundEmote?.Name.ToString() ?? $"No name";
    }

    public static ushort? GetRealEmoteIconById(uint emoteId)
    {
        var foundEmote = EmoteHelper.GetEmoteById(emoteId);
        return foundEmote?.Icon ?? null;
    }

    public static EmoteData.EmotePlayType GetEmotePlayType(Emote emote)
    {
        var emoteSpecification = TryGetEmoteSpecification(emote);
        if (emoteSpecification != null) return emoteSpecification.Value.PlayType;

        return (EmoteHelper.GetEmoteCategory(emote) == NoireLib.Enums.EmoteCategory.Special) ? EmoteData.EmotePlayType.Looped : EmoteData.EmotePlayType.OneShot;
    }

    public static (object Object, EmoteData.EmotePlayType PlayType, string? Name, ushort? Icon)? TryGetEmoteSpecification(Emote emote)
    {
        foreach (var specification in EmoteData.EmoteSpecifications)
        {
            var emoteId = specification.Key;

            // If emoteId is uint, and its value == emote.RowId then return it
            if (emoteId is uint singleId && singleId == emote.RowId)
                return (emoteId, specification.Value.PlayType, specification.Value.Name, specification.Value.Icon);

            // If emoteId is Tuple and emote.RowId is between Tuple item 1 and item 2 both included then return it
            if (emoteId is Tuple<uint, uint> range && emote.RowId >= range.Item1 && emote.RowId <= range.Item2)
                return (emoteId, specification.Value.PlayType, specification.Value.Name, specification.Value.Icon);
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

    public static float GetRotationToTarget(ICharacter from, ICharacter to)
    {
        return MathHelper.GetAngleBetweenPoints(new(from.Position.Z, from.Position.X), new(to.Position.Z, to.Position.X));
    }
}
