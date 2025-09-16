using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.Models;

public record ChangelogEntry
{
    public required string Text { get; init; }
    public Vector4? TextColor { get; init; }
    public FontAwesomeIcon? Icon { get; init; }
    public Vector4? IconColor { get; init; }
    public string? ButtonText { get; init; }
    public Action? ButtonAction { get; init; }
    public Vector4? ButtonColor { get; init; }
    public bool IsHeader { get; init; } = false;
    public bool IsSeparator { get; init; } = false;
    public int IndentLevel { get; init; } = 0;

    // Helper constructor for separators
    public static ChangelogEntry CreateSeparator() => new() { Text = string.Empty, IsSeparator = true };
    
    // Helper constructor for indented entries
    public static ChangelogEntry CreateIndented(string text, int indentLevel, FontAwesomeIcon? icon = null, Vector4? iconColor = null) 
        => new() { Text = text, IndentLevel = indentLevel, Icon = icon, IconColor = iconColor };
}

public record ChangelogVersion
{
    public required string Version { get; init; }
    public required string Date { get; init; }
    public required List<ChangelogEntry> Entries { get; init; }
    public string? Title { get; init; }
    public Vector4? TitleColor { get; init; }
    public string? Description { get; init; }
}
