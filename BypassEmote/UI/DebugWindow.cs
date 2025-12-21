using BypassEmote.IPC;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
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

        Service.Ipc.OnStateChanged += LogStateChanged;
    }

    private void LogStateChanged(string newState)
    {
        NoireLogger.LogDebug($"State changed: {newState}");
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
        // Initialize emote list if needed
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

        ImGui.Separator();

        InitCache();

        // Emote selection combo
        ImGui.Text("Select Emote:");

        var selectedEmoteName = selectedEmoteId == 0 ? "None" : GetEmoteDisplayName(selectedEmoteId);

        ImGui.SetNextItemWidth(250);

        using (var combo = ImRaii.Combo("##EmoteCombo", selectedEmoteName, ImGuiComboFlags.HeightRegular))
        {
            if (combo)
            {
                // Search bar
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##EmoteSearch", "Search emotes...", ref emoteSearchText, 256);

                // "None" option
                bool isNoneSelected = selectedEmoteId == 0;
                if (ImGui.Selectable("None", isNoneSelected))
                {
                    selectedEmoteId = 0;
                }

                // Emote list with icons
                foreach (var emote in cachedEmoteList)
                {
                    var emoteName = Helpers.CommonHelper.GetEmoteName(emote);
                    var emoteId = emote.RowId;

                    // Filter based on search text
                    if (!string.IsNullOrWhiteSpace(emoteSearchText) &&
                        !emoteName.Contains(emoteSearchText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var initialPosY = ImGui.GetCursorPosY();

                    // Draw icon
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
            Service.Ipc.SetStateForPlayer(NoireService.ObjectTable.LocalPlayer.Address, new IpcData(selectedEmoteId).Serialize());

        ImGui.SameLine();

        if (ImGui.Button("Set target state") && NoireService.TargetManager.Target != null)
            Service.Ipc.SetStateForPlayer(NoireService.TargetManager.Target.Address, new IpcData(selectedEmoteId).Serialize());

        ImGui.Separator();
        ImGui.TextWrapped("Current IPC Data (Looping only):");

        using (ImRaii.Child("IpcDataBlock", new Vector2(-1, 150), true))
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

    private string GetEmoteDisplayName(uint emoteId)
    {
        if (emoteId == 0) return "None";

        var emote = cachedEmoteList?.FirstOrDefault(e => e.RowId == emoteId);
        if (emote == null) return $"Unknown ({emoteId})";

        return Helpers.CommonHelper.GetEmoteName(emote.Value);
    }

    public void Dispose()
    {
        Service.Ipc.OnStateChanged -= LogStateChanged;
    }
}
