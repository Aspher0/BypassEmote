using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

#if DEBUG
    private readonly string localNetworkIp = GetPreferredLocalNetworkIp();
    private string peer1Host;
    private string peer2Host;
    private int peer1Port = 53740;
    private int peer2Port = 53741;
#endif

    public DebugWindow() : base("Bypass Emote Debug###BypassEmote")
    {
#if DEBUG
        peer1Host = localNetworkIp;
        peer2Host = localNetworkIp;

        if (Configuration.AutoRegister == 1)
        {
            SetRelay("Peer-1", "Peer 1", peer1Port);
            RegisterRelayPeer("Peer-2", peer2Host, peer2Port, "Peer 2");
        }
        else if (Configuration.AutoRegister == 2)
        {
            SetRelay("Peer-2", "Peer 2", peer2Port);
            RegisterRelayPeer("Peer-1", peer1Host, peer1Port, "Peer 1");
        }
#endif

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MinValue, float.MaxValue),
        };

        IpcProvider.OnReady += Logready;
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
        }
    }

#if DEBUG
    private void SetRelay(string peerId, string displayName, int port)
    {
        Service.NetworkRelay.SetPort(port)
            .RegisterSelf(peerId, displayName, true);
    }

    private void UnregisterRelaySelf()
    {
        Service.NetworkRelay.UnregisterSelf();
    }

    private void RegisterRelayPeer(string peerId, string host, int port, string displayName)
    {
        var endPoint = CreatePeerEndPoint(host, port);
        if (endPoint == null)
        {
            NoireLogger.PrintToChat($"Invalid peer IP address: {host}");
            return;
        }

        Service.NetworkRelay.RegisterPeer(peerId, endPoint, displayName);
    }

    private static IPEndPoint? CreatePeerEndPoint(string host, int port)
    {
        var ipText = string.IsNullOrWhiteSpace(host) ? GetPreferredLocalNetworkIp() : host.Trim();
        return IPAddress.TryParse(ipText, out var address) ? new IPEndPoint(address, port) : null;
    }

    private static string GetPreferredLocalNetworkIp()
    {
        try
        {
            var address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                .Where(adapter => adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                                           !IPAddress.IsLoopback(address) &&
                                           IsPrivateIpv4(address));

            if (address != null)
                return address.ToString();
        }
        catch
        {
        }

        return IPAddress.Loopback.ToString();
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    private void DrawNetworkRelayTab()
    {
        var autoRegisterSelf = Configuration.AutoRegister;
        if (ImGui.InputInt("##AutoRegisterSelf", ref autoRegisterSelf, 1, 1))
        {
            if (autoRegisterSelf < 0 || autoRegisterSelf > 2)
                autoRegisterSelf = 0;
            Configuration.AutoRegister = autoRegisterSelf;
        }

        if (ImGui.Button("Register self as peer 1"))
            SetRelay("Peer-1", "Peer 1", peer1Port);

        ImGui.SameLine();

        if (ImGui.Button("Register self as peer 2"))
            SetRelay("Peer-2", "Peer 2", peer2Port);

        ImGui.SameLine();

        if (ImGui.Button("Unregister self"))
            UnregisterRelaySelf();

        ImGui.TextColoredWrapped(ColorHelper.HexToVector4("#FF0000"), "Only use the below if your game instances are on the same PC and peers don't automatically get detected.\nIf using different PCs, don't touch these and only 'register self'.\nPorts needs to be different if on the same PC and has to be the same port if on different PCs.");

        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##Peer1Host", "Peer 1 LAN IP or hostname", ref peer1Host, 256);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            peer1Host = localNetworkIp;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Right click to reset");

        ImGui.SameLine();

        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##Peer1Port", ref peer1Port, 256);

        ImGui.SameLine();

        if (ImGui.Button("Register peer 1"))
            RegisterRelayPeer("Peer-1", peer1Host, peer1Port, "Peer 1");

        ImGui.SameLine();

        if (ImGui.Button("Unregister peer 1"))
            Service.NetworkRelay.UnregisterPeer("Peer-1");

        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##Peer2Host", "Peer 2 LAN IP or hostname", ref peer2Host, 256);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            peer2Host = localNetworkIp;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Right click to reset");

        ImGui.SameLine();

        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##Peer2Port", ref peer2Port, 256);

        ImGui.SameLine();

        if (ImGui.Button("Register peer 2"))
            RegisterRelayPeer("Peer-2", peer2Host, peer2Port, "Peer 2");

        ImGui.SameLine();

        if (ImGui.Button("Unregister peer 2"))
            Service.NetworkRelay.UnregisterPeer("Peer-2");

        ImGui.Text("Network Peers:");
        using (var child = ImRaii.Child("##NetworkPeers", new Vector2(-1, 200), true))
        {
            if (child)
            {
                var peers = Service.NetworkRelay.GetPeers();
                foreach (var peer in peers)
                {
                    ImGui.Text($"{(peer.PeerId == Service.NetworkRelay.InstanceId ? "You - " : "")}{peer.DisplayName} ({peer.PeerId}) Port: {peer.EndPoint}, Last Seen: {peer.LastSeenUtc.ToLocalTime()}");
                }
            }
        }
    }

#endif

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
                    .Where(e => Helpers.CommonHelper.GetEmotePlayType(e) != EmotePlayType.DoNotPlay)
                    .Where(e => Helpers.CommonHelper.IsEmoteDisplayable(e))
                    .OrderByDescending(e => e.RowId)
                    .ToList();
            }
            else
            {
                cachedEmoteList = new List<Emote>();
            }
        }
    }

    private unsafe void DrawIpcTestsTab()
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

        return Helpers.CommonHelper.GetEmoteName(emote.Value);
    }

    private static string FormatJson(string json)
    {
        return JToken.Parse(json).ToString(Formatting.Indented);
    }

    private void Logready()
        => NoireLogger.LogDebug(this, $"BypassEmote IPC is Ready");
    private void LogStateChanged(string liveData, string? cacheData, bool isLocalPlayer)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent state changed message. IsLocalPlayer: {isLocalPlayer}, LiveData: {liveData}, CacheData: {cacheData ?? "<null>"}");
    private void LogStateChangedImmediate(string liveData, string? cacheData, bool isLocalPlayer)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent IMMEDIATE state changed message. IsLocalPlayer: {isLocalPlayer}, LiveData: {liveData}, CacheData: {cacheData ?? "<null>"}");

    public void Dispose()
    {
        IpcProvider.OnReady -= Logready;
        IpcProvider.OnStateChange -= LogStateChanged;
        IpcProvider.OnStateChangeImmediate -= LogStateChangedImmediate;
    }
}
