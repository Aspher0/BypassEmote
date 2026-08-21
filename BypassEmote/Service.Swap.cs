using BypassEmote.EmoteSwap;
using BypassEmote.IPC;
using NoireLib;
using NoireLib.Helpers;

namespace BypassEmote;

public partial class Service
{
    // Name of the generated Penumbra mod. Tags in braces are replaced per character:
    // {playerFullName}, {playerName}, {playerWorld}
    public static string PenumbraModNameTemplate { get; set; } = "BypassEmote [{playerFullName}]";

    public static string PenumbraModAuthor { get; set; } = "Aspher_XIV - BypassEmote";

    public static string PenumbraModWebsite { get; set; } = "https://github.com/Aspher0/BypassEmote";

    public static string PenumbraModVersion
        => NoireService.PluginInstance?.GetType().Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

    public static SwapModIdentity? SwapIdentity;
    public static IPCCaller_Penumbra? Penumbra;
    public static EmoteAttributeCatalog? Catalog;
    public static SwapModManager? SwapMods;
    public static SwapEndWatcher? EndWatcher;
    public static SkeletonWatcher? BodyWatcher;
    public static SchedulerResidencyProbe? ResidencyProbe;
#if DEBUG
    public static ClientTriggerMonitor? TriggerMonitor;
#endif
    public static SwapOrchestrator? Orchestrator;

    private static string? _sweptFor;

    private static void InitializeSwap()
    {
        SwapIdentity = new SwapModIdentity();

        Penumbra = new IPCCaller_Penumbra(NoireService.PluginInterface, SwapIdentity);

        Catalog = new EmoteAttributeCatalog();
        NoireService.ClientState.Login += Catalog.StartBuild;

        if (NoireService.ClientState.IsLoggedIn)
            Catalog.StartBuild();

        SwapMods = new SwapModManager(Penumbra, SwapIdentity, NoireService.PluginInterface.GetPluginConfigDirectory());
        EndWatcher = new SwapEndWatcher(SwapMods);
        ResidencyProbe = new SchedulerResidencyProbe();
        SwapMods.ResidencyProbe = ResidencyProbe;
#if DEBUG
        TriggerMonitor = new ClientTriggerMonitor();
#endif
        Orchestrator = new SwapOrchestrator(Penumbra, Catalog, SwapMods, EndWatcher, ResidencyProbe);

        BodyWatcher = new SkeletonWatcher(SwapMods);

        SwapIdentity.Changed += OnSwapIdentityChanged;

        ScheduleStartupSweep();

        _ = AsyncHelper.RunInBackgroundAsync(SwapOrchestrator.WarmUpBytePipeline, "WarmUpBytePipeline");
    }

    private static void ScheduleStartupSweep()
    {
        if (Penumbra == null)
            return;

        if (Penumbra.Available)
        {
            RunStartupSweep();
            return;
        }

        Penumbra.AvailabilityChanged += OnPenumbraAvailableForStartupSweep;
    }

    private static void OnPenumbraAvailableForStartupSweep(bool available)
    {
        if (available)
            RunStartupSweep();
    }

    private static void OnSwapIdentityChanged(SwapModNames? previous)
    {
        if (SwapIdentity?.Names != null && Penumbra?.Available == true)
            RunStartupSweep();
    }

    private static void RunStartupSweep()
    {
        _ = NoireService.Framework.RunOnFrameworkThread(() =>
        {
            if (SwapIdentity?.Names is { } names && _sweptFor != names.CharacterKey)
            {
                _sweptFor = names.CharacterKey;
                SwapMods?.StartupSweep();
            }
        });
    }

    private static void DisposeSwap()
    {
        if (Catalog != null)
            NoireService.ClientState.Login -= Catalog.StartBuild;

        BodyWatcher?.Dispose();
        Orchestrator?.Dispose();

        EndWatcher?.Disarm();

        if (Penumbra != null)
            Penumbra.AvailabilityChanged -= OnPenumbraAvailableForStartupSweep;

        if (SwapIdentity != null)
            SwapIdentity.Changed -= OnSwapIdentityChanged;

        Penumbra?.Dispose();
        SwapIdentity?.Dispose();
    }
}
