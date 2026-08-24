using NoireLib;
using NoireLib.Helpers;
using System.Numerics;

namespace BypassEmote.Helpers;

// Chat payloads (links) to look at a mod that prevented a swap or to switch it off
internal static class ModActionChatPayloads
{
    private static readonly Vector3 LinkColor = ColorHelper.HexToVector3("#4FA3FF");

    private static string PenumbraReason()
        => Service.Penumbra?.UnavailableReason is { Length: > 0 } reason ? reason : "Penumbra is not running.";

    internal static void Append(NoireLogger.ChatMessageBuilder chat, string modDirectory, string modName)
    {
        if (string.IsNullOrEmpty(modDirectory))
            return;

        chat.AddText(" ");
        chat.AddLink("[Open]", $"BypassEmote.OpenMod.{modDirectory}", () => Open(modDirectory, modName), LinkColor);
        chat.AddText(" ");
        chat.AddLink("[Disable]", $"BypassEmote.DisableMod.{modDirectory}", () => Disable(modDirectory, modName), LinkColor);
    }

    private static void Open(string modDirectory, string modName)
    {
        if (Service.Penumbra is not { Available: true } penumbra)
        {
            LogHelper.Error($"{PenumbraReason()} Mod not opened.");
            return;
        }

        if (!penumbra.OpenMod(modDirectory, modName))
            LogHelper.Error($"Penumbra would not open '{modName}'.", "modaction.open-failed");
    }

    private static void Disable(string modDirectory, string modName)
    {
        if (Service.Penumbra is not { Available: true } penumbra)
        {
            LogHelper.Error($"{PenumbraReason()} Mod not disabled.");
            return;
        }

        if (penumbra.GetPlayerCollection() is not { } collection)
        {
            LogHelper.Error("No Penumbra collection is assigned to your character. Mod not disabled.");
            return;
        }

        if (!penumbra.TrySetModEnabled(collection.Id, modDirectory, false))
        {
            LogHelper.Error($"Penumbra would not switch '{modName}' off in {collection.Name}.", "modaction.disable-failed");
            return;
        }

        LogHelper.Info($"'{modName}' is now off in {collection.Name}.");
    }
}
