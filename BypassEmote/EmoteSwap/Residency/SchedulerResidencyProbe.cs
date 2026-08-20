using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using InteropGenerator.Runtime;
using NoireLib;
using NoireLib.Animations.PapFormat.Tmb;
using NoireLib.Helpers;
using NoireLib.Helpers.Memory;
using NoireLib.Hooking;
using System;
using System.Collections.Generic;
using System.Text;

namespace BypassEmote.EmoteSwap;

// Hooks the animation load path so a swapped emote serves its own content, and answers whether the scheduler cache
// still holds a timeline key. The loader dedupes by ActionTimeline key, so the doors below hand it a unique name
// per content.
public sealed unsafe class SchedulerResidencyProbe
{
    private const string LogPrefix = "[SchedulerResidencyProbe] ";

    private const string HookGroup = "BypassEmote.Scheduler";

    // SchedulerResourceManagement::GetCachedScheduleResource
    private const string GetCachedScheduleResourceSignature = "40 53 48 83 EC ?? 44 8B 4A";

    // loadData is { byte* Path; uint Id; }. Bare pointers so no struct mapping can drift.
    private delegate nint GetCachedScheduleResourceDelegate(nint management, nint loadData, byte useMap);

    private readonly NoireHook<GetCachedScheduleResourceDelegate>? _hook;

    // ResourceManager::GetResourceAsync. A wrapper hardcodes its no-cache flag to false, so a cached path
    // comes back with the old bytes. XIVClientStructs declares it, hence no signature here.
    private readonly NoireHook<ResourceManager.Delegates.GetResourceAsync>? _getResourceAsyncHook;

    // Penumbra hooks this to attribute a pap/tmb load to a character. A load that skips it belongs to nobody.
    private const string LoadTimelineResourcesSignature =
        "E8 ?? ?? ?? ?? 83 7F ?? ?? 75 ?? 0F B6 87 ?? ?? ?? ?? A8";

    private delegate ulong LoadTimelineResourcesDelegate(nint timeline);

    private readonly NoireHook<LoadTimelineResourcesDelegate>? _timelineResourcesHook;

    // SchedulerTimeline's key and its owner, from the FFXIVClientStructs layout.
    private const int TimelineKeyOffset = 0xA8;

    private const int TimelineOwnerIndexOffset = 0x18C;

    private const uint LocalPlayerObjectIndex = 0;

    // The migratory motion-pack loader; a7 is the timeline key we substitute.
    private const string LoadMigratoryMotionPackSignature = "E9 ?? ?? ?? ?? 8B 84 24 ?? ?? ?? ?? 48 8D 8C 24";

    private delegate nint MotionPackLoaderDelegate(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6,
        nint a7, nint a8);

    private readonly NoireHook<MotionPackLoaderDelegate>? _migratoryHook;

    // The bind-level pack entry, reached on fast rebinds that skip the loader; a4 is the name.
    private const string BindMotionPackSignature =
        "40 55 53 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 8B 85 ?? ?? ?? ?? 4C 8B E9";

    private delegate nint BindMotionPackDelegate(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6,
        nint a7, nint a8, nint a9, nint a10, nint a11, nint a12);

    private readonly NoireHook<BindMotionPackDelegate>? _bindHook;

    // The pack-request get-or-create; its cache is keyed by the name in a3.
    private const string PackRequestSignature =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 30 41 8B F1 49 8B E8 44 8B F2 48 8B F9 E8";

    private delegate nint PackRequestDelegate(nint owner, nint type, nint name, nint variant,
        nint a5, nint a6, nint a7, nint a8);

    private readonly NoireHook<PackRequestDelegate>? _packRequestHook;

    // Only compares names for variant -1; the detour makes that unconditional for our names.
    private const string PackRequestMatchSignature = "39 51 18 75 ?? 8B 41 5C 41 3B C1 75 ?? 83 F8 FF 75 ?? 48 8B 41 20";

    private delegate byte PackRequestMatchDelegate(nint inner, nint type, nint name, nint variant);

    private readonly NoireHook<PackRequestMatchDelegate>? _packMatchHook;

    // The inner slot's strdup'd name pointer.
    private const int InnerSlotNameOffset = 0x20;

    // The C010 animation-name resolver (container, name, ctxArray, count), through the per-character by-name
    // map at container+0x928. The leading 0x53 is the real first byte, some disassemblers report it one high.
    private const string ResolveTimelineNameSignature =
        "53 41 54 41 55 41 56 41 57 48 83 EC 20 48 8B 05";

    private delegate nint ResolveTimelineNameDelegate(nint container, nint name, nint ctxArray, nint count);

    private readonly NoireHook<ResolveTimelineNameDelegate>? _resolveNameHook;

    // Per-timeline-item bind, used to learn which content a TMB stream belongs to.
    private const string TimelineItemBindSignature =
        "48 89 5C 24 18 56 57 41 56 48 83 EC 30 33 DB 48 8B F9 38 99 65 01 00 00";

    private delegate byte TimelineItemBindDelegate(nint item);

    private readonly NoireHook<TimelineItemBindDelegate>? _itemBindHook;

    // Pack-side name walk. Reaches the pressed content's pack earlier than the binding scan does.
    private const string FindPackAnimationSignature =
        "48 89 5C 24 08 48 89 7C 24 10 44 0F B7 91 D8 00 00 00 45 33 C9";

    private delegate nint FindPackAnimationDelegate(nint pack, nint name);

    private readonly NoireHook<FindPackAnimationDelegate>? _findPackAnimationHook;

    // Returns the first pack in the set declaring the animation. Decides what gets displayed.
    private const string BindingScanSignature =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 54 41 56 41 57 48 83 EC 20 "
        + "4C 8B F2 4C 8D 61 08 45 33 FF 0F 1F 84 00 00 00 00 00 33 ED 49 8B F4 66 66 66 0F 1F 84 "
        + "00 00 00 00 00 48 8B 1E 48 8B 7E 08 48 3B DF 74 1D 0F 1F 40 00 48 8B 0B 49 8B D6 E8 85 E9 0F 00";

    private delegate nint BindingScanDelegate(nint packSet, nint name);

    private readonly NoireHook<BindingScanDelegate>? _bindingScanHook;

    // The mapping walk. Hands back the retarget the sampler scatters transforms through, picked off the first
    // pack in the set declaring the name, while the binding scan decides which animation plays.
    private const string MapperSourceSignature =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 20 44 88 44 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 20 "
        + "4C 8B F2 4C 8D 61 08";

    private delegate nint MapperSourceDelegate(nint packSet, nint name, byte flag);

    private readonly NoireHook<MapperSourceDelegate>? _mapperSourceHook;

#if DEBUG
    // Animation control setup: (owner, hkaAnimationBinding, flag, int). The binding carries the animation and
    // the bone mapping it was authored against, so applying one to another skeleton stretches the character.
    private const string AnimationControlSetupSignature =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 48 8B D9 41 8B F9 "
        + "8B 0D ?? ?? ?? ?? 41 0F B6 F0 48 8B EA";

    private delegate nint AnimationControlSetupDelegate(nint owner, nint binding, byte flag, uint arg4);

    private readonly NoireHook<AnimationControlSetupDelegate>? _animationControlHook;

    // The per-slot animation container setup. Its fourth argument is what the mapper is built from, and the
    // installer is skipped outright when it is null.
    private const string ContainerSetupSignature =
        "4C 89 44 24 18 55 57 41 54 41 57 48 8D 6C 24 D9 48 81 EC D8 00 00 00 48 83 B9 48 01 00 00 00";

    private delegate nint ContainerSetupDelegate(nint container, nint a2, nint a3, nint mapSource,
        nint a5, nint a6, byte a7);

    private readonly NoireHook<ContainerSetupDelegate>? _containerSetupHook;
#endif

    // Captured from the first intercepted lookup; 0 until the game does one.
    private nint _management;

    // Resources the resolver answered for our names.
    private readonly HashSet<nint> _ourSwapResources = new();

    // Owner of our character's motion-pack loads, captured where the substitution fires.
    private nint _ourPackOwner;

    // The current scope's source identity: resolved path plus stamp.
    private string? _lastSourceKey;

    // The same identity as it stood at the last execute. The scope is pushed when the manifest is applied,
    // which is earlier, so under fast alternation _lastSourceKey can move before this press's binding scan.
    private volatile string? _pressedSourceKey;

    // Reference-swapped so the detours can read them from any thread.
    private IReadOnlyList<ushort> _redirectedIds = [];
    private HashSet<string> _redirectedPaths = [];

