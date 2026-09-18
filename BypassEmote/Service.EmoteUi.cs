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

    private static void StartEmoteUi()
    {
        NoireService.Framework.Update += OnEmoteUiFrame;

        emoteAddonSetup = AddonHelper.RegisterLifecycleListener(
            AddonEvent.PreSetup, EmoteAddonName, OnEmoteAddonValues);

        emoteAddonRefresh = AddonHelper.RegisterLifecycleListener(
            AddonEvent.PreRefresh, EmoteAddonName, OnEmoteAddonValues);
    }

    private static void StopEmoteUi()
    {
        NoireService.Framework.Update -= OnEmoteUiFrame;

        emoteAddonSetup?.Dispose();
        emoteAddonSetup = null;

        emoteAddonRefresh?.Dispose();
        emoteAddonRefresh = null;

        if (hotbarSlotsLit)
            PaintHotbarSlots(lightUp: false);

        if (!EmoteWindowIsOpen())
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

        var showAsAvailable = Configuration.ShowLockedEmotesAsUsable;

        if (Configuration.ShowLockedEmotesInGameWindow)
        {
            EmoteAddonValues.AddMissingEmotes(
                values, count, LockedEmotesByOrder, LockedEmoteCommands, showAsAvailable);

            if (SearchTextOf((AtkUnitBase*)args.Addon.Address) is { Length: > 0 } search)
            {
                EmoteAddonValues.AddSearchMatches(
                    values, count, MatchLockedEmotes(search), LockedEmoteCommands, showAsAvailable);
            }
        }

        if (showAsAvailable)
            EmoteAddonValues.ShowLockedRowsAsAvailable(values, count);
    }

    private static unsafe string SearchTextOf(AtkUnitBase* addon)
        => addon == null ? string.Empty : SearchTextIn(&addon->UldManager, 0);

    private static unsafe string SearchTextIn(AtkUldManager* manager, int depth)
    {
        if (depth > 3)
            return string.Empty;

        for (var index = 0; index < manager->NodeListCount; index++)
        {
            var node = manager->NodeList[index];

            if (node == null || (ushort)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;

            if (component == null)
                continue;

            if ((ushort)node->Type == 1007)
            {
                var input = (AtkComponentTextInput*)component;
                var text = input->AtkComponentInputBase.RawString.ToString().Trim();

                return text.Length > 0 ? text : input->AtkComponentInputBase.EvaluatedString.ToString().Trim();
            }

            var nested = SearchTextIn(&component->UldManager, depth + 1);

            if (nested.Length > 0)
                return nested;
        }

        return string.Empty;
    }

    private static List<uint> MatchLockedEmotes(string search)
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
        var menu = (AtkUnitBase*)NoireService.GameGui.GetAddonByName(ContextMenuAddonName).Address;

        if (menu == null || !menu->IsVisible)
            return;

        menu->FireCallbackInt(-1);
    }

    private static unsafe void PaintEmoteWindowRows()
    {
        if (!Configuration.PluginEnabled || LockedEmoteIds.Count == 0)
            return;

        var addon = (AtkUnitBase*)NoireService.GameGui.GetAddonByName(EmoteAddonName).Address;

        if (addon == null)
            return;

        EmoteListDimmer.Paint(addon, Configuration.ShowLockedEmotesAsUsable);
    }

    private static unsafe void EnableContextMenuExecute()
    {
        if (!Configuration.PluginEnabled || !EmoteWindowIsOpen())
            return;

        ContextMenuValues.EnableExecute(
            (AtkUnitBase*)NoireService.GameGui.GetAddonByName(ContextMenuAddonName).Address);
    }

    private static bool EmoteWindowIsOpen()
        => NoireService.GameGui.GetAddonByName(EmoteAddonName).IsAddonLoaded();

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
            var addon = (AddonActionBarBase*)NoireService.GameGui.GetAddonByName(addonName).Address;

            if (addon == null || !addon->AtkUnitBase.IsVisible)
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
        if (iconNode == null || iconNode->Component == null)
            return;

        var icon = (AtkComponentIcon*)iconNode->Component;

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
        if (commands.Count == 0)
            return 0;

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
