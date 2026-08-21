#if DEBUG
using BypassEmote.EmoteSwap;
using BypassEmote.Helpers;
using BypassEmote.IPC;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI;

public class DebugWindow : Window, IDisposable
{
    private Vector3 pos1 = Vector3.Zero;
    private Vector3 pos2 = Vector3.Zero;

    private float rot1 = 0f;
    private float rot2 = 0f;

    private uint selectedEmoteId = 0;
    private string emoteSearchText = string.Empty;
    private List<Emote>? cachedEmoteList = null;

    public DebugWindow() : base("Bypass Emote Debug###BypassEmote")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MinValue, float.MaxValue),
        };

        IpcProvider.OnReady += LogReady;
        IpcProvider.OnStateChange += LogStateChanged;
        IpcProvider.OnStateChangeImmediate += LogStateChangedImmediate;
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("DebugTabs"))
        {
            using (var tab = ImRaii.TabItem("Position/Rotation"))
            {
                if (tab)
                    DrawPositionRotationTab();
            }

            using (var tab = ImRaii.TabItem("IPC Tests"))
            {
                if (tab)
                    DrawIpcTestsTab();
            }

            using (var tab = ImRaii.TabItem("Tracked Characters"))
            {
                if (tab)
                    DrawTrackedCharactersTab();
            }

#if DEBUG
            using (var tab = ImRaii.TabItem("Network Relay"))
            {
                if (tab)
                    DrawNetworkRelayTab();
            }
#endif

            using (var tab = ImRaii.TabItem("Swap Layers"))
            {
                if (tab)
                    DrawSwapLayers();
            }

            using (var tab = ImRaii.TabItem("Emote Pool"))
            {
                if (tab)
                    EmotePoolTab.Draw();
            }

            using (var tab = ImRaii.TabItem("Kept Swaps"))
            {
                if (tab)
                    DrawKeptSwapsTab();
            }
        }
    }

    private static void DrawKeptSwapsTab()
    {
        var manager = Service.SwapMods;

        if (manager == null)
        {
            ImGui.TextUnformatted("The swap mod manager is not initialized.");
            return;
        }

        var registry = manager.Registry;
        var directory = manager.ModDirectoryName;

        ImGui.TextUnformatted($"Mod directory: {(directory.Length == 0 ? "<no character loaded>" : directory)}");
        ImGui.TextUnformatted($"Penumbra: {DescribeModState(manager.PenumbraState())}");
        ImGui.TextUnformatted($"Drawn body: {registry.Skeleton ?? "<unknown>"}");
        ImGui.TextUnformatted($"Swap files: {DescribeSize(manager.SwapFilesSize())}");

        var clearing = ImGui.Button("Clear kept swaps##KeptSwaps");

        ImGui.Separator();

        if (registry.Entries.Count == 0)
        {
            ImGui.TextUnformatted("No swap is kept.");
        }
        else
        {
            DrawKeptSwapsTable(manager, registry.Entries);
        }

        // Run after the table, so the registry never changes under the rows being drawn.
        if (clearing)
        {
            manager.ForgetAll();
            Service.Orchestrator?.ResetDispatchMemory();
        }
    }

    private static void DrawKeptSwapsTable(SwapModManager manager, IReadOnlyList<SwapOptionEntry> entries)
    {
        using var table = ImRaii.Table("##KeptSwapsTable", 8,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings);

        if (!table)
            return;

        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("Source");
        ImGui.TableSetupColumn("Option");
        ImGui.TableSetupColumn("Races", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Last used", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Selected", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Rules", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##KeptSwapsActions", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        var currentRules = SwapRulesStamp.Current();

        (SwapOptionEntry Entry, bool Select)? pending = null;

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(EmoteLabel(entry.TargetEmote) + (entry.IsIdlePoseSwap ? " (idle pose)" : string.Empty));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(EmoteLabel(entry.SourceEmote));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{entry.GroupName} / {entry.OptionName}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.FilesByRace.Count.ToString());

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(string.Join(", ", entry.FilesByRace.Keys));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.LastUsedStamp.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.SelectedByUs ? "yes" : "no");

            ImGui.TableNextColumn();

            if (ImGui.Button($"Select##KeptSwap{index}"))
                pending = (entry, true);

            ImGui.SameLine();

            using (ImRaii.Disabled(!entry.SelectedByUs))
            {
                if (ImGui.Button($"Turn off##KeptSwap{index}"))
                    pending = (entry, false);
            }
        }

        // Run after the rows: both calls rewrite the registry the table is reading.
        if (pending is not { } action)
            return;

        if (action.Select)
            manager.SelectExisting(action.Entry);
        else
            manager.DeselectEntry(action.Entry);
    }

    private static string DescribeModState(ModState? state)
    {
        if (state is not { } held)
            return "does not hold the mod";

        return $"{(held.Enabled ? "enabled" : "disabled")}, priority {held.Priority}";
    }

    private static string DescribeSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024f:0.#} KB";

        return $"{bytes / (1024f * 1024f):0.##} MB";
    }

    private static string EmoteLabel(uint emoteRowId)
    {
        if (emoteRowId == 0)
            return "-";

        return EmoteHelper.GetEmoteById(emoteRowId) is { } emote
            ? $"{CommonHelper.GetEmoteName(emote)} (#{emoteRowId})"
            : $"#{emoteRowId}";
    }