    // Vanilla timeline key mapped to a pinned buffer holding the unique name we substitute.
    private Dictionary<string, nint>? _uniqueNameMap;

    // Never freed: the game may keep the pointer inside the pack it registers. The pool owns that decision
    // and its own lock; this class only decides which names to hand it.
    private static readonly NativeStringPool NamePool = new();

    // The internal animation names the current swap's output paps declare.
    private HashSet<string>? _internalNames;

    // Every name issued this session, so a request built for a previous swap still mismatches. Guarded by a
    // lock of its own: it used to share the pinned buffers' lock, which is an invariant easy to lose.
    private static readonly HashSet<string> IssuedUniqueNames = new(StringComparer.Ordinal);
    private static readonly object IssuedNamesGate = new();

    // The loader's non-name arguments, recorded from real calls so the prewarm can replay them.
    private bool _loaderArgsCaptured;

    // The same arguments, kept only from a load that carried one of our own composed names, so the skeleton
    // the loader builds its path from is this character's. The loose set above takes whatever called last.
    private bool _ourLoaderArgsCaptured;

    private nint _ourLoaderA1, _ourLoaderA2, _ourLoaderA3, _ourLoaderA4, _ourLoaderA5, _ourLoaderA6, _ourLoaderA8;
    private nint _loaderA1, _loaderA2, _loaderA3, _loaderA4, _loaderA5, _loaderA6, _loaderA8;

    // One request walk visits dozens of nodes, so the refusal line is throttled.
    private long _lastRefusalLogTick;

    // Window opened by our own execute: each key substitutes at most once within it.
    private const long SubstituteWindowMs = 500;

    // Open only for the length of one timeline reload, during which the substitution stands down so the game
    // goes back to the file under the target's vanilla name.
    private volatile bool _republishing;

    // The thread that opened the window, so another character's load on another thread is not caught by it.
    private volatile int _republishThreadId;

    private long _republishedWindowStamp;
    private readonly HashSet<string> _republishedThisWindow = new(StringComparer.Ordinal);

    // True only while the local player's timeline is loading its resources. Penumbra holds this character's
    // collection in scope there, so a load issued outside it is attributed to nobody.
    private volatile bool _insideLocalTimelineLoad;

    // The vanilla key that timeline is loading, or null when it is not one of ours.
    private volatile string? _loadingTimelineKey;

    private readonly object _substituteLock = new();
    private readonly HashSet<string> _substitutedThisWindow = new(StringComparer.Ordinal);
    private long _substituteArmedTick;

    // Resolves every hook. The ones logged as parked are created but never enabled here.
    public SchedulerResidencyProbe()
    {
        try
        {
            _hook = new NoireHook<GetCachedScheduleResourceDelegate>(
                GetCachedScheduleResourceSignature, Detour, autoEnable: true, name: "GetCachedScheduleResource");
            NoireLogger.LogDebug("Hooked the scheduler resource cache to capture its manager.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the scheduler resource cache; residency will read as released.", LogPrefix);
            _hook = null;
        }

        try
        {
            _getResourceAsyncHook = new NoireHook<ResourceManager.Delegates.GetResourceAsync>(
                GetResourceAsyncDetour, autoEnable: false, name: "GetResourceAsync");
            NoireLogger.LogDebug("Resolved GetResourceAsync; parked.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook GetResourceAsync; redirected creates will reuse cached handles.", LogPrefix);
            _getResourceAsyncHook = null;
        }

        try
        {
            _timelineResourcesHook = new NoireHook<LoadTimelineResourcesDelegate>(
                LoadTimelineResourcesSignature, TimelineResourcesDetour,
                autoEnable: false, name: "LoadTimelineResources");
            NoireLogger.LogDebug("Resolved the timeline resource load.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the timeline resource load; the vanilla republish is unavailable.", LogPrefix);
            _timelineResourcesHook = null;
        }

        try
        {
            _migratoryHook = new NoireHook<MotionPackLoaderDelegate>(
                LoadMigratoryMotionPackSignature,
                (a1, a2, a3, a4, a5, a6, a7, a8) =>
                {
                    CaptureLoaderArgs(a1, a2, a3, a4, a5, a6, a7, a8);
                    return _migratoryHook!.Original(
                        a1, a2, a3, a4, a5, a6, SubstituteMotionPackName("loader", a1, a7), a8);
                },
                autoEnable: false, name: "LoadMigratoryMotionPack");
            NoireLogger.LogDebug("Resolved LoadMigratoryMotionPack; parked.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook LoadMigratoryMotionPack.", LogPrefix);
            _migratoryHook = null;
        }

        try
        {
            _packRequestHook = new NoireHook<PackRequestDelegate>(
                PackRequestSignature,
                (owner, type, name, variant, a5, a6, a7, a8) => _packRequestHook!.Original(
                    owner, type, SubstituteMotionPackName("request", owner, name), variant, a5, a6, a7, a8),
                autoEnable: true, name: "GetOrCreatePackRequest");
            NoireLogger.LogDebug("Hooked the pack-request get-or-create.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the pack-request get-or-create; the release wait stays on.", LogPrefix);
            _packRequestHook = null;
        }

        try
        {
            _packMatchHook = new NoireHook<PackRequestMatchDelegate>(
                PackRequestMatchSignature, PackRequestMatchDetour,
                autoEnable: true, name: "PackRequestMatch");
            NoireLogger.LogDebug("Hooked the pack-request match check.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the pack-request match check; the release wait stays on.", LogPrefix);
            _packMatchHook = null;
        }

        try
        {
            _bindHook = new NoireHook<BindMotionPackDelegate>(
                BindMotionPackSignature,
                (a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) => _bindHook!.Original(
                    a1, a2, a3, SubstituteMotionPackName("bind", a1, a4), a5, a6, a7, a8, a9, a10, a11, a12),
                autoEnable: false, name: "BindMotionPack");
            NoireLogger.LogDebug("Resolved the bind-level pack entry; parked.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the bind-level pack entry; the release wait stays on.", LogPrefix);
            _bindHook = null;
        }

        try
        {
            _resolveNameHook = new NoireHook<ResolveTimelineNameDelegate>(
                ResolveTimelineNameSignature,
                (container, name, ctxArray, count) =>
                {
                    var result = _resolveNameHook!.Original(container, name, ctxArray, count);
                    NoteResolvedName(name, result);
                    return result;
                },
                autoEnable: true, name: "ResolveTimelineName");
            NoireLogger.LogDebug("Hooked the C010 timeline-name resolver.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the C010 timeline-name resolver.", LogPrefix);
            _resolveNameHook = null;
        }

        try
        {
            _itemBindHook = new NoireHook<TimelineItemBindDelegate>(
                TimelineItemBindSignature,
                item =>
                {
                    var result = _itemBindHook!.Original(item);
                    NoteBoundContent(item);
                    return result;
                },
                autoEnable: true, name: "TimelineItemBind");
            NoireLogger.LogDebug("Hooked the per-item bind.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the per-item bind; pack contents cannot be identified.", LogPrefix);
            _itemBindHook = null;
        }

        try
        {
            _findPackAnimationHook = new NoireHook<FindPackAnimationDelegate>(
                FindPackAnimationSignature,
                (pack, name) =>
                {
                    RememberSeenPack(pack, name);
                    return _findPackAnimationHook!.Original(pack, name);
                },
                autoEnable: true, name: "FindPackAnimationByName");
            NoireLogger.LogDebug("Hooked the pack-side name walk.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the pack-side name walk.", LogPrefix);
            _findPackAnimationHook = null;
        }

        try
        {
            _bindingScanHook = new NoireHook<BindingScanDelegate>(
                BindingScanSignature, BindingScanDetour, autoEnable: true, name: "BindingScan");
            NoireLogger.LogDebug("Hooked the binding scan.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the binding scan; swaps may play the previous emote.", LogPrefix);
            _bindingScanHook = null;
        }

        try
        {
            _mapperSourceHook = new NoireHook<MapperSourceDelegate>(
                MapperSourceSignature, MapperSourceDetour, autoEnable: true, name: "MapperSource");
            NoireLogger.LogDebug("Hooked the mapping walk.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the mapping walk; swaps may deform the character.", LogPrefix);
            _mapperSourceHook = null;
        }

#if DEBUG
        try
        {
            _animationControlHook = new NoireHook<AnimationControlSetupDelegate>(
                AnimationControlSetupSignature,
                (owner, binding, flag, arg4) =>
                {
                    LogAnimationBinding(owner, binding);
                    return _animationControlHook!.Original(owner, binding, flag, arg4);
                },
                autoEnable: false, name: "AnimationControlSetup");
            NoireLogger.LogDebug("Resolved the animation control setup; parked.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the animation control setup; the skeleton trace is off.", LogPrefix);
            _animationControlHook = null;
        }

        try
        {
            _containerSetupHook = new NoireHook<ContainerSetupDelegate>(
                ContainerSetupSignature,
                (container, a2, a3, mapSource, a5, a6, a7) =>
                {
                    LogContainerSetup(container, mapSource, a7);
                    return _containerSetupHook!.Original(container, a2, a3, mapSource, a5, a6, a7);
                },
                autoEnable: false, name: "ContainerSetup");
            NoireLogger.LogDebug("Resolved the animation container setup; parked.", LogPrefix);
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not hook the animation container setup; the mapper trace is off.", LogPrefix);
            _containerSetupHook = null;
        }
#endif

        foreach (var hook in AllHooks())
            hook.Group = HookGroup;

        ApplyLayerSwitches();
    }

