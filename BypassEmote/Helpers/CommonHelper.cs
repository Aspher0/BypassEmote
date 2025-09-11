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
    public static string? GetPlayerFullNameFromPlayerCharacterObject(IPlayerCharacter? playerCharacter)
    {
        if (playerCharacter == null) return null;

        string playerName = playerCharacter.Name.ToString();
        World? playerHomeworld = Service.DataManager.GetExcelSheet<World>()?.GetRow(playerCharacter.HomeWorld.RowId);

        if (playerName == null || playerHomeworld == null) return null;

        string playerHomeworldName = playerHomeworld?.Name.ToString() ?? "Unknown";
        return $"{playerName}@{playerHomeworldName}";
    }

    public static unsafe Character* GetCharacter(IPlayerCharacter chara) => (Character*)chara.Address;

    public static IPlayerCharacter? TryGetPlayerCharacterFromAddress(nint address)
    {
        if (address == 0)
            return null;

        return Service.Objects.PlayerObjects.FirstOrDefault(p => p.Address == address) as IPlayerCharacter;
    }

    public static TrackedCharacter? TryGetCharacterFromTrackedList(IPlayerCharacter? chara)
    {
        if (chara == null)
            return null;

        foreach (var lc in EmotePlayer.TrackedCharacters)
        {
            if (lc.Character != null && lc.Character.Address == chara.Address)
            {
                return lc;
            }
        }
        return null;
    }

    public static TrackedCharacter? AddOrUpdateCharacterInTrackedList(IPlayerCharacter? chara, ushort activeLoopTimelineId)
    {
        if (chara == null)
            return null;

        var existing = TryGetCharacterFromTrackedList(chara);
        if (existing != null)
        {
            existing.Character = chara;
            existing.ActiveLoopTimelineId = activeLoopTimelineId;
            existing.LastPlayerPosition = chara.Position;
            existing.LastPlayerRotation = chara.Rotation;
            return existing;
        }
        else
        {
            var newTracked = new TrackedCharacter(chara, activeLoopTimelineId, chara.Position, chara.Rotation);
            EmotePlayer.TrackedCharacters.Add(newTracked);
            return newTracked;
        }
    }

    public static void RemoveCharacterFromTrackedListByCharacter(IPlayerCharacter? chara)
    {
        if (chara == null)
            return;

        EmotePlayer.TrackedCharacters.RemoveAll(tc => tc.Character != null && tc.Character.Address == chara.Address);
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
        if (trackedCharacter == null || trackedCharacter.Character == null)
            return false;

        return Service.Objects.PlayerObjects.Any(o => o.Address == (nint)GetCharacter(trackedCharacter.Character));
    }

    public static unsafe bool IsCharacterVisible(TrackedCharacter? trackedCharacter)
    {
        if (trackedCharacter == null || trackedCharacter.Character == null)
            return false;

        var gameObject = (GameObject*)trackedCharacter.Character.Address;
        if (gameObject == null || gameObject->DrawObject == null)
            return false;

        return gameObject->DrawObject->IsVisible;
    }

    public unsafe static bool IsEmoteUnlocked(uint emoteId)
    {
        return UIState.Instance()->IsEmoteUnlocked((ushort)emoteId);
    }
}
