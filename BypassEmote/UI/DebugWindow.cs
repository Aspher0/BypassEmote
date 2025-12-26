using BypassEmote.IPC;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;

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

        Service.Ipc.OnReady += Logready;
        Service.Ipc.OnStateChange += LogStateChanged;
        Service.Ipc.OnOwnedObjectStateChange += LogOwnedObjectStateChanged;
        Service.Ipc.OnEmoteStateStart += LogEmoteStateStart;
        Service.Ipc.OnOwnedObjectEmoteStateStart += LogOwnedObjectEmoteStateStart;
        Service.Ipc.OnEmoteStateStop += LogEmoteStateStop;
        Service.Ipc.OnOwnedObjectEmoteStateStop += LogOwnedObjectEmoteStateStop;
        Service.Ipc.OnStateChangeImmediate += LogStateChangedImmediate;
        Service.Ipc.OnOwnedObjectStateChangeImmediate += LogOwnedObjectStateChangedImmediate;
        Service.Ipc.OnEmoteStateStartImmediate += LogEmoteStateStartImmediate;
        Service.Ipc.OnOwnedObjectEmoteStateStartImmediate += LogOwnedObjectEmoteStateStartImmediate;
        Service.Ipc.OnEmoteStateStopImmediate += LogEmoteStateStopImmediate;
        Service.Ipc.OnOwnedObjectEmoteStateStopImmediate += LogOwnedObjectEmoteStateStopImmediate;
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
        }
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
            ImGui.Text($"Distance: {MathHelper.Distance(pos1, pos2)}");

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
            var difference = MathHelper.Abs(MathHelper.DeltaAngle(normalized1, normalized2));

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

    private void DrawIpcTestsTab()
    {
        ImGui.Text($"IPC Version: {Service.Ipc.ApiVersion().ToString()}");
        ImGui.Text($"IPC Is Ready: {Service.Ipc.IsReady()}");

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

        if (ImGui.Button("Set local player state") && NoireService.ObjectTable.LocalPlayer != null)
            Service.Ipc.SetStateForCharacter(NoireService.ObjectTable.LocalPlayer.Address, new IpcData(selectedEmoteId, NoireService.ObjectTable.LocalPlayer.Address).Serialize());

        ImGui.SameLine();

        if (ImGui.Button("Set target state") && NoireService.TargetManager.Target != null)
            Service.Ipc.SetStateForCharacter(NoireService.TargetManager.Target.Address, new IpcData(selectedEmoteId, NoireService.TargetManager.Target.Address).Serialize());

        if (ImGui.Button("Clear local player state") && NoireService.ObjectTable.LocalPlayer != null)
            Service.Ipc.ClearStateForCharacter(NoireService.ObjectTable.LocalPlayer.Address);

        ImGui.SameLine();

        if (ImGui.Button("Clear target state") && NoireService.TargetManager.Target != null)
            Service.Ipc.ClearStateForCharacter(NoireService.TargetManager.Target.Address);

        ImGui.Separator();
        ImGui.Text("Current Local Player IPC Data (Looping only):");

        using (var child = ImRaii.Child("IpcDataBlockLocal", new Vector2(-1, 125), true))
        {
            if (child)
            {
                var ipcData = Service.Ipc.GetStateForLocalPlayer();

                if (!string.IsNullOrEmpty(ipcData))
                {
                    try
                    {
                        var jsonDocument = JsonDocument.Parse(ipcData);
                        var formattedJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        });
                        ImGui.TextUnformatted(formattedJson);
                    }
                    catch
                    {
                        ImGui.TextUnformatted(ipcData);
                    }
                }
            }
        }

        ImGui.Separator();
        ImGui.Text("Current Target IPC Data (Looping only):");

        using (var child = ImRaii.Child("IpcDataBlockTarget", new Vector2(-1, 125), true))
        {
            if (child)
            {
                if (NoireService.TargetManager.Target is ICharacter targettedChar)
                {
                    var ipcData = Service.Ipc.GetStateForCharacter(targettedChar.Address);

                    if (!string.IsNullOrEmpty(ipcData))
                    {
                        try
                        {
                            var jsonDocument = JsonDocument.Parse(ipcData);
                            var formattedJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                            });
                            ImGui.TextUnformatted(formattedJson);
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
                    var formattedJson = JsonSerializer.Serialize(
                        EmotePlayer.TrackedCharacters,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            IncludeFields = true,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        });

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

    private void Logready()
        => NoireLogger.LogDebug(this, $"BypassEmote IPC is Ready");
    private void LogStateChanged(string newState)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent state changed message. Data: {newState}");
    private void LogOwnedObjectStateChanged(nint ownedObjAddress, string ipcData)
        => NoireLogger.LogDebug($"BypassEmote IPC sent state changed message for owned object {ownedObjAddress}. Data: {{newState}}\"");
    private void LogEmoteStateStart(bool isLooping, string ipcData)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent start message. IsLooping: {isLooping}, Data: {ipcData}");
    private void LogOwnedObjectEmoteStateStart(nint ownedObjAddress, bool isLooping, string ipcData)
        => NoireLogger.LogDebug($"BypassEmote IPC sent start message for owned object {ownedObjAddress}. IsLooping: {isLooping}, Data: {ipcData}");
    private void LogEmoteStateStop()
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent stop message");
    private void LogOwnedObjectEmoteStateStop(nint ownedObjAddress)
        => NoireLogger.LogDebug($"BypassEmote IPC sent stop message for owned object {ownedObjAddress}");
    private void LogStateChangedImmediate(string ipcData)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent IMMEDIATE state changed message. Data: {ipcData}");
    private void LogOwnedObjectStateChangedImmediate(nint ownedObjAddress, string ipcData)
        => NoireLogger.LogDebug($"BypassEmote IPC sent IMMEDIATE state changed message for owned object {ownedObjAddress}. Data: {ipcData}");
    private void LogEmoteStateStartImmediate(bool isLooping, string ipcData)
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent IMMEDIATE start message. IsLooping: {isLooping}, Data: {ipcData}");
    private void LogOwnedObjectEmoteStateStartImmediate(nint ownedObjAddress, bool isLooping, string ipcData)
        => NoireLogger.LogDebug($"BypassEmote IPC sent IMMEDIATE start message for owned object {ownedObjAddress}. IsLooping: {isLooping}, Data: {ipcData}");
    private void LogEmoteStateStopImmediate()
        => NoireLogger.LogDebug(this, $"BypassEmote IPC sent IMMEDIATE stop message");
    private void LogOwnedObjectEmoteStateStopImmediate(nint ownedObjAddress)
        => NoireLogger.LogDebug($"BypassEmote IPC sent IMMEDIATE stop message for owned object {ownedObjAddress}");

    public void Dispose()
    {
        Service.Ipc.OnReady -= Logready;
        Service.Ipc.OnStateChange -= LogStateChanged;
        Service.Ipc.OnOwnedObjectStateChange -= LogOwnedObjectStateChanged;
        Service.Ipc.OnEmoteStateStart -= LogEmoteStateStart;
        Service.Ipc.OnOwnedObjectEmoteStateStart -= LogOwnedObjectEmoteStateStart;
        Service.Ipc.OnEmoteStateStop -= LogEmoteStateStop;
        Service.Ipc.OnOwnedObjectEmoteStateStop -= LogOwnedObjectEmoteStateStop;
        Service.Ipc.OnStateChangeImmediate -= LogStateChangedImmediate;
        Service.Ipc.OnOwnedObjectStateChangeImmediate -= LogOwnedObjectStateChangedImmediate;
        Service.Ipc.OnEmoteStateStartImmediate -= LogEmoteStateStartImmediate;
        Service.Ipc.OnOwnedObjectEmoteStateStartImmediate -= LogOwnedObjectEmoteStateStartImmediate;
        Service.Ipc.OnEmoteStateStopImmediate -= LogEmoteStateStopImmediate;
        Service.Ipc.OnOwnedObjectEmoteStateStopImmediate -= LogOwnedObjectEmoteStateStopImmediate;
    }
}
