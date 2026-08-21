using Dalamud.Plugin.Services;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Security.Cryptography;
using System.Text;

namespace BypassEmote.EmoteSwap;

public sealed record SwapModNames(string Directory, string Display, string CharacterKey);

public sealed class SwapModIdentity : IDisposable
{
    private const string LogPrefix = "[SwapModIdentity] ";

    public const string DirectoryPrefix = "_BypassEmote_";

    private const int MaxNameLength = 24;

    private const string UnnamedCharacter = "Character";

    private bool _subscribed;
    private ulong _contentId;
    private bool _anonymous;

    public SwapModIdentity()
    {
        NoireService.Framework.Update += OnFrameworkUpdate;
        _subscribed = true;
    }

    public event Action<SwapModNames?>? Changed;

    public SwapModNames? Names { get; private set; }

    public string? DirectoryName => Names?.Directory;

    public void Dispose()
    {
        if (!_subscribed)
            return;

        NoireService.Framework.Update -= OnFrameworkUpdate;
        _subscribed = false;

        Changed = null;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            Evaluate();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "Could not read who the generated mod belongs to.", LogPrefix);
        }
    }

    private void Evaluate()
    {
        var contentId = CharacterHelper.IsPlayerLoaded ? CharacterHelper.LocalContentId : 0;
        var anonymous = Configuration.AnonymizeModName;

        if (contentId == _contentId && anonymous == _anonymous)
            return;

        var previous = Names;

        _contentId = contentId;
        _anonymous = anonymous;
        Names = contentId == 0 ? null : BuildNames(contentId, anonymous);

        NoireLogger.LogDebug(Names is { } names
            ? $"The generated mod for this character is '{names.Directory}'."
            : "No character is loaded, so no generated mod is named.", LogPrefix);

        Changed?.Invoke(previous);
    }

    private static SwapModNames BuildNames(ulong contentId, bool anonymous)
    {
        var name = LocalPlayerName();
        var world = LocalPlayerWorld();

        var key = $"{Sanitize(name)}_{ShortDigest(contentId)}";

        var display = anonymous
            ? FillTemplate(Service.PenumbraModNameTemplate, Initials(name), string.Empty)
            : FillTemplate(Service.PenumbraModNameTemplate, name, world);

        return new SwapModNames($"{DirectoryPrefix}{key}", display, key);
    }

    internal static string Initials(string name)
    {
        var initials = new StringBuilder();

        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (initials.Length > 0)
                initials.Append(' ');

            initials.Append(char.ToUpperInvariant(word[0])).Append('.');
        }

        return initials.Length == 0 ? UnnamedCharacter : initials.ToString();
    }

    internal static string FillTemplate(string template, string playerName, string playerWorld)
    {
        if (string.IsNullOrWhiteSpace(template))
            return playerName;

        var fullName = playerWorld.Length == 0 ? playerName : $"{playerName} @ {playerWorld}";

        return template
            .Replace("{playerFullName}", fullName, StringComparison.OrdinalIgnoreCase)
            .Replace("{playerName}", playerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{playerWorld}", playerWorld, StringComparison.OrdinalIgnoreCase);
    }

    private static string LocalPlayerName()
    {
        var name = NoireService.ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
        return string.IsNullOrWhiteSpace(name) ? UnnamedCharacter : name;
    }

    private static string LocalPlayerWorld()
    {
        try
        {
            return WorldHelper.Name(WorldHelper.HomeId());
        }
        catch (Exception ex)
        {
            NoireLogger.LogDebug($"Could not read the home world for the mod name ({ex.Message}).", LogPrefix);
            return string.Empty;
        }
    }

    internal static string Sanitize(string name)
    {
        var kept = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
                kept.Append(character);
            else if (kept.Length > 0 && kept[^1] != '_')
                kept.Append('_');

            if (kept.Length >= MaxNameLength)
                break;
        }

        var sanitized = kept.ToString().Trim('_');

        return sanitized.Length == 0 ? UnnamedCharacter : sanitized;
    }

    internal static string ShortDigest(ulong contentId)
        => Convert.ToHexString(SHA1.HashData(BitConverter.GetBytes(contentId)), 0, 4).ToLowerInvariant();
}
