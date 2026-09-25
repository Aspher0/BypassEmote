using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;

namespace BypassEmote.UI;

internal enum Companion
{
    Minion,
    Pet,
    Chocobo,
}

internal static class EmoteActions
{
    internal static bool InEmoteSwap => Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap;

    internal static bool PenumbraReady => Service.Penumbra is { Available: true };

    internal static bool WouldSwap(Silk.Main.SilkEmoteEntry entry) => InEmoteSwap && !Service.LeaveToTheGame(entry.RowId);

    internal static void PlaySelf(Emote emote)
    {
        if (InEmoteSwap)
            Plugin.PlaySelfEmote(emote);
        else
            EmotePlayer.PlayEmote(NoireService.ObjectTable.LocalPlayer, emote);
    }

    internal static void Sync(bool everyone) => EmotePlayer.SyncEmotes(everyone);

    internal static void Refresh() => Service.RefreshLockedEmotes();

    internal static void OpenCreateMod() => Service.Plugin.OpenCreateMod();

    internal static void OpenCreateMod(Emote emote) => Service.Plugin.OpenCreateMod(emote);

    internal static void OpenHotbar(Emote emote) => Service.Plugin.OpenAssignHotbar(emote);

    internal static void OpenOverride(uint rowId) => Service.Plugin.OpenOverrides(rowId);

#if DEBUG
    internal static bool CanForceSwap => true;

    internal static void ForceSwap(Emote emote)
        => NoireService.Framework.RunOnFrameworkThread(() => Service.Orchestrator?.TrySwap(emote));
#else
    internal static bool CanForceSwap => false;

    internal static void ForceSwap(Emote emote)
    {
    }
#endif

    internal static void ToggleFavourite(uint rowId)
    {
        if (!Configuration.FavoriteEmotes.Remove(rowId))
            Configuration.FavoriteEmotes.Add(rowId);
    }

    internal static void ToggleBlocked(uint rowId)
    {
        if (!Configuration.BlockedTargetEmotesEmoteSwap.Remove(rowId))
            Configuration.BlockedTargetEmotesEmoteSwap.Add(rowId);
    }

    internal static void PlayOn(Companion companion, Emote emote)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var addr = companion switch
        {
            Companion.Minion => CharacterHelper.GetCompanionAddress(player),
            Companion.Pet => CharacterHelper.GetPetAddress(player),
            _ => CharacterHelper.GetBuddyAddress(player),
        };

        if (addr == 0)
        {
            LogHelper.Info(companion switch
            {
                Companion.Minion => L.NoMinion,
                Companion.Pet => L.NoPet,
                _ => L.NoChocobo,
            });
            return;
        }

        foreach (var obj in NoireService.ObjectTable)
        {
            if (obj.Address != addr)
                continue;

            if (obj is ICharacter character)
                EmotePlayer.PlayEmote(character, emote);

            return;
        }
    }
}
