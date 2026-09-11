#if DEBUG
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace BypassEmote.Safety;

public static class ApprovalPublisher
{
    private const string LogPrefix = "[ApprovalPublisher] ";

    private const string FileName = "patch-approval.json";

    private const string BackupFolder = "Approval Backup";

    public static string Publish(string gameVersion, Version? pluginVersion, GameClient client,
        string notice)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
            return "The game build could not be read.";

        if (RepoRoot() is not { } root)
            return $"{FileName} was not found above the plugin sources.";

        var path = Path.Combine(root, FileName);

        try
        {
            var backup = Backup(root, path);

            var document = JObject.Parse(File.ReadAllText(path));

            if (document["approved"] is not JArray approved)
            {
                approved = new JArray();
                document["approved"] = approved;
            }

            var minimum = pluginVersion?.ToString() ?? string.Empty;

            if (Entry(approved, client) is { } entry)
            {
                entry["gameVersion"] = gameVersion;
                entry["minimumPluginVersion"] = minimum;
                entry["notice"] = notice.Trim();
            }
            else
            {
                approved.Add(new JObject
                {
                    ["gameVersion"] = gameVersion,
                    ["client"] = client.ToString(),
                    ["minimumPluginVersion"] = minimum,
                    ["notice"] = notice.Trim(),
                });
            }

            var text = JsonConvert.SerializeObject(document, Formatting.Indented).Replace("\r\n", "\n");

            File.WriteAllText(path, text + "\n");

            var note = $"Game build {gameVersion} is approved for the {GameClientHelper.Name(client)} client from "
                + $"plugin {minimum}. Backed up as {Path.GetFileName(backup)}.";

            Log.Debug(note, LogPrefix);

            return note;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            var note = $"{FileName} could not be written: {ex.Message}";

            Log.Warning(note, LogPrefix);

            return note;
        }
    }

    private static string Backup(string root, string path)
    {
        var folder = Path.Combine(root, BackupFolder);

        Directory.CreateDirectory(folder);

        var backup = Path.Combine(folder, $"patch-approval {DateTime.Now:yyyy-MM-dd HH-mm-ss}.json");

        File.Copy(path, backup, true);

        return backup;
    }

    private static JObject? Entry(JArray approved, GameClient client)
    {
        foreach (var token in approved)
        {
            if (token is JObject entry && GameClientHelper.Parse((string?)entry["client"]) == client)
                return entry;
        }

        return null;
    }

    private static string? RepoRoot()
    {
        var directory = Path.GetDirectoryName(SourcePath());

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory, FileName)))
                return directory;

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
#endif
