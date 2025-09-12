using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECommons.MathHelpers;

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

    public enum EmoteCategory
    {
        Looped = 1,
        OneShot = 2,
    }

    // TODO: Add custom behavior for /draw and /sheathe
    public static readonly Dictionary<uint, EmoteCategory> EmoteCategories = new Dictionary<uint, EmoteCategory>
    {
        { 62, EmoteCategory.OneShot }, // /megaflare
        { 175, EmoteCategory.OneShot }, // /ultima
        { 144, EmoteCategory.OneShot }, // /iceheart
        { 138, EmoteCategory.OneShot }, // /zantetsuken
    };

    public static EmoteCategory GetRealEmoteCategory(Emote emote)
    {
        var categoryString = emote.EmoteCategory.Value.Name.ToString();

        return (categoryString == "Special") ? EmoteCategory.Looped : EmoteCategory.OneShot;
    }

    public static EmoteCategory GetEmoteCategory(Emote emote)
    {
        var emoteCategory = TryGetEmoteCategory(emote);

        if (emoteCategory != null) return emoteCategory.Value;

        var categoryString = emote.EmoteCategory.Value.Name.ToString();

        return (categoryString == "Special") ? EmoteCategory.Looped : EmoteCategory.OneShot;
    }

    public static EmoteCategory? TryGetEmoteCategory(Emote emote)
    {
        if (EmoteCategories.TryGetValue(emote.RowId, out var category))
            return category;

        return null;
    }
    
    public static Emote? TryGetEmoteFromStringCommand(string command)
    {
        if (command.StartsWith('/'))
            command = command[1..];

        var foundEmote = LinqExtensions.FirstOrNull(Service.Emotes, e => e.Item1 == $"/{command}");
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
