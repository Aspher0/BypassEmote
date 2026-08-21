using BypassEmote.Helpers;
using BypassEmote.Models;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Animations.AvfxFormat;
using NoireLib.Animations.Helpers;
using NoireLib.Animations.PapFormat.Tmb;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BypassEmote.EmoteSwap;

internal sealed record RawSlotData(int SlotIndex, uint TimelineRowId, string Key, int LoadType, int SlotColumn,
    bool Pause, int ActionTimelineIdMode = 0);

internal sealed record RawEmoteData(
    uint RowId, string Command, uint CategoryRowId, uint EmoteModeRowId, bool EmoteModeCamera, bool DrawsWeapon,
    bool DoNotPlay, IReadOnlyList<RawSlotData> Slots, IReadOnlyList<TmbEntryInfo> EmbeddedTmbEntries,
    bool IntroPapExists = false, IReadOnlyDictionary<string, string>? ActionTmbFaceLibraries = null,
    IReadOnlyDictionary<string, string>? EmbeddedFaceLibraries = null);

public sealed class EmoteAttributeCatalog
{
    private const string LogPrefix = "[EmoteAttributeCatalog] ";
    private const string CacheFileName = "emote_catalog.json";

    internal const int RulesVersion = 3;

    private const int ActionTimelineSlotCount = ActionTimelineSlots.SlotCount;
    private const string SharedFolder = "bt_common";

    // Weapon-motion slots are catalogued against one folder that carries every battle key, and the swap path
    // moves them onto the folder the player's own weapons name.
    internal const string ReferenceMotionFolder = WeaponMotionFolders.ReferenceFolder;

    private const string CatalogProbeSkeleton = "c0101";

    private const string ActionTmbPathFormat = "chara/action/{0}.tmb";
    private const string FacialPosePrefix = "facial/pose/";
    private const int BuildPacingBatchSize = 8;
    private const int BuildPacingSleepMs = 5;
    private const uint GeneralCategoryRowId = 1;
    private const uint SpecialCategoryRowId = 2;
    private const uint ChangePoseRowId = 90;
    private static readonly HashSet<uint> PoseFamilyRowIds = new() { ChangePoseRowId, 218, 219, 243, 244, 253 };
    private const int LoadTypePerJob = 1;
    private const int WeaponMotionIdMode = 2;
    private static readonly HashSet<uint> PostureLockEmoteModes = new() { 1, 2 };
    private static readonly HashSet<string> ScannedMagics =
        new(StringComparer.Ordinal) { "C053", "C063", "C012", "C173", "TMPP" };

    private static readonly Dictionary<string, bool> VfxSoundReadings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly PublishedData EmptyPublished = new(Array.Empty<EmoteAttributes>(), new Dictionary<uint, EmoteAttributes>());
    private int _buildState; // 0 = not started, 1 = started
    private PublishedData _published = EmptyPublished;

    public bool Ready => !ReferenceEquals(Volatile.Read(ref _published), EmptyPublished);
    public IReadOnlyList<EmoteAttributes> All => Volatile.Read(ref _published).All;

    public EmoteAttributes? Get(uint emoteRowId)
        => Volatile.Read(ref _published).ByRowId.TryGetValue(emoteRowId, out var attributes) ? attributes : null;

    public void StartBuild()
    {
        if (Interlocked.CompareExchange(ref _buildState, 1, 0) != 0)
            return;

        _ = RunBuildAsync();
    }

    private async Task RunBuildAsync()
    {
        try
        {
            await AsyncHelper.RunOnFrameworkThreadAsync(WarmSheets).ConfigureAwait(false);
            await Task.Run(BuildAndPublish).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Build failed; the catalog stays empty for this session.", LogPrefix);
        }
    }

    private static void WarmSheets()
    {
        try
        {
            ExcelSheetHelper.GetSheet<Emote>();
            ExcelSheetHelper.GetSheet<ActionTimeline>();
            ExcelSheetHelper.GetSheet<TextCommand>();
            ExcelSheetHelper.GetSheet<ResidentMotionType>();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not pre-load the game's Excel sheets.", LogPrefix);
        }
    }

    private void BuildAndPublish()
    {
        var buildClock = Stopwatch.StartNew();

        WeaponMotionTable.Warm();

        WeaponMotionFolders.GroupedFolders();

        if (Cache.Read(RulesVersion) is { Count: > 0 } cachedRows)
        {
            Publish(cachedRows);
            NoireLogger.LogDebug($"Loaded {cachedRows.Count} rows from cache in {buildClock.ElapsedMilliseconds}ms.", LogPrefix);
            return;
        }

        var rows = BuildFromGameData();

        if (rows.Count == 0)
        {
            NoireLogger.LogError("Emote catalog build produced no rows, will retry next start.", LogPrefix);
            return;
        }

        Publish(rows);
        Cache.Write(rows, RulesVersion);
        NoireLogger.LogDebug($"Built {rows.Count} rows from game data in {buildClock.ElapsedMilliseconds}ms.", LogPrefix);
    }