    public void ApplyLayerSwitches()
    {
        foreach (var entry in HookPlan())
        {
            if (entry.HasSwitch)
                entry.Hook?.SetEnabled(entry.Wanted);
        }

        var off = new List<string>();
        foreach (var hook in AllHooks())
        {
            if (!hook.IsEnabled)
                off.Add(hook.Name);
        }

        NoireLogger.LogDebug(
            off.Count == 0 ? "Every swap hook is on." : $"Swap hooks off: {string.Join(", ", off)}.", LogPrefix);
    }

    // Name is kept because a signature that never resolved leaves Hook null with nothing else to call it by.
    // HasSwitch tells a layer-controlled hook from one only ever turned off by hand.
    private readonly record struct PlannedHook(string Name, INoireHook? Hook, bool Wanted, bool HasSwitch);

    // Every hook a swap leans on and whether it should be running. The bind door and the no-cache switch are not
    // here: they reach nothing under the current layers, so their being off proves nothing either way.
    private IEnumerable<PlannedHook> HookPlan()
    {
        var uniqueNames = SwapLayers.UniquePackNames;

        yield return new("GetCachedScheduleResource", _hook, true, false);
        yield return new("ResolveTimelineName", _resolveNameHook, true, false);
        yield return new("TimelineItemBind", _itemBindHook, true, false);
        yield return new("FindPackAnimationByName", _findPackAnimationHook, true, false);
        yield return new("BindingScan", _bindingScanHook, true, false);

        yield return new("GetOrCreatePackRequest", _packRequestHook, uniqueNames && SwapLayers.DoorRequest, true);
        yield return new("PackRequestMatch", _packMatchHook, uniqueNames && SwapLayers.MatchEnforcement, true);
        yield return new("MapperSource", _mapperSourceHook, SwapLayers.MappingPackCorrection, true);
        yield return new("LoadTimelineResources", _timelineResourcesHook, SwapLayers.PublishVanillaPath, true);

        // The republish replays this loader and reads its arguments off the same detour, so the capture has
        // to be running before the switch is thrown: it stays up for every composed load.
        yield return new("LoadMigratoryMotionPack", _migratoryHook,
            uniqueNames || SwapLayers.PrewarmPacks || SwapLayers.PublishVanillaPath, true);
    }

    // Every hook installed, skipping the ones whose signature did not resolve.
    private IEnumerable<INoireHook> AllHooks()
    {
        INoireHook?[] hooks =
        [
            _hook, _getResourceAsyncHook, _migratoryHook, _bindHook, _packRequestHook, _packMatchHook,
            _resolveNameHook, _itemBindHook, _findPackAnimationHook, _bindingScanHook, _mapperSourceHook,
            _timelineResourcesHook,
#if DEBUG
            _animationControlHook, _containerSetupHook,
#endif
        ];

        foreach (var hook in hooks)
        {
            if (hook != null)
                yield return hook;
        }
    }

    // File name of a source key's resolved path, stamp dropped.
    private static string ShortSourceName(string sourceKey)
    {
        var stampSeparator = sourceKey.LastIndexOf(':');
        var path = stampSeparator > 1 ? sourceKey[..stampSeparator] : sourceKey; // > 1 keeps 'C:\...' drives intact
        var slash = path.LastIndexOfAny(['\\', '/']);
        return slash < 0 ? path : path[(slash + 1)..];
    }

    // Records which resources belong to the swap currently being played.
    private void NoteResolvedName(nint name, nint result)
    {
        try
        {
            var names = _internalNames;
            if (names == null || name == 0 || result == 0 || !names.Contains(ReadCString(name)))
                return;

            lock (_ourSwapResources)
                _ourSwapResources.Add(result);
        }
        catch
        {
            // Never interfere with the resolve.
        }
    }

    // Flips the no-cache flag for redirected paths so they load fresh instead of from cache.
    private ResourceHandle* GetResourceAsyncDetour(ResourceManager* resourceManager, ResourceCategory* category,
        uint* type, uint* hash, CStringPointer path, void* requestParams, bool noCache, void* a8, uint a9)
    {
        try
        {
            var redirectedPaths = _redirectedPaths;
            if (!noCache && path.HasValue && redirectedPaths.Count > 0
                && SwapLayers.NoCacheFlip)
            {
                var requested = ReadCString((nint)path.Value);
                if (redirectedPaths.Contains(requested))
                {
                    NoireLogger.LogDebug($"Forcing a no-cache fresh load for redirected '{requested}'.", LogPrefix);
                    noCache = true;
                }
            }
        }
        catch
        {
            // The flip must never break the game's own loads.
        }

        return _getResourceAsyncHook!.Original(
            resourceManager, category, type, hash, path, requestParams, noCache, a8, a9);
    }

    // Sets the redirect scope. Called on every manifest change and redirect liveness flip. targetEmoteRowId:
    // Target emote row, or 0 to clear the scope.. uniqueNameByKey: Vanilla timeline key mapped to the unique name
    // to substitute.. sourceKey: Identity of the source content being served..
    public void SetRedirectedScope(uint targetEmoteRowId, IEnumerable<string>? redirectedGamePaths = null,
        IReadOnlyDictionary<string, string>? uniqueNameByKey = null,
        IReadOnlyList<string>? internalNames = null, string? sourceKey = null)
    {
        _redirectedIds = targetEmoteRowId == 0 ? [] : EmoteHelper.GetActionTimelineIds(targetEmoteRowId);
        _redirectedPaths = BuildForcedFreshPathSet(redirectedGamePaths);
        _uniqueNameMap = targetEmoteRowId == 0 || !SwapLayers.UniquePackNames
            ? null
            : BuildUniqueNameMap(uniqueNameByKey);
        _internalNames = targetEmoteRowId == 0 || internalNames is not { Count: > 0 }
            ? null
            : new HashSet<string>(internalNames, StringComparer.Ordinal);

        if (targetEmoteRowId == 0)
        {
            _lastSourceKey = null;
            _pressedSourceKey = null;
        }
        else if (sourceKey != null)
        {
            _lastSourceKey = sourceKey;
        }

        NoireLogger.LogDebug(
            _redirectedIds.Count == 0
                ? "Redirect scope cleared."
                : $"Redirect scope set to emote {targetEmoteRowId} (ids {string.Join(", ", _redirectedIds)}; "
                  + $"{_redirectedPaths.Count} redirected path(s); {_uniqueNameMap?.Count ?? 0} unique name(s); "
                  + $"{_internalNames?.Count ?? 0} internal name(s)).",
            LogPrefix);

        PrewarmUniquePacks(_uniqueNameMap);
    }

