using BypassEmote.Helpers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BypassEmote;

public partial class Service
{
    private const string EmoteAddonName = "Emote";
    private const string ContextMenuAddonName = "ContextMenu";

    private static readonly string[] ActionBarAddons =
    [
        "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04",
        "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09",
        "_ActionCross",
    ];

    private static readonly HashSet<uint> LockedEmoteIds = [];
    private static readonly HashSet<string> LockedEmoteNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<uint> LockedEmotesByOrder = [];
    private static readonly List<uint> PendingHistory = [];
    private static readonly Dictionary<uint, nint> LockedEmoteCommands = [];
    private static readonly Dictionary<uint, string[]> LockedEmoteSearchTerms = [];

    private static bool hotbarSlotsLit;

    private static IDisposable? emoteAddonSetup;
    private static IDisposable? emoteAddonRefresh;
    private static IDisposable? contextMenuSetup;
    private static IDisposable? contextMenuRefresh;

    private static void StartEmoteUi()
    {
        NoireService.Framework.Update += OnEmoteUiFrame;

        emoteAddonSetup = AddonHelper.RegisterLifecycleListener(
            AddonEvent.PreSetup, EmoteAddonName, OnEmoteAddonValues);

        emoteAddonRefresh = AddonHelper.RegisterLifecycleListener(
            AddonEvent.PreRefresh, EmoteAddonName, OnEmoteAddonValues);

        contextMenuSetup = AddonHelper.RegisterLifecycleListener(
            AddonEvent.PreSetup, ContextMenuAddonName, OnContextMenuValues);

        contextMenuRefresh = AddonHelper.RegisterLifecycleListener(
            AddonEvent.PreRefresh, ContextMenuAddonName, OnContextMenuValues);
    }

    private static void StopEmoteUi()
    {
        NoireService.Framework.Update -= OnEmoteUiFrame;

        emoteAddonSetup?.Dispose();
        emoteAddonSetup = null;

        emoteAddonRefresh?.Dispose();
        emoteAddonRefresh = null;

        contextMenuSetup?.Dispose();
        contextMenuSetup = null;

        contextMenuRefresh?.Dispose();
        contextMenuRefresh = null;

        if (hotbarSlotsLit)
            PaintHotbarSlots(lightUp: false);

        if (!EmoteWindowExists())
            ReleaseLockedEmoteCommands();
    }

    private static void ClearEmoteUiState()
    {
        LockedEmoteIds.Clear();
        LockedEmoteNames.Clear();
        LockedEmotesByOrder.Clear();
        LockedEmoteSearchTerms.Clear();
        PendingHistory.Clear();
    }

    private static void OnEmoteUiFrame(IFramework framework)
    {
        if (PendingHistory.Count > 0 && !EmoteWindowIsOpen())
            FlushPendingHistory();

        var wanted = Configuration.PluginEnabled && Configuration.ShowLockedEmotesAsUsable
            && LockedEmoteIds.Count > 0 && NoireService.ClientState.IsLoggedIn;

        if (wanted || hotbarSlotsLit)
            PaintHotbarSlots(wanted);

        PaintEmoteWindowRows();
        EnableContextMenuExecute();
    }

    private static unsafe void OnEmoteAddonValues(AddonEvent type, AddonArgs args)
    {
        if (!Configuration.PluginEnabled || LockedEmoteIds.Count == 0)
            return;

        AtkValue* values = null;
        var count = 0u;

        switch (args)
        {
            case AddonSetupArgs setup:
                values = (AtkValue*)setup.AtkValues;
                count = setup.AtkValueCount;
                break;

            case AddonRefreshArgs refresh:
                values = (AtkValue*)refresh.AtkValues;
                count = refresh.AtkValueCount;
                break;
        }

        if (values == null)
            return;

        if (Configuration.ShowLockedEmotesInGameWindow)
        {
            EmoteAddonValues.AddMissingEmotes(values, count, LockedEmotesByOrder, LockedEmoteCommands);

            if (type == AddonEvent.PreRefresh
                && AddonHelper.TryGetAddon(args, out var addon)
                && AddonHelper.TryReadTextInput(addon, out var search)
                && search.Trim() is { Length: > 0 } trimmed)
            {
                EmoteAddonValues.AddSearchMatches(values, count, MatchLockedEmotes(trimmed), LockedEmoteCommands);
            }
        }

        EmoteAddonValues.ClearLockedRowFlags(values, count);
    }

