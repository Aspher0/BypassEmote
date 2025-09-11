using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BypassEmote.Helpers;

public static class CommonHelper
{
    public static string? GetPlayerFullNameFromPlayerCharacterObject(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return null;

        var playerCharacter = TryGetPlayerCharacterFromAddress(charaAddress);

        if (playerCharacter == null) return null;

        string playerName = playerCharacter.Name.ToString();
        World? playerHomeworld = Service.DataManager.GetExcelSheet<World>()?.GetRow(playerCharacter.HomeWorld.RowId);

        if (playerName == null || playerHomeworld == null) return null;

        string playerHomeworldName = playerHomeworld?.Name.ToString() ?? "Unknown";
        return $"{playerName}@{playerHomeworldName}";
    }

    public static unsafe Character* GetCharacter(IPlayerCharacter chara) => (Character*)chara.Address;

    public static IPlayerCharacter? TryGetPlayerCharacterFromAddress(nint charaAddress)
    {
        if (charaAddress == nint.Zero)
            return null;

        return Service.Objects.PlayerObjects.FirstOrDefault(p => p.Address == charaAddress) as IPlayerCharacter;
    }

    public static TrackedCharacter? TryGetCharacterFromTrackedList(nint charaAddress)
    {
        var CID = GetCIDFromPlayerPointer(charaAddress);

        if (CID == null)
            return null;

        foreach (var lc in EmotePlayer.TrackedCharacters)
        {
            if (lc.CID == CID)
            {
                return lc;
            }
        }
        return null;
    }

    public unsafe static TrackedCharacter? AddOrUpdateCharacterInTrackedList(nint charaAddress, ushort activeLoopTimelineId)
    {
        if (charaAddress == nint.Zero) return null;

        var CID = GetCIDFromPlayerPointer(charaAddress);

        if (CID == null) return null;

        var chara = TryGetPlayerCharacterFromAddress(charaAddress);

        if (chara == null) return null;

        var existing = TryGetCharacterFromTrackedList(charaAddress);
        if (existing != null)
        {
            existing.CID = CID.Value;
            existing.ActiveLoopTimelineId = activeLoopTimelineId;
            existing.LastPlayerPosition = chara.Position;
            existing.LastPlayerRotation = chara.Rotation;
            return existing;
        }
        else
        {
            var newTracked = new TrackedCharacter(CID.Value, activeLoopTimelineId, chara.Position, chara.Rotation, IsCharacterWeaponDrawn(charaAddress));
            EmotePlayer.TrackedCharacters.Add(newTracked);
            return newTracked;
        }
    }

    public unsafe static ulong? GetCIDFromPlayerPointer(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return null;

        var character = TryGetPlayerCharacterFromAddress(charaAddress);

        if (character == null) return null;

        var castChar = ((BattleChara*)character.Address);
        return castChar->Character.ContentId;
    }

    public static IPlayerCharacter? TryGetPlayerCharacterFromCID(ulong cid)
    {
        return Service.Objects.PlayerObjects
            .Where(o => o is IPlayerCharacter)
            .Select(o => o as IPlayerCharacter)
            .FirstOrDefault(p => p != null && GetCIDFromPlayerPointer(p.Address) == cid);
    }

    public static TrackedCharacter? TryGetTrackedCharacterFromCID(ulong cid)
    {
        return EmotePlayer.TrackedCharacters.FirstOrDefault(tc => tc.CID == cid);
    }

    public static void RemoveCharacterFromTrackedListByCharacterAddress(nint charaAddress)
    {
        if (charaAddress == nint.Zero) return;

        var CID = GetCIDFromPlayerPointer(charaAddress);

        if (CID == null) return;

        EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.CID == CID.Value);
    }

    public static void RemoveChracterFromTrackedListByID(string id)
    {
        EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.UniqueId == id);
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

    public static EmoteCategory? TryGetEmoteCategory(Emote emote)
    {
        if (EmoteCategories.TryGetValue(emote.RowId, out var category))
        {
            return category;
        }
        else
        {
            return null;
        }
    }
    
    public static Emote? TryGetEmoteFromStringCommand(string command)
    {
        if (command.StartsWith('/'))
            command = command[1..];

        var foundEmote = Service.Emotes.FirstOrNull(e => e.Item1 == $"/{command}");
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

    public static unsafe bool IsCharacterInObjectTable(TrackedCharacter? trackedCharacter)
    {
        if (trackedCharacter == null) return false;

        var character = TryGetPlayerCharacterFromCID(trackedCharacter.CID);

        if (character == null) return false;

        return Service.Objects.PlayerObjects.Any(o => o.Address == (nint)GetCharacter(character));
    }

    public unsafe static bool IsEmoteUnlocked(uint emoteId)
    {
        return UIState.Instance()->IsEmoteUnlocked((ushort)emoteId);
    }

    public static unsafe bool IsCharacterWeaponDrawn(nint charaAddress)
    {
        //if (charaAddress == nint.Zero) return false;

        //var gameObject = (GameObject*)charaAddress;

        //if (gameObject == null || gameObject->GetObjectKind() != ObjectKind.Pc) return false;

        //var character = (Character*)charaAddress;
        //return character->IsWeaponDrawn;

        var character = TryGetPlayerCharacterFromAddress(charaAddress);
        if (character == null) return false;
        return character.StatusFlags.HasFlag(StatusFlags.WeaponOut);
    }
}