    // Every redirected path except TMBs, whose content-addressed names miss the cache anyway.
    internal static HashSet<string> BuildForcedFreshPathSet(IEnumerable<string>? redirectedGamePaths)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in redirectedGamePaths ?? [])
        {
            if (!path.EndsWith(".tmb", StringComparison.OrdinalIgnoreCase))
                set.Add(path);
        }

        return set;
    }

    // A pinned buffer holding a name, from the never-freed pool. Maps each vanilla timeline key to a pinned buffer
    // holding its unique name.
    private static Dictionary<string, nint>? BuildUniqueNameMap(IReadOnlyDictionary<string, string>? uniqueNameByKey)
    {
        if (uniqueNameByKey == null || uniqueNameByKey.Count == 0)
            return null;

        var map = new Dictionary<string, nint>(uniqueNameByKey.Count, StringComparer.Ordinal);

        foreach (var (vanillaKey, uniqueName) in uniqueNameByKey)
        {
            lock (IssuedNamesGate)
                IssuedUniqueNames.Add(uniqueName);

            map[vanillaKey] = NamePool.Pin(uniqueName);
        }

        return map;
    }

    // Marks the span of the local player's own timeline load.
    private ulong TimelineResourcesDetour(nint timeline)
    {
        var key = LocalTimelineKey(timeline);
        if (key == null)
            return _timelineResourcesHook!.Original(timeline);

        var previousInside = _insideLocalTimelineLoad;
        var previousKey = _loadingTimelineKey;

        _insideLocalTimelineLoad = true;
        _loadingTimelineKey = key;
        try
        {
            return _timelineResourcesHook!.Original(timeline);
        }
        finally
        {
            _insideLocalTimelineLoad = previousInside;
            _loadingTimelineKey = previousKey;
        }
    }

    // The key a timeline is playing, when it is the local player's and the swap is served composed. A swap served
    // on the vanilla path has no map entry, so this answers null and nothing is republished.
    private string? LocalTimelineKey(nint timeline)
    {
        try
        {
            var map = _uniqueNameMap;
            if (timeline == 0 || map is not { Count: > 0 })
                return null;

            if (!GuardedMemory.IsReadable(timeline + TimelineOwnerIndexOffset, sizeof(uint))
                || *(uint*)(timeline + TimelineOwnerIndexOffset) != LocalPlayerObjectIndex)
                return null;

            if (!GuardedMemory.TryReadPointer(timeline + TimelineKeyOffset, out var keyPointer) || keyPointer == 0)
                return null;

            var key = ReadCString(keyPointer);
            return map.ContainsKey(key) ? key : null;
        }
        catch
        {
            return null;
        }
    }

    // Loads the target's pack a second time under its vanilla name from inside the timeline load, so Penumbra
    // announces that path for this character. The pack is loaded, never bound.
    private void RepublishVanillaPath(string vanillaKey)
    {
        if (_republishing || _migratoryHook == null)
            return;

        nint a1, a2, a3, a4, a5, a6, a8;
        lock (_substituteLock)
        {
            // A foreign argument set builds the path on the wrong skeleton.
            if (!_ourLoaderArgsCaptured)
            {
                NoireLogger.LogWarning(
                    $"Republish of '{vanillaKey}' skipped: no load of ours captured yet this session.", LogPrefix);
                return;
            }

            var armed = _substituteArmedTick;
            if (armed != _republishedWindowStamp)
            {
                _republishedWindowStamp = armed;
                _republishedThisWindow.Clear();
            }

            if (!_republishedThisWindow.Add(vanillaKey))
                return;

            a1 = _ourLoaderA1; a2 = _ourLoaderA2; a3 = _ourLoaderA3; a4 = _ourLoaderA4;
            a5 = _ourLoaderA5; a6 = _ourLoaderA6; a8 = _ourLoaderA8;
        }

        var vanillaBuffer = NamePool.Pin(vanillaKey);

        _republishThreadId = Environment.CurrentManagedThreadId;
        _republishing = true;
        try
        {
            _ = _migratoryHook.Original(a1, a2, a3, a4, a5, a6, vanillaBuffer, a8);
        }
        finally
        {
            _republishing = false;
        }

        NoireLogger.LogWarning($"Republished '{vanillaKey}' under its vanilla name.", LogPrefix);
    }

    // Opens the substitution window, right before each of our own executes.
    public void ArmNameSubstitution()
    {
        lock (_substituteLock)
        {
            _substitutedThisWindow.Clear();
            _substituteArmedTick = Environment.TickCount64;
            _pressedSourceKey = _lastSourceKey;
        }
    }

    // Records the loader's non-name arguments so the prewarm can replay the call.
    private void CaptureLoaderArgs(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        try
        {
            if (a7 == 0 || a1 == 0)
                return;

            var first = *(byte*)a7;
            if (first < 0x20 || first > 0x7E)
                return;

            var name = ReadCString(a7);

            bool ours;
            lock (IssuedNamesGate)
                ours = IssuedUniqueNames.Contains(name);

            lock (_substituteLock)
            {
                _loaderA1 = a1;
                _loaderA2 = a2;
                _loaderA3 = a3;
                _loaderA4 = a4;
                _loaderA5 = a5;
                _loaderA6 = a6;
                _loaderA8 = a8;
                _loaderArgsCaptured = true;

                if (!ours)
                    return;

                _ourLoaderA1 = a1;
                _ourLoaderA2 = a2;
                _ourLoaderA3 = a3;
                _ourLoaderA4 = a4;
                _ourLoaderA5 = a5;
                _ourLoaderA6 = a6;
                _ourLoaderA8 = a8;
                _ourLoaderArgsCaptured = true;
            }
        }
        catch
        {
        }
    }

    // Loads the scope's packs at scope-set time so they are in the registry before the execute binds. Otherwise
    // the bind lands while the pap is still arriving and the channel keeps the old animation.
    private void PrewarmUniquePacks(Dictionary<string, nint>? map)
    {
        try
        {
            if (map == null || map.Count == 0 || _migratoryHook == null
                || !SwapLayers.PrewarmPacks)
                return;

            nint a1, a2, a3, a4, a5, a6, a8;
            lock (_substituteLock)
            {
                if (!_loaderArgsCaptured)
                {
                    NoireLogger.LogDebug("Prewarm skipped: no loader call captured yet this session.", LogPrefix);
                    return;
                }

                a1 = _loaderA1; a2 = _loaderA2; a3 = _loaderA3; a4 = _loaderA4;
                a5 = _loaderA5; a6 = _loaderA6; a8 = _loaderA8;
            }

            foreach (var (vanillaKey, uniqueBuffer) in map)
            {
                _ = _migratoryHook.Original(a1, a2, a3, a4, a5, a6, uniqueBuffer, a8);
                NoireLogger.LogDebug(
                    $"Prewarmed pack '{ReadCString(uniqueBuffer)}' (for '{vanillaKey}').", LogPrefix);
            }
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Pack prewarm failed; the execute's own load path still applies.", LogPrefix);
        }
    }

    // For our own names the stored name must match exactly; everything else keeps the game's answer.
    private byte PackRequestMatchDetour(nint inner, nint type, nint name, nint variant)
    {
        try
        {
            if (name != 0 && inner != 0)
            {
                var first = *(byte*)name;
                if (first >= 0x20 && first <= 0x7E)
                {
                    var requested = ReadCString(name);

                    bool ours;
                    if (!SwapLayers.MatchEnforcement)
                        return _packMatchHook!.Original(inner, type, name, variant);

                    lock (IssuedNamesGate)
                        ours = IssuedUniqueNames.Contains(requested);

                    if (ours)
                    {
                        var slot = inner + InnerSlotNameOffset;
                        var storedPointer = GuardedMemory.IsReadable(slot, sizeof(nint)) ? *(nint*)slot : 0;
                        var stored = storedPointer == 0 ? null : GuardedMemory.ReadNullTerminated(storedPointer, new byte[PackNameMaxLength]);

                        if (!string.Equals(stored, requested, StringComparison.Ordinal))
                        {
                            if (Environment.TickCount64 - _lastRefusalLogTick > 100)
                            {
                                _lastRefusalLogTick = Environment.TickCount64;
                                NoireLogger.LogDebug(
                                    $"Pack-request match refused: requested '{requested}' vs stored '{stored ?? "<null>"}' (throttled).",
                                    LogPrefix);
                            }

                            return 0;
                        }
                    }
                }
            }
        }
        catch
        {
            // Never break the game's own match; fall through to its answer.
        }

        return _packMatchHook!.Original(inner, type, name, variant);
    }

    private static bool DoorEnabled(string door) => door switch
    {
        "loader" => SwapLayers.DoorLoader,
        "bind" => SwapLayers.DoorBind,
        "request" => SwapLayers.DoorRequest,
        _ => true,
    };

    // Swaps a mapped timeline key for our unique name so the reuse guard misses and the game composes a pap
    // request the mod redirects. Any gate that fails hands back the original name.
    private nint SubstituteMotionPackName(string door, nint a1, nint namePointer)
    {
        try
        {
            // A republish exists to let the vanilla name through untouched.
            if (_republishing && Environment.CurrentManagedThreadId == _republishThreadId)
                return namePointer;

            var map = _uniqueNameMap;
            if (map == null || namePointer == 0 || !DoorEnabled(door))
                return namePointer;

            if (Environment.TickCount64 - _substituteArmedTick > SubstituteWindowMs)
                return namePointer;

            var first = *(byte*)namePointer;
            if (first < 0x20 || first > 0x7E)
                return namePointer;

            var requested = ReadCString(namePointer);
            if (!map.TryGetValue(requested, out var uniqueBuffer))
                return namePointer;

            lock (_substituteLock)
            {
                if (Environment.TickCount64 - _substituteArmedTick > SubstituteWindowMs)
                    return namePointer;

                if (!_substitutedThisWindow.Add(requested))
                    return namePointer;
            }

            // The same object our C010 items carry at [ctx+0x38].
            if (a1 != 0)
                _ourPackOwner = a1;

            NoireLogger.LogDebug(
                $"MotionPack name substituted [{door}]: '{requested}' -> '{ReadCString(uniqueBuffer)}' (a1 0x{a1:X}).",
                LogPrefix);
            return uniqueBuffer;
        }
        catch
        {
            return namePointer; // Never break the load; the vanilla name is always safe.
        }
    }

    // A manifest is applied and every hook of the load chain resolved. A missing link means a rebind could still
    // reach old content, so the caller's release wait has to stay on.
    public bool RedirectionActive
        => _redirectedIds.Count > 0 && _hook != null && _getResourceAsyncHook != null
            && _migratoryHook != null && _bindHook != null && _packRequestHook != null && _packMatchHook != null;

    // A manifest whose redirects are not served substitutes nothing.
    public bool SubstitutionArmed => _uniqueNameMap is { Count: > 0 };

    // Whether every hook a swap needs to land twice on one emote and still play fresh content is up.
    public bool CacheBreakIntact => MissingCacheBreakHooks().Count == 0;

    // What the fresh-content trick is missing, empty when nothing is.
    public string DescribeCacheBreakFault()
    {
        var missing = MissingCacheBreakHooks();

        return missing.Count == 0 ? string.Empty : string.Join(", ", missing);
    }

    // The hooks a swap needs running that are not running, by name. Switched off counts as missing.
    private List<string> MissingCacheBreakHooks()
    {
        var missing = new List<string>();

        foreach (var entry in HookPlan())
        {
            if (!entry.Wanted)
                continue;

            if (entry.Hook is not { IsDisposed: false, IsEnabled: true })
                missing.Add(entry.Name);
        }

        return missing;
    }

    // How far ahead of a stream pointer the fingerprint reads.
    internal const int ChunkWindowForward = 0x400;

    private const int ChunkWalkMaxChunks = 64;
    private const int ChunkTagCap = 24;
    private const int ChunkNameCap = 12;
    private const int ChunkNameMinLength = 6;
    private const int FingerprintNameCap = 3;

    // Which content a TMB stream belongs to. Packs all declare the same name, so shape decides.
    private readonly Dictionary<string, string> _tmbSourceByFingerprint = new(StringComparer.Ordinal);

    private long _learnWindowStamp;
    private readonly HashSet<nint> _learnedThisWindow = new();

    // Fingerprints the TMB stream at a pointer, null when it is not a readable stream header.
    private static string? FingerprintStreamAt(nint resource)
    {
        if (resource == 0)
            return null;

        var buffer = new byte[ChunkWindowForward];
        var read = GuardedMemory.ReadSpan(resource, buffer, 0, ChunkWindowForward);
        if (read == 0)
            return null;

        var data = read == buffer.Length ? buffer : buffer[..read];
        if (!TmbChunkScanner.TryReadStreamHeader(data, 0, out var totalSize, out var entryCount))
            return null;

        var end = Math.Min(data.Length, totalSize);
        var chunks = TmbChunkScanner.Walk(data, TmbChunkScanner.StreamHeaderSize, ChunkWalkMaxChunks, end);
        var names = TmbChunkScanner.FindNames(data, 0, end, ChunkNameMinLength, ChunkNameCap);
        return TmbChunkScanner.Fingerprint(totalSize, entryCount, chunks, names, ChunkTagCap, FingerprintNameCap);
    }

    private string? SourceOfFingerprint(string? fingerprint)
    {
        if (fingerprint == null)
            return null;

        lock (_tmbSourceByFingerprint)
            return _tmbSourceByFingerprint.TryGetValue(fingerprint, out var source) ? source : null;
    }

    // Ties the data one of our C010 items resolved to the content being played.
    private void NoteBoundContent(nint item)
    {
        try
        {
            var pressedKey = _pressedSourceKey;
            if (item == 0 || _internalNames == null || pressedKey == null)
                return;

            // C010 items only.
            if (*(uint*)(item + 0x84) != 7)
                return;

            var resolved = *(nint*)(item + 0x148);
            if (resolved == 0)
                return;

            // [item+0x28] is the lazily cached ctx, [ctx+0x38] the container it resolved through.
            var ctx = *(nint*)(item + 0x28);
            var container = GuardedMemory.TryReadPointer(ctx + 0x38, out var owner) ? owner : 0;

            bool ours;
            lock (_ourSwapResources)
                ours = _ourSwapResources.Contains(resolved);

            if (!ours && (container == 0 || container != _ourPackOwner))
                return;

            var armed = _substituteArmedTick;
            if (armed != _learnWindowStamp)
            {
                _learnWindowStamp = armed;
                _learnedThisWindow.Clear();
            }

            if (!_learnedThisWindow.Add(resolved))
                return;

            var fingerprint = FingerprintStreamAt(resolved);
            if (fingerprint == null)
                return;

            var pressed = ShortSourceName(pressedKey);
            lock (_tmbSourceByFingerprint)
                _tmbSourceByFingerprint.TryAdd(fingerprint, pressed);
        }
        catch
        {
            // Never interfere with the bind.
        }
    }

    // How long after an execute a diagnostic line is still worth writing.
    private const long DiagnosticWindowMs = 1500;

    private const int MapperSourceLogsPerPress = 8;

    private long _mapperSourceWindowStamp;
    private int _mapperSourceCount;

    // Armed, inside the window and under the cap. True takes one slot.
    private bool TakeLogSlot(ref long windowStamp, ref int count, int cap)
        => BurstWindow.TryTake(_substituteArmedTick, Environment.TickCount64,
            ref windowStamp, ref count, cap, DiagnosticWindowMs);

