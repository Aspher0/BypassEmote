using BypassEmote.Safety;
using Dalamud.Plugin;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Hooking;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BypassEmote.Helpers;

internal static class DebugLogExporter
{
    private const string LogPrefix = "[DebugLogExporter] ";
    private const string OperationName = "BypassEmote.ExportDebugLogs";

    private const string FolderName = "DebugLogs";
    private const string ArchivePrefix = "DO_NOT_POST_PUBLICLY_DebugLogs_";
    private const string StagingPrefix = ".staging_";
    private const string ReportEntryName = "report.txt";
    private const string LogEntryName = "bypassemote.log";
    private const string ConfigEntryFolder = "config";

    private const string CurrentLogFileName = "dalamud.log";
    private const string RotatedLogFileName = "dalamud.old.log";

    private const string PluginTag = "BypassEmote";

    private const int MaxHeadChars = 4 * 1024 * 1024;
    private const int MaxTailChars = 12 * 1024 * 1024;

    private static readonly TimeSpan LoadMargin = TimeSpan.FromMinutes(1);

    private static readonly Vector3 LinkColor = ColorHelper.HexToVector3("#4FA3FF");

    private const string ExportingMessage = "Exporting logs...";

    private const string OpeningText = "Debug logs exported to ";

    private const string BodyText =
        ". Send this file to the developer. Feel free to check the content of the zip and if you need to "
        + "anonymize any information, please do so before sending it. ";

    private const string AlertText =
        "Personal information appears in it, DO NOT send this in a public channel.";

    private const string ClosingText =
        " Ask the developer where to send this file to be extra safe.";

    private const string AlreadyRunningMessage = "A debug log export is already running.";
    private const string FailedMessage = "Debug logs could not be exported. The Dalamud log has the reason.";

    private readonly record struct LogExtract(int Kept, int Dropped, string Sources);

    private sealed record LiveSnapshot(string Report, bool LoggedIn, IReadOnlyList<Emote> Unlocked,
        IReadOnlyList<Emote> Locked);

    private static int _running;

