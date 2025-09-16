using BypassEmote.Models;
using Dalamud.Interface;
using System;
using System.Numerics;

namespace BypassEmote.Changelog;

/// <summary>
/// Base class for changelog version files with helper methods
/// </summary>
public abstract class BaseChangelogVersion : IChangelogVersion
{
    // Helper colors
    protected static readonly Vector4 DefaultColor = new(1f, 1f, 1f, 1f); // White
    protected static readonly Vector4 SuccessColor = new(0.2f, 0.8f, 0.2f, 1f); // Green
    protected static readonly Vector4 WarningColor = new(0.9f, 0.7f, 0.1f, 1f); // Yellow
    protected static readonly Vector4 ErrorColor = new(0.9f, 0.2f, 0.2f, 1f); // Red
    protected static readonly Vector4 InfoColor = new(0.4f, 0.7f, 1f, 1f); // Blue
    protected static readonly Vector4 GrayColor = new(0.7f, 0.7f, 0.7f, 1f); // Gray
    protected static readonly Vector4 PastelPinkColor = new(1, 0.639f, 0.831f, 1f); // PastelPink

    public abstract ChangelogVersion GetVersion();

    protected static ChangelogEntry Feature(string text, FontAwesomeIcon? icon = null, int indentLevel = 0, Vector4? textColor = null)
        => new() { Text = text, Icon = icon ?? FontAwesomeIcon.Plus, IconColor = SuccessColor, IndentLevel = indentLevel, TextColor = textColor };

    protected static ChangelogEntry Improvement(string text, FontAwesomeIcon? icon = null, int indentLevel = 0, Vector4? textColor = null)
        => new() { Text = text, Icon = icon ?? FontAwesomeIcon.Wrench, IconColor = InfoColor, IndentLevel = indentLevel, TextColor = textColor };

    protected static ChangelogEntry BugFix(string text, FontAwesomeIcon? icon = null, int indentLevel = 0, Vector4? textColor = null)
        => new() { Text = text, Icon = icon ?? FontAwesomeIcon.Bug, IconColor = GrayColor, IndentLevel = indentLevel, TextColor = textColor };

    protected static ChangelogEntry Warning(string text, FontAwesomeIcon? icon = null, int indentLevel = 0, Vector4? textColor = null)
        => new() { Text = text, Icon = icon ?? FontAwesomeIcon.ExclamationTriangle, IconColor = WarningColor, IndentLevel = indentLevel, TextColor = textColor };

    protected static ChangelogEntry Header(string text, Vector4? color = null, FontAwesomeIcon? icon = null)
        => new() { Text = text, IsHeader = true, TextColor = color, Icon = icon };

    protected static ChangelogEntry Separator()
        => ChangelogEntry.CreateSeparator();

    protected static ChangelogEntry Button(string text, string buttonText, Action action, FontAwesomeIcon? icon = null, Vector4? buttonColor = null, Vector4? textColor = null)
        => new() { Text = text, Icon = icon, ButtonText = buttonText, ButtonAction = action, ButtonColor = buttonColor, TextColor = textColor };

    protected static ChangelogEntry Entry(string text, FontAwesomeIcon? icon = null, Vector4? iconColor = null, int indentLevel = 0, Vector4? textColor = null)
        => new() { Text = text, Icon = icon, IconColor = iconColor, IndentLevel = indentLevel, TextColor = textColor };

    protected static ChangelogEntry Indented(string text, int indentLevel, FontAwesomeIcon? icon = null, Vector4? iconColor = null, Vector4? textColor = null)
        => new() { Text = text, IndentLevel = indentLevel, Icon = icon, IconColor = iconColor, TextColor = textColor };
}