#if DEBUG
    private const int AnimationBindingDumpSize = BindingView.HeadSize;
    private const int AnimationBindingDumpsPerPress = 24;
    private const int AnimationBindingNameMin = 4;
    private const int AnimationBindingNameMax = 64;
    // What the body binding's own name ends with; a face or a material never carries it.
    private const string BodyBindingMarker = "_0:mdl:n_root";

    private const int TrackIndexHeadCount = 24;
    private const int TrackIndexTailCount = 8;
    private const int TrackIndexMaxBytes = 1024;

    private const int ContainerSetupLogsPerPress = 8;

    private long _bindingDumpWindowStamp;
    private int _bindingDumpCount;

    private long _containerSetupWindowStamp;
    private int _containerSetupCount;

    // A null mapping argument means no mapper is installed: tracks then land on bones one for one.
    private void LogContainerSetup(nint container, nint mapSource, byte flag)
    {
        try
        {
            if (_internalNames == null)
                return;

            if (!TakeLogSlot(ref _containerSetupWindowStamp, ref _containerSetupCount, ContainerSetupLogsPerPress))
                return;

            NoireLogger.LogWarning(
                $"Container setup #{_containerSetupCount} 0x{container:X}: mapArg 0x{mapSource:X} flag {flag}.",
                LogPrefix);
        }
        catch
        {
            // Diagnostic only.
        }
    }

    // The ends of a binding's track-to-bone index array, empty when it has none.
    private static string ReadTrackIndices(byte[] head, int read)
    {
        var pointer = BindingView.TrackIndicesPointer(head, read);
        var count = BindingView.TrackCount(head, read);
        if (pointer == 0 || count <= 0)
            return string.Empty;

        var wanted = count > TrackIndexMaxBytes / sizeof(short) ? TrackIndexMaxBytes : count * sizeof(short);
        var buffer = new byte[wanted];
        var got = GuardedMemory.ReadSpan((nint)pointer, buffer, 0, wanted);
        return BindingView.FormatIndices(buffer, got, TrackIndexHeadCount, TrackIndexTailCount);
    }

    // Dumps the animation binding the game is about to install, a few times per press.
    private void LogAnimationBinding(nint owner, nint binding)
    {
        try
        {
            if (binding == 0 || _internalNames == null)
                return;

            var armed = _substituteArmedTick;
            if (armed == 0)
                return;

            if (armed != _bindingDumpWindowStamp)
            {
                _bindingDumpWindowStamp = armed;
                _bindingDumpCount = 0;
            }

            if (_bindingDumpCount >= AnimationBindingDumpsPerPress)
                return;

            var sinceExecute = Environment.TickCount64 - armed;
            if (sinceExecute < 0 || sinceExecute > DiagnosticWindowMs)
                return;

            var head = new byte[AnimationBindingDumpSize];
            var read = GuardedMemory.ReadSpan(binding, head, 0, AnimationBindingDumpSize);
            if (read < sizeof(nint))
            {
                NoireLogger.LogWarning($"Animation binding 0x{binding:X}: unreadable.", LogPrefix);
                return;
            }

            _bindingDumpCount++;

            var strings = new StringBuilder();
            for (var offset = 0; offset + sizeof(nint) <= read; offset += sizeof(nint))
            {
                var name = GuardedMemory.ReadPrintableRun((nint)BitConverter.ToInt64(head, offset), AnimationBindingNameMax);
                if (name.Length < AnimationBindingNameMin)
                    continue;

                if (strings.Length > 0)
                    strings.Append(", ");

                strings.Append($"+0x{offset:X} '{name}'");
            }

            // The blend hint decides whether the animation replaces the pose or adds to it.
            var tracks = BindingView.TrackCount(head, read);
            var blend = BindingView.BlendHint(head, read);
            var indices = ReadTrackIndices(head, read);

            // The body's own binding names the skeleton it was built against, as "<skeleton>_0:mdl:n_root".
            var body = strings.ToString().Contains(BodyBindingMarker, StringComparison.Ordinal) ? " BODY" : "";

            var pressedKey = _pressedSourceKey;
            NoireLogger.LogWarning(
                $"Animation binding #{_bindingDumpCount}{body} 0x{binding:X} owner 0x{owner:X} at +{sinceExecute}ms "
                + $"playing [{(pressedKey == null ? "none" : ShortSourceName(pressedKey))}]: {tracks} track(s), "
                + $"blend {blend}, bones [" + (indices.Length == 0 ? "none" : indices)
                + $"], strings [" + (strings.Length == 0 ? "none" : strings.ToString())
                + $"]; head {GuardedMemory.ToHexDump(head, read)}.",
                LogPrefix);
        }
        catch
        {
            // Diagnostic only.
        }
    }
