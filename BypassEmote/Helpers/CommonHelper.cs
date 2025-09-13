using BypassEmote.Data;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Linq;
using System.Text;

namespace BypassEmote.Helpers;

public static class CommonHelper
{
    public static unsafe Character* GetCharacter(ICharacter chara) => (Character*)chara.Address;

    public static ICharacter? TryGetCharacterFromAddress(nint charaAddress)
    {
        if (charaAddress == nint.Zero)
            return null;

        return Service.Objects.FirstOrDefault(p => p is ICharacter && p.Address == charaAddress) as ICharacter;
    }

    public static ICharacter? TryGetCharacterFromTrackedCharacter(TrackedCharacter? trackedCharacter)
    {
        if (trackedCharacter == null) return null;

        if (trackedCharacter.CID != null)
            return TryGetCharacterFromCID(trackedCharacter.CID.Value);
        else if (trackedCharacter.DataId != null)
            return TryGetCharacterFromDataId(trackedCharacter.DataId.Value);

        return null;
    }

    public static TrackedCharacter? TryGetTrackedCharacterFromAddress(nint charaAddress)
    {
        var castChar = TryGetCharacterFromAddress(charaAddress);
        if (castChar == null) return null;

        if (castChar is INpc)
            return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.DataId == castChar.DataId);
        else if (castChar is IPlayerCharacter)
            return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.CID == GetCIDFromPlayerPointer(charaAddress));
        else
            return null;
    }

    public static TrackedCharacter? AddOrUpdateCharacterInTrackedList(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return null;

        var castChar = TryGetCharacterFromAddress(charaAddress);

        if (castChar == null) return null;

        var existing = TryGetTrackedCharacterFromAddress(charaAddress);

        if (existing != null)
        {
            if (castChar is INpc)
                existing.DataId = castChar.DataId;
            else if (castChar is IPlayerCharacter)
            {
                var CID = GetCIDFromPlayerPointer(charaAddress);
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

            var newTracked = new TrackedCharacter((castChar is IPlayerCharacter ? GetCIDFromPlayerPointer(charaAddress) : null), (castChar is INpc ? castChar.DataId : null), castChar.Position, castChar.Rotation, IsCharacterWeaponDrawn(charaAddress));
            EmotePlayer.TrackedCharacters.Add(newTracked);
            return newTracked;
        }
    }

    public unsafe static ulong? GetCIDFromPlayerPointer(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return null;

        var castChar = TryGetCharacterFromAddress(charaAddress);

        if (castChar is not IPlayerCharacter) return null;

        var castBattleChara = (BattleChara*)castChar.Address;
        return castBattleChara->Character.ContentId;
    }

    public static ICharacter? TryGetCharacterFromCID(ulong cid)
    {
        return Service.Objects.PlayerObjects
            .Where(o => o is IPlayerCharacter)
            .Select(o => o as ICharacter)
            .FirstOrDefault(p => p != null && GetCIDFromPlayerPointer(p.Address) == cid);
    }

    public static ICharacter? TryGetCharacterFromDataId(uint dataId)
    {
        return Service.Objects
            .Where(o => o is ICharacter)
            .Select(o => o as ICharacter)
            .FirstOrDefault(p => p != null && p.DataId == dataId);
    }

    public static void RemoveCharacterFromTrackedListByAddress(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return;

        var castChar = TryGetCharacterFromAddress(charaAddress);

        if (castChar is INpc)
            EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.DataId == castChar.DataId);
        else if (castChar is IPlayerCharacter)
        {
            var CID = GetCIDFromPlayerPointer(charaAddress);
            if (CID == null) return;
            EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.CID == CID);
        }
    }

    public static void RemoveChracterFromTrackedListByUniqueID(string uniqueId)
    {
        EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.UniqueId == uniqueId);
    }

    public static string GetEmoteName(Emote emote)
    {
        var name = "";

#if DEBUG
        name = $"[{emote.RowId}] ";
#endif

        var specification = TryGetEmoteSpecification(emote);

        if (specification == null)
        {
            return string.IsNullOrWhiteSpace(emote.Name.ToString()) ?
                (emote.TextCommand.ValueNullable?.Command.ExtractText() ?? $"[{emote.RowId}] No name")
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
        var foundEmote = TryGetEmoteById(emoteId);
        return foundEmote?.Name.ToString() ?? $"[{emoteId}] No name";
    }

    public static ushort? GetRealEmoteIconById(uint emoteId)
    {
        var foundEmote = TryGetEmoteById(emoteId);
        return foundEmote?.Icon ?? null;
    }

    public static Emote? TryGetEmoteById(uint emoteId)
    {
        var foundEmote = LinqExtensions.FirstOrNull(Service.Emotes, e => e.RowId == emoteId);
        return foundEmote ?? null;
    }

    public static EmoteData.EmoteCategory GetEmoteCategory(Emote emote)
    {
        var categoryString = GetEmoteCategoryString(emote);

        switch (categoryString)
        {
            case "General":
                return EmoteData.EmoteCategory.General;
            case "Special":
                return EmoteData.EmoteCategory.Special;
            case "Expressions":
                return EmoteData.EmoteCategory.Expressions;
            default:
                return EmoteData.EmoteCategory.Unknown;
        }
    }

    public static string GetEmoteCategoryString(Emote emote) => emote.EmoteCategory.Value.Name.ToString();

    public static EmoteData.EmotePlayType GetEmotePlayType(Emote emote)
    {
        var emoteSpecification = TryGetEmoteSpecification(emote);
        if (emoteSpecification != null) return emoteSpecification.Value.PlayType;

        return (GetEmoteCategory(emote) == EmoteData.EmoteCategory.Special) ? EmoteData.EmotePlayType.Looped : EmoteData.EmotePlayType.OneShot;
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
    
    public static Emote? TryGetEmoteFromStringCommand(string command)
    {
        if (command.StartsWith('/'))
            command = command[1..];

        var foundEmote = LinqExtensions.FirstOrNull(Service.EmoteCommands, e => e.Item1 == $"/{command}");
        return foundEmote.HasValue ? foundEmote.Value.Item2 : null;
    }

    public static unsafe ReadOnlySpan<byte> GetUtf8Span(Utf8String* s) => s == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(s->StringPtr, (int)s->Length);

    public static string RemoveFirstAndLastTwoChars(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= 4) return string.Empty;
        return s.AsSpan(2, s.Length - 4).ToString();
    }

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
                    sb.Append(RemoveFirstAndLastTwoChars(a.Text ?? string.Empty));
                    break;
            }
        }
        return sb.ToString();
    }

    public unsafe static bool IsEmoteUnlocked(uint emoteId)
    {
        return UIState.Instance()->IsEmoteUnlocked((ushort)emoteId);
    }

    public static unsafe bool IsCharacterInObjectTable(TrackedCharacter? trackedCharacter)
    {
        if (trackedCharacter == null) return false;

        var castChar = TryGetCharacterFromTrackedCharacter(trackedCharacter);

        if (castChar == null) return false;

        return Service.Objects.Any(o => o.Address == (nint)GetCharacter(castChar));
    }

    public static unsafe bool IsCharacterWeaponDrawn(nint charaAddress)
    {
        var castChar = TryGetCharacterFromAddress(charaAddress);
        if (castChar == null) return false;
        return castChar.StatusFlags.HasFlag(StatusFlags.WeaponOut);
    }

    public static float GetRotationToTarget(ICharacter from, ICharacter to)
    {
        return MathHelper.GetAngleBetweenPoints(new(from.Position.Z, from.Position.X), new(to.Position.Z, to.Position.X));
    }
}