    private void Publish(List<EmoteAttributes> rows)
        => Volatile.Write(ref _published, new PublishedData(rows, rows.ToDictionary(r => r.RowId)));

    internal static EmoteAttributes BuildAttributes(RawEmoteData raw, Func<string, bool>? vfxPlaysSound = null)
    {
        var populatedSlots = raw.Slots.Where(s => s.TimelineRowId != 0).ToList();

        var loopKind = CatalogRules.ClassifyLoop(populatedSlots.Select(s => s.Pause));
        var sound = CatalogRules.ClassifySound(raw.EmbeddedTmbEntries, vfxPlaysSound);

        var slot0 = populatedSlots.FirstOrDefault(s => s.SlotIndex == 0);
        var turn = slot0 != null ? CatalogRules.ClassifyTurn(slot0.SlotColumn) : TurnClass.Unknown;

        var postures = PostureFlags.None;
        foreach (var slot in populatedSlots)
            postures |= CatalogRules.PostureForSlot(slot.SlotIndex);

        var introSlot = populatedSlots.FirstOrDefault(s => s.SlotIndex == 1);
        var hasIntro = introSlot != null;
        var introRelativePapPath = introSlot != null ? UsablePapPathFor(introSlot) : null;

        var intro = introSlot == null
            ? IntroKind.None
            : introRelativePapPath != null && raw.IntroPapExists ? IntroKind.Pap : IntroKind.TmbOnly;

        var variants = BuildVariants(populatedSlots);

        var command = raw.Command.TrimStart('/');

        var isPoseFamily = IsPoseFamily(raw.RowId, populatedSlots);

        var eligibleTarget = !IsExcluded(raw, populatedSlots, isPoseFamily);

        var faceLibraries = BuildFaceLibraries(populatedSlots, raw.ActionTmbFaceLibraries, raw.EmbeddedFaceLibraries);

        var animationTimelineIds = AnimationTimelineIdsFor(populatedSlots);

        return new EmoteAttributes(raw.RowId, command, loopKind, sound, turn, postures, hasIntro, introRelativePapPath,
            eligibleTarget, variants, raw.EmoteModeCamera, intro, isPoseFamily, faceLibraries, animationTimelineIds,
            introSlot != null && IsWeaponMotionSlot(introSlot));
    }

    private static IReadOnlyDictionary<string, string>? BuildFaceLibraries(List<RawSlotData> populatedSlots,
        IReadOnlyDictionary<string, string>? actionTmbFaceLibraries,
        IReadOnlyDictionary<string, string>? embeddedFaceLibraries)
    {
        Dictionary<string, string>? faceLibraries = null;

        foreach (var slot in populatedSlots)
        {
            if (UsablePapPathFor(slot) is not { } relativePapPath)
                continue;

            var faceLibrary = LookUp(actionTmbFaceLibraries, slot.Key) ?? LookUp(embeddedFaceLibraries, slot.Key);
            if (faceLibrary == null)
                continue;

            faceLibraries ??= new Dictionary<string, string>(StringComparer.Ordinal);
            faceLibraries.TryAdd(relativePapPath, faceLibrary);
        }

        return faceLibraries;

        static string? LookUp(IReadOnlyDictionary<string, string>? map, string key)
            => map != null && map.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
    }

    private static bool IsPoseFamily(uint rowId, List<RawSlotData> populatedSlots)
        => PoseFamilyRowIds.Contains(rowId) || populatedSlots.Any(s => IsPoseFamilyKey(s.Key));

    internal static bool IsPoseFamilyKey(string key)
        => key.StartsWith("emote/pose", StringComparison.Ordinal)
        || key.StartsWith("emote/s_pose", StringComparison.Ordinal)
        || key.StartsWith("emote/j_pose", StringComparison.Ordinal)
        || key.StartsWith("emote/l_pose", StringComparison.Ordinal)
        || (key.StartsWith("ornament_sp/", StringComparison.Ordinal) && key.Contains("onm_pose", StringComparison.Ordinal))
        || key == "resident/idle";

    internal static List<ushort> AnimationTimelineIdsFor(List<RawSlotData> populatedSlots)
    {
        var ids = new List<ushort>(populatedSlots.Count);

        foreach (var slot in populatedSlots)
        {
            if (UsablePapPathFor(slot) == null)
                continue;

            var id = (ushort)slot.TimelineRowId;
            if (id != 0 && !ids.Contains(id))
                ids.Add(id);
        }

        return ids;
    }