#endif

    private static nint PackAnimationAt(nint pack, int index)
    {
        if (index < 0 || !GuardedMemory.TryReadPointer(pack + PackScanLayout.PackAnimationTableOffset, out var table))
            return 0;

        return GuardedMemory.TryReadPointer(PackScanLayout.PackAnimationSlot(table, index), out var entry) ? entry : 0;
    }

    private static nint PackHavokAnimationAt(nint pack, int index)
    {
        if (index < 0 || !GuardedMemory.TryReadPointer(pack + PackScanLayout.PackHavokHolderOffset, out var holder))
            return 0;

        if (!GuardedMemory.TryReadPointer(PackScanLayout.PackHavokTable(holder), out var table))
            return 0;

        return GuardedMemory.TryReadPointer(PackScanLayout.PackAnimationSlot(table, index), out var animation) ? animation : 0;
    }

    // Index of a name in a pack's table, or -1. Guarded one entry at a time: a large table can straddle a region
    // boundary, and a pack freed since it was recorded leaves its memory mapped but its contents junk.
    private static int IndexOfPackNamePerEntry(nint pack, string name)
    {
        if (pack == 0 || !GuardedMemory.IsReadable(pack + PackScanLayout.PackNameCountOffset, sizeof(ushort)))
            return -1;

        int count = *(ushort*)(pack + PackScanLayout.PackNameCountOffset);
        if (!GuardedMemory.TryReadPointer(pack + PackScanLayout.PackNameTableOffset, out var table))
            return -1;

        if (!PackScanLayout.IsPlausiblePackNameTable(count, table))
            return -1;

        var buffer = new byte[PackScanLayout.PackNameEntryStride];
        for (var i = 0; i < count; i++)
        {
            // The table base is the pointer at the field, and each entry carries its name inline.
            var entry = (nint)PackScanLayout.PackNameEntry(table, i);
            if (!GuardedMemory.IsReadable(entry, PackScanLayout.PackNameEntryStride))
                continue;

            if (string.Equals(GuardedMemory.ReadNullTerminated(entry, buffer), name, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    // Longest pack name worth comparing; the game's are far shorter.
    private const int PackNameMaxLength = 128;

    // Packs the game handed to its own name walk this press. The binding scan's set lags, but the resolver
    // reaches the right pack tens of milliseconds earlier. Every use re-validates the pointer.
    private readonly List<nint> _seenPacks = new();

    private long _seenPacksWindowStamp;

    private const int SeenPackCap = 16;

    // Every character in the zone walks this same function, so only our own names get in. A full list drops
    // its oldest entry rather than the newest, since our own load lands after the crowd's.
    private void RememberSeenPack(nint pack, nint name)
    {
        if (pack == 0 || name == 0)
            return;

        var names = _internalNames;
        if (names == null)
            return;

        var armed = _substituteArmedTick;
        if (armed == 0 || Environment.TickCount64 - armed > SubstituteWindowMs)
            return;

        if (!names.Contains(ReadCString(name)))
            return;

        lock (_seenPacks)
        {
            if (armed != _seenPacksWindowStamp)
            {
                _seenPacksWindowStamp = armed;
                _seenPacks.Clear();
            }

            if (_seenPacks.Contains(pack))
                return;

            if (_seenPacks.Count >= SeenPackCap)
                _seenPacks.RemoveAt(0);

            _seenPacks.Add(pack);
        }
    }

    private nint[] SeenPacksSnapshot()
    {
        lock (_seenPacks)
            return _seenPacks.ToArray();
    }

    private nint BindingScanDetour(nint packSet, nint name)
    {
        var pack = _bindingScanHook!.Original(packSet, name);
        try
        {
            // A character has one pack set per animation container, so every set the scan walks is kept.
            RememberPackSet(packSet);

            var names = _internalNames;
            if (names == null || name == 0 || pack == 0)
                return pack;

            var text = ReadCString(name);
            if (!names.Contains(text))
                return pack;

            var corrected = CorrectBindingPack(packSet, text, pack, out var note);
            if (note.Length > 0)
                NoireLogger.LogWarning($"Binding scan for '{text}': {note}.", LogPrefix);

            // Recorded corrected or not, so the mapper can follow the animation that plays.
            RememberBoundPack(packSet, text, corrected, pack);
            return corrected;
        }
        catch
        {
            return pack;
        }
    }

    // Picks the pack whose content is being played instead of the first in the game's list. Our packs all declare
    // the same name, so with two resident the game's answer comes down to list order. chosen: The pack the game's
    // own scan returned.. note: Empty when nothing changed, otherwise a description for the log.. The pack to
    // bind.
    private nint CorrectBindingPack(nint packSet, string text, nint chosen, out string note)
    {
        note = string.Empty;
        var pressedKey = _pressedSourceKey;
        if (!SwapLayers.BindingPackCorrection || chosen == 0 || pressedKey == null)
            return chosen;

        var pressed = ShortSourceName(pressedKey);
        var chosenSource = SourceOfPackContent(chosen, text);
        if (chosenSource == null)
        {
            // Nothing has named what this pack holds, so two of ours cannot be told apart and list order wins.
            if (TakeBindingLogSlot())
                NoireLogger.LogWarning(
                    $"Binding scan for '{text}': no content map for pack 0x{chosen:X}, [{pressed}] kept on the "
                    + "game's own choice.", LogPrefix);

            return chosen;
        }

        if (string.Equals(chosenSource, pressed, StringComparison.Ordinal))
            return chosen;

        for (var group = 0; group < PackScanLayout.Groups; group++)
        {
            for (var slot = 0; slot < PackScanLayout.SlotsPerGroup; slot++)
            {
                var vector = PackScanLayout.VectorAddress(packSet, group, slot);
                if (!GuardedMemory.TryReadPointer(vector, out var begin) || !GuardedMemory.TryReadPointer(vector + 8, out var end))
                    continue;

                if (!PackScanLayout.IsPlausibleVector(begin, end))
                    continue;

                var count = PackScanLayout.PackCount(begin, end);
                for (var index = 0; index < count; index++)
                {
                    if (!GuardedMemory.TryReadPointer(begin + index * 8, out var candidate) || candidate == 0
                        || candidate == chosen)
                        continue;

                    if (!string.Equals(SourceOfPackContent(candidate, text), pressed, StringComparison.Ordinal))
                        continue;

                    note = $"corrected from [{chosenSource}] pack 0x{chosen:X} -> pack 0x{candidate:X}";
                    return candidate;
                }
            }
        }

        foreach (var candidate in SeenPacksSnapshot())
        {
            if (candidate == 0 || candidate == chosen)
                continue;

            if (!string.Equals(SourceOfPackContent(candidate, text), pressed, StringComparison.Ordinal))
                continue;

            // It must still declare the name and hold a havok animation for it.
            if (PackHavokAnimationAt(candidate, IndexOfPackNamePerEntry(candidate, text)) == 0)
                continue;

            note = $"corrected from [{chosenSource}] pack 0x{chosen:X} -> pack 0x{candidate:X} (seen this press)";
            return candidate;
        }

        note = $"not correctable, first match is [{chosenSource}] and nothing resident holds [{pressed}]";
        return chosen;
    }

    // The pack sets the binding scan has walked, other characters' included. Packs live in one until the game
    // drops them, which is what a composed name works around.
    private readonly List<nint> _packSets = new();

    private const int PackSetCap = 16;

    private void RememberPackSet(nint packSet)
    {
        if (packSet == 0)
            return;

        lock (_packSets)
        {
            if (_packSets.Contains(packSet))
                return;

            if (_packSets.Count >= PackSetCap)
                _packSets.RemoveAt(0);

            _packSets.Add(packSet);
        }
    }

    // Whether a pack declaring any of these names still sits in one of the pack sets seen so far. names: The
    // animation names a leftover pack would declare.. trace: How many sets were held and how many packs the walk
    // read.. Null before any pack set has been seen, so the caller can decide for itself.
    public bool? AnyPackNameResident(IReadOnlyList<string> names, out string trace)
    {
        nint[] packSets;
        lock (_packSets)
            packSets = _packSets.ToArray();

        trace = $"{packSets.Length} set(s), 0 pack(s) read";

        if (packSets.Length == 0 || names.Count == 0)
            return null;

        var packsRead = 0;

        try
        {
            foreach (var packSet in packSets)
            {
                foreach (var name in names)
                {
                    if (IsNameInPackSet(packSet, name, ref packsRead))
                    {
                        trace = $"{packSets.Length} set(s), {packsRead} pack(s) read";
                        return true;
                    }
                }
            }

            trace = $"{packSets.Length} set(s), {packsRead} pack(s) read";
            return false;
        }
        catch (Exception ex)
        {
            // Reading it as resident only costs a composed name, which always plays.
            NoireLogger.LogError(ex, "Could not walk the pack sets; reading the name as resident.", LogPrefix);
            trace = $"{packSets.Length} set(s), {packsRead} pack(s) read, walk failed";
            return true;
        }
    }

    // The same guarded walk the binding correction uses, asking only whether the name is declared. No havok
    // animation is required: a pack answers the game's name walk on the name alone.
    private static bool IsNameInPackSet(nint packSet, string name, ref int packsRead)
    {
        for (var group = 0; group < PackScanLayout.Groups; group++)
        {
            for (var slot = 0; slot < PackScanLayout.SlotsPerGroup; slot++)
            {
                var vector = PackScanLayout.VectorAddress(packSet, group, slot);
                if (!GuardedMemory.TryReadPointer(vector, out var begin) || !GuardedMemory.TryReadPointer(vector + 8, out var end))
                    continue;

                if (!PackScanLayout.IsPlausibleVector(begin, end))
                    continue;

                var count = PackScanLayout.PackCount(begin, end);
                for (var index = 0; index < count; index++)
                {
                    if (!GuardedMemory.TryReadPointer(begin + index * 8, out var pack) || pack == 0)
                        continue;

                    packsRead++;

                    if (IndexOfPackNamePerEntry(pack, name) >= 0)
                        return true;
                }
            }
        }

        return false;
    }

    // Which content a pack belongs to, via its TMB stream's fingerprint.
    private string? SourceOfPackContent(nint pack, string text)
    {
        var index = IndexOfPackNamePerEntry(pack, text);
        if (index < 0)
            return null;

        var timeline = PackAnimationAt(pack, index);
        return timeline == 0 ? null : SourceOfFingerprint(FingerprintStreamAt(timeline));
    }

    // Set when the self-check fails; the correction then stands down for the rest of the session.
    private volatile bool _replicaDisarmed;

    private const int BoundPackCap = 8;

    // Per press: the pack that ends up bound, beside the one the game's own scan chose.
    private readonly Dictionary<(nint PackSet, string Name), (nint Bound, nint GameChose)> _boundPacks = new();

    private long _boundPacksWindowStamp;

    // Per press: what the mapping walk was answered, so the arithmetic runs once per name.
    private readonly Dictionary<(nint PackSet, string Name, byte Flag), nint> _mapperSourceCache = new();

    private long _mapperSourceCacheStamp;

    private void RememberBoundPack(nint packSet, string text, nint bound, nint gameChose)
    {
        lock (_boundPacks)
        {
            var armed = _substituteArmedTick;
            if (armed != _boundPacksWindowStamp)
            {
                _boundPacksWindowStamp = armed;
                _boundPacks.Clear();
            }

            var key = (packSet, text);
            if (_boundPacks.Count >= BoundPackCap && !_boundPacks.ContainsKey(key))
                return;

            _boundPacks[key] = (bound, gameChose);
        }
    }

    // The pack bound for a name this press; false when the binding scan has not named one. bound: The pack that
    // will play.. gameChose: The pack the game's own scan returned before any correction.. True when a record for
    // this press exists.
    private bool TryBoundPack(nint packSet, string text, out nint bound, out nint gameChose)
    {
        bound = 0;
        gameChose = 0;

        lock (_boundPacks)
        {
            if (_substituteArmedTick != _boundPacksWindowStamp
                || !_boundPacks.TryGetValue((packSet, text), out var record))
                return false;

            bound = record.Bound;
            gameChose = record.GameChose;
            return true;
        }
    }

    private const int BindingScanLogsPerPress = 4;

    private long _bindingScanWindowStamp;
    private int _bindingScanCount;

    private bool TakeBindingLogSlot()
        => TakeLogSlot(ref _bindingScanWindowStamp, ref _bindingScanCount, BindingScanLogsPerPress);

    private bool TakeMapperLogSlot()
        => TakeLogSlot(ref _mapperSourceWindowStamp, ref _mapperSourceCount, MapperSourceLogsPerPress);

    // Keeps the game's answer and says why, so a missing input is never taken for a correction.
    private nint DeclineMapperSource(string text, string reason, nint original)
    {
        if (TakeMapperLogSlot())
            NoireLogger.LogWarning(
                $"Mapping source declined for '{text}': {reason}; kept mapper 0x{original:X}.", LogPrefix);

        return original;
    }

    // The keys a skeleton's retarget maps were built from.
    private static string ChainKeys(nint owner)
    {
        const int span = MapperTreeLayout.SkeletonChainCount * sizeof(uint);
        if (!GuardedMemory.IsReadable(owner + MapperTreeLayout.SkeletonChainOffset, span))
            return "unreadable";

        var text = new StringBuilder();
        for (var i = 0; i < MapperTreeLayout.SkeletonChainCount; i++)
        {
            if (text.Length > 0)
                text.Append(' ');

            var key = *(uint*)(owner + MapperTreeLayout.SkeletonChainOffset + i * sizeof(uint));
            text.Append($"0x{key:X8}");
        }

        return text.ToString();
    }

    // Hands the mapper installer the retarget the playing animation calls for. Any gate that fails hands back the
    // game's own answer untouched.
    private nint MapperSourceDetour(nint packSet, nint name, byte flag)
    {
        var original = _mapperSourceHook!.Original(packSet, name, flag);

        try
        {
            // This walk runs for every character in the zone, so the cheapest gate comes first.
            var armed = _substituteArmedTick;
            if (armed == 0)
                return original;

            var sinceExecute = Environment.TickCount64 - armed;
            if (sinceExecute < 0 || sinceExecute > SubstituteWindowMs)
                return original;

            if (!SwapLayers.MappingPackCorrection || _replicaDisarmed)
                return original;

            var names = _internalNames;
            if (names == null || packSet == 0 || name == 0)
                return original;

            var text = ReadCString(name);
            if (!names.Contains(text))
            {
                // Print the name this walk is keyed on before refusing.
                if (TakeMapperLogSlot())
                    NoireLogger.LogWarning(
                        $"Mapper source asked for '{text}' (flag {flag}, returned 0x{original:X}), "
                        + "which is not one of ours; left alone.", LogPrefix);

                return original;
            }

            return CorrectMapperSource(packSet, text, flag, original);
        }
        catch
        {
            return original;
        }
    }

    // Re-derives the retarget from the pack the binding scan settled on instead of the one the game's walk stopped
    // at. Nothing is written, and every computed address goes through the guard. flag: The walk's third argument,
    // which decides whether the primary map is consulted.. original: What the walk returned.. The retarget to
    // install.
    private nint CorrectMapperSource(nint packSet, string text, byte flag, nint original)
    {
        lock (_mapperSourceCache)
        {
            var armed = _substituteArmedTick;
            if (armed != _mapperSourceCacheStamp)
            {
                _mapperSourceCacheStamp = armed;
                _mapperSourceCache.Clear();
            }
            else if (_mapperSourceCache.TryGetValue((packSet, text, flag), out var cached))
            {
                return cached;
            }
        }

        if (!TryBoundPack(packSet, text, out var bound, out var gameChose))
            return DeclineMapperSource(text, "the binding scan named no pack this press", original);

        if (bound == 0)
            return DeclineMapperSource(text, "the bound pack is null", original);

        if (!GuardedMemory.IsReadable(bound + MapperTreeLayout.PackSkeletonKeyOffset, sizeof(uint)))
            return DeclineMapperSource(text, $"pack 0x{bound:X} carries no readable skeleton key", original);

        var packKey = *(uint*)(bound + MapperTreeLayout.PackSkeletonKeyOffset);

        if (!GuardedMemory.IsReadable(packSet, sizeof(nint)))
            return DeclineMapperSource(text, "the pack set names no readable skeleton", original);

        var owner = *(nint*)packSet;
        if (owner == 0 || !GuardedMemory.IsReadable(owner + MapperTreeLayout.OwnerSkeletonKeyOffset, sizeof(uint)))
            return DeclineMapperSource(text, "the skeleton carries no readable key of its own", original);

        var ownerKey = *(uint*)(owner + MapperTreeLayout.OwnerSkeletonKeyOffset);
        var computed = MapperTreeLayout.Select(ProcessGuardedMemory.Instance, owner, ownerKey, packKey, flag);
        var answer = ActOnMapperSource(computed, text, flag, original, bound, gameChose, owner, packKey, ownerKey);

        lock (_mapperSourceCache)
        {
            if (_substituteArmedTick == _mapperSourceCacheStamp && _mapperSourceCache.Count < BoundPackCap)
                _mapperSourceCache[(packSet, text, flag)] = answer;
        }

        return answer;
    }

    // Carries out the decision, prints what it was made from, and returns the retarget to install.
    private nint ActOnMapperSource(MapperSource computed, string text, byte flag, nint original,
        nint bound, nint gameChose, nint owner, uint packKey, uint ownerKey)
    {
        var pressedKey = _pressedSourceKey;
        var pressed = pressedKey == null ? "none" : ShortSourceName(pressedKey);

        switch (MapperTreeLayout.Decide(computed, original, bound == gameChose))
        {
            case MapperAction.Disarm:
                _replicaDisarmed = true;
                if (TakeMapperLogSlot())
                    NoireLogger.LogWarning(
                        $"Mapping source for '{text}': replica disagrees on pack 0x{bound:X} "
                        + $"(game 0x{original:X}, ours 0x{computed.Value:X}, outcome {computed.Outcome}); "
                        + "correction disarmed for this session.",
                        LogPrefix);
                return original;

            case MapperAction.InstallFound:
                if (TakeMapperLogSlot())
                    NoireLogger.LogWarning(
                        $"Mapping source for '{text}': playing [{pressed}] off pack 0x{bound:X}, pack key "
                        + $"0x{packKey:X8} vs skeleton key 0x{ownerKey:X8}; mapper 0x{original:X} -> "
                        + $"0x{computed.Value:X}.",
                        LogPrefix);
                return (nint)computed.Value;

            case MapperAction.InstallNone when computed.Outcome == MapperSourceOutcome.Native:
                if (TakeMapperLogSlot())
                    NoireLogger.LogWarning(
                        $"Mapping source for '{text}': playing [{pressed}] off pack 0x{bound:X}, pack key "
                        + $"0x{packKey:X8} is the skeleton's own; mapper 0x{original:X} -> none.",
                        LogPrefix);
                return 0;

            case MapperAction.InstallNone:
                if (TakeMapperLogSlot())
                    NoireLogger.LogWarning(
                        $"Mapping source for '{text}': skeleton 0x{owner:X} holds no retarget for pack key "
                        + $"0x{packKey:X8} (its own key is 0x{ownerKey:X8}, chain {ChainKeys(owner)}); "
                        + "installing none.",
                        LogPrefix);
                return 0;

            default:
                if (computed.Outcome == MapperSourceOutcome.Unreadable)
                {
                    if (TakeMapperLogSlot())
                        NoireLogger.LogWarning(
                            $"Mapping source declined for '{text}': the retarget maps of skeleton 0x{owner:X} "
                            + $"are unreadable (pack key 0x{packKey:X8}, its own key 0x{ownerKey:X8}, chain "
                            + $"{ChainKeys(owner)}); kept mapper 0x{original:X}.",
                            LogPrefix);
                }
                else if (TakeMapperLogSlot())
                {
                    var same = bound == gameChose ? "the game's own pack" : "a pack the game's walk passed";
                    NoireLogger.LogDebug(
                        $"Mapping source for '{text}': already agrees with pack 0x{bound:X} ({same}), "
                        + $"mapper 0x{original:X}, flag {flag}.",
                        LogPrefix);
                }

                return original;
        }
    }

    // Only for pointers the game handed us directly, such as a hook argument. AccessViolationException is not
    // catchable in.NET, so the try below cannot save a bad pointer. Anything read out of a structure goes through
    // ReadGuardedAscii instead.
    private static string ReadCString(nint pointer)
    {
        try
        {
            var bytes = new List<byte>(64);
            for (var i = 0; i < 256; i++)
            {
                var b = *(byte*)(pointer + i);
                if (b == 0)
                    break;
                bytes.Add(b);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }
        catch
        {
            return "<unreadable>";
        }
    }

    private nint Detour(nint management, nint loadData, byte useMap)
    {
        _management = management;

        var result = _hook!.Original(management, loadData, useMap);

        // Penumbra has our collection in scope inside the timeline load, so this is the only moment a
        // republish can be announced for this character.
        if (_insideLocalTimelineLoad && !_republishing && SwapLayers.PublishVanillaPath
            && _loadingTimelineKey is { } key)
        {
            try
            {
                RepublishVanillaPath(key);
            }
            catch
            {
                // Must never break the scheduler's own lookup.
            }
        }

        return result;
    }

    // Whether the lookup can answer: it needs its hook installed and the management pointer captured.
    public bool ResidencyReadable => _hook is not null && _management != 0;

    public bool AnyResidentIds(IReadOnlyList<ushort> timelineIds)
    {
        foreach (var id in timelineIds)
        {
            if (IsResidentById(id))
                return true;
        }

        return false;
    }

    private const int ConsumersOffset = 0x7C;

    // Asks the game's own lookup. Any failure reads as not resident.
    public bool IsResidentById(ushort timelineId)
        => ConsumersOf(timelineId) > 0;

    // The live consumer count of one ActionTimeline row: how many channels still hold the scheduler entry. It
    // falls to zero as the outgoing animation's fade ends. The count, 0 for an entry nothing consumes, or -1 when
    // the lookup could not be made at all.
    public int ConsumersOf(ushort timelineId)
    {
        if (_hook is null || _management == 0 || timelineId == 0)
            return -1;

        try
        {
            // The id goes in as a number, not as text: a non -1 Id with useMap 0 sends the lookup down the
            // ID-map walk (the tree at management+0x10, comparing the id at node+0x20). Passing -1 sends it
            // down the name walk, where a row id rendered as decimal matches nothing.
            var empty = stackalloc byte[1];
            empty[0] = 0;

            var loadData = stackalloc byte[16];
            *(nint*)loadData = (nint)empty;
            *(uint*)(loadData + 8) = timelineId;

            var resource = _hook.Original(_management, (nint)loadData, 0);
            if (resource == 0 || !GuardedMemory.IsReadable(resource + ConsumersOffset, sizeof(int)))
                return 0;

            // An absurd count means the offset drifted after a patch, and that reads as released.
            var consumers = *(int*)(resource + ConsumersOffset);
            return consumers > 0 && consumers <= 100_000 ? consumers : 0;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, $"Residency lookup for timeline {timelineId} failed; reading as released.", LogPrefix);
            return -1;
        }
    }
}
