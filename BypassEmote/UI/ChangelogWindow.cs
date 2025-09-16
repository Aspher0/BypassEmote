using BypassEmote.Managers;
using BypassEmote.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices.Legacy;
using System;
using System.Linq;
using System.Numerics;

namespace BypassEmote.UI;

public class ChangelogWindow : Window, IDisposable
{
    private string _selectedVersion = string.Empty;
    private ChangelogVersion? _currentChangelog;
    private readonly string[] _availableVersions;

    public ChangelogWindow() : base("Bypass Emote - Changelog##BypassEmoteChangelog", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(750, 500);

        var versions = ChangelogManager.GetAllVersions();
        _availableVersions = versions.Select(v => v.Version).ToArray();
        
        if (_availableVersions.Length > 0)
        {
            _selectedVersion = _availableVersions[0];
            _currentChangelog = ChangelogManager.GetVersion(_selectedVersion);
        }

        TitleBarButtons.Add(new()
        {
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.Plugin.OpenSettings(); },
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Open settings"),
        });

        TitleBarButtons.Add(new()
        {
            Click = (m) => { if (m == ImGuiMouseButton.Left) Service.OpenKofi(); },
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Support me"),
        });
    }

    public override void Draw()
    {
        DrawVersionSelector();
        ImGui.Dummy(new Vector2(0, 3));
        DrawChangelogContent();
        ImGui.Dummy(new Vector2(0, 3));
        DrawFooter();
    }

    private void DrawVersionSelector()
    {
        ImGui.Text("Select Version:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##VersionSelector", _selectedVersion))
        {
            foreach (var version in _availableVersions)
            {
                bool isSelected = version == _selectedVersion;
                if (ImGui.Selectable($"{version}##version_{version}", isSelected))
                {
                    _selectedVersion = version;
                    _currentChangelog = ChangelogManager.GetVersion(_selectedVersion);
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        // Show selected version info
        if (_currentChangelog != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({_currentChangelog.Date})");

            if (!string.IsNullOrWhiteSpace(_currentChangelog.Title))
            {
                ImGui.SameLine();
                var titleColor = _currentChangelog.TitleColor ?? new Vector4(1f, 1f, 1f, 1f);
                ImGui.TextColored(titleColor, $"- {_currentChangelog.Title}");
            }
        }
    }

    private void DrawChangelogContent()
    {
        if (_currentChangelog == null)
        {
            ImGui.TextDisabled("No changelog available for this version.");
            return;
        }

        var availHeight = ImGui.GetContentRegionAvail().Y - 40f; // Reserve space for footer
       
        var bgColor = new Vector4(0.5f, 0.5f, 0.5f, 0.05f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, bgColor);
        
        ImGui.BeginChild("##ChangelogContent", new Vector2(0, availHeight), false);
        
        var padding = 5f;
        ImGui.Dummy(new Vector2(0, padding));
        ImGui.Indent(padding);
        
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X - padding);

        if (!string.IsNullOrWhiteSpace(_currentChangelog.Description))
        {
            ImGui.TextWrapped(_currentChangelog.Description);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        foreach (var entry in _currentChangelog.Entries)
        {
            DrawChangelogEntry(entry);
        }
        
        ImGui.PopTextWrapPos();
        ImGui.Unindent(padding);
        ImGui.Dummy(new Vector2(0, padding));

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawChangelogEntry(ChangelogEntry entry)
    {
        if (entry.IsSeparator)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            return;
        }

        if (entry.IsHeader)
        {
            ImGui.Spacing();
            
            if (entry.Icon.HasValue)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                var iconColor = entry.IconColor ?? new Vector4(1f, 1f, 1f, 1f);
                ImGui.TextColored(iconColor, entry.Icon.Value.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine();
            }

            var headerTextColor = entry.TextColor ?? new Vector4(1f, 1f, 1f, 1f);
            ImGui.PushStyleColor(ImGuiCol.Text, headerTextColor);
            
            var originalPos = ImGui.GetCursorPos();
            
            ImGui.SetCursorPos(new Vector2(originalPos.X + 0.5f, originalPos.Y));
            ImGui.TextUnformatted(entry.Text);
            ImGui.SetCursorPos(originalPos);
            ImGui.TextUnformatted(entry.Text);
            
            ImGui.PopStyleColor();
            
            ImGui.Spacing();
            return;
        }

        var startPos = ImGui.GetCursorPos();
        var levelIndent = 20f;
        var totalIndent = entry.IndentLevel * levelIndent;
        
        if (totalIndent > 0)
        {
            ImGui.SetCursorPosX(startPos.X + totalIndent);
        }
        
        var entryTextColor = entry.TextColor ?? new Vector4(1f, 1f, 1f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Text, entryTextColor);

        if (entry.Icon.HasValue)
        {
            var bulletColor = entry.IconColor ?? new Vector4(0.7f, 0.7f, 0.7f, 1f);
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(bulletColor, entry.Icon.Value.ToIconString());
            ImGui.PopFont();
            ImGui.SameLine();
            
            var availWidth = ImGui.GetContentRegionAvail().X;
            if (!string.IsNullOrWhiteSpace(entry.ButtonText))
            {
                var buttonWidth = ImGui.CalcTextSize(entry.ButtonText).X + 20f;
                availWidth -= buttonWidth + 10f;
            }
            
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + availWidth);
            ImGui.TextWrapped(entry.Text);
            ImGui.PopTextWrapPos();
        }
        else
        {
            ImGui.BulletText(entry.Text);
        }

        ImGui.PopStyleColor();

        if (!string.IsNullOrWhiteSpace(entry.ButtonText) && entry.ButtonAction != null)
        {
            ImGui.SameLine();
            
            if (entry.ButtonColor.HasValue)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, entry.ButtonColor.Value);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, entry.ButtonColor.Value * 1.1f);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, entry.ButtonColor.Value * 0.9f);
            }

            if (ImGui.Button(entry.ButtonText))
            {
                entry.ButtonAction.Invoke();
            }

            if (entry.ButtonColor.HasValue)
            {
                ImGui.PopStyleColor(3);
            }
        }

        ImGui.Spacing();
    }

    private void DrawFooter()
    {
        var buttonWidth = 100f;
        var windowWidth = ImGui.GetWindowWidth();
        
        ImGui.SetCursorPosX((windowWidth - buttonWidth) * 0.5f);
        
        if (ImGui.Button("Close", new Vector2(buttonWidth, 0)))
            IsOpen = false;
    }

    public void ShowChangelogForVersion(string? version = null)
    {
        if (_availableVersions.Length == 0)
        {
            Plugin.PluginInterface.UiBuilder.AddNotification(
                    "There are no changelogs available",
                    "No changelog available",
                    NotificationType.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(version) && _availableVersions.Contains(version))
            _selectedVersion = version;
        else
            _selectedVersion = _availableVersions[0];

        _currentChangelog = ChangelogManager.GetVersion(_selectedVersion);

        IsOpen = true;
    }

    public void Dispose() { }
}