    private static unsafe void OnContextMenuValues(AddonEvent type, AddonArgs args)
    {
        if (!Configuration.PluginEnabled)
            return;

        switch (args)
        {
            case AddonSetupArgs setup:
                ContextMenuValues.EnableExecute((AtkValue*)setup.AtkValues, setup.AtkValueCount);
                break;

            case AddonRefreshArgs refresh:
                ContextMenuValues.EnableExecute((AtkValue*)refresh.AtkValues, refresh.AtkValueCount);
                break;
        }
    }

    internal static List<uint> MatchLockedEmotes(string search)
    {
        var matches = new List<uint>();

        foreach (var emoteId in LockedEmotesByOrder)
        {
            if (!LockedEmoteSearchTerms.TryGetValue(emoteId, out var terms))
                continue;

            foreach (var term in terms)
            {
                if (!term.Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;

                matches.Add(emoteId);
                break;
            }
        }

        return matches;
    }

    internal static bool IsEmoteLocked(uint emoteRowId) => LockedEmoteIds.Contains(emoteRowId);

    internal static bool IsLockedEmoteName(string name)
        => !string.IsNullOrWhiteSpace(name) && LockedEmoteNames.Contains(name.Trim());

    internal static unsafe void CloseContextMenu()
    {
        if (AddonHelper.TryGetReadyAddon(ContextMenuAddonName, out var menu))
            menu->FireCallbackInt(-1);
    }

    private static unsafe void PaintEmoteWindowRows()
    {
        if (!Configuration.PluginEnabled || LockedEmoteIds.Count == 0)
            return;

        if (AddonHelper.TryGetReadyAddon(EmoteAddonName, out var addon))
            EmoteListDimmer.Paint(addon, Configuration.ShowLockedEmotesAsUsable);
    }

    private static unsafe void EnableContextMenuExecute()
    {
        if (Configuration.PluginEnabled && AddonHelper.TryGetReadyAddon(ContextMenuAddonName, out var menu))
            ContextMenuValues.EnableExecute(menu);
    }

    private static bool EmoteWindowIsOpen()
        => AddonHelper.IsAddonReady(EmoteAddonName);

    private static unsafe bool EmoteWindowExists()
        => AddonHelper.TryGetAddon(EmoteAddonName, out _);

    private static unsafe void PaintHotbarSlots(bool lightUp)
    {
        var hotbars = RaptureHotbarModule.Instance();

        if (hotbars == null)
            return;

        var emotesAllowedNow = lightUp
            && NoireService.ObjectTable.LocalPlayer != null
            && EmoteHelper.CanUseEmote(68); // 68 = straight face, always unlocked

        var anyLit = false;

        foreach (var addonName in ActionBarAddons)
        {
            if (!AddonHelper.TryGetReadyAddon<AddonActionBarBase>(addonName, out var addon))
                continue;

            var slots = addon->ActionBarSlotVector;
            var slotCount = Math.Min((long)addon->SlotCount, slots.LongCount);

            for (var index = 0L; index < slotCount; index++)
            {
                var slot = hotbars->GetSlotById(addon->RaptureHotbarId, (uint)index);

                if (slot == null || slot->ApparentSlotType != RaptureHotbarModule.HotbarSlotType.Emote)
                    continue;

                var emoteId = slot->ApparentActionId;

                if (!LockedEmoteIds.Contains(emoteId))
                    continue;

                PaintSlotIcon(slots[index].Icon, emotesAllowedNow ? (byte)100 : (byte)50);
                anyLit |= emotesAllowedNow;
            }
        }

        hotbarSlotsLit = anyLit;
    }

    private static unsafe void PaintSlotIcon(AtkComponentNode* iconNode, byte multiply)
    {
        if (iconNode == null || !AddonHelper.TryGetComponent(&iconNode->AtkResNode, ComponentType.Icon, out var component))
            return;

        var icon = (AtkComponentIcon*)component;

        if (icon->IconImage == null)
            return;

        ref var image = ref icon->IconImage->AtkResNode;

        if (image.MultiplyRed == multiply && image.MultiplyGreen == multiply && image.MultiplyBlue == multiply)
            return;

        image.MultiplyRed = multiply;
        image.MultiplyGreen = multiply;
        image.MultiplyBlue = multiply;
    }

    private static void RefreshLockedEmoteIds()
    {
        ClearEmoteUiState();

        if (!NoireService.ClientState.IsLoggedIn || NoireService.ObjectTable.LocalPlayer == null)
            return;

        var ordered = new List<(uint RowId, ushort Order)>();

        foreach (var emote in EmoteHelper.GetLockedEmotes())
        {
            if (emote.RowId == 0)
                continue;

            LockedEmoteIds.Add(emote.RowId);

            if (emote.Name.ExtractText() is { Length: > 0 } name)
                LockedEmoteNames.Add(name.Trim());

            if (emote.Order != 0)
                ordered.Add((emote.RowId, emote.Order));
        }

        foreach (var emote in EmoteHelper.GetUnlockedEmotes())
        {
            if (emote.Name.ExtractText() is { Length: > 0 } name)
                LockedEmoteNames.Remove(name.Trim());
        }

        ordered.Sort((left, right) => left.Order.CompareTo(right.Order));

        foreach (var entry in ordered)
        {
            var commands = CommandsOf(entry.RowId);

            LockedEmotesByOrder.Add(entry.RowId);

            if (!LockedEmoteCommands.ContainsKey(entry.RowId))
                LockedEmoteCommands[entry.RowId] = AllocateCommandText(commands);

            if (EmoteHelper.GetEmoteById(entry.RowId) is { } emote && emote.Name.ExtractText() is { Length: > 0 } name)
                commands.Insert(0, name);

            LockedEmoteSearchTerms[entry.RowId] = [.. commands];
        }
    }

    private static List<string> CommandsOf(uint emoteRowId)
    {
        var commands = new List<string>();

        if (EmoteHelper.GetEmoteById(emoteRowId) is not { } emote
            || emote.TextCommand.ValueNullable is not { } textCommand)
        {
            return commands;
        }

        foreach (var command in new[]
        {
            textCommand.Command.ExtractText(),
            textCommand.ShortCommand.ExtractText(),
            textCommand.Alias.ExtractText(),
            textCommand.ShortAlias.ExtractText(),
        })
        {
            if (!string.IsNullOrWhiteSpace(command) && !commands.Contains(command))
                commands.Add(command);
        }

        return commands;
    }

    private static nint AllocateCommandText(List<string> commands)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join(" ", commands));
        var buffer = Marshal.AllocHGlobal(bytes.Length + 1);

        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        Marshal.WriteByte(buffer, bytes.Length, 0);

