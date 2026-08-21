using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.CommandRouter;
using NoireLib.Helpers;
using NoireLib.Hooking;
using System;

namespace BypassEmote;

public sealed partial class Plugin
{
    private void SetupCommands()
    {
        var commandRouter = NoireLibMain.AddModule(new NoireCommandRouter("CommandRouterModule"));

        var mainCommand = commandRouter.Map("/bypassemote")
            .AddAlias("/be")
            .WithHelp("Opens the Bypass Emote main window.")
            .WithDisplayOrder(0)
            .Handle(ToggleMainWindow)
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp("Bypasses any emote (including locked ones) on yourself, by command name or ID.")
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveLocalPlayer(), args.RawTokens[0], "Usage: /be <emote_command> or /be stop")))
            .AddSubCommand("config", sub => sub
                .WithHelp("Opens the configuration window.")
                .AddAlias("c")
                .WithDisplayOrder(0)
                .Handle(ToggleSettings))
            .AddSubCommand("sync", sub => sub
                .WithHelp("Syncs only players that are bypassing an emote.")
                .WithDisplayOrder(1)
                .Handle(() => EmotePlayer.SyncEmotes(false)))
            .AddSubCommand("syncall", sub => sub
                .WithHelp("Syncs everyone playing an emote.")
                .WithDisplayOrder(2)
                .Handle(() => EmotePlayer.SyncEmotes(true)))
            .AddSubCommand("changelog", sub => sub
                .WithHelp("Opens the changelog window.")
                .WithDisplayOrder(3)
                .Handle(OpenChangelog))
            .AddSubCommand("stop", sub => sub
                .WithHelp("Stops the emote currently playing on yourself.")
                .WithDisplayOrder(4)
                .Handle(() => StopEmote(ResolveLocalPlayer())));

#if DEBUG
        mainCommand
            .AddSubCommand("debug", sub => sub
                .WithHelp("Opens the debug window.")
                .AddAlias("d")
                .WithDisplayOrder(5)
                .Handle(ToggleDebug))
            .AddSubCommand("hooks", sub => sub
                .WithHelp("Shows the hooks window.")
                .WithDisplayOrder(6)
                .Handle(() => NoireHook.ShowWindow()));
