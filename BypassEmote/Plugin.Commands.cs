using BypassEmote.Enums;
using BypassEmote.Helpers;
using BypassEmote.Localization;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.CommandRouter;
using NoireLib.Helpers;
#if DEBUG
using NoireLib.Hooking;
#endif
using NoireLib.Localizer;
using System;

namespace BypassEmote;

public sealed partial class Plugin
{
    private void SetupCommands()
    {
        var commandRouter = NoireLibMain.AddModule(new NoireCommandRouter("CommandRouterModule"));

        var mainCommand = commandRouter.Map("/bypassemote")
            .AddAlias("/be")
            .WithHelp(L.HelpMain)
            .WithDisplayOrder(0)
            .Handle(ToggleMainWindow)
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp(L.HelpSelfEmote)
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveLocalPlayer(), args.RawTokens[0],
                    () => commandRouter.PrintHelp("/bypassemote"))))
            .AddSubCommand("config", sub => sub
                .WithHelp(L.HelpConfig)
                .AddAlias("c")
                .WithDisplayOrder(0)
                .Handle(ToggleSettings))
            .AddSubCommand("sync", sub => sub
                .AddAlias("syncall")
                .WithHelp(L.HelpSync)
                .WithDisplayOrder(1)
                .Handle(() => EmotePlayer.SyncEmotes(true)))
            .AddSubCommand("syncdirect", sub => sub
                .WithHelp(L.HelpSyncDirect)
                .AddAlias("syncd")
                .WithDisplayOrder(2)
                .Handle(() => EmotePlayer.SyncEmotes(false)))
            .AddSubCommand("changelog", sub => sub
                .WithHelp(L.HelpChangelog)
                .WithDisplayOrder(3)
                .Handle(OpenChangelog))
            .AddSubCommand("stop", sub => sub
                .WithHelp(L.HelpStop)
                .WithDisplayOrder(4)
                .Handle(() => StopSelf(() => commandRouter.PrintHelp("/bypassemote"))))
            .AddSubCommand("logs", sub => sub
                .WithHelp(L.HelpLogs)
                .WithDisplayOrder(5)
                .Handle(DebugLogExporter.Export));

#if DEBUG
        mainCommand
            .AddSubCommand("debug", sub => sub
                .WithHelp(L.HelpDebug)
                .AddAlias("d")
                .WithDisplayOrder(6)
                .Handle(ToggleDebug))
            .AddSubCommand("hooks", sub => sub
                .WithHelp(L.HelpHooks)
                .WithDisplayOrder(7)
                .Handle(() => NoireHook.ShowWindow()));