    internal static void Export()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            FeedbackHelper.Info(AlreadyRunningMessage);
            return;
        }

        FeedbackHelper.NoticeAlways(ExportingMessage);

        _ = AsyncHelper.RunInBackgroundAsync(ExportAsync, OperationName);
    }

    private static async Task ExportAsync()
    {
        try
        {
            var live = await AsyncHelper.RunOnFrameworkThreadAsync(ReadLive);
            var archive = Write(live);

            await AsyncHelper.RunOnFrameworkThreadAsync(() => Announce(archive));
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Exporting the debug logs failed.", LogPrefix);
            FeedbackHelper.Error(FailedMessage);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private static void Announce(string archivePath)
    {
        var key = $"BypassEmote.OpenDebugLogs.{Path.GetFileName(archivePath)}";

        void Open() => SystemHelper.OpenFileLocation(archivePath);

        var chat = NoireLogger.CreateChatMessageBuilder();

        chat.AddText(OpeningText, FeedbackHelper.NoticeColor);
        chat.AddLink(archivePath, key, Open, FeedbackHelper.NoticeColor);
        chat.AddText(BodyText, FeedbackHelper.NoticeColor);
        chat.AddText(AlertText, FeedbackHelper.AlertColor);
        chat.AddText(ClosingText, FeedbackHelper.NoticeColor);
        chat.AddText(" ");
        chat.AddLink("[Open folder]", key, Open, LinkColor);

        FeedbackHelper.NoticeAlways(OpeningText + archivePath + BodyText + AlertText + ClosingText, chat);
    }

    private static string Write(LiveSnapshot live)
    {
        if (NoireService.PluginInterface.GetPluginConfigDirectory() is not { Length: > 0 } configDirectory)
            throw new DirectoryNotFoundException("The plugin config directory is unavailable.");

        var destination = Path.Combine(configDirectory, FolderName);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var staging = Path.Combine(destination, StagingPrefix + stamp);

        Directory.CreateDirectory(staging);

        try
        {
            var logPath = Path.Combine(staging, LogEntryName);
            var extract = WriteLog(logPath);

            var reportPath = Path.Combine(staging, ReportEntryName);

            File.WriteAllText(reportPath, live.Report + EmoteSection(live) + ArchiveSection(extract),
                new UTF8Encoding(false));

            var files = new List<(string FilePath, string? EntryName)>
            {
                (reportPath, ReportEntryName),
                (logPath, LogEntryName),
            };

            files.AddRange(ConfigEntries(configDirectory, destination));

            return FileHelper.ZipFiles(files, destination, $"{ArchivePrefix}{stamp}.zip")
                ?? throw new IOException("The archive could not be written.");
        }
        finally
        {
            Remove(staging);
        }
    }

    private static List<(string FilePath, string? EntryName)> ConfigEntries(string configDirectory, string excluded)
    {
        var entries = new List<(string FilePath, string? EntryName)>();

        foreach (var file in Directory.EnumerateFiles(configDirectory, "*", SearchOption.AllDirectories))
        {
            if (file.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(configDirectory, file).Replace('\\', '/');
            entries.Add((file, $"{ConfigEntryFolder}/{relative}"));
        }

        return entries;
    }

    private static void Remove(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoireLogger.LogDebug($"Could not remove the staging folder '{directory}' ({ex.Message}).", LogPrefix);
        }
    }

    private static LogExtract WriteLog(string outputPath)
    {
        var cutoff = LoadedAt() - LoadMargin;
        var sources = LogFiles(cutoff).ToList();

        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));

        var tail = new Queue<string>();
        var head = 0;
        var tailChars = 0;
        var kept = 0;
        var dropped = 0;

        foreach (var line in SelectedLines(sources, cutoff))
        {
            kept++;

            if (head < MaxHeadChars)
            {
                writer.WriteLine(line);
                head += line.Length + 1;
                continue;
            }

            tail.Enqueue(line);
            tailChars += line.Length + 1;

            while (tailChars > MaxTailChars && tail.Count > 0)
            {
                tailChars -= tail.Dequeue().Length + 1;
                dropped++;
            }
        }

        if (dropped > 0)
        {
            writer.WriteLine($"... {dropped} line(s) dropped here to keep this file under "
                + $"{(MaxHeadChars + MaxTailChars) / (1024 * 1024)} MB ...");
        }

        foreach (var line in tail)
            writer.WriteLine(line);

        var names = sources.Select(Path.GetFileName).ToList();

        return new LogExtract(kept, dropped, names.Count == 0 ? "none found" : string.Join(", ", names));
    }

    private static IEnumerable<string> SelectedLines(IReadOnlyList<string> sources, DateTimeOffset cutoff)
    {
        foreach (var path in sources)
        {
            var keeping = false;

            foreach (var line in ReadLines(path))
            {
                if (EntryStamp(line) is { } stamp)
                    keeping = stamp >= cutoff && line.Contains(PluginTag, StringComparison.Ordinal);

                if (keeping)
                    yield return line;
            }
        }
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static DateTimeOffset? EntryStamp(string line)
    {
        var bracket = line.IndexOf('[');

        if (bracket < 20 || bracket > 48)
            return null;

        return DateTimeOffset.TryParse(line.AsSpan(0, bracket).Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var stamp) ? stamp : null;
    }

    private static IEnumerable<string> LogFiles(DateTimeOffset cutoff)
    {
        if (DalamudRoot() is not { } root)
            yield break;

        var rotated = Path.Combine(root, RotatedLogFileName);

        if (File.Exists(rotated) && new DateTimeOffset(File.GetLastWriteTime(rotated)) >= cutoff)
            yield return rotated;

        var current = Path.Combine(root, CurrentLogFileName);

        if (File.Exists(current))
            yield return current;
    }

    private static string? DalamudRoot()
        => NoireService.PluginInterface.ConfigDirectory.Parent?.Parent?.FullName;

    private static DateTimeOffset LoadedAt()
    {
        DateTimeOffset loaded = NoireService.PluginInterface.LoadTime;
        return loaded;
    }

    private static string ArchiveSection(LogExtract extract)
    {
        var section = new StringBuilder();

        section.AppendLine("== Archive ==");
        section.AppendLine($"Log sources: {extract.Sources}");
        section.AppendLine($"Log lines kept: {extract.Kept}");
        section.AppendLine($"Log lines dropped: {extract.Dropped}");
        section.AppendLine($"Contents: {ReportEntryName}, {LogEntryName}, {ConfigEntryFolder}/");

        return section.ToString();
    }

    private static LiveSnapshot ReadLive()
    {
        var report = new StringBuilder();

        report.AppendLine("BypassEmote debug report");
        report.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        report.AppendLine();

        Section(report, "Plugin", AppendPlugin);
        Section(report, "Game", AppendGame);
        Section(report, "Patch approval", AppendPatchApproval);
        Section(report, "Settings", AppendSettings);
        Section(report, "Penumbra", AppendPenumbra);
        Section(report, "Character", AppendCharacter);
        Section(report, "Emote catalog", AppendCatalog);
        Section(report, "Hooks", AppendHooks);

        var loggedIn = NoireService.ClientState.IsLoggedIn;

        IReadOnlyList<Emote> unlocked = [];
        IReadOnlyList<Emote> locked = [];

        if (loggedIn)
        {
            try
            {
                unlocked = EmoteHelper.GetUnlockedEmotes();
                locked = EmoteHelper.GetLockedEmotes();
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(ex, "The emote unlock state could not be read.", LogPrefix);
            }
        }

        return new LiveSnapshot(report.ToString(), loggedIn, unlocked, locked);
    }

    private static void Section(StringBuilder report, string title, Action<StringBuilder> body)
    {
        report.AppendLine($"== {title} ==");

        try
        {
            body(report);
        }
        catch (Exception ex)
        {
            report.AppendLine($"This section could not be read: {ex.GetType().Name}: {ex.Message}");
        }

        report.AppendLine();
    }

    private static void AppendPlugin(StringBuilder report)
    {
        var pluginInterface = NoireService.PluginInterface;
        var loaded = LoadedAt();

        report.AppendLine($"Version: {typeof(Plugin).Assembly.GetName().Version}");
        report.AppendLine($"NoireLib: {typeof(NoireService).Assembly.GetName().Version}");
        report.AppendLine($"Dalamud: {typeof(IDalamudPluginInterface).Assembly.GetName().Version}");
        report.AppendLine($"Loaded at: {loaded:yyyy-MM-dd HH:mm:ss zzz} (up {DateTimeOffset.Now - loaded:d\\.hh\\:mm\\:ss})");
        report.AppendLine($"Source repository: {pluginInterface.SourceRepository}");
        report.AppendLine($"Dev build: {pluginInterface.IsDev}, testing: {pluginInterface.IsTesting}, debugging: {pluginInterface.IsDebugging}");
        report.AppendLine($"OS: {SystemHelper.OSDescription}");
    }

    private static void AppendGame(StringBuilder report)
    {
        var client = GameClientReader.Current();

        report.AppendLine($"Build: {Service.PatchApproval?.GameVersion ?? "unknown"}");
        report.AppendLine($"Client: {GameClientReader.Name(client)} ({client})");
        report.AppendLine($"UI language: {NoireService.PluginInterface.UiLanguage}");
        report.AppendLine($"Logged in: {NoireService.ClientState.IsLoggedIn}");
    }

    private static void AppendPatchApproval(StringBuilder report)
    {
        if (Service.PatchApproval is not { } gate)
        {
            report.AppendLine("Not initialized.");
            return;
        }

        report.AppendLine($"Status: {gate.Status}");
        report.AppendLine($"Reason: {gate.Reason}");
        report.AppendLine($"Notice: {gate.Notice ?? "none"}");
        report.AppendLine($"Plugin version seen: {gate.PluginVersion?.ToString() ?? "unknown"}");
        report.AppendLine($"Last checked: {gate.LastCheckedUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "never"}");
        report.AppendLine($"Governs hooks: {gate.Governs}, holding hooks: {gate.HoldsHooks}, held: {gate.HeldCount}");
        report.AppendLine($"Remembered approval: game {Configuration.ApprovedGameVersion}, plugin {Configuration.ApprovedPluginVersion}");
    }

    private static void AppendSettings(StringBuilder report)
    {
        report.AppendLine($"Plugin enabled: {Configuration.PluginEnabled}");
        report.AppendLine($"Self bypass mode: {Configuration.SelfBypassMode}");
        report.AppendLine($"Swap lifetime: {Configuration.SwapLifetime}");
        report.AppendLine($"Swap behavior: {Configuration.SwapBehavior} (max {Configuration.MaxKeptSwapsPerTarget} per target)");
        report.AppendLine($"Matching: loop {Configuration.LoopMatching}, turn {Configuration.TurnMatching}, sound {Configuration.SoundMatching}");
        report.AppendLine($"Idle pose loops: {Configuration.IdlePoseLoops}");
        report.AppendLine($"Modded targets: {Configuration.ModdedTargets}");
        report.AppendLine($"Cached dispatch: {Configuration.CachedDispatch}, fidelity {Configuration.DispatchFidelity}, max targets per rank {Configuration.MaxTargetsPerRank}");
        report.AppendLine($"Anonymize mod name: {Configuration.AnonymizeModName}");
        report.AppendLine($"Always cache break: {Configuration.AlwaysCacheBreak}");
        report.AppendLine($"Direct play unsafe: {Configuration.DirectPlayUnsafe}");
        report.AppendLine($"Blocked targets: {Configuration.BlockedTargetEmotesEmoteSwap.Count}");
        report.AppendLine($"Chat: swap {Configuration.ShowSwapMessages}, warnings {Configuration.ShowWarningMessages}, errors {Configuration.ShowErrorMessages}");
    }

    private static void AppendPenumbra(StringBuilder report)
    {
        if (Service.Penumbra is not { } penumbra)
        {
            report.AppendLine("Not initialized.");
            return;
        }

        report.AppendLine($"Available: {penumbra.Available}");
        report.AppendLine($"Reason: {penumbra.UnavailableReason}");

        if (!penumbra.Available)
            return;

        report.AppendLine($"API version: {(penumbra.ReportedApiVersion() is { } api ? $"{api.Breaking}.{api.Feature}" : "unknown")}");
        report.AppendLine($"Mod root: {(penumbra.GetModRootDirectory() is { Length: > 0 } ? "detected" : "unknown")}");

        if (penumbra.GetPlayerCollection() is not { } collection)
        {
            report.AppendLine("Effective collection: none assigned");
            return;
        }

        report.AppendLine("Effective collection: detected");

        report.AppendLine($"Matches the registry collection: "
            + $"{Service.SwapMods?.Registry.CollectionId == collection.Id}");
    }

    private static void AppendCharacter(StringBuilder report)
    {
        report.AppendLine(Service.SwapIdentity?.Names is { } names
            ? $"Character key: {names.CharacterKey}{Environment.NewLine}Generated mod: {names.Directory}"
            : "No character is loaded.");

        if (Service.SwapMods is not { } swapMods)
        {
            report.AppendLine("The swap mod manager is not initialized.");
            return;
        }

        var registry = swapMods.Registry;
        var armed = registry.Entries.Where(entry => entry.SelectedByUs).ToList();

        report.AppendLine($"Registry schema: {registry.SchemaVersion}");
        report.AppendLine($"Registry collection: {registry.CollectionId}");
        report.AppendLine($"Registry skeleton: {registry.Skeleton ?? "none"}");
        report.AppendLine($"Applied priority: {registry.AppliedPriority}");
        report.AppendLine($"Entries: {registry.Entries.Count} ({armed.Count} armed)");
        report.AppendLine($"Competing mods: {(registry.CompetingMods is { Count: > 0 } competing ? string.Join(", ", competing) : "none")}");
        report.AppendLine($"Dispatch records: {registry.Dispatch?.Count ?? 0}");

        report.AppendLine(swapMods.PenumbraState() is { } state
            ? $"Generated mod state: enabled {state.Enabled}, priority {state.Priority}"
            : "Generated mod state: unknown to Penumbra");

        foreach (var entry in armed)
        {
            report.AppendLine($"  armed: {entry.GroupName} / {entry.OptionName}"
                + $" | source {entry.SourceEmote} -> target {entry.TargetEmote}"
                + $"{(entry.IsIdlePoseSwap ? $" | idle pose {entry.IdlePoseIndex}" : string.Empty)}");
        }
    }

    private static void AppendCatalog(StringBuilder report)
    {
        report.AppendLine($"Ready: {Service.Catalog?.Ready == true}");
        report.AppendLine($"Emotes read: {Service.Catalog?.All.Count ?? 0}");
        report.AppendLine($"Locked emotes known: {Service.LockedEmotes.Count}");
    }

    private static string EmoteSection(LiveSnapshot live)
    {
        var report = new StringBuilder();

        Section(report, "Emotes", section => AppendEmotes(section, live));

        return report.ToString();
    }

    private static void AppendEmotes(StringBuilder report, LiveSnapshot live)
    {
        if (!live.LoggedIn)
        {
            report.AppendLine("No character is loaded, so no unlock state can be read.");
            return;
        }

        var blocked = Configuration.BlockedTargetEmotesEmoteSwap;

        report.AppendLine($"Locked: {live.Locked.Count}");
        report.AppendLine($"Unlocked: {live.Unlocked.Count}");
        report.AppendLine($"Favorites: {Configuration.FavoriteEmotes.Count}");

        report.AppendLine($"Blocked as swap targets: {blocked.Count}"
            + (blocked.Count > 0 ? $" (ids {string.Join(", ", blocked)})" : string.Empty));

        AppendEmoteList(report, "locked", live.Locked);
        AppendEmoteList(report, "unlocked", live.Unlocked);
    }

    private static void AppendEmoteList(StringBuilder report, string title, IReadOnlyList<Emote> emotes)
    {
        report.AppendLine();
        report.AppendLine($"-- {title} ({emotes.Count}) --");

        foreach (var emote in emotes)
            report.AppendLine($"{emote.RowId} {Describe(emote)} | {CatalogReading(emote.RowId)}");
    }

    private static string Describe(Emote emote)
    {
        var command = emote.TextCommand.ValueNullable?.Command.ExtractText() ?? string.Empty;
        var name = emote.Name.ExtractText();

        if (string.IsNullOrWhiteSpace(name))
            name = "unnamed";

        return string.IsNullOrWhiteSpace(command) ? name : $"{command} ({name})";
    }

    private static string CatalogReading(uint emoteRowId)
    {
        if (Service.Catalog?.Get(emoteRowId) is not { } attributes)
            return "not in catalog";

        return $"loop {attributes.LoopKind}, intro {attributes.Intro}, turn {attributes.Turn}"
            + $", sound {attributes.Sound}, postures {attributes.Postures}"
            + $", eligible target {attributes.EligibleTarget}"
            + (attributes.IsPoseFamily ? ", pose family" : string.Empty);
    }

    private static void AppendHooks(StringBuilder report)
    {
        var hooks = NoireHook.All;

        report.AppendLine($"Registered: {hooks.Count}"
            + $" | installed {hooks.Count(hook => hook.State == HookState.Installed)}"
            + $" | failed {hooks.Count(hook => hook.State == HookState.Failed)}"
            + $" | pending {hooks.Count(hook => hook.State == HookState.Pending)}"
            + $" | disposed {hooks.Count(hook => hook.State == HookState.Disposed)}"
            + $" | enabled {hooks.Count(hook => hook.IsEnabled)}");

        foreach (var hook in hooks)
        {
            report.AppendLine();
            report.AppendLine($"[{hook.State}, {(hook.IsEnabled ? "enabled" : "disabled")}] {hook.Name}");
            report.AppendLine($"    group: {hook.Group ?? "none"}");
            report.AppendLine($"    address: 0x{hook.Address:X}{(hook.Identity is { } identity ? $" ({identity})" : string.Empty)}");
            report.AppendLine($"    backend: {hook.BackendName}, guarded: {hook.IsGuarded}, delegate: {hook.DelegateType.Name}");
            report.AppendLine($"    target: {hook.Target.Describe()}");
            report.AppendLine($"    verification: {hook.Verification.Status}");

            if (hook.Verification.Status == HookVerificationStatus.Mismatched)
                report.AppendLine(Indent(hook.Verification.Describe()));

            if (hook.CollectsStats)
            {
                report.AppendLine($"    calls: {hook.Stats.CallCount}, faults: {hook.Stats.FaultCount}"
                    + $", last call: {hook.Stats.LastCallUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "never"}");
            }
        }
    }

    private static string Indent(string block)
        => "    " + block.Replace(Environment.NewLine, Environment.NewLine + "    ");
}