#endif

        commandRouter.Map("/bet")
            .WithHelp("Applies any emote to a targetted NPC. Only works on NPCs and owned minions/pets. Use /bet <emote_command> or /bet stop.")
            .WithDisplayOrder(1)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp("Stops the emote currently playing on your target.")
                .Handle(() => StopEmote(ResolveTargetedNpc())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp("Plays the emote on your target, by command name or ID.")
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveTargetedNpc(), args.RawTokens[0], "Usage: /bet <emote_command> or /bet stop")));

        commandRouter.Map("/bem")
            .WithHelp("Applies any emote to your own minion if summoned, without needing to target it. Use /bem <emote_command> or /bem stop.")
            .WithDisplayOrder(2)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp("Stops the emote currently playing on your minion.")
                .Handle(() => StopEmote(ResolveMinion())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp("Plays the emote on your minion, by command name or ID.")
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveMinion(), args.RawTokens[0], "Usage: /bem <emote_command> or /bem stop")));

        commandRouter.Map("/bep")
            .WithHelp("Applies any emote to your own pet (carbuncle/eos) if summoned, without needing to target it. Use /bep <emote_command> or /bep stop.")
            .WithDisplayOrder(3)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp("Stops the emote currently playing on your pet.")
                .Handle(() => StopEmote(ResolvePet())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp("Plays the emote on your pet, by command name or ID.")
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolvePet(), args.RawTokens[0], "Usage: /bep <emote_command> or /bep stop")));

        commandRouter.Map("/bec")
            .WithHelp("Applies any emote to your own chocobo if summoned, without needing to target it. Use /bec <emote_command> or /bec stop.")
            .WithDisplayOrder(4)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp("Stops the emote currently playing on your chocobo.")
                .Handle(() => StopEmote(ResolveChocobo())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp("Plays the emote on your chocobo, by command name or ID.")
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveChocobo(), args.RawTokens[0], "Usage: /bec <emote_command> or /bec stop")));
    }

    private static ICharacter? ResolveLocalPlayer()
    {
        if (NoireService.ObjectTable.LocalPlayer is { } player)
            return player;

        FeedbackHelper.Info("Error trying to process command");
        return null;
    }

    private static ICharacter? ResolveTargetedNpc()
    {
        if (CommonHelper.GetLocalTarget() is not ICharacter target ||
            target is not INpc && target is not IBattleNpc)
        {
            FeedbackHelper.Info("No NPC targeted.");
            return null;
        }

        // Minion (Companion) or pet/chocobo (SubKind 2 and 3).
        if ((target.ObjectKind == ObjectKind.Companion || target.SubKind == 2 || target.SubKind == 3) && !CharacterHelper.IsLocalObject(target))
        {
            FeedbackHelper.Info("You can only target your own minion, pet, chocobo.");
            return null;
        }

        return target;
    }

    private static ICharacter? ResolveMinion()
        => ResolveOwned(CharacterHelper.GetCompanion, "No minion summoned.");

    private static ICharacter? ResolvePet()
        => ResolveOwned(CharacterHelper.GetPet, "No pet summoned.");

    private static ICharacter? ResolveChocobo()
        => ResolveOwned(CharacterHelper.GetBuddy, "No chocobo summoned.");

    private static ICharacter? ResolveOwned(Func<ICharacter, ICharacter?> lookup, string absentMessage)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return null;

        if (lookup(player) is { } owned)
            return owned;

        FeedbackHelper.Info(absentMessage);
        return null;
    }

    // The swap mode only affects the player character
    private static bool IsSwappedSelfPlay(ICharacter character)
        => Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap && IsLocalPlayer(character);

    private static bool IsLocalPlayer(ICharacter character)
        => NoireService.ObjectTable.LocalPlayer is { } localPlayer && character.Address == localPlayer.Address;

    private static void StopEmote(ICharacter? character)
    {
        if (character == null)
            return;

        if (IsSwappedSelfPlay(character))
            NoireService.Framework.RunOnFrameworkThread(() => Service.EndWatcher?.Disarm());
        else
            EmotePlayer.StopLoop(character, true);
    }

    private static void PlayEmoteFromArg(ICharacter? character, string arg, string usage)
    {
        if (character == null)
            return;

        var emote = EmoteHelper.GetEmoteByCommand(arg);

        if (uint.TryParse(arg, out var emoteId))
            emote = EmoteHelper.GetEmoteById(emoteId);

        if (!emote.HasValue)
        {
            FeedbackHelper.Info($"Emote not found: {arg}\n{usage}");
            return;
        }

        if (IsSwappedSelfPlay(character))
        {
            PlaySelfEmote(emote.Value);
            return;
        }

        EmotePlayer.PlayEmote(character, emote.Value);
    }

    internal static void PlaySelfEmote(Emote emote)
    {
        var attributes = Service.Catalog?.Get(emote.RowId);

        if (attributes?.IsPoseFamily == true)
        {
            FeedbackHelper.Error("Poses cannot be swapped.");
            return;
        }

        if (EmoteHelper.GetEmoteCategory(emote) == NoireLib.Enums.EmoteCategory.Unknown
            || (Service.Catalog?.Ready == true && attributes == null))
        {
            FeedbackHelper.Error("This emote cannot be played in Emote Swap mode.");
            return;
        }

        if (Service.LeaveToTheGame(emote.RowId))
        {
            NoireService.Framework.RunOnFrameworkThread(() => ExecuteOwnedEmote(emote.RowId));
            return;
        }

        NoireService.Framework.RunOnFrameworkThread(() => Service.Orchestrator?.TrySwap(emote));
    }

    private static void ExecuteOwnedEmote(uint emoteRowId)
        => EmoteHelper.ExecuteEmoteAtCurrentTarget(emoteRowId);
}