#if DEBUG
    private void DrawNetworkRelayTab()
    {
        var relay = Service.Networker;

        if (relay == null)
        {
            ImGui.Text("Network relay is not initialized.");
            return;
        }

        ImGui.Text($"Network: {relay.NetworkName}");
        ImGui.Text($"State: {relay.State}{(relay.IsHub ? " (hub)" : string.Empty)}");
        ImGui.Text($"Self: {relay.SelfId}");

        var isActive = relay.IsActive;
        if (ImGui.Checkbox("Active", ref isActive))
            relay.SetActive(isActive);

        ImGui.SameLine();

        var enableLan = relay.Options.EnableLan;
        if (ImGui.Checkbox("LAN discovery", ref enableLan))
        {
            relay.Options.EnableLan = enableLan;

            // Option changes only apply on activation
            if (relay.IsActive)
                relay.SetActive(false).SetActive(true);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Instances on the same PC find each other with no configuration.\nLAN discovery reaches other PCs, and may need a Windows Firewall inbound allow.");

        var localPlayer = NoireService.ObjectTable.LocalPlayer;
        if (localPlayer != null)
            relay.Self.Set("character", localPlayer.Name.TextValue);

        ImGui.Text("Peers:");
        using (var child = ImRaii.Child("##NetworkPeers", new Vector2(-1, -1), true))
        {
            if (child)
            {
                ImGui.Text($"You - {relay.Self} [{(relay.IsHub ? "hub" : "client")}]");

                foreach (var peer in relay.OtherPeers)
                    ImGui.Text($"{peer} [{(peer.IsSameMachine ? "same PC" : "LAN")}]");
            }
        }
    }
#endif

    private static void DrawSwapLayers()
    {
        LayerSwitch("Swap owned emotes##SwapLayer", SwapLayers.SwapOwnedEmotes,
            value => SwapLayers.SwapOwnedEmotes = value);

        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Sends emotes you already own through the swap instead of letting the game play them, for debugging only.");

        LayerSwitch("Weapon in hand##SwapLayer", SwapLayers.WeaponInHand,
            value => SwapLayers.WeaponInHand = value);

        LayerSwitch("Weapon stow at end##SwapLayer", SwapLayers.WeaponStowAtEnd,
            value => SwapLayers.WeaponStowAtEnd = value);

        LayerSwitch("Weapon travel animation##SwapLayer", SwapLayers.WeaponTravelAnimation,
            value => SwapLayers.WeaponTravelAnimation = value);

        ImGui.Spacing();

        LayerSwitch("Unique pack names##SwapLayer", SwapLayers.UniquePackNames,
            value => SwapLayers.UniquePackNames = value);

        ImGui.TextUnformatted("Substitution doors");

        LayerSwitch("Loader door##SwapLayer", SwapLayers.DoorLoader,
            value => SwapLayers.DoorLoader = value);

        ImGui.SameLine();
        LayerSwitch("Bind door##SwapLayer", SwapLayers.DoorBind,
            value => SwapLayers.DoorBind = value);

        ImGui.SameLine();
        LayerSwitch("Request door##SwapLayer", SwapLayers.DoorRequest,
            value => SwapLayers.DoorRequest = value);

        ImGui.SameLine();
        LayerSwitch("Match enforcement##SwapLayer", SwapLayers.MatchEnforcement,
            value => SwapLayers.MatchEnforcement = value);

        ImGui.Spacing();

        LayerSwitch("Prewarm packs##SwapLayer", SwapLayers.PrewarmPacks,
            value => SwapLayers.PrewarmPacks = value);

        LayerSwitch("Force fresh file loads##SwapLayer", SwapLayers.NoCacheFlip,
            value => SwapLayers.NoCacheFlip = value);

        LayerSwitch("Binding pack correction##SwapLayer", SwapLayers.BindingPackCorrection,
            value => SwapLayers.BindingPackCorrection = value);

        LayerSwitch("Mapping pack correction##SwapLayer", SwapLayers.MappingPackCorrection,
            value => SwapLayers.MappingPackCorrection = value);

        ImGui.Spacing();

        LayerSwitch("Always compose paths##SwapLayer", SwapLayers.AlwaysComposePaths,
            value => SwapLayers.AlwaysComposePaths = value);

        LayerSwitch("Republish the vanilla path##SwapLayer", SwapLayers.PublishVanillaPath,
            value => SwapLayers.PublishVanillaPath = value);
    }

    private static void LayerSwitch(string label, bool current, Action<bool> write)
    {
        var value = current;
        if (!ImGui.Checkbox(label, ref value))
            return;

        write(value);
        Service.ResidencyProbe?.ApplyLayerSwitches();
    }

    private void DrawPositionRotationTab()
    {
        if (NoireService.ObjectTable.LocalPlayer != null)
        {
            var player = NoireService.ObjectTable.LocalPlayer;

            if (ImGui.Button("Set Position 1"))
            {
                pos1 = player.Position;
            }

            ImGui.SameLine();

            if (ImGui.Button("Set Position 2"))
            {
                pos2 = player.Position;
            }

            ImGui.Text($"Position 1: X: {pos1.X}, Y: {pos1.Y}, Z: {pos1.Z}");
            ImGui.Text($"Position 2: X: {pos2.X}, Y: {pos2.Y}, Z: {pos2.Z}");
            ImGui.Text($"Distance: {Vector3.Distance(pos1, pos2)}");

            ImGui.Separator();

            if (ImGui.Button("Set Rotation 1"))
            {
                rot1 = player.Rotation;
            }

            ImGui.SameLine();

            if (ImGui.Button("Set Rotation 2"))
            {
                rot2 = player.Rotation;
            }

            var normalizedCurrent = MathHelper.NormalizeAngle(MathHelper.ToDegrees(player.Rotation));
            var normalized1 = MathHelper.NormalizeAngle(MathHelper.ToDegrees(rot1));
            var normalized2 = MathHelper.NormalizeAngle(MathHelper.ToDegrees(rot2));
            var difference = Math.Abs(MathHelper.DeltaAngle(normalized1, normalized2));

            ImGui.Text("Current Rotation: " + normalizedCurrent);
            ImGui.Text($"Rotation 1: {normalized1}");
            ImGui.Text($"Rotation 2: {normalized2}");
            ImGui.Text($"Rotation Difference: {difference}");

            ImGui.Separator();

            var groundMaterial = CharacterHelper.GetGroundMaterial(player);
            var throwResolvesTo = CommonHelper.ResolveTargetedEmote(player, 85);

            ImGui.Text($"Ground material: {(byte)groundMaterial} ({groundMaterial})");
            ImGui.Text($"Standing in water: {CharacterHelper.IsStandingInWater(player)}"
                + $" (with fallback: {CharacterHelper.IsStandingInWater(player, includeFallback: true)})");
            ImGui.Text($"/throw resolves to row: {throwResolvesTo}"
                + (throwResolvesTo == 85 ? " (throw)" : " (snowball)"));
            ImGui.Text($"/splash allowed here: {EmoteHelper.MeetsEnvironmentFor(player, 178)}");

            ImGui.Separator();

            ImGui.Text($"Player Address: {player.Address:X}");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Click to copy");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    ImGui.SetClipboardText($"{player.Address:X}");
            }
        }
        else
        {
            ImGui.Text("Player not loaded.");
        }
    }

    private void InitCache()
    {
        if (cachedEmoteList == null)
        {
            var emoteSheet = ExcelSheetHelper.GetSheet<Emote>();
            if (emoteSheet != null)
            {
                cachedEmoteList = emoteSheet
                    .Where(e => CommonHelper.GetEmotePlayType(e) != EmotePlayType.DoNotPlay)
                    .Where(e => CommonHelper.IsEmoteDisplayable(e))
                    .OrderByDescending(e => e.RowId)
                    .ToList();
            }
            else
            {
                cachedEmoteList = new List<Emote>();
            }
        }
    }

    private void DrawIpcTestsTab()
    {
        ImGui.Text($"IPC Version: {IpcProvider.ApiVersion().ToString()}");
        ImGui.Text($"IPC Is Ready: {IpcProvider.IsReady()}");

        ImGui.Separator();

        InitCache();

        ImGui.Text("Select Emote:");

        var selectedEmoteName = selectedEmoteId == 0 ? "None" : GetEmoteDisplayName(selectedEmoteId);

        ImGui.SetNextItemWidth(250);

        using (var combo = ImRaii.Combo("##EmoteCombo", selectedEmoteName, ImGuiComboFlags.HeightRegular))
        {
            if (combo)
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##EmoteSearch", "Search emotes...", ref emoteSearchText, 256);

                bool isNoneSelected = selectedEmoteId == 0;
                if (ImGui.Selectable("None", isNoneSelected))
                {
                    selectedEmoteId = 0;
                }

                foreach (var emote in cachedEmoteList)
                {
                    var emoteName = Helpers.CommonHelper.GetEmoteName(emote);
                    var emoteId = emote.RowId;

                    if (!string.IsNullOrWhiteSpace(emoteSearchText) &&
                        !emoteName.Contains(emoteSearchText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var initialPosY = ImGui.GetCursorPosY();

                    var iconSize = 25f;
                    try
                    {
                        var iconTex = NoireService.TextureProvider.GetFromGameIcon(Helpers.CommonHelper.GetEmoteIcon(emote));
                        var wrap = iconTex?.GetWrapOrEmpty();
                        if (wrap != null)
                        {
                            var posY = ImGui.GetCursorPosY();
                            ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
                            ImGui.SameLine();
                            ImGui.SetCursorPosY(posY + MathF.Max(0, (iconSize - ImGui.GetTextLineHeight()) * 0.5f));
                        }
                    }
                    catch
                    {
                        // ignore icon issues
                    }

                    bool isSelected = selectedEmoteId == emoteId;
                    if (ImGui.Selectable($"{emoteName}##{emoteId}", isSelected))
                    {
                        selectedEmoteId = emoteId;
                        ImGui.CloseCurrentPopup();
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.SameLine();

        var localPlayer = NoireService.ObjectTable.LocalPlayer;
        var target = CommonHelper.GetLocalTarget();
        var executedAction = selectedEmoteId == 0 ? ExecutedAction.StoppedEmote : ExecutedAction.StartedEmote;
        var currentState = CurrentState.Stopped;

        if (selectedEmoteId != 0)
        {
            var emote = EmoteHelper.GetEmoteById(selectedEmoteId);
            if (emote != null && CommonHelper.GetEmotePlayType(emote.Value) == EmotePlayType.Looped)
                currentState = CurrentState.PlayingEmote;
        }

        if (ImGui.Button("Set local player state") && localPlayer != null)
        {
            var characterState = CommonHelper.CreateCharacterState(localPlayer.Address, executedAction, currentState, selectedEmoteId);
            IpcProvider.SetStateForCharacter(localPlayer.Address, characterState.Serialize());
        }

        ImGui.SameLine();

        if (ImGui.Button("Set target state") && target != null)
        {
            if (target is not ICharacter castTarget)
                return;
            var characterState = CommonHelper.CreateCharacterState(castTarget.Address, executedAction, currentState, selectedEmoteId);
            IpcProvider.SetStateForCharacter(castTarget.Address, characterState.Serialize());
        }

        if (ImGui.Button("Clear local player state") && localPlayer != null)
            IpcProvider.ClearStateForCharacter(localPlayer.Address);

        ImGui.SameLine();

        if (ImGui.Button("Clear target state") && target != null)
            IpcProvider.ClearStateForCharacter(target.Address);

        ImGui.Separator();
        ImGui.Text("Current Local Player IPC Data (Looping only):");

        var remainingHeight = ImGui.GetContentRegionAvail().Y;
        var heightBlocks = (int)(remainingHeight / 2 - 20);

        using (var child = ImRaii.Child("IpcDataBlockLocal", new Vector2(-1, heightBlocks), true))
        {
            if (child)
            {
                if (localPlayer != null)
                {
                    var ipcData = IpcProvider.GetStateForCharacter(localPlayer.Address);

                    if (!string.IsNullOrEmpty(ipcData))
                    {
                        try
                        {
                            ImGui.TextUnformatted(FormatJson(ipcData));
                        }
                        catch
                        {
                            ImGui.TextUnformatted(ipcData);
                        }
                    }
                }
            }
        }

        ImGui.Separator();
        ImGui.Text("Current Target IPC Data (Looping only):");

        using (var child = ImRaii.Child("IpcDataBlockTarget", new Vector2(-1, heightBlocks), true))
        {
            if (child)
            {
                if (CommonHelper.GetLocalTarget() is ICharacter targettedChar)
                {
                    var ipcData = IpcProvider.GetStateForCharacter(targettedChar.Address);

                    if (!string.IsNullOrEmpty(ipcData))
                    {
                        try
                        {
                            ImGui.TextUnformatted(FormatJson(ipcData));
                        }
                        catch
                        {
                            ImGui.TextUnformatted(ipcData);
                        }
                    }
                }
            }
        }
    }

    private void DrawTrackedCharactersTab()
    {
        ImGui.Text("Tracked Characters:");

        using (var child = ImRaii.Child("BlockTrackedCharacters", new Vector2(-1, -1), true))
        {
            if (child)
            {
                try
                {
                    var formattedJson = JsonConvert.SerializeObject(EmotePlayer.TrackedCharacters, Formatting.Indented);

                    ImGui.TextUnformatted(formattedJson);
                }
                catch (Exception ex)
                {
                    ImGui.TextUnformatted($"Serialization error: {ex.Message}");
                }
            }
        }
    }

    private string GetEmoteDisplayName(uint emoteId)
    {
        if (emoteId == 0) return "None";

        var emote = cachedEmoteList?.FirstOrDefault(e => e.RowId == emoteId);
        if (emote == null) return $"Unknown ({emoteId})";

        return CommonHelper.GetEmoteName(emote.Value);
    }

    private static string FormatJson(string json)
    {
        return JToken.Parse(json).ToString(Formatting.Indented);
    }

    private void LogReady()
        => NoireLogger.LogDebug(this, "BypassEmote IPC is ready");
    private void LogStateChanged(string liveData, string? cacheData, bool isLocalPlayer)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent state changed message. IsLocalPlayer: {isLocalPlayer}, LiveData: {liveData}, CacheData: {cacheData ?? "<null>"}");
    private void LogStateChangedImmediate(string liveData, string? cacheData, bool isLocalPlayer)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent immediate state changed message. IsLocalPlayer: {isLocalPlayer}, LiveData: {liveData}, CacheData: {cacheData ?? "<null>"}");

    public void Dispose()
    {
        IpcProvider.OnReady -= LogReady;
        IpcProvider.OnStateChange -= LogStateChanged;
        IpcProvider.OnStateChangeImmediate -= LogStateChangedImmediate;
    }
}
#endif