    private static List<VariantPaths> BuildVariants(List<RawSlotData> populatedSlots)
    {
        var variants = new List<VariantPaths>();

        foreach (var slot in populatedSlots)
        {
            var posture = CatalogRules.PostureForSlot(slot.SlotIndex);
            if (posture == PostureFlags.None)
                continue;

            if (UsablePapPathFor(slot) is not { } relativePapPath)
                continue;

            variants.Add(new VariantPaths(posture, relativePapPath, IsWeaponMotionSlot(slot)));
        }

        return variants;
    }

    private static string? UsablePapPathFor(RawSlotData slot)
    {
        if (string.IsNullOrEmpty(slot.Key) || slot.Key.StartsWith(FacialPosePrefix, StringComparison.Ordinal))
            return null;

        var weaponMotion = IsWeaponMotionSlot(slot);

        if (slot.LoadType == LoadTypePerJob && !weaponMotion)
            return null;

        return (weaponMotion ? ReferenceMotionFolder : SharedFolder) + "/" + slot.Key + ".pap";
    }

    internal static bool IsWeaponMotionSlot(RawSlotData slot) => slot.ActionTimelineIdMode == WeaponMotionIdMode;

    private static string CatalogProbePath(RawSlotData slot)
        => EmotePathHelper.GetSkeletonPath(CatalogProbeSkeleton,
            UsablePapPathFor(slot) ?? SharedFolder + "/" + slot.Key + ".pap");

    private static bool IsExcluded(RawEmoteData raw, List<RawSlotData> populatedSlots, bool isPoseFamily)
        => PostureLockEmoteModes.Contains(raw.EmoteModeRowId)
        || raw.CategoryRowId is not (GeneralCategoryRowId or SpecialCategoryRowId)
        || raw.RowId == ChangePoseRowId
        || string.IsNullOrEmpty(raw.Command)
        || populatedSlots.Any(s => s.LoadType == LoadTypePerJob)
        || populatedSlots.Any(IsWeaponMotionSlot)
        || raw.DrawsWeapon
        || raw.DoNotPlay
        || CatalogRules.SideEffectToggleEmotes.Contains(raw.RowId)
        || isPoseFamily;

    private static List<EmoteAttributes> BuildFromGameData()
    {
        var rows = new List<EmoteAttributes>();

        var sheet = ExcelSheetHelper.GetSheet<Emote>();
        if (sheet == null)
        {
            NoireLogger.LogError("Could not load the Emote sheet; the catalog stays empty for this session.", LogPrefix);
            return rows;
        }

        var processed = 0;

        foreach (var emote in sheet)
        {
            if (string.IsNullOrEmpty(emote.Name.ExtractText()) && !emote.ActionTimeline.Any(t => t.RowId != 0))
                continue;

            try
            {
                var raw = ReadRawEmoteData(emote);
                rows.Add(BuildAttributes(raw, VfxPlaysSound));
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(ex, $"Failed to process emote {emote.RowId}; skipped.", LogPrefix);
            }

            if (++processed % BuildPacingBatchSize == 0)
                Thread.Sleep(BuildPacingSleepMs);
        }

        return rows;
    }

    private static RawEmoteData ReadRawEmoteData(Emote emote)
    {
        var command = emote.TextCommand.ValueNullable?.Command.ExtractText() ?? string.Empty;
        var specification = CommonHelper.TryGetEmoteSpecification(emote);
        var doNotPlay = specification?.PlayType == EmotePlayType.DoNotPlay;

        var slots = ReadSlots(emote);
        var (embeddedTmbEntries, embeddedFaceLibraries) = ReadEmbeddedTmbEntries(emote.RowId, slots);

        var emoteModeCamera = emote.EmoteMode.RowId != 0 && (emote.EmoteMode.ValueNullable?.Camera ?? false);

        return new RawEmoteData(
            emote.RowId, command, emote.EmoteCategory.RowId, emote.EmoteMode.RowId, emoteModeCamera,
            emote.DrawsWeapon, doNotPlay, slots, embeddedTmbEntries,
            IntroPapExists: IntroPapExistsFor(slots),
            ActionTmbFaceLibraries: ReadActionTmbFaceLibraries(emote.RowId, slots),
            EmbeddedFaceLibraries: embeddedFaceLibraries);
    }

    private static bool IntroPapExistsFor(List<RawSlotData> slots)
    {
        var introSlot = slots.FirstOrDefault(s => s.SlotIndex == 1 && s.TimelineRowId != 0);
        if (introSlot == null || UsablePapPathFor(introSlot) == null)
            return false;

        try
        {
            return NoireService.DataManager.FileExists(CatalogProbePath(introSlot));
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Intro pap existence probe failed for key '{introSlot.Key}': {ex.Message}", LogPrefix);
            return false;
        }
    }