        return buffer;
    }

    private static void ReleaseLockedEmoteCommands()
    {
        foreach (var buffer in LockedEmoteCommands.Values)
        {
            if (buffer != 0)
                Marshal.FreeHGlobal(buffer);
        }

        LockedEmoteCommands.Clear();
    }

    internal static void RecordEmoteHistory(uint emoteRowId)
    {
        if (!Configuration.PluginEnabled || emoteRowId == 0 || emoteRowId > ushort.MaxValue)
            return;

        if (!NoireService.ClientState.IsLoggedIn || EmoteHelper.IsEmoteUnlocked(emoteRowId))
            return;

        if (EmoteWindowIsOpen())
        {
            PendingHistory.Remove(emoteRowId);
            PendingHistory.Add(emoteRowId);

            while (PendingHistory.Count > 6)
                PendingHistory.RemoveAt(0);

            return;
        }

        WriteEmoteHistory(emoteRowId);
    }

    private static void FlushPendingHistory()
    {
        if (PendingHistory.Count == 0)
            return;

        var queued = PendingHistory.ToArray();
        PendingHistory.Clear();

        foreach (var emoteRowId in queued)
            WriteEmoteHistory(emoteRowId);
    }

    private static unsafe void WriteEmoteHistory(uint emoteRowId)
    {
        var history = EmoteHistoryModule.Instance();

        if (history == null)
            return;

        try
        {
            history->AddToHistory((ushort)emoteRowId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Emote {emoteRowId} could not be added to the emote history.");
        }
    }
}
