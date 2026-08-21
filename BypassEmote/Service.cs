using BypassEmote.IPC;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System.Collections.Generic;
using System.Threading;
#if DEBUG
using NoireLib.Networker;
#endif

namespace BypassEmote;

public partial class Service
{
    public static Plugin Plugin { get; set; } = null!;

    public static IReadOnlyList<(Emote, NoireLib.Enums.EmoteCategory)> LockedEmotes { get; private set; } = [];

    private static readonly CancellationTokenSource DisposalTokens = new();

    public static ActionTimelinePlayer ActionTimelinePlayer = new ActionTimelinePlayer();

#if DEBUG
    public static NoireNetworker Networker { get; set; }
#endif

    public static void InitializeService(Plugin plugin)
    {
        Plugin = plugin;

        NoireService.ClientState.Login += RefreshLockedEmotes;
        NoireService.ClientState.Logout += OnLogout;

        _ = FetchAndBuildEmoteSourcesAsync(DisposalTokens.Token);

        NoireService.Framework.RunOnFrameworkThread(() =>
        {
            if (NoireService.ClientState.IsLoggedIn && NoireService.ObjectTable.LocalPlayer != null)
                RefreshLockedEmotes();
        });

#if DEBUG
        Networker = NoireLibMain.AddModule(new NoireNetworker("BypassEmote.Relay"));
        IpcProvider.EnsureListeningRelay();
#endif

        InstallHooks();
        InitializeSwap();
    }

    public static void RefreshLockedEmotes()
    {
        if (!NoireService.ClientState.IsLoggedIn || NoireService.ObjectTable.LocalPlayer == null)
        {
            ClearLockedEmotes();
            return;
        }

        var built = new List<(Emote, NoireLib.Enums.EmoteCategory)>();

        foreach (var emote in EmoteHelper.GetLockedEmotes())
            built.Add((emote, EmoteHelper.GetEmoteCategory(emote)));

        LockedEmotes = built;
    }

    public static void ClearLockedEmotes()
    {
        LockedEmotes = [];
    }

    private static void OnLogout(int type, int code)
    {
        ClearLockedEmotes();

        Orchestrator?.CancelPendingExecute();
        EndWatcher?.StopWatching();
        SwapMods?.DeselectAll();
    }

    public static void OpenKofi() => SystemHelper.OpenUrl("https://ko-fi.com/aspher0");

    public static void Dispose()
    {
        DisposalTokens.Cancel();

        NoireService.ClientState.Login -= RefreshLockedEmotes;
        NoireService.ClientState.Logout -= OnLogout;

        DisposeSwap();

        IpcProvider.Dispose();
        EmotePlayer.Dispose();

        DisposalTokens.Dispose();
    }
}