#endif

        commandRouter.Map("/belogs")
            .WithHelp(L.HelpLogs)
            .WithDisplayOrder(5)
            .ShowDetailedDalamudHelp(false)
            .Handle(DebugLogExporter.Export);

        commandRouter.Map("/bet")
            .WithHelp(L.HelpTarget)
            .WithDisplayOrder(1)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp(L.HelpTargetStop)
                .Handle(() => StopEmote(ResolveTargetedNpc())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp(L.HelpTargetEmote)
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveTargetedNpc(), args.RawTokens[0],
                    () => commandRouter.PrintHelp("/bet"))));

        commandRouter.Map("/bem")
            .WithHelp(L.HelpMinion)
            .WithDisplayOrder(2)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp(L.HelpMinionStop)
                .Handle(() => StopEmote(ResolveMinion())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp(L.HelpMinionEmote)
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveMinion(), args.RawTokens[0],
                    () => commandRouter.PrintHelp("/bem"))));

        commandRouter.Map("/bep")
            .WithHelp(L.HelpPet)
            .WithDisplayOrder(3)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp(L.HelpPetStop)
                .Handle(() => StopEmote(ResolvePet())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp(L.HelpPetEmote)
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolvePet(), args.RawTokens[0],
                    () => commandRouter.PrintHelp("/bep"))));

        commandRouter.Map("/bec")
            .WithHelp(L.HelpChocobo)
            .WithDisplayOrder(4)
            .ShowDetailedDalamudHelp(false)
            .AddSubCommand("stop", sub => sub
                .WithHelp(L.HelpChocoboStop)
                .Handle(() => StopEmote(ResolveChocobo())))
            .AddFallbackCommand("emote_command", fallback => fallback
                .WithHelp(L.HelpChocoboEmote)
                .WithDisplayOrder(0)
                .Handle(args => PlayEmoteFromArg(ResolveChocobo(), args.RawTokens[0],
                    () => commandRouter.PrintHelp("/bec"))));
    }

    private static ICharacter? ResolveLocalPlayer()
    {
        if (NoireService.ObjectTable.LocalPlayer is { } player)
            return player;

        LogHelper.Info(L.CommandFailed);
        return null;
    }

    private static ICharacter? ResolveTargetedNpc()
    {
        if (GameObjectHelper.GetLocalTarget() is not ICharacter target ||
            target is not INpc && target is not IBattleNpc)
        {
            LogHelper.Info(L.NoNpcTargeted);
            return null;
        }

        // Minion (Companion) or pet/chocobo (SubKind 2 and 3).
        if ((target.ObjectKind == ObjectKind.Companion || target.SubKind == 2 || target.SubKind == 3) && !CharacterHelper.IsLocalObject(target))
        {
            LogHelper.Info(L.OnlyOwnCompanion);
            return null;
        }

        return target;
    }

    private static ICharacter? ResolveMinion()
        => ResolveOwned(CharacterHelper.GetCompanion, L.NoMinion);

    private static ICharacter? ResolvePet()
        => ResolveOwned(CharacterHelper.GetPet, L.NoPet);

    private static ICharacter? ResolveChocobo()
        => ResolveOwned(CharacterHelper.GetBuddy, L.NoChocobo);

    private static ICharacter? ResolveOwned(Func<ICharacter, ICharacter?> lookup, NoireString absentMessage)
    {
        if (NoireService.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return null;

        if (lookup(player) is { } owned)
            return owned;

        LogHelper.Info(absentMessage);
        return null;
    }

    private static bool IsSwappedSelfPlay(ICharacter character)
        => Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap && IsLocalPlayer(character);

    private static bool IsLocalPlayer(ICharacter character)
        => NoireService.ObjectTable.LocalPlayer is { } localPlayer && character.Address == localPlayer.Address;

    private static void StopSelf(System.Action printHelp)
    {
        if (Configuration.SelfBypassMode == SelfBypassMode.EmoteSwap)
        {
            LogHelper.Info(ChatText.Of(L.EmoteNotFound, "arg", "stop"));
            printHelp();
            return;
        }

        StopEmote(ResolveLocalPlayer());
    }

    private static void StopEmote(ICharacter? character)
    {
        if (character == null)
            return;

        if (IsSwappedSelfPlay(character))
            NoireService.Framework.RunOnFrameworkThread(() => Service.EndWatcher?.Disarm());
        else
            EmotePlayer.StopLoop(character, true);
    }

    private static void PlayEmoteFromArg(ICharacter? character, string arg, System.Action printHelp)
    {
        if (character == null)
            return;

        var emote = EmoteHelper.GetEmoteByCommand(arg);

        if (uint.TryParse(arg, out var emoteId))
            emote = EmoteHelper.GetEmoteById(emoteId);

        if (!emote.HasValue)
        {
            LogHelper.Info(ChatText.Of(L.EmoteNotFound, "arg", arg));
            printHelp();
            return;
        }

        if (IsSwappedSelfPlay(character))
        {
#if DEBUG
            PlaySelfEmote(emote.Value, force: true);
#else
            PlaySelfEmote(emote.Value);
#endif
            return;
        }

        EmotePlayer.PlayEmote(character, emote.Value);
    }

    internal static void PlaySelfEmote(Emote emote, bool force = false)
    {
        var attributes = Service.Catalog?.Get(emote.RowId);

        if (attributes?.IsPoseFamily == true)
        {
            LogHelper.Error(L.PosesCannotSwap);
            return;
        }

        if (EmoteHelper.GetEmoteCategory(emote) == NoireLib.Enums.EmoteCategory.Unknown
            || (Service.Catalog?.Ready == true && attributes == null))
        {
            LogHelper.Error(L.NotInEmoteSwap);
            return;
        }

        if (!force && Service.LeaveToTheGame(emote.RowId))
        {
            NoireService.Framework.RunOnFrameworkThread(() => ExecuteOwnedEmote(emote.RowId));
            return;
        }

        NoireService.Framework.RunOnFrameworkThread(() => Service.Orchestrator?.TrySwap(emote));
    }

    private static void ExecuteOwnedEmote(uint emoteRowId)
        => EmoteHelper.ExecuteEmoteAtCurrentTarget(emoteRowId);
}