    private static List<RawSlotData> ReadSlots(Emote emote)
    {
        var timelineRefs = emote.ActionTimeline;
        var slots = new List<RawSlotData>(ActionTimelineSlotCount);

        for (var i = 0; i < timelineRefs.Count; i++)
        {
            var slotRef = timelineRefs[i];
            if (slotRef.RowId == 0)
            {
                slots.Add(new RawSlotData(i, 0, string.Empty, 0, 0, false));
                continue;
            }

            var resolved = slotRef.ValueNullable;
            if (resolved is not { } timeline)
            {
                slots.Add(new RawSlotData(i, slotRef.RowId, string.Empty, 0, 0, false));
                continue;
            }

            slots.Add(new RawSlotData(i, slotRef.RowId, timeline.Key.ExtractText(), timeline.LoadType,
                timeline.Slot, timeline.Pause, timeline.ActionTimelineIDMode));
        }

        return slots;
    }

    private static (List<TmbEntryInfo> Entries, Dictionary<string, string> FaceLibraries) ReadEmbeddedTmbEntries(uint emoteRowId, List<RawSlotData> slots)
    {
        var results = new List<TmbEntryInfo>();
        var faceLibraries = new Dictionary<string, string>(StringComparer.Ordinal);
        var scannedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            if (slot.TimelineRowId == 0 || string.IsNullOrEmpty(slot.Key) || !scannedKeys.Add(slot.Key))
                continue;

            try
            {
                var path = CatalogProbePath(slot);
                if (!NoireService.DataManager.FileExists(path))
                    continue;

                var data = NoireService.DataManager.GetFile(path)?.Data;
                if (data is not { Length: > 0 })
                    continue;

                foreach (var entry in TmbEntryScanner.ScanPap(data, ScannedMagics))
                {
                    if (entry.Magic == "TMPP")
                    {
                        if (entry.Path is { Length: > 0 } faceLibrary && !faceLibraries.ContainsKey(slot.Key))
                            faceLibraries[slot.Key] = faceLibrary;
                    }
                    else
                    {
                        results.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                NoireLogger.LogDebug($"Embedded TMB scan skipped for emote {emoteRowId} key '{slot.Key}': {ex.Message}", LogPrefix);
            }
        }

        return (results, faceLibraries);
    }

    private static bool VfxPlaysSound(string vfxPath)
    {
        if (VfxSoundReadings.TryGetValue(vfxPath, out var known))
            return known;

        var plays = false;

        try
        {
            if (NoireService.DataManager.FileExists(vfxPath)
                && NoireService.DataManager.GetFile(vfxPath)?.Data is { Length: > 0 } data)
            {
                plays = AvfxSound.HasSound(data);
            }
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Vfx sound read skipped for '{vfxPath}': {ex.Message}", LogPrefix);
        }

        VfxSoundReadings[vfxPath] = plays;

        return plays;
    }

    private static Dictionary<string, string> ReadActionTmbFaceLibraries(uint emoteRowId, List<RawSlotData> slots)
    {
        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        var scannedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            if (slot.TimelineRowId == 0 || UsablePapPathFor(slot) == null || !scannedKeys.Add(slot.Key))
                continue;

            try
            {
                var path = string.Format(ActionTmbPathFormat, slot.Key);
                if (!NoireService.DataManager.FileExists(path))
                    continue;

                var data = NoireService.DataManager.GetFile(path)?.Data;
                if (data is not { Length: > 0 })
                    continue;

                if (TmbEntryScanner.FindFaceLibrary(data) is { } faceLibrary)
                    results[slot.Key] = faceLibrary;
            }
            catch (Exception ex)
            {
                NoireLogger.LogDebug($"Action tmb face scan skipped for emote {emoteRowId} key '{slot.Key}': {ex.Message}", LogPrefix);
            }
        }

        return results;
    }

    private static VersionedJsonCache<List<EmoteAttributes>>? _cache;

    private static VersionedJsonCache<List<EmoteAttributes>> Cache
        => _cache ??= new VersionedJsonCache<List<EmoteAttributes>>(
            Path.Combine(NoireService.PluginInterface.GetPluginConfigDirectory(), CacheFileName),
            () => typeof(EmoteAttributeCatalog).Assembly.GetName().Version?.ToString() ?? "unknown");

    private sealed record PublishedData(IReadOnlyList<EmoteAttributes> All, IReadOnlyDictionary<uint, EmoteAttributes> ByRowId);
}
