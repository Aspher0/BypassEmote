using NoireLib;
using System;

namespace BypassEmote.Safety;

public enum GameClient
{
    Global,
    Korean,
    Chinese,
    Unknown,
}

public static class GameClientReader
{
    private const string SchemaProbePath = "exd/addon.exh";
    private const string GlobalDataPath = "exd/addon_0_en.exd";
    private const string KoreanDataPath = "exd/addon_0_ko.exd";

    private static readonly string[] ChineseDataPaths =
        ["exd/addon_0_chs.exd", "exd/addon_0_cht.exd", "exd/addon_0_tc.exd"];

    private static GameClient? _detected;

    public static GameClient? Forced { get; set; }

    public static GameClient Current() => Forced ?? Detected();

    public static GameClient Detected()
    {
        if (_detected is { } cached)
            return cached;

        if (!NoireService.IsInitialized())
            return GameClient.Global;

        _detected = Probe();

        return _detected.Value;
    }

    private static GameClient Probe()
    {
        try
        {
            var data = NoireService.DataManager.GameData;

            if (!data.FileExists(SchemaProbePath))
                return GameClient.Global;

            if (data.FileExists(KoreanDataPath))
                return GameClient.Korean;

            foreach (var path in ChineseDataPaths)
            {
                if (data.FileExists(path))
                    return GameClient.Chinese;
            }

            return data.FileExists(GlobalDataPath) ? GameClient.Global : GameClient.Unknown;
        }
        catch (Exception)
        {
            return GameClient.Global;
        }
    }

    public static string Name(GameClient client) => client switch
    {
        GameClient.Korean => "Korean",
        GameClient.Chinese => "Chinese",
        GameClient.Unknown => "unidentified",
        _ => "Global",
    };

    public static GameClient Parse(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "kr" or "korea" or "korean" => GameClient.Korean,
        "cn" or "china" or "chinese" => GameClient.Chinese,
        "other" or "unknown" => GameClient.Unknown,
        _ => GameClient.Global,
    };
}
