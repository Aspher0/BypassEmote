using BypassEmote.Models;
using BypassEmote.Changelog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BypassEmote.Managers;

public static class ChangelogManager
{
    private static readonly Dictionary<string, ChangelogVersion> _changelogs = new();
    private static bool _initialized = false;

    public static IReadOnlyList<ChangelogVersion> GetAllVersions()
    {
        if (!_initialized)
            Initialize();
        
        return _changelogs.Values
            .OrderByDescending(v => Version.TryParse(v.Version, out var ver) ? ver : new Version(0, 0, 0))
            .ToList();
    }

    public static ChangelogVersion? GetVersion(string version)
    {
        if (!_initialized)
            Initialize();
        
        return _changelogs.GetValueOrDefault(version);
    }

    public static string? GetLatestVersion()
    {
        var versions = GetAllVersions();
        return versions.FirstOrDefault()?.Version;
    }

    private static void Initialize()
    {
        if (_initialized) return;
        
        LoadVersionsFromAssembly();
        _initialized = true;
    }

    private static void LoadVersionsFromAssembly()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var versionTypes = assembly.GetTypes()
                .Where(t => t.Namespace == "BypassEmote.Changelog.Versions" && 
                           typeof(IChangelogVersion).IsAssignableFrom(t) && 
                           !t.IsAbstract && !t.IsInterface &&
                           !t.Name.Contains("TEMPLATE"))
                .ToList();

            foreach (var type in versionTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IChangelogVersion versionInstance)
                    {
                        var version = versionInstance.GetVersion();
                        _changelogs[version.Version] = version;
                    }
                }
                catch (Exception ex)
                {
                    Service.Log.Error($"Failed to load changelog version from type {type.Name}: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error($"Failed to load changelog versions from assembly: {ex}");
        }
    }

    public static void AddVersion(ChangelogVersion version)
    {
        _changelogs[version.Version] = version;
    }

    public static void ClearVersions()
    {
        _changelogs.Clear();
        _initialized = false;
    }
}
